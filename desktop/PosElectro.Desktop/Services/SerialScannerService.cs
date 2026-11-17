using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.Win32;

namespace PosElectro.Desktop.Services
{
    /// <summary>
    /// Skaner "USB Serial" (COM port) rejimida bo'lganda uni fonda tinglaydi.
    /// Bu rejimda skaner klaviatura emas, shuning uchun kodlar Telegram va boshqa oynalarga yozilmaydi.
    /// Kassa oynasi yopiq (trayda) yoki boshqa dastur faol bo'lsa ham shtrix-kod shu yerga keladi.
    ///
    /// Portni topish: Windows ro'yxatidagi (HARDWARE\DEVICEMAP\SERIALCOMM) USB-COM portlar tinglanadi.
    /// Kompyuterning o'z COM1 porti va Bluetooth portlari ochilmaydi. Birinchi shtrix-kod kelgan port
    /// eslab qolinadi va qolganlari yopiladi, shunda boshqa COM qurilmalarga xalaqit berilmaydi.
    /// </summary>
    public sealed class SerialScannerService : IDisposable
    {
        public const string SettingKey = "scanner_com_port";

        private readonly object _lock = new();
        private readonly Dictionary<string, SerialPort> _open = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, StringBuilder> _buffers = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, DateTime> _lastByte = new(StringComparer.OrdinalIgnoreCase);
        private readonly Func<string> _loadPort;
        private readonly Action<string> _savePort;
        private Timer? _scanTimer;
        private Timer? _flushTimer;
        private string _lockedPort;
        private bool _disposed;

        /// <summary>To'liq shtrix-kod (fon oqimida chaqiriladi — UI oqimiga o'tkazish chaqiruvchining ishi).</summary>
        public event Action<string>? BarcodeScanned;

        /// <summary>Holat o'zgarganda (port ulandi/uzildi) — tray maslahati uchun.</summary>
        public event Action? StatusChanged;

        public SerialScannerService(Func<string> loadPort, Action<string> savePort)
        {
            _loadPort = loadPort;
            _savePort = savePort;
            _lockedPort = SafeLoad();
        }

        /// <summary>"COM3" — tinglanayotgan skaner porti; hali aniqlanmagan bo'lsa bo'sh.</summary>
        public string ActivePort
        {
            get { lock (_lock) return !string.IsNullOrEmpty(_lockedPort) && _open.ContainsKey(_lockedPort) ? _lockedPort : string.Empty; }
        }

        public int ListeningCount { get { lock (_lock) return _open.Count; } }

        public void Start()
        {
            // Har 3 soniyada yangi ulangan / uzilgan portlarni tekshiradi (skaner qayta ulanganda o'zi tiklanadi)
            _scanTimer = new Timer(_ => RefreshPorts(), null, 0, 3000);
            // Oxirida CR/LF yubormaydigan skanerlar uchun: 150 ms jimlikdan keyin kod tugadi deb hisoblanadi
            _flushTimer = new Timer(_ => FlushIdle(), null, 150, 75);
        }

        private string SafeLoad()
        {
            try { return _loadPort() ?? string.Empty; } catch { return string.Empty; }
        }

