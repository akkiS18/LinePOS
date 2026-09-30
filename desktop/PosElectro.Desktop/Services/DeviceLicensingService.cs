using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using Newtonsoft.Json;
using PosElectro.Desktop.Data;

namespace PosElectro.Desktop.Services
{
    public class DeviceLicensingService : IDisposable
    {
        public const string ADMIN_MASTER_PIN = "2846";
        private const string FIREBASE_BASE_URL = "https://line-pos-56296-default-rtdb.firebaseio.com";
        private const string KEY_IS_ACTIVATED = "is_device_activated";
        private const string KEY_ACTIVATION_DATE = "activation_date";
        private const string KEY_DEVICE_ID = "device_id";
        private const string KEY_CUSTOM_DEVICE_NAME = "custom_device_name";

        private readonly DatabaseContext _database;
        private readonly HttpClient _httpClient;
        private readonly CancellationTokenSource _cts = new();

        public string DeviceId { get; }
        public string DeviceModel { get; }
        public bool IsActivated { get; private set; }

        public event Action<bool>? ActivationStateChanged;

        public DeviceLicensingService(DatabaseContext database)
        {
            _database = database;
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };

            DeviceId = ResolveDeviceId();
            DeviceModel = $"{Environment.MachineName} (Windows)";

            // Mahalliy holatni tekshirish
            var settingVal = _database.GetSetting(KEY_IS_ACTIVATED, "");
            IsActivated = settingVal == "true";

            // Orqa fonda Realtime Sync va Heartbeat boshlash
            _ = StartSyncLoopAsync(_cts.Token);
        }

        private string ResolveDeviceId()
        {
            try
            {
                // 1. Windows MachineGuid ni o'qish (o'zgarmas Windows ID)
                using var key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)
                                           .OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
                var guid = key?.GetValue("MachineGuid")?.ToString();
                if (!string.IsNullOrWhiteSpace(guid))
                {
                    // Toza va ixcham ID (masalan 16 ta belgi yoki qisqartirilgan)
                    var cleaned = guid.Replace("-", "").Trim().ToLowerInvariant();
                    if (cleaned.Length > 16) cleaned = cleaned[..16];
                    return cleaned;
                }
            }
            catch
            {
                // Registryga ruxsat bo'lmasa fallback
            }

            // 2. Bazadagi saqlangan ID ni tekshirish
            var savedId = _database.GetSetting(KEY_DEVICE_ID, "");
            if (!string.IsNullOrWhiteSpace(savedId))
            {
                return savedId;
            }

