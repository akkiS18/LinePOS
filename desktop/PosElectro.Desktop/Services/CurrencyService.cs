using System;
using System.Globalization;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using PosElectro.Desktop.Data;

namespace PosElectro.Desktop.Services
{
    public class CurrencyService
    {
        private const string CBU_API_URL = "https://cbu.uz/uz/arkhiv-kursov-valyut/json/";
        private const string KEY_USD_RATE = "last_usd_rate";
        private const string KEY_LAST_UPDATED = "last_usd_rate_updated";
        public const double DEFAULT_USD_RATE = 12850.0;

        private readonly DatabaseContext _db;
        private readonly HttpClient _httpClient;
        private double _cachedRate;

        public event Action<double>? RateUpdated;

        public CurrencyService(DatabaseContext db)
        {
            _db = db;
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(6)
            };

            // Keshdagi kursni bazadan yuklab olamiz
            var savedRateStr = _db.GetSetting(KEY_USD_RATE, DEFAULT_USD_RATE.ToString(CultureInfo.InvariantCulture));
            if (double.TryParse(savedRateStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var rate) && rate > 0)
            {
                _cachedRate = rate;
            }
            else
            {
                _cachedRate = DEFAULT_USD_RATE;
            }
        }

        public double GetCachedUsdRate() => _cachedRate;

        public async Task<(bool success, double rate, string message)> FetchLatestUsdRateAsync()
        {
            try
            {
                var response = await _httpClient.GetStringAsync(CBU_API_URL);
                if (string.IsNullOrWhiteSpace(response))
                {
                    return (false, _cachedRate, "⚠️ Markaziy bank serveridan bo'sh javob keldi. Oxirgi kesh kursi ishlatilmoqda.");
                }

                var jsonArray = JArray.Parse(response);
                double? foundRate = null;

                foreach (var item in jsonArray)
                {
                    if (string.Equals(item["Ccy"]?.ToString(), "USD", StringComparison.OrdinalIgnoreCase))
                    {
                        var rateStr = item["Rate"]?.ToString();
                        if (double.TryParse(rateStr?.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
                        {
                            foundRate = parsed;
                            break;
                        }
                    }
                }

                if (foundRate.HasValue && foundRate.Value > 0)
                {
                    _cachedRate = foundRate.Value;
                    _db.SetSetting(KEY_USD_RATE, _cachedRate.ToString(CultureInfo.InvariantCulture));
                    _db.SetSetting(KEY_LAST_UPDATED, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString());

                    RateUpdated?.Invoke(_cachedRate);

                    var rateFormatted = _cachedRate % 1 == 0 ? $"{_cachedRate:N0}" : $"{_cachedRate:N2}";
                    return (true, _cachedRate, $"✅ CBU kursi yangilandi: 1$ = {rateFormatted} so'm");
                }
                else
                {
                    return (false, _cachedRate, "⚠️ Kurs ma'lumotlaridan USD topilmadi. Kesh kursi ishlatilmoqda.");
                }
            }
            catch (HttpRequestException)
            {
                return (false, _cachedRate, "⚠️ Internetga ulanib bo'lmadi! Kursni yangilash uchun internetni yoqing. Hozirda oxirgi kesh kursi ishlatilmoqda.");
            }
            catch (TaskCanceledException)
            {
                return (false, _cachedRate, "⚠️ Internet aloqasi juda sekin yoki ulanish vaqti tugadi. Kesh kursi ishlatilmoqda.");
            }
            catch (Exception ex)
            {
                return (false, _cachedRate, $"⚠️ Kursni olishda xatolik: {ex.Message}. Kesh kursi ishlatilmoqda.");
            }
        }
    }
}