        /// <summary>USB-COM portlar ro'yxati (COM1 kabi platadagi va Bluetooth portlarsiz).</summary>
        internal static List<string> FindUsbSerialPorts()
        {
            var result = new List<string>();
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DEVICEMAP\SERIALCOMM");
                if (key == null) return result;
                foreach (var name in key.GetValueNames())
                {
                    if (key.GetValue(name) is not string port || string.IsNullOrWhiteSpace(port)) continue;
                    if (IsExcludedDevice(name)) continue;
                    result.Add(port.Trim());
                }
            }
            catch { }
            return result.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        /// <summary>Platadagi COM port (\Device\Serial0) va Bluetooth virtual portlari tinglanmaydi.</summary>
        public static bool IsExcludedDevice(string deviceName)
        {
            var n = deviceName ?? string.Empty;
            if (n.IndexOf("Bth", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (System.Text.RegularExpressions.Regex.IsMatch(n, @"^\\Device\\Serial\d+$", System.Text.RegularExpressions.RegexOptions.IgnoreCase)) return true;
            return false;
        }

        private void RefreshPorts()
        {
            if (_disposed) return;
            List<string> present = FindUsbSerialPorts();
            bool changed = false;

            lock (_lock)
            {
                // Uzilgan portlarni yopish
                foreach (var name in _open.Keys.ToList())
                {
                    if (!present.Contains(name, StringComparer.OrdinalIgnoreCase) || !_open[name].IsOpen)
                    {
                        ClosePort(name);
                        changed = true;
                    }
                }

                // Skaner porti aniq bo'lsa — faqat shu, aks holda barcha USB-COM portlar
                IEnumerable<string> wanted = !string.IsNullOrEmpty(_lockedPort) && present.Contains(_lockedPort, StringComparer.OrdinalIgnoreCase)
                    ? new[] { _lockedPort }
                    : present;

                foreach (var name in wanted)
                {
                    if (_open.ContainsKey(name)) continue;
                    if (TryOpen(name)) changed = true;
                }
            }

            if (changed) StatusChanged?.Invoke();
        }

        private bool TryOpen(string name)
        {
            try
            {
                var sp = new SerialPort(name, 9600, Parity.None, 8, StopBits.One)
                {
                    Encoding = Encoding.ASCII,
                    ReadTimeout = 500,
                    DtrEnable = true,
                    RtsEnable = true
                };
                sp.DataReceived += (_, _) => OnData(name, sp);
                sp.Open();
                _open[name] = sp;
                _buffers[name] = new StringBuilder();
                return true;
            }
            catch
            {
                // Port band (boshqa dastur ishlatyapti) yoki drayver tayyor emas — keyingi tekshiruvda qayta urinamiz
                return false;
            }
        }

        /// <summary>
        /// Portni ro'yxatdan chiqaradi va alohida oqimda yopadi: SerialPort.Close() o'z DataReceived
        /// hodisasi tugashini kutadi, u esa _lock ni kutayotgan bo'lishi mumkin — lock ichida yopish osilib qoladi.
        /// </summary>
        private void ClosePort(string name)
        {
            if (_open.TryGetValue(name, out var sp))
            {
                System.Threading.Tasks.Task.Run(() =>
                {
                    try { sp.Close(); } catch { }
                    try { sp.Dispose(); } catch { }
                });
            }
            _open.Remove(name);
            _buffers.Remove(name);
            _lastByte.Remove(name);
        }

        private void OnData(string name, SerialPort sp)
        {
            string chunk;
            try { chunk = sp.ReadExisting(); }
            catch { return; }
            if (string.IsNullOrEmpty(chunk)) return;

            var ready = new List<string>();
            lock (_lock)
            {
                if (!_buffers.TryGetValue(name, out var buf)) return;
                foreach (var ch in chunk)
                {
                    if (ch == '\r' || ch == '\n')
                    {
                        if (buf.Length > 0) { ready.Add(buf.ToString()); buf.Clear(); }
                    }
                    else if (!char.IsControl(ch))
                    {
                        buf.Append(ch);
                    }
                }
                _lastByte[name] = DateTime.UtcNow;
            }

            foreach (var code in ready) Deliver(name, code);
        }

        private void FlushIdle()
        {
            var ready = new List<(string port, string code)>();
            lock (_lock)
            {
                foreach (var kv in _buffers)
                {
                    if (kv.Value.Length == 0) continue;
                    if (_lastByte.TryGetValue(kv.Key, out var t) && (DateTime.UtcNow - t).TotalMilliseconds >= 150)
                    {
                        ready.Add((kv.Key, kv.Value.ToString()));
                        kv.Value.Clear();
                    }
                }
            }
            foreach (var (port, code) in ready) Deliver(port, code);
        }

        private void Deliver(string port, string raw)
        {
            var code = raw.Trim();
            if (code.Length < 3) return;

            // Birinchi haqiqiy kod kelgan port — skaner. Qolgan portlar bo'shatiladi.
            bool changed = false;
            lock (_lock)
            {
                if (!string.Equals(_lockedPort, port, StringComparison.OrdinalIgnoreCase))
                {
                    _lockedPort = port;
                    foreach (var other in _open.Keys.Where(k => !k.Equals(port, StringComparison.OrdinalIgnoreCase)).ToList())
                        ClosePort(other);
                    changed = true;
                }
            }
            if (changed)
            {
                try { _savePort(port); } catch { }
                StatusChanged?.Invoke();
            }

            try { BarcodeScanned?.Invoke(code); } catch { }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _scanTimer?.Dispose();
            _flushTimer?.Dispose();
            lock (_lock)
            {
                foreach (var name in _open.Keys.ToList()) ClosePort(name);
            }
        }
    }
}
