using System;
using System.Collections.Generic;
using System.Drawing.Printing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Printing;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PosElectro.Desktop.Data;
using PosElectro.Desktop.Models;

namespace PosElectro.Desktop.Services
{
    public class PrinterService
    {
        private readonly DatabaseContext _db;

        static PrinterService()
        {
            try
            {
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            }
            catch { }
        }

        public PrinterService(DatabaseContext? db = null)
        {
            _db = db ?? new DatabaseContext();
        }

        /// <summary>
        /// Tizimda o'rnatilgan barcha printerlar ro'yxatini qaytaradi.
        /// </summary>
        public static List<string> GetInstalledPrinters()
        {
            var list = new List<string>();
            try
            {
                foreach (string printer in PrinterSettings.InstalledPrinters)
                {
                    list.Add(printer);
                }
            }
            catch { }
            return list;
        }

        /// <summary>
        /// Virtual/tizim printerlarini aniqlash (bular fizik printer emas).
        /// </summary>
        public static bool IsVirtualPrinter(string printerName)
        {
            if (string.IsNullOrWhiteSpace(printerName)) return true;
            var p = printerName.ToLowerInvariant();
            return p.Contains("pdf") ||
                   p.Contains("onenote") ||
                   p.Contains("fax") ||
                   p.Contains("xps") ||
                   p.Contains("root print") ||
                   p.Contains("anydesk") ||
                   p.Contains("send to");
        }

        /// <summary>
        /// Kassa chek printeri (Xprinter, POS-58, POS-80, Thermal va h.k.) ni aniqlash.
        /// </summary>
        public string? FindReceiptPrinter()
        {
            if (_db != null)
            {
                var saved = _db.GetSetting("receipt_printer_name", "");
                if (!string.IsNullOrWhiteSpace(saved) && PrinterExists(saved))
                {
                    return saved;
                }
            }

            var printers = GetInstalledPrinters();
            if (printers.Count == 0) return null;

            // 1-bosqich: Chek printerlarining kengaytirilgan kalit so'zlari
            string[] receiptKeywords = new[] {
                "58", "80", "xp-58", "xp-80", "pos-58", "pos-80", "receipt", "thermal",
                "termo", "chek", "bill", "kassa", "rp", "zj", "gp-58", "gp-80",
                "rongta", "hoin", "milestone", "netum", "epson", "star", "sam4s"
            };

            var match = printers.FirstOrDefault(p =>
            {
                var lower = p.ToLowerInvariant();
                // Barcode/Label printerlarini chetlab o'tish
                if (lower.Contains("label") || lower.Contains("barcode") || lower.Contains("etiketka") || lower.Contains("365"))
                    return false;

                return receiptKeywords.Any(k => lower.Contains(k));
            });

            if (!string.IsNullOrWhiteSpace(match)) return match;

            // 2-bosqich: Xprinter yoki POS nomli har qanday printer (agar yuqorida chiqmagan bo'lsa)
            match = printers.FirstOrDefault(p =>
            {
                var lower = p.ToLowerInvariant();
                if (lower.Contains("label") || lower.Contains("barcode") || lower.Contains("etiketka") || lower.Contains("365"))
                    return false;
                return lower.Contains("xprinter") || lower.Contains("pos");
            });

            if (!string.IsNullOrWhiteSpace(match)) return match;

            // 3-bosqich: Kompyuterdagi virtual bo'lmagan (haqiqiy jismoniy) printerlar orasidan tanlash
            var physicalPrinters = printers.Where(p => !IsVirtualPrinter(p)).ToList();
            if (physicalPrinters.Count > 0)
            {
                var nonLabel = physicalPrinters.FirstOrDefault(p =>
                {
                    var lower = p.ToLowerInvariant();
                    return !lower.Contains("label") && !lower.Contains("barcode") && !lower.Contains("etiketka") && !lower.Contains("365");
                });
                return nonLabel ?? physicalPrinters[0];
            }

            return null;
        }

