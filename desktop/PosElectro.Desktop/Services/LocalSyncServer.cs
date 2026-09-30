using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using PosElectro.Desktop.Sync;
using Newtonsoft.Json;
using PosElectro.Desktop.Data;
using PosElectro.Desktop.Models;

namespace PosElectro.Desktop.Services
{
    public class SyncPushPayload
    {
        public List<Product> Products { get; set; } = new();
        public List<Sale> Sales { get; set; } = new();
    }

    public class SyncPullPayload
    {
        public List<Product> Products { get; set; } = new();
        public long ServerTimestamp { get; set; }
    }

    public class SyncCheckRequest
    {
        public string Direction { get; set; } = "phone_to_desktop";
        public string DeviceName { get; set; } = "Android Telefon";
        public int PhoneProductsCount { get; set; }
        public int PhoneSalesCount { get; set; }
        public long PhoneLastSyncTime { get; set; }
        public int PhoneModifiedProducts { get; set; }
        public int PhoneUnsyncedSales { get; set; }
    }

    public class SyncTransferRequest
    {
        public string Direction { get; set; } = "phone_to_desktop"; // "phone_to_desktop", "desktop_to_phone", "smart_merge"
        public string DeviceName { get; set; } = "Android Telefon";
        public List<Product> Products { get; set; } = new();
        public List<Sale> Sales { get; set; } = new();
        public List<Warehouse> Warehouses { get; set; } = new();
        public long ClientTimestamp { get; set; }
    }

