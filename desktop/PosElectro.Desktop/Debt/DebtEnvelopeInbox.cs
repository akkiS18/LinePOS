using System;
using System.Globalization;
using Microsoft.Data.Sqlite;
using static PosElectro.Desktop.Debt.DebtRepository;

namespace PosElectro.Desktop.Debt;

public enum DebtReceiveStatus { Applied, AlreadyApplied, WaitingForDependency, WaitingForSaleAdapter }
public sealed record DebtPendingPacket(string Wire,long ReceivedAt,string Reason);
public sealed class DebtInboxFullException : InvalidOperationException
{
    public DebtInboxFullException() : base("Debt inbox capacity exceeded; retain sender packet") { }
}

// Durable validated inbox. Desktop openings opt in with a trusted actor/user resolver.
// No arbitrary sale writer is exposed; all opening effects use DebtSaleReceiver.
// No transport ACK/cursor or HELD release; callers must distinguish Waiting from Applied.
public sealed class DebtEnvelopeInbox
{
    public const int MaxPendingPackets=128;
    public const long MaxPendingChars=32L*1024*1024;
    private readonly string store;
    private readonly DebtSyncStore bridge;
    private readonly Func<string,long?>? resolveActorUser;
    public DebtEnvelopeInbox(string path,string storeGuid,Func<bool> canSync,Func<string,bool> canImportActor,Func<string,long?>? resolveActorUser=null) {
        store=storeGuid;bridge=new(path,storeGuid,canSync,canImportActor);this.resolveActorUser=resolveActorUser;
    }
    private static void Need(bool ok) { if(!ok)throw new InvalidOperationException("Debt envelope integrity conflict"); }
    private static string Key(string id)=>"debt_envelope_v1:"+id;
    private static string Seal(string wire)=>DebtWire.Fingerprint(wire)+"\n"+wire;
    private void Authorize(DebtEnvelopePacket p) {
        if(p.CustomerWire.Length>0)bridge.Authorize(DebtWire.CommandFields(DebtWire.DecodeCustomer(p.CustomerWire,store).Payload)[2]);
        if(p.EventWire.Length>0)bridge.Authorize(DebtWire.DecodeEvent(p.EventWire,store).ActorGuid);
    }
    private void VerifyApplied(SqliteConnection db,SqliteTransaction tx,DebtEnvelopePacket p) {
        if(p.SaleWire.Length>0)DebtSaleReceiver.Verify(db,tx,p.SaleWire,p.Guid);
        if(p.CustomerWire.Length>0)Need(bridge.CustomerWire(db,tx,DebtWire.DecodeCustomer(p.CustomerWire,store).Guid)==p.CustomerWire);
        if(p.EventWire.Length>0)Need(bridge.EventWire(db,tx,p.Guid)==p.EventWire);
    }
    public DebtReceiveStatus Receive(string wire,long receivedAt) {
        if(receivedAt<0)throw new ArgumentException("Invalid receipt timestamp");
        var packet=DebtEnvelope.Decode(wire,store);
        return bridge.Write((db,tx)=>Receive(db,tx,packet,wire,receivedAt));
    }
    internal DebtReceiveStatus Receive(SqliteConnection db,SqliteTransaction tx,DebtEnvelopePacket p,string wire,long receivedAt) {
        Authorize(p);var old=Rows(db,tx,"SELECT store_guid,payload FROM debt_sync_inbox WHERE packet_guid=@p0",p.Guid);
        if(old.Count>0)Need((string)old[0][0]! ==store && (string)old[0][1]! ==wire);
        var receipt=Scalar(db,tx,"SELECT value FROM sync_meta WHERE key=@p0",Key(p.Guid));
        if(receipt!=null) {
            Need((string)receipt==Seal(wire) && old.Count==0);VerifyApplied(db,tx,p);return DebtReceiveStatus.AlreadyApplied;
        }
        // Dependency preflight and mutation share this writer transaction. Never catch a
        // dependency exception after partial writes; unexpected failures roll back everything.
        bool missing=false;
        if(p.CustomerWire.Length>0) {
            var c=DebtWire.DecodeCustomer(p.CustomerWire,store);
            Need(Scalar(db,tx,"SELECT 1 FROM debt_events WHERE request_guid=@p0",c.Guid)==null);
            if(Scalar(db,tx,"SELECT 1 FROM debt_customers WHERE guid=@p0",c.Guid)!=null)Need(bridge.CustomerWire(db,tx,c.Guid)==p.CustomerWire);
        }
        if(p.EventWire.Length>0) {
            var e=DebtWire.DecodeEvent(p.EventWire,store);
            Need(Scalar(db,tx,"SELECT 1 FROM debt_customers WHERE guid=@p0",e.Guid)==null);
            var sequence=Scalar(db,tx,"SELECT guid FROM debt_events WHERE device_guid=@p0 AND device_sequence=@p1",e.DeviceGuid,e.DeviceSequence);
            Need(sequence==null || (string)sequence==e.Guid);
            if(Scalar(db,tx,"SELECT 1 FROM debt_events WHERE guid=@p0",e.Guid)!=null)Need(bridge.EventWire(db,tx,e.Guid)==p.EventWire);
            var owner=Scalar(db,tx,"SELECT store_guid FROM debt_customers WHERE guid=@p0",e.CustomerGuid);
            if(owner==null)missing=p.CustomerWire.Length==0;else Need((string)owner==store);
            foreach(var line in e.Lines) {
                var account=Rows(db,tx,"SELECT customer_guid,store_guid FROM debt_accounts WHERE guid=@p0",line.AccountGuid);
                if(account.Count==0)missing=true;else Need((string)account[0][0]! ==e.CustomerGuid && (string)account[0][1]! ==store);
            }
        }
        var gated=p.SaleWire.Length>0 && resolveActorUser==null;
        DebtSaleReceiver? sale=null;
        if(p.SaleWire.Length>0 && !gated) {
            sale=DebtSaleReceiver.Prepare(db,tx,p.SaleWire,DebtWire.DecodeEvent(p.EventWire,store),resolveActorUser!);
            missing|=sale==null;
        }
        if(gated || missing) {
            var reason=gated?"sale_adapter_pending":"missing_dependency";
            if(old.Count==0) {
                var count=Convert.ToInt64(Scalar(db,tx,"SELECT COUNT(*) FROM debt_sync_inbox"),CultureInfo.InvariantCulture);
                var chars=Convert.ToInt64(Scalar(db,tx,"SELECT COALESCE(SUM(length(payload)),0) FROM debt_sync_inbox"),CultureInfo.InvariantCulture);
                if(count>=MaxPendingPackets || chars>MaxPendingChars-wire.Length)throw new DebtInboxFullException();
                Exec(db,tx,"INSERT INTO debt_sync_inbox(packet_guid,store_guid,payload,received_at,error) VALUES(@p0,@p1,@p2,@p3,@p4)",p.Guid,store,wire,receivedAt,reason);
            } else Exec(db,tx,"UPDATE debt_sync_inbox SET error=@p0 WHERE packet_guid=@p1",reason,p.Guid);
            return gated?DebtReceiveStatus.WaitingForSaleAdapter:DebtReceiveStatus.WaitingForDependency;
        }
        Exec(db,tx,"UPDATE sync_control SET applying=1 WHERE id=1");
        if(p.CustomerWire.Length>0)bridge.Customer(db,tx,DebtWire.DecodeCustomer(p.CustomerWire,store),p.CustomerWire);
        if(p.EventWire.Length>0)bridge.Event(db,tx,DebtWire.DecodeEvent(p.EventWire,store),p.EventWire,sale==null?null:(c,t,e)=>sale.Apply(c,t));
        Exec(db,tx,"INSERT INTO sync_meta(key,value) VALUES(@p0,@p1)",Key(p.Guid),Seal(wire));
        Exec(db,tx,"DELETE FROM debt_sync_inbox WHERE packet_guid=@p0",p.Guid);
        Exec(db,tx,"UPDATE sync_control SET applying=0 WHERE id=1");return DebtReceiveStatus.Applied;
    }

