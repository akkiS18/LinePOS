using System;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using PosElectro.Desktop.Sync;

namespace PosElectro.Desktop.Services;

// The existing token hash proves this is the paired PC without broadcasting the token.
internal static class LanDiscoveryResponder
{
    public static async Task Run(WifiSyncStore store, int port, CancellationToken token, Action<string> log)
    {
        try
        {
            using var socket = new UdpClient(new IPEndPoint(IPAddress.Any, port));
            using var registration = token.Register(socket.Dispose);
            while (!token.IsCancellationRequested)
            {
                UdpReceiveResult packet;
                try { packet = await socket.ReceiveAsync(token); }
                catch (SocketException) when (!token.IsCancellationRequested)
                { await Task.Delay(250, token); continue; }
                try
                {
                    var ip = packet.RemoteEndPoint.Address.GetAddressBytes();
                    bool local = ip.Length == 4 && (ip[0] == 10 || ip[0] == 192 && ip[1] == 168 ||
                        ip[0] == 172 && ip[1] >= 16 && ip[1] <= 31 || ip[0] == 169 && ip[1] == 254);
                    if (!local || packet.Buffer.Length > 1024) continue;
                    var request = JObject.Parse(Encoding.UTF8.GetString(packet.Buffer));
                    if ((string?)request["kind"] != "linepos-discover-v1") continue;
                    var nonce = (string?)request["nonce"];
                    var device = (string?)request["deviceId"];
                    if (!Guid.TryParseExact(nonce, "D", out _) || !Guid.TryParseExact(device, "D", out _)) continue;
                    // Read each time: restore/revocation must take effect without restarting.
                    var serverId = store.ServerId;
                    if ((string?)request["serverId"] != serverId) continue;
                    using var db = store.Open();
                    using var command = db.CreateCommand();
                    command.CommandText = "SELECT token_hash FROM sync_devices WHERE device_id=@id ORDER BY rowid DESC LIMIT 16";
                    command.Parameters.AddWithValue("@id", device!);
                    using var reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        var key = Convert.FromHexString(reader.GetString(0));
                        using var hmac = new HMACSHA256(key);
                        var proof = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes($"linepos-discover-v1\n{nonce}\n{serverId}\n{port}")));
                        var response = new JObject { ["kind"] = "linepos-discover-v1", ["nonce"] = nonce,
                            ["serverId"] = serverId, ["port"] = port, ["proof"] = proof };
                        var bytes = Encoding.UTF8.GetBytes(response.ToString(Newtonsoft.Json.Formatting.None));
                        await socket.SendAsync(bytes.AsMemory(), packet.RemoteEndPoint, token);
                    }
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { break; }
                catch (Exception e) when (e is Newtonsoft.Json.JsonException || e is FormatException || e is ArgumentException)
                { /* Ignore malformed local datagrams. */ }
                catch (Exception e) when (e is Microsoft.Data.Sqlite.SqliteException || e is SocketException)
                { await Task.Delay(250, token); } // Network/DB replacement can be transient.
            }
        }
        catch (Exception) when (token.IsCancellationRequested) { }
        catch (Exception e) { log($"Desktopni avtomatik topish ishlamadi: {e.Message}. IP yoki QR orqali ulanish mumkin."); }
    }
}
