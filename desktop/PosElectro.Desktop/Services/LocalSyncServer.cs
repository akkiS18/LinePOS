using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
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
        private TcpListener? _listener;
        private readonly DatabaseContext _db;
        private readonly CurrencyService? _currencyService;
        private bool _isRunning;
        public int Port { get; } = 8080;

        private readonly List<LiveClientSession> _liveClients = new();
        private readonly object _clientsLock = new();

        public event Action<string>? LogMessageReceived;
        public event Action<string, string>? ActivityLogged; // (type: "info"|"success"|"warning", message)
        public event Action<SyncClientInfo>? ClientStatusUpdated;
        public event Action<int>? LiveClientsCountChanged;
        public event Action<List<ConnectedClientInfo>>? ConnectedClientsListChanged;
        public event Action? DataSynced;

        public int ConnectedLiveClientsCount
        {
            get
            {
                lock (_clientsLock)
                {
                    return _liveClients.Count;
                }
            }
        }

        public List<ConnectedClientInfo> GetConnectedClients()
        {
            lock (_clientsLock)
            {
                return _liveClients.Select(c => new ConnectedClientInfo
                {
                    Id = c.Id,
                    DeviceName = c.DeviceName,
                    IpAddress = c.IpAddress,
                    ConnectedAt = c.ConnectedAt
                }).ToList();
            }
        }

        public LocalSyncServer(DatabaseContext db, CurrencyService? currencyService = null)
        {
            _db = db;
            _currencyService = currencyService;
            _db.LocalSaleCompleted += OnLocalSaleCompleted;
            _db.LocalProductSaved += OnLocalProductSaved;
            _db.LocalWarehouseSaved += OnLocalWarehouseSaved;
            _db.LocalStockTransferred += OnLocalStockTransferred;
            _db.CardTaxRateChanged += OnCardTaxRateChanged;
            if (_currencyService != null)
            {
                _currencyService.RateUpdated += OnCurrencyRateUpdated;
            }
        }

        private void OnLocalSaleCompleted(Sale sale)
        {
            _ = BroadcastLiveEventAsync("sale_created", sale);
            if (sale.Items != null)
            {
                foreach (var item in sale.Items)
                {
                    if (!string.IsNullOrWhiteSpace(item.ProductGuid))
                    {
                        var prod = _db.GetProductByGuid(item.ProductGuid);
                        if (prod != null)
                        {
                            _ = BroadcastLiveEventAsync("product_updated", prod);
                        }
                    }
                }
            }
        }

        private void OnLocalProductSaved(Product product)
        {
            _ = BroadcastLiveEventAsync("product_updated", product);
        }

        private void OnLocalWarehouseSaved(Warehouse warehouse)
        {
            _ = BroadcastLiveEventAsync("warehouse_updated", warehouse);
        }

        private void OnLocalStockTransferred(StockTransferEvent evt)
        {
            _ = BroadcastLiveEventAsync("stock_transferred", evt);
        }

        private void OnCardTaxRateChanged(double rate)
        {
            _ = BroadcastLiveEventAsync("settings_updated", new
            {
                cardTaxRate = rate,
                usdRate = _currencyService?.GetCachedUsdRate() ?? 12850.0
            });
        }

        private void OnCurrencyRateUpdated(double rate)
        {
            _ = BroadcastLiveEventAsync("settings_updated", new
            {
                cardTaxRate = _db.GetCardTaxRate(),
                usdRate = rate
            });
        }

        public async Task BroadcastLiveEventAsync(string eventType, object data, string? excludeClientId = null)
        {
            List<LiveClientSession> clientsToBroadcast;
            lock (_clientsLock)
            {
                clientsToBroadcast = _liveClients.Where(c => c.Id != excludeClientId).ToList();
            }

            if (clientsToBroadcast.Count == 0) return;

            var json = JsonConvert.SerializeObject(data);
            var payload = $"event: {eventType}\ndata: {json}\n\n";

            var deadClients = new List<LiveClientSession>();
            foreach (var client in clientsToBroadcast)
            {
                try
                {
                    await client.Writer.WriteAsync(payload);
                    await client.Writer.FlushAsync();
                }
                catch
                {
                    deadClients.Add(client);
                }
            }

            if (deadClients.Count > 0)
            {
                lock (_clientsLock)
                {
                    foreach (var dead in deadClients)
                    {
                        _liveClients.Remove(dead);
                        try { dead.Client.Close(); } catch { }
                    }
                }
                LiveClientsCountChanged?.Invoke(ConnectedLiveClientsCount);
                ConnectedClientsListChanged?.Invoke(GetConnectedClients());
            }
        }

        public string GetLocalIpAddress()
        {
            try
            {
                using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0);
                socket.Connect("8.8.8.8", 65530);
                if (socket.LocalEndPoint is IPEndPoint endPoint)
                {
                    return endPoint.Address.ToString();
                }
            }
            catch
            {
                foreach (var ip in Dns.GetHostAddresses(Dns.GetHostName()))
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
                    {
                        return ip.ToString();
                    }
                }
            }
            return "127.0.0.1";
        }

        public void Start()
        {
            if (_isRunning) return;

            try
            {
                _listener = new TcpListener(IPAddress.Any, Port);
                _listener.Start();
                _isRunning = true;
                LogMessageReceived?.Invoke($"🟢 Lokal Wi-Fi Server faol: {GetLocalIpAddress()}:{Port}");
                ActivityLogged?.Invoke("info", $"Wi-Fi Server ishga tushdi ({GetLocalIpAddress()}:{Port})");

                Task.Run(ListenLoop);
            }
            catch (Exception ex)
            {
                var uzb = TranslateToUzbek(ex.Message);
                LogMessageReceived?.Invoke($"❌ Serverni ishga tushirishda xatolik: {uzb}");
                ActivityLogged?.Invoke("error", $"Server xatosi: {uzb}");
            }
        }

        public void Stop()
        {
            _isRunning = false;
            try
            {
                _listener?.Stop();
            }
            catch { }

            lock (_clientsLock)
            {
                foreach (var c in _liveClients)
                {
                    try { c.Client.Close(); } catch { }
                }
                _liveClients.Clear();
            }
            LiveClientsCountChanged?.Invoke(0);

            LogMessageReceived?.Invoke("⚪ Lokal Server to'xtatildi.");
            ActivityLogged?.Invoke("info", "Server to'xtatildi.");
        }

        private async Task ListenLoop()
        {
            while (_isRunning && _listener != null)
            {
                try
                {
                    var client = await _listener.AcceptTcpClientAsync();
                    _ = Task.Run(() => HandleClientAsync(client));
                }
                catch
                {
                    if (!_isRunning) break;
                }
            }
        }

        private async Task HandleClientAsync(TcpClient client)
        {
            var clientIp = (client.Client.RemoteEndPoint as IPEndPoint)?.Address.ToString() ?? "";

            using (client)
            using (var stream = client.GetStream())
            using (var reader = new StreamReader(stream, Encoding.UTF8, false, 4096, leaveOpen: true))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false), 4096, leaveOpen: true))
            {
                try
                {
                    var requestLine = await reader.ReadLineAsync();
                    if (string.IsNullOrWhiteSpace(requestLine)) return;

                    var parts = requestLine.Split(' ');
                    if (parts.Length < 2) return;

                    var method = parts[0].ToUpperInvariant();
                    var url = parts[1];

                    // Read headers
                    int contentLength = 0;
                    string? headerLine;
                    while (!string.IsNullOrEmpty(headerLine = await reader.ReadLineAsync()))
                    {
                        if (headerLine.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                        {
                            int.TryParse(headerLine.Substring("Content-Length:".Length).Trim(), out contentLength);
                        }
                    }

                    if (method == "OPTIONS")
                    {
                        await WriteHttpResponseAsync(writer, 200, "OK", "text/plain", "");
                        return;
                    }

                    var uri = new Uri("http://localhost" + url);
                    var path = uri.AbsolutePath.ToLowerInvariant();

                    // 1. PING: Aloqani tekshirish
                    if (path == "/api/ping")
                    {
                        var pingData = new
                        {
                            status = "ok",
                            name = "Line kassa",
                            ip = GetLocalIpAddress(),
                            port = Port,
                            timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                            productsCount = _db.GetActiveProductsCount(),
                            salesCount = _db.GetTotalSalesCount()
                        };

                        ClientStatusUpdated?.Invoke(new SyncClientInfo
                        {
                            DeviceName = "Android Telefon",
                            IpAddress = clientIp,
                            LastSeen = DateTime.Now
                        });
                        ActivityLogged?.Invoke("info", $"📱 Telefon ulandi ({clientIp}) — aloqa muvaffaqiyatli o'rnatildi.");

                        await WriteHttpResponseAsync(writer, 200, "OK", "application/json", JsonConvert.SerializeObject(pingData));
                        return;
                    }

                    // 1.05 DOWNLOAD FULL DB: Yangi telefon yoki zaxira uchun SQLite bazasini to'liq yuklab berish
                    if (path == "/api/sync/download_db" && method == "GET")
                    {
                        try
                        {
                            var tempFile = Path.Combine(Path.GetTempPath(), $"LinePOS_Export_{DateTime.Now:yyyyMMdd_HHmmss}.db");
                            _db.BackupDatabase(tempFile);
                            ActivityLogged?.Invoke("success", $"📤 Baza to'liq nusxasi yuklab olish uchun berildi: {clientIp}");
                            await WriteHttpFileResponseAsync(writer, 200, "OK", "application/octet-stream", tempFile, "Line_POS_Baza_Nusxasi.db");
                            try { File.Delete(tempFile); } catch { }
                            return;
                        }
                        catch (Exception ex)
                        {
                            await WriteHttpResponseAsync(writer, 500, "Internal Server Error", "application/json", JsonConvert.SerializeObject(new { error = ex.Message }));
                            return;
                        }
                    }

                    // 1.1 SSE EVENTS STREAM: Doimiy jonli sinxron oqimi (Real-Time Live Sync)
                    if (path == "/api/sync/events" && method == "GET")
                    {
                        string devName = "Android Telefon";
                        if (!string.IsNullOrEmpty(uri.Query))
                        {
                            var queryParts = uri.Query.TrimStart('?').Split('&');
                            foreach (var part in queryParts)
                            {
                                var kv = part.Split('=');
                                if (kv.Length == 2 && kv[0].Equals("device", StringComparison.OrdinalIgnoreCase))
                                {
                                    devName = Uri.UnescapeDataString(kv[1]);
                                }
                            }
                        }

                        var session = new LiveClientSession
                        {
                            DeviceName = devName,
                            IpAddress = clientIp,
                            Writer = writer,
                            Client = client,
                            ConnectedAt = DateTime.Now
                        };

                        await writer.WriteLineAsync("HTTP/1.1 200 OK");
                        await writer.WriteLineAsync("Server: PosElectro-Desktop");
                        await writer.WriteLineAsync("Content-Type: text/event-stream; charset=utf-8");
                        await writer.WriteLineAsync("Cache-Control: no-cache, no-transform");
                        await writer.WriteLineAsync("Connection: keep-alive");
                        await writer.WriteLineAsync("Access-Control-Allow-Origin: *");
                        await writer.WriteLineAsync();
                        await writer.FlushAsync();

                        var connectPayload = JsonConvert.SerializeObject(new
                        {
                            status = "connected",
                            serverTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                            productsCount = _db.GetActiveProductsCount(),
                            salesCount = _db.GetTotalSalesCount(),
                            warehousesCount = _db.GetWarehouses(includeDeleted: false).Count,
                            cardTaxRate = _db.GetCardTaxRate(),
                            usdRate = _currencyService?.GetCachedUsdRate() ?? 12850.0
                        });
                        await writer.WriteAsync($"event: connected\ndata: {connectPayload}\n\n");
                        await writer.FlushAsync();

                        lock (_clientsLock)
                        {
                            _liveClients.Add(session);
                        }
                        LiveClientsCountChanged?.Invoke(ConnectedLiveClientsCount);
                        ConnectedClientsListChanged?.Invoke(GetConnectedClients());
                        ClientStatusUpdated?.Invoke(new SyncClientInfo
                        {
                            DeviceName = devName,
                            IpAddress = clientIp,
                            LastSeen = DateTime.Now
                        });
                        ActivityLogged?.Invoke("success", $"🟢 Telefon ulandi: {devName} ({clientIp})");

                        try
                        {
                            while (_isRunning && client.Connected)
                            {
                                await Task.Delay(10000);
                                await writer.WriteAsync(": ping\n\n");
                                await writer.FlushAsync();
                            }
                        }
                        catch { }
                        finally
                        {
                            lock (_clientsLock)
                            {
                                _liveClients.Remove(session);
                            }
                            LiveClientsCountChanged?.Invoke(ConnectedLiveClientsCount);
                            ConnectedClientsListChanged?.Invoke(GetConnectedClients());
                            ActivityLogged?.Invoke("info", $"⚪ Telefon uzildi: {devName} ({clientIp})");
                        }
                        return;
                    }

                    // 1.2 GET DELTA: Ma'lum vaqtdan keyingi o'zgarishlar (Oflayndan chiqqanda)
                    if (path == "/api/sync/delta" && method == "GET")
                    {
                        long since = 0;
                        if (!string.IsNullOrEmpty(uri.Query))
                        {
                            var queryParts = uri.Query.TrimStart('?').Split('&');
                            foreach (var part in queryParts)
                            {
                                var kv = part.Split('=');
                                if (kv.Length == 2 && kv[0].Equals("since", StringComparison.OrdinalIgnoreCase))
                                {
                                    long.TryParse(kv[1], out since);
                                }
                            }
                        }

                        var products = _db.GetProductsChangedSince(since);
                        var sales = _db.GetSalesSince(since);
                        var warehouses = _db.GetWarehousesChangedSince(since);
                        var stocks = since <= 0 ? _db.GetAllProductStocks() : _db.GetProductStocksChangedSince(since);

                        var deltaResp = new
                        {
                            success = true,
                            products = products,
                            sales = sales,
                            warehouses = warehouses,
                            productStocks = stocks,
                            serverTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                        };
                        await WriteHttpResponseAsync(writer, 200, "OK", "application/json", JsonConvert.SerializeObject(deltaResp));
                        return;
                    }

                    // Read POST body if available
                    string body = "";
                    if (contentLength > 0)
                    {
                        var buffer = new char[contentLength];
                        int totalRead = 0;
                        while (totalRead < contentLength)
                        {
                            int read = await reader.ReadAsync(buffer, totalRead, contentLength - totalRead);
                            if (read <= 0) break;
                            totalRead += read;
                        }
                        body = new string(buffer, 0, totalRead);
                    }

                    // 1.3 LIVE SALE: Telefondan darhol kelgan jonli savdo cheki
                    if (path == "/api/sync/live_sale" && method == "POST")
                    {
                        var liveSale = JsonConvert.DeserializeObject<Sale>(body);
                        if (liveSale != null)
                        {
                            liveSale.IsSynced = true;
                            _db.InsertSale(liveSale, isFromSync: true, deductStock: true);
                            DataSynced?.Invoke();
                            ActivityLogged?.Invoke("success", $"⚡ Telefondan jonli savdo qabul qilindi: {liveSale.TotalAmount:N0} so'm ({liveSale.PaymentTypeDisplay})");
                            _ = BroadcastLiveEventAsync("sale_created", liveSale);
                        }
                        var resp = new { success = true, serverTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() };
                        await WriteHttpResponseAsync(writer, 200, "OK", "application/json", JsonConvert.SerializeObject(resp));
                        return;
                    }

                    // 1.4 LIVE PRODUCT: Telefondan darhol kelgan tovar qo'shish/tahrirlash
                    if (path == "/api/sync/live_product" && method == "POST")
                    {
                        var liveProduct = JsonConvert.DeserializeObject<Product>(body);
                        if (liveProduct != null)
                        {
                            _db.UpsertSyncProduct(liveProduct);
                            DataSynced?.Invoke();
                            ActivityLogged?.Invoke("info", $"⚡ Telefondan tovar yangilandi: {liveProduct.Name}");
                            _ = BroadcastLiveEventAsync("product_updated", liveProduct);
                        }
                        var resp = new { success = true, serverTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() };
                        await WriteHttpResponseAsync(writer, 200, "OK", "application/json", JsonConvert.SerializeObject(resp));
                        return;
                    }

                    // 1.45 LIVE WAREHOUSE: Telefondan darhol kelgan ombor qo'shish/tahrirlash
                    if (path == "/api/sync/live_warehouse" && method == "POST")
                    {
                        var liveWarehouse = JsonConvert.DeserializeObject<Warehouse>(body);
                        if (liveWarehouse != null)
                        {
                            _db.UpsertSyncWarehouse(liveWarehouse);
                            DataSynced?.Invoke();
                            ActivityLogged?.Invoke("info", $"⚡ Telefondan ombor yangilandi: {liveWarehouse.Name}");
                            _ = BroadcastLiveEventAsync("warehouse_updated", liveWarehouse);
                        }
                        var resp = new { success = true, serverTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() };
                        await WriteHttpResponseAsync(writer, 200, "OK", "application/json", JsonConvert.SerializeObject(resp));
                        return;
                    }

                    // 1.46 PUSH WAREHOUSES: Telefondagi omborlarni qabul qilish
                    if (path == "/api/sync/push_warehouses" && method == "POST")
                    {
                        var warehousesList = JsonConvert.DeserializeObject<List<Warehouse>>(body) ?? new List<Warehouse>();
                        foreach (var w in warehousesList)
                        {
                            _db.UpsertSyncWarehouse(w);
                        }
                        if (warehousesList.Count > 0)
                        {
                            DataSynced?.Invoke();
                            ActivityLogged?.Invoke("info", $"📥 Telefondan {warehousesList.Count} ta ombor qabul qilindi.");
                        }
                        var resp = new { success = true, count = warehousesList.Count, serverTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() };
                        await WriteHttpResponseAsync(writer, 200, "OK", "application/json", JsonConvert.SerializeObject(resp));
                        return;
                    }

                    // 1.47 PUSH PRODUCTS: Telefondan oflaynda o'zgargan tovarlarni qabul qilish
                    if (path == "/api/sync/push_products" && method == "POST")
                    {
                        var prods = JsonConvert.DeserializeObject<List<Product>>(body) ?? new List<Product>();
                        foreach (var p in prods)
                        {
                            _db.UpsertSyncProduct(p);
                        }
                        if (prods.Count > 0)
                        {
                            DataSynced?.Invoke();
                            ActivityLogged?.Invoke("info", $"📥 Telefondan {prods.Count} ta tovar qabul qilindi.");
                        }
                        var resp = new { success = true, count = prods.Count, serverTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() };
                        await WriteHttpResponseAsync(writer, 200, "OK", "application/json", JsonConvert.SerializeObject(resp));
                        return;
                    }

                    // 1.48 LIVE STOCK TRANSFER: Telefondan omborlararo tovar ko'chirish
                    if (path == "/api/sync/live_transfer_stock" && method == "POST")
                    {
                        var evt = JsonConvert.DeserializeObject<StockTransferEvent>(body);
                        if (evt != null && !string.IsNullOrWhiteSpace(evt.ProductGuid))
                        {
                            _db.TransferStock(evt.ProductGuid, evt.FromWarehouseGuid, evt.ToWarehouseGuid, evt.Quantity, isFromSync: true);
                            DataSynced?.Invoke();
                            ActivityLogged?.Invoke("info", $"⚡ Telefondan tovar ko'chirildi: {evt.Quantity:N0} dona");
                            _ = BroadcastLiveEventAsync("stock_transferred", evt);
                        }
                        var resp = new { success = true, serverTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() };
                        await WriteHttpResponseAsync(writer, 200, "OK", "application/json", JsonConvert.SerializeObject(resp));
                        return;
                    }

                    // 1.49 PUSH STOCKS: Telefondan omborlardagi qoldiqlarni qabul qilish
                    if (path == "/api/sync/push_stocks" && method == "POST")
                    {
                        var stocksList = JsonConvert.DeserializeObject<List<ProductStock>>(body) ?? new List<ProductStock>();
                        foreach (var st in stocksList)
                        {
                            if (!string.IsNullOrWhiteSpace(st.ProductGuid) && !string.IsNullOrWhiteSpace(st.WarehouseGuid) && st.WarehouseGuid != "null")
                            {
                                _db.SetProductStockInWarehouse(st.ProductGuid, st.WarehouseGuid, st.Quantity);
                            }
                        }
                        if (stocksList.Count > 0)
                        {
                            DataSynced?.Invoke();
                            ActivityLogged?.Invoke("info", $"📥 Telefondan {stocksList.Count} ta ombor qoldig'i qabul qilindi.");
                        }
                        var resp = new { success = true, count = stocksList.Count, serverTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() };
                        await WriteHttpResponseAsync(writer, 200, "OK", "application/json", JsonConvert.SerializeObject(resp));
                        return;
                    }

                    // 1.5 PUSH UNSYNCED: Oflaynda to'plangan cheklarni qabul qilish
                    if (path == "/api/sync/push_unsynced" && method == "POST")
                    {
                        var unsyncedSales = JsonConvert.DeserializeObject<List<Sale>>(body) ?? new List<Sale>();
                        int savedCount = 0;
                        foreach (var s in unsyncedSales)
                        {
                            try
                            {
                                s.IsSynced = true;
                                _db.InsertSale(s, isFromSync: true, deductStock: true);
                                savedCount++;
                            }
                            catch (Exception ex)
                            {
                                LogMessageReceived?.Invoke($"❌ Oflayn chek saqlanmadi ({s.Guid}): {ex.Message}");
                            }
                        }
                        if (savedCount > 0)
                        {
                            DataSynced?.Invoke();
                            ActivityLogged?.Invoke("success", $"📥 Oflaynda to'plangan {savedCount} ta chek to'liq sinxronlandi.");
                        }
                        var resp = new { success = true, count = savedCount, serverTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() };
                        await WriteHttpResponseAsync(writer, 200, "OK", "application/json", JsonConvert.SerializeObject(resp));
                        return;
                    }

                    // 2. CHECK: O'tkazishdan oldin tahlil (Omborda va Cheklarda o'zgarishlar sonini hisoblash)
                    if (path == "/api/sync/check" && method == "POST")
                    {
                        var checkReq = JsonConvert.DeserializeObject<SyncCheckRequest>(body) ?? new SyncCheckRequest();

                        int desktopProducts = _db.GetActiveProductsCount();
                        int desktopSales = _db.GetTotalSalesCount();
                        int desktopModifiedProducts = _db.GetModifiedProductsCount(checkReq.PhoneLastSyncTime);
                        int desktopUnsyncedSales = _db.GetUnsyncedSalesCount();

                        bool hasConflict = (checkReq.PhoneModifiedProducts > 0 || checkReq.PhoneUnsyncedSales > 0) &&
                                           (desktopModifiedProducts > 0 || desktopUnsyncedSales > 0);

                        ClientStatusUpdated?.Invoke(new SyncClientInfo
                        {
                            DeviceName = string.IsNullOrWhiteSpace(checkReq.DeviceName) ? "Android Telefon" : checkReq.DeviceName,
                            IpAddress = clientIp,
                            LastSeen = DateTime.Now,
                            PhoneProductsCount = checkReq.PhoneProductsCount,
                            PhoneSalesCount = checkReq.PhoneSalesCount
                        });

                        var resp = new
                        {
                            status = "ok",
                            desktopProductsCount = desktopProducts,
                            desktopSalesCount = desktopSales,
                            desktopModifiedProducts = desktopModifiedProducts,
                            desktopUnsyncedSales = desktopUnsyncedSales,
                            phoneModifiedProducts = checkReq.PhoneModifiedProducts,
                            phoneUnsyncedSales = checkReq.PhoneUnsyncedSales,
                            hasConflict = hasConflict
                        };

                        ActivityLogged?.Invoke("info", $"🔍 O'tkazish tekshiruvi: Telefonda {checkReq.PhoneModifiedProducts} tovar, {checkReq.PhoneUnsyncedSales} savdo; Kompyuterda {desktopModifiedProducts} tovar.");

                        await WriteHttpResponseAsync(writer, 200, "OK", "application/json", JsonConvert.SerializeObject(resp));
                        return;
                    }

                    // 3. TRANSFER: Aniq yo'nalishli o'tkazish yoki Smart Merge
                    if (path == "/api/sync/transfer" && method == "POST")
                    {
                        var transferReq = JsonConvert.DeserializeObject<SyncTransferRequest>(body) ?? new SyncTransferRequest();
                        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                        ClientStatusUpdated?.Invoke(new SyncClientInfo
                        {
                            DeviceName = string.IsNullOrWhiteSpace(transferReq.DeviceName) ? "Android Telefon" : transferReq.DeviceName,
                            IpAddress = clientIp,
                            LastSeen = DateTime.Now
                        });

                        if (transferReq.Direction == "phone_to_desktop")
                        {
                            int pCount = 0;
                            int sCount = 0;

                            if (transferReq.Products != null)
                            {
                                foreach (var p in transferReq.Products)
                                {
                                    _db.UpsertSyncProduct(p);
                                    pCount++;
                                }
                            }

                            if (transferReq.Warehouses != null)
                            {
                                foreach (var w in transferReq.Warehouses)
                                {
                                    _db.UpsertSyncWarehouse(w);
                                }
                            }

                            if (transferReq.Sales != null)
                            {
                                foreach (var s in transferReq.Sales)
                                {
                                    try
                                    {
                                        s.IsSynced = true;
                                        _db.InsertSale(s, isFromSync: true);
                                        sCount++;
                                    }
                                    catch (Exception ex)
                                    {
                                        var uzbErr = TranslateToUzbek(ex.Message);
                                        LogMessageReceived?.Invoke($"❌ Chek saqlashda xatolik ({s.Guid}): {uzbErr}");
                                        ActivityLogged?.Invoke("warning", $"Chek saqlanmadi ({s.Guid}): {uzbErr}");
                                    }
                                }
                            }

                            DataSynced?.Invoke();
                            ActivityLogged?.Invoke("success", $"📥 Telefondan qabul qilindi: {pCount} ta tovar, {sCount} ta chek.");

                            var autoBackupPath = _db.AutoBackup("phone_sync");
                            if (!string.IsNullOrEmpty(autoBackupPath))
                            {
                                ActivityLogged?.Invoke("info", "💾 Kompyuterda telefon bazasining xavfsiz zaxira nusxasi avtomatik saqlandi.");
                            }

                            var transferResp = new
                            {
                                success = true,
                                direction = "phone_to_desktop",
                                syncedProducts = pCount,
                                syncedSales = sCount,
                                serverTimestamp = now
                            };
                            await WriteHttpResponseAsync(writer, 200, "OK", "application/json", JsonConvert.SerializeObject(transferResp));
                            return;
                        }
                        else if (transferReq.Direction == "desktop_to_phone")
                        {
                            var allProducts = _db.GetAllProducts(includeDeleted: true);
                            var allWarehouses = _db.GetWarehouses(includeDeleted: true);
                            var recentSales = _db.GetSalesSince(DateTimeOffset.UtcNow.AddDays(-30).ToUnixTimeMilliseconds());

                            // Mark desktop sales as synced
                            var unsyncedGuids = recentSales.Where(s => !s.IsSynced).Select(s => s.Guid).ToList();
                            if (unsyncedGuids.Count > 0)
                            {
                                _db.MarkSalesSynced(unsyncedGuids);
                            }

                            DataSynced?.Invoke();
                            ActivityLogged?.Invoke("success", $"📤 Kompyuterdagi {allProducts.Count} ta tovar va {recentSales.Count} ta chek telefonga uzatildi.");

                            var transferResp = new
                            {
                                success = true,
                                direction = "desktop_to_phone",
                                products = allProducts,
                                warehouses = allWarehouses,
                                sales = recentSales,
                                syncedProducts = allProducts.Count,
                                syncedSales = recentSales.Count,
                                serverTimestamp = now
                            };
                            await WriteHttpResponseAsync(writer, 200, "OK", "application/json", JsonConvert.SerializeObject(transferResp));
                            return;
                        }
                        else if (transferReq.Direction == "smart_merge")
                        {
                            // Avval telefondagilarni kompyuterga qo'shish/yangilash
                            int pCount = 0;
                            int sCount = 0;
                            if (transferReq.Products != null)
                            {
                                foreach (var p in transferReq.Products)
                                {
                                    _db.UpsertSyncProduct(p);
                                    pCount++;
                                }
                            }
                            if (transferReq.Warehouses != null)
                            {
                                foreach (var w in transferReq.Warehouses)
                                {
                                    _db.UpsertSyncWarehouse(w);
                                }
                            }
                            if (transferReq.Sales != null)
                            {
                                foreach (var s in transferReq.Sales)
                                {
                                    try
                                    {
                                        s.IsSynced = true;
                                        _db.InsertSale(s, isFromSync: true);
                                        sCount++;
                                    }
                                    catch (Exception ex)
                                    {
                                        var uzbErr = TranslateToUzbek(ex.Message);
                                        LogMessageReceived?.Invoke($"❌ Chek saqlashda xatolik ({s.Guid}): {uzbErr}");
                                        ActivityLogged?.Invoke("warning", $"Chek saqlanmadi ({s.Guid}): {uzbErr}");
                                    }
                                }
                            }

                            // So'ngra barcha tovarlar va savdolarni telefonga qaytarish
                            var allProducts = _db.GetAllProducts(includeDeleted: true);
                            var allWarehouses = _db.GetWarehouses(includeDeleted: true);
                            var allSales = _db.GetSalesSince(DateTimeOffset.UtcNow.AddDays(-60).ToUnixTimeMilliseconds());

                            // Mark all desktop sales as synced
                            var unsyncedGuids = allSales.Where(s => !s.IsSynced).Select(s => s.Guid).ToList();
                            if (unsyncedGuids.Count > 0)
                            {
                                _db.MarkSalesSynced(unsyncedGuids);
                            }

                            DataSynced?.Invoke();

                            ActivityLogged?.Invoke("success", $"🔄 Aqlli birlashtirish (Smart Merge) yakunlandi: {allProducts.Count} ta tovar, {allSales.Count} ta chek tenglashtirildi.");

                            var mergeBackupPath = _db.AutoBackup("smart_merge");
                            if (!string.IsNullOrEmpty(mergeBackupPath))
                            {
                                ActivityLogged?.Invoke("info", "💾 Kompyuterda birlashtirilgan bazaning zaxira nusxasi avtomatik saqlandi.");
                            }

                            var transferResp = new
                            {
                                success = true,
                                direction = "smart_merge",
                                products = allProducts,
                                warehouses = allWarehouses,
                                sales = allSales,
                                syncedProducts = allProducts.Count,
                                syncedSales = allSales.Count,
                                serverTimestamp = now
                            };
                            await WriteHttpResponseAsync(writer, 200, "OK", "application/json", JsonConvert.SerializeObject(transferResp));
                            return;
                        }
                    }

                    // 4. Eski PULL va PUSH (moslik uchun saqlab qolindi)
                    if (path == "/api/sync/pull" && method == "GET")
                    {
                        var allProducts = _db.GetAllProducts(includeDeleted: true);
                        var payload = new SyncPullPayload
                        {
                            Products = allProducts,
                            ServerTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                        };
                        await WriteHttpResponseAsync(writer, 200, "OK", "application/json", JsonConvert.SerializeObject(payload));
                        return;
                    }

                    if (path == "/api/sync/push" && method == "POST")
                    {
                        var payload = JsonConvert.DeserializeObject<SyncPushPayload>(body);
                        int pCount = 0;
                        if (payload?.Products != null)
                        {
                            foreach (var p in payload.Products)
                            {
                                _db.UpsertSyncProduct(p);
                                pCount++;
                            }
                        }
                        DataSynced?.Invoke();
                        ActivityLogged?.Invoke("success", $"Telefondan {pCount} ta tovar qabul qilindi.");
                        await WriteHttpResponseAsync(writer, 200, "OK", "application/json", "{\"success\":true}");
                        return;
                    }

                    await WriteHttpResponseAsync(writer, 404, "Not Found", "application/json", "{\"error\":\"Not found\"}");
                }
                catch (Exception ex)
                {
                    try
                    {
                        var uzbError = TranslateToUzbek(ex.Message);
                        ActivityLogged?.Invoke("error", $"Ulanishda xatolik: {uzbError}");
                        await WriteHttpResponseAsync(writer, 500, "Internal Server Error", "application/json", "{\"error\":\"" + uzbError.Replace("\"", "\\\"") + "\"}");
                    }
                    catch { }
                }
            }
        }

        private static string TranslateToUzbek(string? message)
        {
            if (string.IsNullOrWhiteSpace(message)) return "Noma'lum xatolik yuz berdi";

            if (message.Contains("Only one usage of each socket address", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("Address already in use", StringComparison.OrdinalIgnoreCase))
            {
                return "Port (8080) band! Boshqa dastur ushbu portdan foydalanmoqda.";
            }

            if (message.Contains("No connection could be made", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("actively refused", StringComparison.OrdinalIgnoreCase))
            {
                return "Aloqa o'rnatilmadi: Qurilma tarmoqqa ulanmagan yoki serverga kirish taqiqlangan.";
            }

            if (message.Contains("forcibly closed", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("Connection reset", StringComparison.OrdinalIgnoreCase))
            {
                return "Telefon aloqasi kutilmaganda uzildi.";
            }

            if (message.Contains("timed out", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("Timeout", StringComparison.OrdinalIgnoreCase))
            {
                return "Kutish vaqti tugadi (Timeout). Tarmoq tezligini tekshiring.";
            }

            if (message.Contains("database is locked", StringComparison.OrdinalIgnoreCase))
            {
                return "Ma'lumotlar bazasi band qilingan, ozroq kuting.";
            }

            if (message.Contains("JSON", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("Unexpected character", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("Cannot deserialize", StringComparison.OrdinalIgnoreCase))
            {
                return "Ma'lumot formati noto'g'ri (JSON o'qishda xatolik).";
            }

            if (message.Contains("Access denied", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("Unauthorized", StringComparison.OrdinalIgnoreCase))
            {
                return "Kirishga ruxsat berilmadi.";
            }

            if (message.Contains("Not found", StringComparison.OrdinalIgnoreCase))
            {
                return "So'ralgan manzil yoki ma'lumot topilmadi.";
            }

            return message;
        }

        private static async Task WriteHttpResponseAsync(StreamWriter writer, int statusCode, string statusDescription, string contentType, string content)
        {
            var contentBytes = Encoding.UTF8.GetBytes(content);

            await writer.WriteLineAsync($"HTTP/1.1 {statusCode} {statusDescription}");
            await writer.WriteLineAsync("Server: PosElectro-Desktop");
            await writer.WriteLineAsync($"Content-Type: {contentType}; charset=utf-8");
            await writer.WriteLineAsync($"Content-Length: {contentBytes.Length}");
            await writer.WriteLineAsync("Access-Control-Allow-Origin: *");
            await writer.WriteLineAsync("Access-Control-Allow-Methods: GET, POST, OPTIONS");
            await writer.WriteLineAsync("Access-Control-Allow-Headers: Content-Type, Authorization");
            await writer.WriteLineAsync("Connection: close");
            await writer.WriteLineAsync();

            await writer.FlushAsync();
            await writer.BaseStream.WriteAsync(contentBytes, 0, contentBytes.Length);
            await writer.BaseStream.FlushAsync();
        }

        private static async Task WriteHttpFileResponseAsync(StreamWriter writer, int statusCode, string statusDescription, string contentType, string filePath, string downloadFileName)
        {
            var fileInfo = new FileInfo(filePath);
            await writer.WriteLineAsync($"HTTP/1.1 {statusCode} {statusDescription}");
            await writer.WriteLineAsync("Server: PosElectro-Desktop");
            await writer.WriteLineAsync($"Content-Type: {contentType}");
            await writer.WriteLineAsync($"Content-Length: {fileInfo.Length}");
            await writer.WriteLineAsync($"Content-Disposition: attachment; filename=\"{downloadFileName}\"");
            await writer.WriteLineAsync("Access-Control-Allow-Origin: *");
            await writer.WriteLineAsync("Access-Control-Allow-Methods: GET, POST, OPTIONS");
            await writer.WriteLineAsync("Access-Control-Allow-Headers: Content-Type, Authorization");
            await writer.WriteLineAsync("Connection: close");
            await writer.WriteLineAsync();

            await writer.FlushAsync();
            using (var fs = File.OpenRead(filePath))
            {
                await fs.CopyToAsync(writer.BaseStream);
            }
            await writer.BaseStream.FlushAsync();
        }
    }
}