        /// <summary>
        /// Etiketka / shtrix-kod printeri (Xprinter XP-365B va h.k.) ni aniqlash.
        /// </summary>
        public string? FindLabelPrinter()
        {
            if (_db != null)
            {
                var saved = _db.GetSetting("label_printer_name", "");
                if (!string.IsNullOrWhiteSpace(saved) && PrinterExists(saved))
                {
                    return saved;
                }
            }

            var printers = GetInstalledPrinters();
            if (printers.Count == 0) return null;

            // 1-bosqich: Etiketka/barcode printerlar kalit so'zlari
            string[] labelKeywords = new[] {
                "365", "xp-365", "barcode", "label", "tsc", "gprinter", "4barcode",
                "argox", "zebra", "etiketka", "shtrix", "xp-2", "xp-4", "godex", "postek"
            };

            var match = printers.FirstOrDefault(p =>
            {
                var lower = p.ToLowerInvariant();
                return labelKeywords.Any(k => lower.Contains(k));
            });

            if (!string.IsNullOrWhiteSpace(match)) return match;

            // 2-bosqich: Agar chek printeri aniqlangan bo'lsa, undan boshqa jismoniy printerni qidiramiz
            var receiptPrinter = FindReceiptPrinter();
            var physicalPrinters = printers.Where(p => !IsVirtualPrinter(p)).ToList();
            var otherPhysical = physicalPrinters.FirstOrDefault(p => !string.Equals(p, receiptPrinter, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(otherPhysical))
            {
                return otherPhysical;
            }

            return physicalPrinters.FirstOrDefault();
        }

        public void SaveReceiptPrinterSetting(string printerName)
        {
            _db?.SetSetting("receipt_printer_name", printerName);
        }

        public void SaveLabelPrinterSetting(string printerName)
        {
            _db?.SetSetting("label_printer_name", printerName);
        }

        public static bool PrinterExists(string printerName)
        {
            return GetInstalledPrinters().Any(p => p.Equals(printerName, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// 58 mm chek printeriga to'g'ridan-to'g'ri ESC/POS baytlar orqali chek chiqaradi.
        /// </summary>
        public bool PrintReceipt(Sale sale, string? printerName = null)
        {
            var targetPrinter = printerName ?? FindReceiptPrinter();
            if (string.IsNullOrWhiteSpace(targetPrinter))
            {
                // Agar printer topilmasa, xatolik
                return false;
            }

            byte[] bytes = BuildEscPosReceipt(sale);
            return RawPrinterHelper.SendBytesToPrinter(targetPrinter, bytes, $"Chek #{sale.ReceiptNumber}");
        }

        /// <summary>
        /// Pul summalarini doimo xavfsiz standart ASCII probel (0x20) bilan formatlash.
        /// Hech qachon \u00A0 (no-break space) chiqarmaydi, shuning uchun printerda "Та" yoki "┬а" chiqmaydi!
        /// </summary>
        public static string FormatMoney(double amount)
        {
            return amount.ToString("#,##0", System.Globalization.CultureInfo.InvariantCulture).Replace(',', ' ');
        }

        public static string FormatMoney(decimal amount)
        {
            return amount.ToString("#,##0", System.Globalization.CultureInfo.InvariantCulture).Replace(',', ' ');
        }

        private static string CenterText(string text, int width)
        {
            if (string.IsNullOrEmpty(text)) return new string(' ', width);
            text = text.Trim();
            if (text.Length >= width) return text;
            int totalPadding = width - text.Length;
            int padLeft = totalPadding / 2;
            int padRight = totalPadding - padLeft;
            return new string(' ', padLeft) + text + new string(' ', padRight);
        }

        /// <summary>
        /// Matnni 58 mm chek kengligiga (maksimal 32 belgi) moslab, so'zlarni buzmagan holda
        /// qatorlarga ajratadi. Hech bir belgi qirqilib qolmaydi, pastki qatordan davom etadi.
        /// </summary>
        public static List<string> SplitIntoLines(string text, int maxChars = 32)
        {
            var result = new List<string>();
            if (string.IsNullOrEmpty(text)) return result;

            var words = text.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0) return result;

            var current = new StringBuilder();

            foreach (var word in words)
            {
                // Agar bitta so'zning o'zi maxChars dan uzun bo'lsa (masalan: uzun chek IDsi yoki shtrix-kod)
                if (word.Length > maxChars)
                {
                    if (current.Length > 0)
                    {
                        result.Add(current.ToString());
                        current.Clear();
                    }

                    int idx = 0;
                    while (idx < word.Length)
                    {
                        int chunk = Math.Min(maxChars, word.Length - idx);
                        result.Add(word.Substring(idx, chunk));
                        idx += chunk;
                    }
                    continue;
                }

                if (current.Length == 0)
                {
                    current.Append(word);
                }
                else if (current.Length + 1 + word.Length <= maxChars)
                {
                    current.Append(" ").Append(word);
                }
                else
                {
                    result.Add(current.ToString());
                    current.Clear();
                    current.Append(word);
                }
            }

            if (current.Length > 0)
            {
                result.Add(current.ToString());
            }

            return result;
        }

        /// <summary>
        /// 58 mm kassa chekining qatorlari (har bir qator qat'iy 32 belgidan oshmaydi).
        /// Ushbu qatorlar ham Jonli Ko'rish (Preview), ham Printer (Chop etish) uchun
        /// YAGONA 1-GA-1 MANBA bo'lib xizmat qiladi!
        /// Hech bir ma'lumot qirqilib qolmaydi, sig'masa pastki qatordan davom etadi.
        /// </summary>
        public static List<string> BuildReceiptLines(Sale sale)
        {
            var lines = new List<string>();

            // Markazlashtirilgan sarlavha (32 belgi)
            lines.Add(CenterText("SMART KASSA", 32));
            lines.Add(CenterText("Elektr jihozlari do'koni", 32));
            lines.Add(new string('-', 32));

            // Chek ma'lumotlari (Chek ID qanchalik uzun bo'lmasin, qirqilmaydi va pastki qatordan davom etadi)
            foreach (var cl in SplitIntoLines($"Chek: #{sale.ReceiptNumber}", 32))
            {
                lines.Add(cl);
            }

            if (!string.IsNullOrEmpty(sale.OriginalReceiptNumber))
            {
                foreach (var acl in SplitIntoLines($"Asl chek: {sale.OriginalReceiptNumber}", 32))
                {
                    lines.Add(acl);
                }
            }

            lines.Add($"Sana: {sale.CreatedDateTime:dd.MM.yyyy HH:mm}");

            foreach (var payLine in SplitIntoLines($"To'lov turi: {sale.PaymentTypeDisplay}", 32))
            {
                lines.Add(payLine);
            }

            lines.Add(new string('-', 32));

            // Jadval sarlavhasi (32 ta belgi)
            lines.Add("Tovar              Miqd.    Jami");
            lines.Add(new string('-', 32));

            // Tovarlar ro'yxati (Nomlar qirqilmaydi, to'liq yozilib, sig'masa pastki qatordan davom etadi)
            foreach (var item in sale.Items)
            {
                string name = (item.ProductName ?? string.Empty).Trim();
                foreach (var nl in SplitIntoLines(name, 32))
                {
                    lines.Add(nl);
                }

                string qtyStr = $"{item.Quantity:0.##}";
                string priceFormatted = FormatMoney(item.PriceAtSale);
                string totalFormatted = FormatMoney(item.TotalPrice);

                // 2-qator: masalan: "  275 000 x 1" chapda, "275 000" o'ngda
                string lineLeft = $"  {priceFormatted} x {qtyStr}";
                string lineRight = totalFormatted;

                int spaceCount = 32 - lineLeft.Length - lineRight.Length;
                if (spaceCount < 1)
                {
                    lines.Add(lineLeft);
                    lines.Add(new string(' ', Math.Max(0, 32 - lineRight.Length)) + lineRight);
                }
                else
                {
                    lines.Add(lineLeft + new string(' ', spaceCount) + lineRight);
                }
            }

            lines.Add(new string('-', 32));

            // Jami summa: "JAMI:" chapda, "275 000 so'm" o'ngda
            string totalTitle = "JAMI:";
            string totalVal = $"{FormatMoney(sale.TotalAmount)} so'm";
            int totalSpace = 32 - totalTitle.Length - totalVal.Length;
            if (totalSpace < 1)
            {
                lines.Add(totalTitle);
                lines.Add(new string(' ', Math.Max(0, 32 - totalVal.Length)) + totalVal);
            }
            else
            {
                lines.Add(totalTitle + new string(' ', totalSpace) + totalVal);
            }

            // Agar aralash to'lov bo'lsa
            if (sale.PaymentType == PaymentType.SPLIT)
            {
                string cashVal = $"{FormatMoney(sale.CashAmount)} so'm";
                int cashSpace = 32 - "  Naqd:".Length - cashVal.Length;
                if (cashSpace < 1)
                {
                    lines.Add("  Naqd:");
                    lines.Add(new string(' ', Math.Max(0, 32 - cashVal.Length)) + cashVal);
                }
                else
                {
                    lines.Add("  Naqd:" + new string(' ', cashSpace) + cashVal);
                }

                string cardVal = $"{FormatMoney(sale.CardAmount)} so'm";
                int cardSpace = 32 - "  Karta:".Length - cardVal.Length;
                if (cardSpace < 1)
                {
                    lines.Add("  Karta:");
                    lines.Add(new string(' ', Math.Max(0, 32 - cardVal.Length)) + cardVal);
                }
                else
                {
                    lines.Add("  Karta:" + new string(' ', cardSpace) + cardVal);
                }
            }
            else if (sale.PaymentType == PaymentType.DEBT)
            {
                double debt = Math.Max(0, sale.TotalAmount - sale.CashAmount - sale.CardAmount);
                if (sale.CashAmount > 0)
                {
                    lines.Add($"  Oldindan (Naqd):  {FormatMoney(sale.CashAmount)} so'm");
                }
                if (sale.CardAmount > 0)
                {
                    lines.Add($"  Oldindan (Karta): {FormatMoney(sale.CardAmount)} so'm");
                }
                lines.Add($"  Nasiya (Qarz):    {FormatMoney(debt)} so'm");
            }

            lines.Add(new string('-', 32));
            lines.Add(CenterText("Rahmat, xaridingiz uchun!", 32));
            lines.Add(CenterText("Yana tashrif buyuring!", 32));

            return lines;
        }

        public (bool Success, string? ErrorMessage) PrintCustomerStatement(
            PosElectro.Desktop.Debt.DebtCustomerDetailDto customer,
            string? printerName = null)
        {
            var targetPrinter = printerName ?? FindReceiptPrinter();
            if (string.IsNullOrWhiteSpace(targetPrinter))
            {
                return (false, "Chek printeri topilmadi yoki sozlanmagan");
            }

            string text = Debt.DebtReceiptFormatter.BuildCustomerStatementText(customer);

            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);
            bw.Write(new byte[] { 0x1B, 0x40 });
            bw.Write(new byte[] { 0x1B, 0x74, 17 });
            WriteCp866(bw, text);
            bw.Write(new byte[] { 0x1D, 0x56, 0x42, 0x00 });
            byte[] bytes = ms.ToArray();

            bool ok = RawPrinterHelper.SendBytesToPrinter(targetPrinter, bytes, $"Ko'chirma - {customer.Customer.Name}");
            return (ok, ok ? null : "Printerga chop etishda xatolik yuz berdi");
        }

        private byte[] BuildEscPosReceipt(Sale sale)
        {
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);

            // ESC @ - Printerni boshlang'ich holatga keltirish (Initialize)
            bw.Write(new byte[] { 0x1B, 0x40 });

            // Kodirovka: PC866 (Kirill/Lotin uchun)
            bw.Write(new byte[] { 0x1B, 0x74, 17 });

            var lines = BuildReceiptLines(sale);

            for (int i = 0; i < lines.Count; i++)
            {
                string line = lines[i];

                if (i == 0) // SMART KASSA
                {
                    bw.Write(new byte[] { 0x1B, 0x61, 0x01 }); // Markazga
                    bw.Write(new byte[] { 0x1B, 0x45, 0x01 }); // Qalin (Bold on)
                    bw.Write(new byte[] { 0x1D, 0x21, 0x11 }); // Double height & width
                    WriteCp866(bw, line.Trim() + "\n");
                    bw.Write(new byte[] { 0x1D, 0x21, 0x00 }); // Normal o'lcham
                    bw.Write(new byte[] { 0x1B, 0x45, 0x00 }); // Bold off
                }
                else if (i == 1) // Elektr jihozlari do'koni
                {
                    bw.Write(new byte[] { 0x1B, 0x61, 0x01 }); // Markazga
                    WriteCp866(bw, line.Trim() + "\n");
                    bw.Write(new byte[] { 0x1B, 0x61, 0x00 }); // Chapga qaytarish
                }
                else if (line.StartsWith("JAMI:"))
                {
                    bw.Write(new byte[] { 0x1B, 0x61, 0x00 }); // Chapga
                    bw.Write(new byte[] { 0x1B, 0x45, 0x01 }); // Qalin (Bold on)
                    WriteCp866(bw, line + "\n");
                    bw.Write(new byte[] { 0x1B, 0x45, 0x00 }); // Bold off
                }
                else if (i >= lines.Count - 2) // Footer lines (Rahmat... Yana tashrif...)
                {
                    bw.Write(new byte[] { 0x1B, 0x61, 0x01 }); // Markazga
                    WriteCp866(bw, line.Trim() + "\n");
                }
                else
                {
                    bw.Write(new byte[] { 0x1B, 0x61, 0x00 }); // Chapga
                    WriteCp866(bw, line + "\n");
                }
            }

            // Chekni qulay yirtib olish uchun 4 qator pastga tushirish va kesish
            WriteCp866(bw, "\n\n\n\n");
            bw.Write(new byte[] { 0x1D, 0x56, 0x42, 0x00 }); // Cut (GS V 66 0)

            return ms.ToArray();
        }

        /// <summary>
        /// 58/80 mm chek printeriga Buyurtma Ro'yxatini to'g'ridan-to'g'ri ESC/POS orqali chop etadi.
        /// </summary>
        public bool PrintReorderReceipt(List<Product> items, string? printerName = null)
        {
            if (items == null || items.Count == 0) return false;

            var targetPrinter = printerName ?? FindReceiptPrinter();
            if (string.IsNullOrWhiteSpace(targetPrinter))
            {
                return false;
            }

            byte[] bytes = BuildEscPosReorderReceipt(items);
            return RawPrinterHelper.SendBytesToPrinter(targetPrinter, bytes, $"Buyurtma Ro'yxati ({items.Count} ta tovar)");
        }

        public static List<string> BuildReorderReceiptLines(List<Product> items)
        {
            var lines = new List<string>();

            lines.Add(CenterText("BUYURTMA RO'YXATI", 32));
            lines.Add(CenterText("(ZAKAZ UCHUN)", 32));
            lines.Add(new string('-', 32));
            lines.Add($"Sana: {DateTime.Now:dd.MM.yyyy HH:mm}");
            lines.Add($"Jami tovarlar: {items.Count} ta");
            lines.Add(new string('-', 32));
            lines.Add("Tovar / Qoldiq          Buyurtma");
            lines.Add(new string('-', 32));

            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                string name = $"{i + 1}. {item.Name.Trim()}";
                if (name.Length > 32)
                {
                    name = name.Substring(0, 29) + "...";
                }
                lines.Add(name);

                string qoldiqStr = $"   Qoldiq: {item.StockDisplay} {item.UnitDisplay}";
                string boxStr = "[     ]";
                int spaceCount = 32 - qoldiqStr.Length - boxStr.Length;
                if (spaceCount < 1) spaceCount = 1;
                lines.Add(qoldiqStr + new string(' ', spaceCount) + boxStr);
            }

            lines.Add(new string('-', 32));
            lines.Add("Eslatma: _______________________");
            lines.Add("         _______________________");
            lines.Add(new string('-', 32));
            lines.Add(CenterText("LINE KASSA", 32));

            return lines;
        }

