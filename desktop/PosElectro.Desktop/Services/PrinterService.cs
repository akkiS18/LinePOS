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

        private byte[] BuildEscPosReceipt(Sale sale)
        {
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);

            // ESC @ - Printerni boshlang'ich holatga keltirish (Initialize)
            bw.Write(new byte[] { 0x1B, 0x40 });

            // Kodirovka: PC866 (Kirill/Lotin uchun)
            bw.Write(new byte[] { 0x1B, 0x74, 17 });

            // Sarlavha (Markazlashtirilgan, qalin, 2 barobar baland)
            bw.Write(new byte[] { 0x1B, 0x61, 0x01 }); // Markazga
            bw.Write(new byte[] { 0x1B, 0x45, 0x01 }); // Qalin (Bold)
            bw.Write(new byte[] { 0x1D, 0x21, 0x11 }); // Double height & width
            WriteCp866(bw, "SMART KASSA\n");

            // Normal shrift
            bw.Write(new byte[] { 0x1D, 0x21, 0x00 });
            bw.Write(new byte[] { 0x1B, 0x45, 0x00 }); // Bold off
            WriteCp866(bw, "Elektr jihozlari do'koni\n");
            WriteCp866(bw, "--------------------------------\n"); // 32 ta belgi

            // Chek ma'lumotlari (Chap tomondan)
            bw.Write(new byte[] { 0x1B, 0x61, 0x00 }); // Chapga
            WriteCp866(bw, $"Chek: #{sale.ReceiptNumber}\n");
            WriteCp866(bw, $"Sana: {sale.CreatedDateTime:dd.MM.yyyy HH:mm}\n");
            WriteCp866(bw, $"To'lov: {sale.PaymentTypeDisplay}\n");
            WriteCp866(bw, "--------------------------------\n");

            // Sarlavha jadvali
            // Kenglik 32 belgi: Nomi (16 ta), Miqdor (6 ta), Summa (10 ta)
            WriteCp866(bw, "Tovar              Miqd.    Jami\n");
            WriteCp866(bw, "--------------------------------\n");

            foreach (var item in sale.Items)
            {
                string name = item.ProductName;
                if (name.Length > 30) name = name.Substring(0, 27) + "...";
                WriteCp866(bw, $"{name}\n");

                string qtyStr = $"{item.Quantity:0.##}";
                string totalStr = $"{item.TotalPrice:N0}";

                // 2-qator: "  45 000 x 2d" chapda, "90 000" o'ngda
                string lineLeft = $"  {item.PriceAtSale:N0} x {qtyStr}";
                string lineRight = totalStr;

                int space = 32 - lineLeft.Length - lineRight.Length;
                if (space < 1) space = 1;
                string line = lineLeft + new string(' ', space) + lineRight + "\n";
                WriteCp866(bw, line);
            }

            WriteCp866(bw, "--------------------------------\n");

            // Jami summa (Qalin va o'ng tomonga)
            bw.Write(new byte[] { 0x1B, 0x45, 0x01 }); // Bold on
            bw.Write(new byte[] { 0x1D, 0x21, 0x01 }); // Double height

            string totalTitle = "JAMI:";
            string totalVal = $"{sale.TotalAmount:N0} so'm";
            int totalSpace = 32 - totalTitle.Length - totalVal.Length;
            if (totalSpace < 1) totalSpace = 1;
            WriteCp866(bw, totalTitle + new string(' ', totalSpace) + totalVal + "\n");

            bw.Write(new byte[] { 0x1D, 0x21, 0x00 }); // Normal
            bw.Write(new byte[] { 0x1B, 0x45, 0x00 }); // Bold off

            // To'lov tafsilotlari (Naqd / Karta)
            if (sale.PaymentType == PaymentType.SPLIT)
            {
                WriteCp866(bw, $"  Naqd:  {sale.CashAmount:N0} so'm\n");
                WriteCp866(bw, $"  Karta: {sale.CardAmount:N0} so'm\n");
            }

            WriteCp866(bw, "--------------------------------\n");

            // Footer (Markazlashtirilgan)
            bw.Write(new byte[] { 0x1B, 0x61, 0x01 }); // Markazga
            WriteCp866(bw, "Rahmat, xaridingiz uchun!\n");
            WriteCp866(bw, "Yana tashrif buyuring!\n\n\n\n");

            // Qog'ozni kesish yoki surish (GS V 66 0)
            bw.Write(new byte[] { 0x1D, 0x56, 0x42, 0x00 });

            return ms.ToArray();
        }

        private static void WriteCp866(BinaryWriter bw, string text)
        {
            // O'zbekcha maxsus belgilarni almashtirish
            string normalized = text
                .Replace("o‘", "o'").Replace("O‘", "O'")
                .Replace("g‘", "g'").Replace("G‘", "G'")
                .Replace("ʻ", "'").Replace("’", "'").Replace("‘", "'");

            byte[] b;
            try
            {
                b = Encoding.GetEncoding(866).GetBytes(normalized);
            }
            catch
            {
                b = Encoding.UTF8.GetBytes(normalized);
            }
            bw.Write(b);
        }

        /// <summary>
        /// 58 mm kassa chekining matnli ko'rinishini hosil qilish (Oldindan ko'rish - Preview uchun)
        /// </summary>
        public static string BuildReceiptText(Sale sale)
        {
            var sb = new StringBuilder();
            sb.AppendLine("          SMART KASSA           ");
            sb.AppendLine("    Elektr jihozlari do'koni    ");
            sb.AppendLine("--------------------------------");
            sb.AppendLine($"Chek: #{sale.ReceiptNumber}");
            sb.AppendLine($"Sana: {sale.CreatedDateTime:dd.MM.yyyy HH:mm}");
            sb.AppendLine($"To'lov turi: {sale.PaymentTypeDisplay}");
            sb.AppendLine("--------------------------------");
            sb.AppendLine("Tovar              Miqd.    Jami");
            sb.AppendLine("--------------------------------");

            foreach (var item in sale.Items)
            {
                string name = item.ProductName;
                if (name.Length > 30) name = name.Substring(0, 27) + "...";
                sb.AppendLine(name);

                string qtyStr = $"{item.Quantity:0.##}";
                string totalStr = $"{item.TotalPrice:N0}";
                string lineLeft = $"  {item.PriceAtSale:N0} x {qtyStr}";
                string lineRight = totalStr;

                int space = 32 - lineLeft.Length - lineRight.Length;
                if (space < 1) space = 1;
                sb.AppendLine(lineLeft + new string(' ', space) + lineRight);
            }

            sb.AppendLine("--------------------------------");
            string totalTitle = "JAMI:";
            string totalVal = $"{sale.TotalAmount:N0} so'm";
            int totalSpace = 32 - totalTitle.Length - totalVal.Length;
            if (totalSpace < 1) totalSpace = 1;
            sb.AppendLine(totalTitle + new string(' ', totalSpace) + totalVal);

            if (sale.PaymentType == PaymentType.SPLIT)
            {
                sb.AppendLine($"  Naqd:  {sale.CashAmount:N0} so'm");
                sb.AppendLine($"  Karta: {sale.CardAmount:N0} so'm");
            }

            sb.AppendLine("--------------------------------");
            sb.AppendLine("    Rahmat, xaridingiz uchun!   ");
            sb.AppendLine("       Yana tashrif buyuring!   ");
            return sb.ToString();
        }

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
                new System.Windows.Documents.Run($"Hujjat raqami: Chek #{sale.ReceiptNumber}\nSana: {sale.CreatedDateTime:dd.MM.yyyy HH:mm}")));
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
            double? offsetMm = null)
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

                var visual = CreateLabelVisual(barcode, productName, price, showName, showPrice, showBarcode, config, currentOffset);

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
        /// </summary>
        public static DrawingVisual CreateLabelVisual(
            string barcode,
            string productName,
            double price,
            bool showName,
            bool showPrice,
            bool showBarcode,
            LabelSizeConfig? config = null,
            double offsetMm = 0)
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
                // Oq fon (chetlar oq chiqishi uchun kengroq)
                dc.DrawRectangle(Brushes.White, null, new Rect(-30, 0, width + 60, height));

                double curY = 2;

                // 1. Do'kon nomi olib tashlandi, lekin uning o'rnini to'ldirish uchun 
                // shtrix-kod joylashuvi o'zgarmasligi uchun curY ga biroz bo'sh joy qo'shamiz (taxminan 4px).
                // Mahsulot nomining o'zi kattalashgani uchun qolgan joyni o'zi egallaydi.
                curY += 4; 

                // 2. Mahsulot nomi (Agar tanlangan bo'lsa) - KATTAROQ FONT BILAN
                if (showName && !string.IsNullOrWhiteSpace(productName))
                {
                    var typefaceName = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
                    int maxLen = cfg.VisualWidth > 200 ? 36 : 26;
                    string displayName = productName.Length > maxLen ? productName.Substring(0, maxLen - 3) + "..." : productName;
                    var ftName = new FormattedText(
                        displayName,
                        CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        typefaceName,
                        cfg.NameFontSize + 2.5, // Mahsulot nomi kattalashtirildi
                        Brushes.Black,
                        96);
                    double nameX = centerX - (ftName.Width / 2.0);
                    dc.DrawText(ftName, new Point(nameX, curY));
                    curY += ftName.Height + 2;
                }

                // 3. Shtrix-kod chiziqlari (To'liq vektor, qop-qora, tiniq va skaner oson o'qiydi)
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
                        96);
                    double codeX = centerX - (ftCode.Width / 2.0);
                    dc.DrawText(ftCode, new Point(codeX, curY));
                    curY += ftCode.Height + 2;
                }

                // 4. Narxi (Agar tanlangan bo'lsa)
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
                        96);
                    double priceX = centerX - (ftPrice.Width / 2.0);
                    dc.DrawText(ftPrice, new Point(priceX, curY));
                }
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