    public class LiveClientSession
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string DeviceName { get; set; } = "Android Mobil";
        public string IpAddress { get; set; } = string.Empty;
        public StreamWriter Writer { get; set; } = null!;
        public TcpClient Client { get; set; } = null!;
        public DateTime ConnectedAt { get; set; } = DateTime.Now;
    }

    public class ConnectedClientInfo
    {
        public string Id { get; set; } = string.Empty;
        public string DeviceName { get; set; } = "Android Telefon";
        public string IpAddress { get; set; } = string.Empty;
        public DateTime ConnectedAt { get; set; } = DateTime.Now;
        public string ConnectedAtText => ConnectedAt.ToString("HH:mm:ss");
    }

    public class SyncClientInfo
    {
        public string DeviceName { get; set; } = "Mobil ilova";
        public string IpAddress { get; set; } = string.Empty;
        public DateTime LastSeen { get; set; } = DateTime.Now;
        public int PhoneProductsCount { get; set; }
        public int PhoneSalesCount { get; set; }
        public bool IsConnected => (DateTime.Now - LastSeen).TotalMinutes < 2;
    }

    public class LocalSyncServer
    {
        private readonly DatabaseContext _db;
        private readonly CurrencyService? _currencyService;
        private readonly WifiSyncStore store;
        private TcpListener? listener;
        private CancellationTokenSource? stop;
        private readonly Dictionary<string, (SyncClientInfo Info, DateTime Seen)> clients = new();
        private readonly object gate = new();
        private string pairingCode = "";
        private DateTime pairingExpires;
        private int pairingAttempts;
        public int Port => 8080;
        public bool IsRunning => stop is { IsCancellationRequested: false };
        public event Action<string>? LogMessageReceived;
        public event Action<string,string>? ActivityLogged;
        public event Action<SyncClientInfo>? ClientStatusUpdated;
        public event Action<int>? LiveClientsCountChanged;
        public event Action<List<ConnectedClientInfo>>? ConnectedClientsListChanged;
        public event Action? DataSynced;
        public int ConnectedLiveClientsCount { get { lock(gate) return clients.Values.Count(c=>(DateTime.UtcNow-c.Seen).TotalSeconds<15); } }
        public List<ConnectedClientInfo> GetConnectedClients()
        {
            lock(gate)return clients.Where(c=>(DateTime.UtcNow-c.Value.Seen).TotalSeconds<15).Select(c=>new ConnectedClientInfo { Id=c.Key, DeviceName=c.Value.Info.DeviceName, IpAddress=c.Value.Info.IpAddress, ConnectedAt=c.Value.Info.LastSeen }).ToList();
        }
        public LocalSyncServer(DatabaseContext db, CurrencyService? currencyService=null)
        { _db=db; _currencyService=currencyService; store=new WifiSyncStore(db.DatabaseFilePath); }
        public string NewPairingCode()
        { lock(gate) { pairingCode=RandomNumberGenerator.GetInt32(10000000,100000000).ToString(); pairingExpires=DateTime.UtcNow.AddMinutes(5); pairingAttempts=0; return pairingCode; } }
        public void RevokeDevices()
        { using var db=store.Open(); WifiSyncStore.Exec(db,null,"DELETE FROM sync_devices"); lock(gate)clients.Clear(); LiveClientsCountChanged?.Invoke(0); }
        public string GetLocalIpAddress()
        {
            var addresses=System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()
                .Where(n=>n.OperationalStatus==System.Net.NetworkInformation.OperationalStatus.Up && n.NetworkInterfaceType!=System.Net.NetworkInformation.NetworkInterfaceType.Loopback)
                .OrderByDescending(n=>n.NetworkInterfaceType==System.Net.NetworkInformation.NetworkInterfaceType.Wireless80211)
                .SelectMany(n=>n.GetIPProperties().UnicastAddresses).Where(a=>a.Address.AddressFamily==AddressFamily.InterNetwork && !IPAddress.IsLoopback(a.Address));
            return addresses.FirstOrDefault()?.Address.ToString()??"127.0.0.1";
        }
        public void Start()
        {
            if(IsRunning)return;
            try { listener=new TcpListener(IPAddress.Any,Port); listener.Start(); stop=new CancellationTokenSource(); _=Listen(stop.Token); _=MonitorClients(stop.Token); ActivityLogged?.Invoke("info",$"Lokal V2 server: {GetLocalIpAddress()}:{Port}"); }
            catch(Exception e){ stop?.Cancel(); ActivityLogged?.Invoke("error",e.Message); }
        }
        public void Stop(){stop?.Cancel();listener?.Stop();lock(gate)clients.Clear();LiveClientsCountChanged?.Invoke(0);}
        // V2 uses a transactional cursor pull. SSE is only a wake-up hint, never the data source.
        public Task BroadcastLiveEventAsync(string eventType,object data,string? excludeClientId=null)=>Task.CompletedTask;
        private readonly SemaphoreSlim connectionSlots=new(32,32);
        private async Task Listen(CancellationToken token)
        {
            while(!token.IsCancellationRequested)
            {
                try { var client=await listener!.AcceptTcpClientAsync(token); if(!connectionSlots.Wait(0)){client.Dispose();continue;} _=Handle(client,token); }
                catch(OperationCanceledException){break;} catch(Exception e){if(token.IsCancellationRequested)break;LogMessageReceived?.Invoke(e.Message);}
            }
        }
        private static string Hash(string token)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
        private JObject Pair(JObject request)
        {
            lock(gate)
            {
                if(++pairingAttempts>10 || DateTime.UtcNow>pairingExpires || pairingCode=="" || (string?)request["code"]!=pairingCode)throw new UnauthorizedAccessException("Kompyuterdan yangi ulanish kodini oling (5 daqiqa amal qiladi).");
                string id=WifiSyncStore.Required(request,"deviceId"), name=WifiSyncStore.Required(request,"deviceName");
                string token=Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
                using var db=store.Open(); WifiSyncStore.Exec(db,null,"INSERT INTO sync_devices(token_hash,device_id,device_name) VALUES(@hash,@id,@name)",("@hash",Hash(token)),("@id",id),("@name",name));
                pairingCode="";
                return new JObject { ["token"]=token,["serverId"]=store.ServerId,["protocol"]=2 };
            }
        }
        private async Task MonitorClients(CancellationToken token)
        {
            try { while(!token.IsCancellationRequested) { await Task.Delay(5000,token); LiveClientsCountChanged?.Invoke(ConnectedLiveClientsCount); ConnectedClientsListChanged?.Invoke(GetConnectedClients()); } }
            catch(OperationCanceledException) { }
        }
        private string Authenticate(Dictionary<string,string> headers,string ip)
        {
            if(!headers.TryGetValue("Authorization",out var auth)||!auth.StartsWith("Bearer ",StringComparison.Ordinal))throw new UnauthorizedAccessException("Avval QR yoki kod orqali qurilmani ulang.");
            using var db=store.Open(); using var cmd=db.CreateCommand(); cmd.CommandText="SELECT device_id,device_name FROM sync_devices WHERE token_hash=@hash"; cmd.Parameters.AddWithValue("@hash",Hash(auth[7..]));using var reader=cmd.ExecuteReader();
            if(!reader.Read())throw new UnauthorizedAccessException("Qurilma ruxsati bekor qilingan. Qayta ulang.");
            string id=reader.GetString(0);var info=new SyncClientInfo { DeviceName=reader.GetString(1),IpAddress=ip,LastSeen=DateTime.Now };
            lock(gate)clients[id]=(info,DateTime.UtcNow);
            ClientStatusUpdated?.Invoke(info); LiveClientsCountChanged?.Invoke(ConnectedLiveClientsCount); ConnectedClientsListChanged?.Invoke(GetConnectedClients());return id;
        }
        private async Task Handle(TcpClient client,CancellationToken token)
        {
            try
            {
                using(client)
                using(var stream=client.GetStream())
                {
                    using var timeout=CancellationTokenSource.CreateLinkedTokenSource(token);timeout.CancelAfter(TimeSpan.FromSeconds(15));
                    try
                    {
                        var request=await WifiHttpRequest.Read(stream,timeout.Token);
                        var uri=new Uri("http://localhost"+request.Target);string path=uri.AbsolutePath;
                        if(path=="/api/v2/pair" && request.Method=="POST") { await Reply(stream,200,Pair(JObject.Parse(request.Body)),timeout.Token);return; }
                        // Never allow legacy unauthenticated writes to bypass V2 invariants.
                        Authenticate(request.Headers,(client.Client.RemoteEndPoint as IPEndPoint)?.Address.ToString()??"");
                        if(path=="/api/ping" && request.Method=="GET")
                        {await Reply(stream,200,new JObject{["protocol"]=2,["serverId"]=store.ServerId,["name"]="Line kassa",["productsCount"]=_db.GetActiveProductsCount(),["salesCount"]=_db.GetTotalSalesCount()},timeout.Token);return;}
                        if(path=="/api/v2/push" && request.Method=="POST")
                        {var reply=store.Push((JArray?)JObject.Parse(request.Body)["operations"]??throw new ArgumentException("Amallar yo'q."));DataSynced?.Invoke();await Reply(stream,200,reply,timeout.Token);return;}
                        if(path=="/api/v2/pull" && request.Method=="GET")
                        {var query=uri.Query.TrimStart('?').Split('&').Select(x=>x.Split('=',2)).Where(x=>x.Length==2).ToDictionary(x=>x[0],x=>x[1]);long cursor=query.TryGetValue("cursor",out var raw)?long.Parse(raw):0;var reply=store.Pull(cursor);reply["cardTaxRate"]=_db.GetCardTaxRate();reply["usdRate"]=_currencyService?.GetCachedUsdRate()??12850;await Reply(stream,200,reply,timeout.Token);return;}
                        if(path=="/api/sync/download_db" && request.Method=="GET")
                        {
                            var temp=Path.Combine(Path.GetTempPath(),$"LinePOS_{Guid.NewGuid():N}.db");
                            try{_db.BackupDatabase(temp);
                                using(var exported=new Microsoft.Data.Sqlite.SqliteConnection("Data Source="+temp)) { exported.Open();
                                    using var triggers=exported.CreateCommand();triggers.CommandText="SELECT name FROM sqlite_master WHERE type='trigger' AND name LIKE 'sync_%'";var names=new List<string>();using(var r=triggers.ExecuteReader())while(r.Read())names.Add(r.GetString(0));
                                    foreach(var name in names)WifiSyncStore.Exec(exported,null,$"DROP TRIGGER [{name}]");
                                    foreach(var table in new[]{"sync_devices","sync_journal","sync_meta","sync_versions","sync_control","sync_conflicts"})WifiSyncStore.Exec(exported,null,$"DROP TABLE IF EXISTS {table}");
                                }
                                await ReplyBytes(stream,200,await File.ReadAllBytesAsync(temp,timeout.Token),"application/octet-stream",timeout.Token);}finally{if(File.Exists(temp))File.Delete(temp);}return;
                        }
                        await Reply(stream,410,new JObject{["error"]="Eski sinxron protokoli o'chirilgan. Ikkala dasturni ham V2 versiyaga yangilang."},timeout.Token);
                    }
                    catch(SyncConflictException e){await Reply(stream,409,new JObject{["error"]=e.Message,["kind"]=e.Kind,["guid"]=e.Guid,["revision"]=e.Revision},token);}
                    catch(UnauthorizedAccessException e){await Reply(stream,401,new JObject{["error"]=e.Message},token);}
                    catch(Exception e) when(e is ArgumentException or JsonException or FormatException or InvalidOperationException){await Reply(stream,400,new JObject{["error"]=e.Message},token);}
                    catch(OperationCanceledException) { }
                    catch(Exception e){LogMessageReceived?.Invoke(e.ToString());await Reply(stream,500,new JObject{["error"]="Amal saqlanmadi; navbatdan o'chirilmaydi."},token);}
                }
            }
            catch(Exception e){LogMessageReceived?.Invoke(e.Message);}
            finally{connectionSlots.Release();}
        }
        private static Task Reply(NetworkStream stream,int status,JObject body,CancellationToken token)=>ReplyBytes(stream,status,Encoding.UTF8.GetBytes(body.ToString(Formatting.None)),"application/json; charset=utf-8",token);
        private static async Task ReplyBytes(NetworkStream stream,int status,byte[] body,string type,CancellationToken token)
        {
            var header=Encoding.ASCII.GetBytes($"HTTP/1.1 {status} Response\r\nContent-Type: {type}\r\nContent-Length: {body.Length}\r\nConnection: close\r\nCache-Control: no-store\r\n\r\n");
            await stream.WriteAsync(header,token);await stream.WriteAsync(body,token);
        }
    }
}