        private byte[] BuildEscPosReorderReceipt(List<Product> items)
        {
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);

            bw.Write(new byte[] { 0x1B, 0x40 }); // ESC @ - Init
            bw.Write(new byte[] { 0x1B, 0x74, 17 }); // PC866

            var lines = BuildReorderReceiptLines(items);

            for (int i = 0; i < lines.Count; i++)
            {
                string line = lines[i];

                if (i == 0) // BUYURTMA RO'YXATI
                {
                    bw.Write(new byte[] { 0x1B, 0x61, 0x01 }); // Markazga
                    bw.Write(new byte[] { 0x1B, 0x45, 0x01 }); // Qalin (Bold on)
                    bw.Write(new byte[] { 0x1D, 0x21, 0x11 }); // Double height & width
                    WriteCp866(bw, line.Trim() + "\n");
                    bw.Write(new byte[] { 0x1D, 0x21, 0x00 }); // Normal
                    bw.Write(new byte[] { 0x1B, 0x45, 0x00 }); // Bold off
                }
                else if (i == 1 || i == lines.Count - 1)
                {
                    bw.Write(new byte[] { 0x1B, 0x61, 0x01 }); // Markazga
                    WriteCp866(bw, line.Trim() + "\n");
                    bw.Write(new byte[] { 0x1B, 0x61, 0x00 }); // Chapga
                }
                else
                {
                    bw.Write(new byte[] { 0x1B, 0x61, 0x00 }); // Chapga
                    WriteCp866(bw, line + "\n");
                }
            }

