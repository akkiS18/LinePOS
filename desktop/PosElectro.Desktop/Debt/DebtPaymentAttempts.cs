using System;
using System.Text.Json;

namespace PosElectro.Desktop.Debt;

public sealed partial class DebtRepository
{
    // Only the local UI uses this slot. Network and imported events retain their own IDs.
    public string TakePaymentFromUi(DebtPaymentCommand proposed) {
        var key="debt_payment_attempt:"+proposed.CustomerGuid;
        var command=Write((c,t)=> {
            Scope(c,t);
            var saved=Scalar(c,t,"SELECT value FROM sync_meta WHERE key=@p0",key) as string;
            if(saved!=null) {
                var old=JsonSerializer.Deserialize<DebtPaymentCommand>(saved)!;
                if(old.CashMinor!=proposed.CashMinor || old.CardMinor!=proposed.CardMinor || old.TargetAccountGuid!=proposed.TargetAccountGuid)
                    throw new ArgumentException("Oldingi to'lov natijasini tekshirish uchun mijozni qayta oching.");
                return old;
            }
            Exec(c,t,"INSERT INTO sync_meta(key,value) VALUES(@p0,@p1)",key,JsonSerializer.Serialize(proposed));return proposed;
        });
        try { return TakePayment(command); }
        catch {
            Write((c,t)=> {
                if(Scalar(c,t,"SELECT 1 FROM debt_command_receipts WHERE request_guid=@p0",command.RequestGuid)==null)
                    Exec(c,t,"DELETE FROM sync_meta WHERE key=@p0",key);
                return true;
            });throw;
        }
    }
    public string? RecoverPaymentAttempt(string customerGuid) => Write((c,t)=> {
        var key="debt_payment_attempt:"+customerGuid;
        var saved=Scalar(c,t,"SELECT value FROM sync_meta WHERE key=@p0",key) as string;
        if(saved==null)return (string?)null;
        var q=JsonSerializer.Deserialize<DebtPaymentCommand>(saved)!;
        var result=Scalar(c,t,"SELECT result FROM debt_command_receipts WHERE request_guid=@p0",q.RequestGuid) as string;
        Exec(c,t,"DELETE FROM sync_meta WHERE key=@p0",key);
        return result;
    });
}