            // 3. Yangi unikal ID yaratib bazada saqlash
            var newId = Guid.NewGuid().ToString("N")[..16].ToLowerInvariant();
            _database.SetSetting(KEY_DEVICE_ID, newId);
            return newId;
        }

        public string GetCustomName()
        {
            return _database.GetSetting(KEY_CUSTOM_DEVICE_NAME, DeviceModel);
        }

        public void SetCustomName(string name)
        {
            _database.SetSetting(KEY_CUSTOM_DEVICE_NAME, name);
        }

        /// <summary>
        /// Admin kod orqali qurilmani faollashtirish (Firebase dan tekshiriladi)
        /// </summary>
        public async Task<bool> ActivateWithAdminPinAsync(string pin)
        {
            try
            {
                var url = $"{FIREBASE_BASE_URL}/settings/globalCode.json";
                var response = await _httpClient.GetAsync(url);
                string remotePin = "1984"; // Default
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadAsStringAsync();
                    if (!string.IsNullOrEmpty(result) && result != "null")
                    {
                        remotePin = result.Trim('"');
                    }
                }

                if (pin.Trim() == remotePin || (string.IsNullOrEmpty(remotePin) && pin.Trim() == "1984"))
                {
                    var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    _database.SetSetting(KEY_IS_ACTIVATED, "true");
                    _database.SetSetting(KEY_ACTIVATION_DATE, now.ToString());

                    IsActivated = true;
                    ActivationStateChanged?.Invoke(true);

                    // Firebase ga sinxronizatsiya
                    _ = SyncStatusToFirebaseAsync(true, now);
                    return true;
                }
            }
            catch
            {
                // Tarmoq xatosi bo'lsa oflayn default zaxira tekshiruvi (1984)
                if (pin.Trim() == "1984")
                {
                    var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    _database.SetSetting(KEY_IS_ACTIVATED, "true");
                    _database.SetSetting(KEY_ACTIVATION_DATE, now.ToString());

                    IsActivated = true;
                    ActivationStateChanged?.Invoke(true);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Qurilmani lokal bloklash (Admin yoki test uchun)
        /// </summary>
        public void BlockDevice()
        {
            _database.SetSetting(KEY_IS_ACTIVATED, "false");
            IsActivated = false;
            ActivationStateChanged?.Invoke(false);

            _ = SyncStatusToFirebaseAsync(false, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        }

        private async Task SyncStatusToFirebaseAsync(bool activated, long timestamp)
        {
            try
            {
                var url = $"{FIREBASE_BASE_URL}/devices/{DeviceId}.json";
                var payload = new
                {
                    deviceId = DeviceId,
                    deviceModel = DeviceModel,
                    isActivated = activated,
                    lastActive = timestamp,
                    appVersion = "1.0.0"
                };

                var json = JsonConvert.SerializeObject(payload);
                using var content = new StringContent(json, Encoding.UTF8, "application/json");
                await _httpClient.PatchAsync(url, content);
            }
            catch
            {
                // Tarmoq bo'lmasa lokalda qoladi
            }
        }

        private async Task StartSyncLoopAsync(CancellationToken ct)
        {
            // Dastlabki kechikish (dastur to'liq yuklanguncha)
            await Task.Delay(1000, ct).ConfigureAwait(false);

            // 0. Boshlang'ich ro'yxatdan o'tish (faqat birinchi marta isActivated: false yuboriladi)
            var localSetting = _database.GetSetting(KEY_IS_ACTIVATED, "");
            if (string.IsNullOrEmpty(localSetting))
            {
                _database.SetSetting(KEY_IS_ACTIVATED, "false");
                await SyncStatusToFirebaseAsync(false, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()).ConfigureAwait(false);
            }

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    var url = $"{FIREBASE_BASE_URL}/devices/{DeviceId}.json";

                    // 1. Heartbeat va qurilma ma'lumotlarini yuborish (isActivated ga tegilmaydi!)
                    var payload = new
                    {
                        deviceId = DeviceId,
                        deviceModel = DeviceModel,
                        lastActive = now,
                        appVersion = "1.0.0"
                    };

                    var json = JsonConvert.SerializeObject(payload);
                    using (var content = new StringContent(json, Encoding.UTF8, "application/json"))
                    {
                        await _httpClient.PatchAsync(url, content, ct).ConfigureAwait(false);
                    }

                    // 2. Firebase dan masofaviy isActivated holatini tekshirish
                    var statusUrl = $"{FIREBASE_BASE_URL}/devices/{DeviceId}/isActivated.json";
                    var resp = await _httpClient.GetAsync(statusUrl, ct).ConfigureAwait(false);
                    if (resp.IsSuccessStatusCode)
                    {
                        var statusStr = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                        statusStr = statusStr.Trim().ToLowerInvariant();

                        if (statusStr == "true")
                        {
                            if (!IsActivated)
                            {
                                _database.SetSetting(KEY_IS_ACTIVATED, "true");
                                IsActivated = true;
                                ActivationStateChanged?.Invoke(true);
                            }
                        }
                        else if (statusStr == "false")
                        {
                            if (IsActivated)
                            {
                                // Admin masofadan blokladi
                                _database.SetSetting(KEY_IS_ACTIVATED, "false");
                                IsActivated = false;
                                ActivationStateChanged?.Invoke(false);
                            }
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                    // Tarmoq xatosi bo'lsa indamay keyingi siklni kutamiz (offline ishlash)
                }

                try
                {
                    // Har 5 soniyada yangilab turish (Admin telefondan yoqqanda tezda ochilishi uchun)
                    await Task.Delay(5000, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        public void Dispose()
        {
            _cts.Cancel();
            _cts.Dispose();
            _httpClient.Dispose();
        }
    }
}