            WriteCp866(bw, "\n\n\n\n");
            bw.Write(new byte[] { 0x1D, 0x56, 0x42, 0x00 }); // Cut

            return ms.ToArray();
        }

        private static void WriteCp866(BinaryWriter bw, string text)
        {
            if (string.IsNullOrEmpty(text)) return;

            // 1. O'zbekcha va universal bo'shliq belgilarini tozalash:
            // MUHIM: Har qanday no-break space (\u00A0) yoki boshqa noan'anaviy bo'shliqlar standart ASCII bo'shliqqa (0x20) aylanadi!
            // Aks holda u UTF-8 da 0xC2 0xA0 bo'lib kodlanadi va printerda "Та" (yoki ┬а) bo'lib chiqadi!
            string normalized = text
                .Replace('\u00A0', ' ')
                .Replace('\u202F', ' ')
                .Replace('\u2007', ' ')
                .Replace('\u200B', ' ')
                .Replace("o‘", "o'").Replace("O‘", "O'")
                .Replace("g‘", "g'").Replace("G‘", "G'")
                .Replace("ʻ", "'").Replace("’", "'").Replace("‘", "'").Replace("`", "'")
                .Replace("“", "\"").Replace("”", "\"");

            byte[] b;
            try
            {
                Encoding cp866 = Encoding.GetEncoding(866);
                b = cp866.GetBytes(normalized);
            }
            catch
            {
                b = Encoding.ASCII.GetBytes(normalized);
            }
            bw.Write(b);
        }