    public DebtPendingPacket? ReadPending(string guid) {
        DebtWire.Id(guid);
        return bridge.Write<DebtPendingPacket?>((db,tx)=> {
            var row=Rows(db,tx,"SELECT store_guid,payload,received_at,error FROM debt_sync_inbox WHERE packet_guid=@p0",guid);
            if(row.Count==0)return null;
            Need((string)row[0][0]! ==store);var wire=(string)row[0][1]!;
            var p=DebtEnvelope.Decode(wire,store);Need(p.Guid==guid);Authorize(p);
            Need(Scalar(db,tx,"SELECT 1 FROM sync_meta WHERE key=@p0",Key(guid))==null);
            return new DebtPendingPacket(wire,Convert.ToInt64(row[0][2],CultureInfo.InvariantCulture),(string)row[0][3]!);
        });
    }
    public string? ExportApplied(string guid) => bridge.Write<string?>((db,tx)=>ExportApplied(db,tx,guid));
    internal string? ExportApplied(SqliteConnection db,SqliteTransaction tx,string guid) {
        DebtWire.Id(guid);
        var value=Scalar(db,tx,"SELECT value FROM sync_meta WHERE key=@p0",Key(guid)) as string;
        if(value==null)return null;
        Need(value.Length>65 && value[64]=='\n');var wire=value[65..];Need(value==Seal(wire));
        var p=DebtEnvelope.Decode(wire,store);Need(p.Guid==guid);Authorize(p);VerifyApplied(db,tx,p);
        Need(Scalar(db,tx,"SELECT 1 FROM debt_sync_inbox WHERE packet_guid=@p0",guid)==null);return wire;
    }

    public int DrainPending(long now) {
        if(now<0)throw new ArgumentException("Invalid timestamp");
        return bridge.Write((db,tx)=>DrainPending(db,tx,now));
    }
    internal int DrainPending(SqliteConnection db,SqliteTransaction tx,long now) {
        if(now<0)throw new ArgumentException("Invalid timestamp");
        int drained=0;
        while(true) {
            var rows=Rows(db,tx,"SELECT packet_guid FROM debt_sync_inbox ORDER BY received_at, packet_guid");
            if(rows.Count==0)break;
            bool anyApplied=false;
            foreach(var r in rows) {
                var guid=(string)r[0]!;
                var pendingRow=Rows(db,tx,"SELECT store_guid,payload,received_at,error FROM debt_sync_inbox WHERE packet_guid=@p0",guid);
                if(pendingRow.Count==0)continue;
                Need((string)pendingRow[0][0]! ==store);var wire=(string)pendingRow[0][1]!;
                var p=DebtEnvelope.Decode(wire,store);Need(p.Guid==guid);
                var status=Receive(db,tx,p,wire,now);
                if(status is DebtReceiveStatus.Applied or DebtReceiveStatus.AlreadyApplied) {
                    anyApplied=true;drained++;
                }
            }
            if(!anyApplied)break;
        }
        return drained;
    }
}
