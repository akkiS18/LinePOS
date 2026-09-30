using System;
using System.Diagnostics;
using System.Text;
using System.Windows.Input;

namespace PosElectro.Desktop.Services
{
    public class BarcodeScannerService
    {
        private readonly StringBuilder _buffer = new();
        private readonly Stopwatch _stopwatch = new();
        private const int MaxInterKeyDelayMs = 120; // Skanerlar harflarni o'ta tez (odatda < 50ms) yuboradi

        public event Action<string>? BarcodeScanned;

        public void HandlePreviewKeyDown(KeyEventArgs e)
        {
            var key = e.Key;

            // Enter bosilganda shtrix-kod tugallandi deb hisoblanadi
            if (key == Key.Enter || key == Key.Return)
            {
                if (_buffer.Length >= 3)
                {
                    var barcode = _buffer.ToString().Trim();
                    _buffer.Clear();
                    _stopwatch.Reset();

                    // Agar kursor biror TextBox ichida turgan bo'lsa, skanerdan tushib qolgan belgini tozalaymiz
                    if (Keyboard.FocusedElement is System.Windows.Controls.TextBox tb)
                    {
                        if (!string.IsNullOrEmpty(tb.Text) && (barcode.StartsWith(tb.Text) || tb.Text.Length <= 2))
                        {
                            tb.Text = string.Empty;
                        }
                    }

                    // Shtrix-kod o'qildi hodisasi
                    BarcodeScanned?.Invoke(barcode);
                    e.Handled = true;
                }
                else
                {
                    _buffer.Clear();
                    _stopwatch.Reset();
                }
                return;
            }

            // Belgilar oralig'idagi vaqtni tekshirish
            if (_stopwatch.IsRunning && _stopwatch.ElapsedMilliseconds > MaxInterKeyDelayMs)
            {
                // Inson klaviaturada sekin tergan bo'lsa, buferni tozalaymiz
                _buffer.Clear();
            }

            char? ch = KeyToChar(key);
            if (ch.HasValue)
            {
                // Agar skaner tezligida belgilar kelyotgan bo'lsa, inputga tushirmaslik uchun bloklaymiz
                if (_stopwatch.IsRunning && _stopwatch.ElapsedMilliseconds <= MaxInterKeyDelayMs)
                {
                    e.Handled = true;
                }

                _buffer.Append(ch.Value);
                _stopwatch.Restart();
            }
        }

        private static char? KeyToChar(Key key)
        {
            if (key >= Key.D0 && key <= Key.D9)
            {
                return (char)('0' + (key - Key.D0));
            }
            if (key >= Key.NumPad0 && key <= Key.NumPad9)
            {
                return (char)('0' + (key - Key.NumPad0));
            }
            if (key >= Key.A && key <= Key.Z)
            {
                return (char)('A' + (key - Key.A));
            }
            if (key == Key.OemMinus || key == Key.Subtract)
            {
                return '-';
            }
            return null;
        }
    }
}