        /// <summary>
        /// 58 mm kassa chekining matnli ko'rinishini hosil qilish (Oldindan ko'rish - Preview uchun)
        /// </summary>
        public static string BuildReceiptText(Sale sale)
        {
            var lines = BuildReceiptLines(sale);
            return string.Join(Environment.NewLine, lines);
        }

        /// <summary>
        /// Mijoz qarz daftari ko'chirmasi matnini shakllantirish (58mm/80mm yoki ko'rish uchun)
        /// </summary>
        public static string BuildCustomerStatementText(Debt.DebtCustomerDetailDto customer) => Debt.DebtReceiptFormatter.BuildCustomerStatementText(customer);

        /// <summary>
        /// A4 formatdagi tovar hisob-fakturasini chop etish (Standard Windows printer or PDF)
        /// </summary>
        public bool PrintInvoiceA4(Sale sale, Window? owner = null)
        {
            try
            {
                var printDialog = new PrintDialog();
                if (owner != null)
                {
                    if (printDialog.ShowDialog() != true) return false;
                }

                var doc = BuildA4FlowDocument(sale);
                var paginator = ((System.Windows.Documents.IDocumentPaginatorSource)doc).DocumentPaginator;
                printDialog.PrintDocument(paginator, $"Hisob-faktura #{sale.ReceiptNumber}");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"A4 print error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// A4 formatdagi FlowDocument hosil qilish
        /// </summary>
        public static System.Windows.Documents.FlowDocument BuildA4FlowDocument(Sale sale)
        {
            var doc = new System.Windows.Documents.FlowDocument
            {
                PageWidth = 793,
                PageHeight = 1122,
                ColumnWidth = 793,
                PagePadding = new Thickness(40),
                FontFamily = new FontFamily("Segoe UI, Arial"),
                FontSize = 12,
                Background = Brushes.White,
                Foreground = Brushes.Black
            };

            // 1. Sarlavha
            var titlePara = new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run("SMART POS — TOVAR CHEKI / HISOB-FAKTURA"))
            {
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(11, 100, 119)),
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 0, 0, 4)
            };
            doc.Blocks.Add(titlePara);

            var subPara = new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run("Elektr uskunalari va jihozlari do'koni"))
            {
                FontSize = 11,
                Foreground = Brushes.Gray,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 0, 0, 20)
            };
            doc.Blocks.Add(subPara);

            // 2. Metama'lumotlar
            var metaTable = new System.Windows.Documents.Table { Margin = new Thickness(0, 0, 0, 16) };
            metaTable.Columns.Add(new System.Windows.Documents.TableColumn { Width = new GridLength(350) });
            metaTable.Columns.Add(new System.Windows.Documents.TableColumn { Width = new GridLength(350) });
            var metaRowGroup = new System.Windows.Documents.TableRowGroup();

            var metaRow = new System.Windows.Documents.TableRow();
            var metaCell1 = new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(
                new System.Windows.Documents.Run($"Hujjat raqami: Chek #{sale.ReceiptNumber}\n{(string.IsNullOrEmpty(sale.OriginalReceiptNumber) ? "" : "Asl chek: " + sale.OriginalReceiptNumber + "\n")}Sana: {sale.CreatedDateTime:dd.MM.yyyy HH:mm}")));
            var metaCell2 = new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(
                new System.Windows.Documents.Run($"To'lov usuli: {sale.PaymentTypeDisplay}\nHolati: To'langan")));
            metaCell2.TextAlignment = TextAlignment.Right;
            metaRow.Cells.Add(metaCell1);
            metaRow.Cells.Add(metaCell2);
            metaRowGroup.Rows.Add(metaRow);
            metaTable.RowGroups.Add(metaRowGroup);
            doc.Blocks.Add(metaTable);

            // 3. Mahsulotlar Jadvali
            var table = new System.Windows.Documents.Table
            {
                CellSpacing = 0,
                BorderBrush = Brushes.LightGray,
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 0, 20)
            };

            table.Columns.Add(new System.Windows.Documents.TableColumn { Width = new GridLength(40) });   // №
            table.Columns.Add(new System.Windows.Documents.TableColumn { Width = new GridLength(320) });  // Nomi
            table.Columns.Add(new System.Windows.Documents.TableColumn { Width = new GridLength(80) });   // Miqdor
            table.Columns.Add(new System.Windows.Documents.TableColumn { Width = new GridLength(120) });  // Narxi
            table.Columns.Add(new System.Windows.Documents.TableColumn { Width = new GridLength(150) });  // Jami

            var rowGroup = new System.Windows.Documents.TableRowGroup();

            // Header row
            var headerRow = new System.Windows.Documents.TableRow { Background = new SolidColorBrush(Color.FromRgb(241, 245, 249)) };
            string[] headers = { "№", "Mahsulot Nomi", "Miqdori", "Narxi", "Jami (so'm)" };
            foreach (var h in headers)
            {
                var cell = new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run(h))
                {
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(6, 6, 6, 6)
                })
                {
                    BorderBrush = Brushes.LightGray,
                    BorderThickness = new Thickness(0, 0, 1, 1)
                };
                headerRow.Cells.Add(cell);
            }
            rowGroup.Rows.Add(headerRow);

            int idx = 1;
            foreach (var item in sale.Items)
            {
                var itemRow = new System.Windows.Documents.TableRow();
                if (idx % 2 == 0)
                {
                    itemRow.Background = new SolidColorBrush(Color.FromRgb(248, 250, 252));
                }

                void AddCell(string text, bool bold = false, TextAlignment align = TextAlignment.Left)
                {
                    var p = new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run(text))
                    {
                        FontWeight = bold ? FontWeights.Bold : FontWeights.Normal,
                        TextAlignment = align,
                        Margin = new Thickness(6, 5, 6, 5)
                    };
                    itemRow.Cells.Add(new System.Windows.Documents.TableCell(p)
                    {
                        BorderBrush = Brushes.LightGray,
                        BorderThickness = new Thickness(0, 0, 1, 1)
                    });
                }

                AddCell(idx.ToString(), align: TextAlignment.Center);
                AddCell(item.ProductName);
                AddCell($"{item.Quantity:0.##}", align: TextAlignment.Center);
                AddCell($"{item.PriceAtSale:N0} so'm", align: TextAlignment.Right);
                AddCell($"{item.TotalPrice:N0} so'm", bold: true, align: TextAlignment.Right);

                rowGroup.Rows.Add(itemRow);
                idx++;
            }

            table.RowGroups.Add(rowGroup);
            doc.Blocks.Add(table);

            // 4. Jami Xulosa
            var summaryPara = new System.Windows.Documents.Paragraph
            {
                TextAlignment = TextAlignment.Right,
                Margin = new Thickness(0, 0, 0, 30)
            };
            summaryPara.Inlines.Add(new System.Windows.Documents.Run("JAMI TO'LOV: ") { FontSize = 15, FontWeight = FontWeights.Bold });
            summaryPara.Inlines.Add(new System.Windows.Documents.Run($"{sale.TotalAmount:N0} SO'M")
            {
                FontSize = 18,
                FontWeight = FontWeights.ExtraBold,
                Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129))
            });
            doc.Blocks.Add(summaryPara);

            // 5. Imzo va qabul qilish bloki
            var signPara = new System.Windows.Documents.Paragraph
            {
                Margin = new Thickness(0, 20, 0, 0),
                FontSize = 11,
                Foreground = Brushes.DimGray
            };
            signPara.Inlines.Add(new System.Windows.Documents.Run("Tovarlar to'liq, butun va soz holatda topshirildi hamda qabul qilindi.\n\n\n"));
            signPara.Inlines.Add(new System.Windows.Documents.Run("Sotuvchi (Berdi): _______________________         Xaridor (Qabul qildi): _______________________"));
            doc.Blocks.Add(signPara);

            return doc;
        }

        /// <summary>
        /// Xprinter XP-365B printeriga etiketka / shtrix-kod chop etadi.
        /// WPF DrawingVisual orqali chiziladi va Windows Print Spooler orqali yuboriladi.
        /// Bu barcha shriftlar (O', G', Sh) va chiziqlarning o'ta aniq chiqishini ta'minlaydi.
        /// </summary>
        public void SaveLabelSizeSetting(string sizeId)
        {
            _db?.SetSetting("label_size_id", sizeId);
        }

        public string GetLabelSizeSetting()
        {
            return _db?.GetSetting("label_size_id", "40x30") ?? "40x30";
        }

        public void SaveLabelOffsetSetting(string sizeId, double offsetMm)
        {
            _db?.SetSetting($"label_offset_{sizeId}", offsetMm.ToString(CultureInfo.InvariantCulture));
        }

        public double GetLabelOffsetSetting(string sizeId)
        {
            var val = _db?.GetSetting($"label_offset_{sizeId}", "0");
            return double.TryParse(val, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : 0.0;
        }

        public void SaveLabelNameOffsetSetting(string sizeId, double offsetMm)
        {
            _db?.SetSetting($"label_name_offset_{sizeId}", offsetMm.ToString(CultureInfo.InvariantCulture));
        }

        public double GetLabelNameOffsetSetting(string sizeId)
        {
            var val = _db?.GetSetting($"label_name_offset_{sizeId}", "0");
            return double.TryParse(val, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : 0.0;
        }

        public void SaveLabelNameAlignSetting(string sizeId, string align)
        {
            _db?.SetSetting($"label_name_align_{sizeId}", align);
        }

        public string GetLabelNameAlignSetting(string sizeId)
        {
            return _db?.GetSetting($"label_name_align_{sizeId}", "left") ?? "left";
        }

        public void SaveLabelNameLinesSetting(string sizeId, int lines)
        {
            _db?.SetSetting($"label_name_lines_{sizeId}", lines.ToString());
        }

        public int GetLabelNameLinesSetting(string sizeId)
        {
            var val = _db?.GetSetting($"label_name_lines_{sizeId}", "1");
            return int.TryParse(val, out var l) ? l : 1;
        }

        public bool PrintBarcodeLabel(
            string barcode,
            string productName,
            double price,
            int copies = 1,
            bool showName = true,
            bool showPrice = true,
            bool showBarcode = true,
            string? printerName = null,
            string? labelSizeId = null,
            double? offsetMm = null,
            double? nameOffsetMm = null,
            string? nameAlignment = null,
            int? maxNameLines = null)
        {
            if (copies <= 0) copies = 1;

            var targetPrinter = printerName ?? FindLabelPrinter();
            if (string.IsNullOrWhiteSpace(targetPrinter))
            {
                return false;
            }

            try
            {
                var printDialog = new PrintDialog();

                // Tanlangan printerni o'rnatish
                var printServer = new LocalPrintServer();
                PrintQueue? targetQueue = null;
                try
                {
                    targetQueue = printServer.GetPrintQueue(targetPrinter);
                }
                catch
                {
                    // Printerni qidirib topish
                    foreach (var queue in printServer.GetPrintQueues())
                    {
                        if (queue.Name.Equals(targetPrinter, StringComparison.OrdinalIgnoreCase))
                        {
                            targetQueue = queue;
                            break;
                        }
                    }
                }

                if (targetQueue != null)
                {
                    printDialog.PrintQueue = targetQueue;
                }

                var sizeId = labelSizeId ?? GetLabelSizeSetting();
                var config = LabelSizeConfig.Get(sizeId);
                double currentOffset = offsetMm ?? GetLabelOffsetSetting(sizeId);
                double currentNameOffset = nameOffsetMm ?? GetLabelNameOffsetSetting(sizeId);
                string currentNameAlign = nameAlignment ?? GetLabelNameAlignSetting(sizeId);
                int currentNameLines = maxNameLines ?? GetLabelNameLinesSetting(sizeId);

                try
                {
                    if (printDialog.PrintTicket != null)
                    {
                        // Millimetrdan 1/96 inch ga o'tkazish (1 mm = 96.0 / 25.4)
                        double dipW = config.WidthMm * (96.0 / 25.4);
                        double dipH = config.HeightMm * (96.0 / 25.4);
                        printDialog.PrintTicket.PageMediaSize = new PageMediaSize(dipW, dipH);
                    }
                }
                catch { }

                var visual = CreateLabelVisual(
                    barcode, 
                    productName, 
                    price, 
                    showName, 
                    showPrice, 
                    showBarcode, 
                    config, 
                    currentOffset,
                    currentNameOffset,
                    currentNameAlign,
                    currentNameLines);

                for (int i = 0; i < copies; i++)
                {
                    printDialog.PrintVisual(visual, $"Stiker: {productName}");
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Label print error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Stikerning vizual ko'rinishini DrawingVisual orqali yaratish (avtomatik markazlashtirilgan va moslashuvchan siljitish bilan).
        /// Ushbu metod Jonli Preview (WPF RenderTargetBitmap) va Haqiqiy Printer (PrintVisual) uchun yagona manba (100% WYSIWYG) hisoblanadi.
        /// </summary>
        public static DrawingVisual CreateLabelVisual(
            string barcode,
            string productName,
            double price,
            bool showName,
            bool showPrice,
            bool showBarcode,
            LabelSizeConfig? config = null,
            double offsetMm = 0,
            double nameOffsetMm = 0,
            string nameAlignment = "left",
            int maxNameLines = 1)
        {
            var cfg = config ?? LabelSizeConfig.Presets["40x30"];
            double width = cfg.VisualWidth;
            double height = cfg.VisualHeight;
            double totalOffsetMm = cfg.ShiftXmm + offsetMm;
            double offsetDip = totalOffsetMm * (96.0 / 25.4);
            double centerX = (width / 2.0) + offsetDip;

            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                // Oq fon
                dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, width, height));

                // Stiker qog'ozi chegarasidan tashqariga chiqib ketmasligi uchun qat'iy qirqish (PushClip)
                dc.PushClip(new RectangleGeometry(new Rect(0, 0, width, height)));

                double curY = 4;

                // 1. Mahsulot nomi (Agar tanlangan bo'lsa)
                if (showName && !string.IsNullOrWhiteSpace(productName))
                {
                    var typefaceName = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
                    double nameFontSize = maxNameLines == 1 ? (cfg.NameFontSize + 2.0) : (cfg.NameFontSize + 0.5);
                    double nameShiftDip = nameOffsetMm * (96.0 / 25.4);
                    double padX = 8.0 + offsetDip; // Chap chetdan xavfsiz oraliq (~2.1 mm)
                    double availWidth = Math.Max(20, width - 16.0);

                    var ftName = new FormattedText(
                        productName,
                        CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        typefaceName,
                        nameFontSize,
                        Brushes.Black,
                        1.0);

                    double nameX;
                    if (maxNameLines > 1)
                    {
                        // 2 qatorda chiqarish
                        ftName.MaxTextWidth = availWidth;
                        ftName.MaxLineCount = 2;
                        ftName.Trimming = TextTrimming.WordEllipsis;

                        if (nameAlignment.Equals("center", StringComparison.OrdinalIgnoreCase))
                        {
                            ftName.TextAlignment = TextAlignment.Center;
                            nameX = (width - availWidth) / 2.0 + offsetDip + nameShiftDip;
                        }
                        else
                        {
                            ftName.TextAlignment = TextAlignment.Left;
                            nameX = padX + nameShiftDip;
                        }
                    }
                    else
                    {
                        // 1 qatorda chiqarish (Mijoz talabi: boshi to'liq ko'rinadi, o'ngga surish/qirqish mumkin)
                        ftName.MaxLineCount = 1;

                        if (nameAlignment.Equals("center", StringComparison.OrdinalIgnoreCase))
                        {
                            nameX = centerX - (ftName.Width / 2.0) + nameShiftDip;
                        }
                        else
                        {
                            // Chapdan boshlanishi: Boshidagi harflar ("K") hech qachon kesilmaydi!
                            nameX = padX + nameShiftDip;
                        }
                    }

                    dc.DrawText(ftName, new Point(nameX, curY));
                    curY += ftName.Height + 2;
                }

                // 2. Shtrix-kod chiziqlari (To'liq vektor, qop-qora, tiniq va skaner oson o'qiydi)
                if (showBarcode && !string.IsNullOrWhiteSpace(barcode))
                {
                    string pattern = BarcodeGeneratorHelper.GetBarcodePattern(barcode);
                    int moduleCount = pattern.Length;
                    int imgW = cfg.BarcodeWidth;
                    int imgH = cfg.BarcodeHeight;
                    double moduleWidth = (double)imgW / moduleCount;
                    double imgX = centerX - (imgW / 2.0);

                    // Oq fon taglik (chiziqlar orasi toza bo'lishi uchun)
                    dc.DrawRectangle(Brushes.White, null, new Rect(imgX - 4, curY, imgW + 8, imgH));

                    for (int m = 0; m < moduleCount; m++)
                    {
                        if (pattern[m] == '1')
                        {
                            double barX = imgX + (m * moduleWidth);
                            dc.DrawRectangle(Brushes.Black, null, new Rect(barX, curY, moduleWidth + 0.15, imgH));
                        }
                    }
                    curY += imgH + 2;

                    // Shtrix-kod raqami ostida
                    var typefaceCode = new Typeface(new FontFamily("Consolas"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
                    var ftCode = new FormattedText(
                        barcode,
                        CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        typefaceCode,
                        cfg.BarcodeFontSize,
                        Brushes.Black,
                        1.0);
                    double codeX = centerX - (ftCode.Width / 2.0);
                    dc.DrawText(ftCode, new Point(codeX, curY));
                    curY += ftCode.Height + 2;
                }

                // 3. Narxi (Agar tanlangan bo'lsa)
                if (showPrice)
                {
                    var typefacePrice = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
                    string priceText = $"{price:N0} SO'M";
                    var ftPrice = new FormattedText(
                        priceText,
                        CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        typefacePrice,
                        cfg.PriceFontSize,
                        Brushes.Black,
                        1.0);
                    double priceX = centerX - (ftPrice.Width / 2.0);
                    dc.DrawText(ftPrice, new Point(priceX, curY));
                }

                // Qirqishni yopish
                dc.Pop();
            }

            return visual;
        }
    }

    /// <summary>
    /// Stiker o'lchamlari konfiguratsiyasi (58x40, 40x30, 43x25, 30x20).
    /// </summary>
    public class LabelSizeConfig
    {
        public string Id { get; set; } = "40x30";
        public string DisplayName { get; set; } = "40 x 30 mm (Standart)";
        public double WidthMm { get; set; } = 40;
        public double HeightMm { get; set; } = 30;
        public double VisualWidth { get; set; } = 151;
        public double VisualHeight { get; set; } = 113;
        public double ShiftXmm { get; set; } = 0.0;
        public double StoreFontSize { get; set; } = 8.5;
        public double NameFontSize { get; set; } = 9.5;
        public int BarcodeWidth { get; set; } = 126;
        public int BarcodeHeight { get; set; } = 34;
        public double BarcodeFontSize { get; set; } = 12;
        public double PriceFontSize { get; set; } = 11.5;

        public static readonly Dictionary<string, LabelSizeConfig> Presets = new()
        {
            ["40x30"] = new LabelSizeConfig
            {
                Id = "40x30",
                DisplayName = "40 x 30 mm (Standart)",
                WidthMm = 40,
                HeightMm = 30,
                VisualWidth = 151,
                VisualHeight = 113,
                ShiftXmm = 0.0,
                StoreFontSize = 8.5,
                NameFontSize = 9.5,
                BarcodeWidth = 126,
                BarcodeHeight = 34,
                BarcodeFontSize = 12,
                PriceFontSize = 11.5
            },
            ["58x40"] = new LabelSizeConfig
            {
                Id = "58x40",
                DisplayName = "58 x 40 mm (Katta)",
                WidthMm = 58,
                HeightMm = 40,
                VisualWidth = 219,
                VisualHeight = 151,
                ShiftXmm = 0.0,
                StoreFontSize = 11,
                NameFontSize = 12,
                BarcodeWidth = 175,
                BarcodeHeight = 44,
                BarcodeFontSize = 14,
                PriceFontSize = 14
            },
            ["43x25"] = new LabelSizeConfig
            {
                Id = "43x25",
                DisplayName = "43 x 25 mm (O'rtacha)",
                WidthMm = 43,
                HeightMm = 25,
                VisualWidth = 162,
                VisualHeight = 94,
                ShiftXmm = 0.0,
                StoreFontSize = 8,
                NameFontSize = 8.5,
                BarcodeWidth = 126,
                BarcodeHeight = 24,
                BarcodeFontSize = 11,
                PriceFontSize = 9.5
            },
            ["30x20"] = new LabelSizeConfig
            {
                Id = "30x20",
                DisplayName = "30 x 20 mm (Kichik)",
                WidthMm = 30,
                HeightMm = 20,
                VisualWidth = 113,
                VisualHeight = 75,
                ShiftXmm = 0.0,
                StoreFontSize = 7,
                NameFontSize = 7.5,
                BarcodeWidth = 92,
                BarcodeHeight = 18,
                BarcodeFontSize = 9,
                PriceFontSize = 8.5
            }
        };

        public static LabelSizeConfig Get(string? sizeId)
        {
            if (!string.IsNullOrWhiteSpace(sizeId) && Presets.TryGetValue(sizeId, out var config))
            {
                return config;
            }
            return Presets["40x30"];
        }
    }
}
