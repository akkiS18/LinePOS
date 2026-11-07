using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using Microsoft.Win32;
using PosElectro.Desktop.Data;
using PosElectro.Desktop.Models;

namespace PosElectro.Desktop.Services
{
    public class ExcelExportService
    {
        public static bool ExportReport(
            string periodTitle, 
            double totalRevenue, 
            double totalCost, 
            double netProfit, 
            int salesCount, 
            List<SaleReportItem> items,
            string categoryTitle = "Barchasi",
            string warehouseTitle = "Barcha omborlar",
            PosElectro.Desktop.Debt.DebtPeriodSummary? debtSummary = null)
        {
            try
            {
                var saveDialog = new SaveFileDialog
                {
                    Filter = "Excel fayli (*.xls)|*.xls|Barcha fayllar (*.*)|*.*",
                    FileName = $"LineKassa_Hisobot_{DateTime.Now:yyyyMMdd_HHmm}.xls",
                    Title = "Excel hisobotni saqlash"
                };

                if (saveDialog.ShowDialog() != true)
                {
                    return false;
                }

                var filePath = saveDialog.FileName;
                var html = new StringBuilder();

                html.Append(@"<html xmlns:o=""urn:schemas-microsoft-com:office:office"" xmlns:x=""urn:schemas-microsoft-com:office:excel"" xmlns=""http://www.w3.org/TR/REC-html40"">
<head>
    <meta http-equiv=""Content-Type"" content=""text/html; charset=UTF-8"">
    <!--[if gte mso 9]>
    <xml>
    <x:ExcelWorkbook>
    <x:ExcelWorksheets>
    <x:ExcelWorksheet>
    <x:Name>Savdo Hisoboti</x:Name>
    <x:WorksheetOptions><x:DisplayGridlines/></x:WorksheetOptions>
    </x:ExcelWorksheet>
    </x:ExcelWorksheets>
    </x:ExcelWorkbook>
    </xml>
    <![endif]-->
    <style>
        body { font-family: Calibri, Arial, sans-serif; }
        .title { font-size: 16pt; font-weight: bold; color: #1E3A8A; }
        .header { background-color: #10B981; color: #ffffff; font-weight: bold; text-align: center; border: 1px solid #000000; }
        .kpi-title { font-weight: bold; background-color: #F1F5F9; }
        .kpi-val { font-weight: bold; }
        .profit-val { font-weight: bold; color: #10B981; }
        td { padding: 6px 10px; border: 1px solid #CBD5E1; }
        .num { text-align: right; }
        .center { text-align: center; }
    </style>
</head>
<body>
    <table>
        <tr>
            <td colspan=""13"" class=""title"" style=""border:none;"">LINE KASSA - SAVDO VA FOYDA HISOBOTI</td>
        </tr>
        <tr>
            <td colspan=""13"" style=""border:none; font-size: 11pt;""><b>Hisobot davri:</b> " + periodTitle + @"</td>
        </tr>
        <tr>
            <td colspan=""13"" style=""border:none; font-size: 10pt;""><b>Kategoriya:</b> " + categoryTitle + @" | <b>Ombor:</b> " + warehouseTitle + @"</td>
        </tr>
        <tr>
            <td colspan=""13"" style=""border:none; font-size: 10pt; color: #64748B;""><b>Yaratilgan vaqt:</b> " + DateTime.Now.ToString("dd.MM.yyyy HH:mm") + @"</td>
        </tr>
        <tr><td colspan=""13"" style=""border:none;""></td></tr>
        
        <!-- KPI Xulosa -->
        <tr>
            <td colspan=""5"" class=""kpi-title"">UMUMIY MOLIYAVIY XULOSA</td>
            <td colspan=""8"" class=""kpi-title"">KO'RSATKICH</td>
        </tr>
        <tr>
            <td colspan=""5"">Jami Tushum:</td>
            <td colspan=""8"" class=""kpi-val"">" + totalRevenue.ToString("N0") + @" so'm</td>
        </tr>
        <tr>
            <td colspan=""5"">Jami Tan Narxi:</td>
            <td colspan=""8"" class=""kpi-val"">" + totalCost.ToString("N0") + @" so'm</td>
        </tr>
        <tr>
            <td colspan=""5"">Sof Foyda (karta solig‘idan keyin):</td>
            <td colspan=""8"" class=""profit-val"">" + netProfit.ToString("N0") + @" so'm</td>
        </tr>
        <tr>
            <td colspan=""5"">Rentabellik (Marja):</td>
            <td colspan=""8"" class=""kpi-val"">" + (totalRevenue > 0 ? $"{(netProfit / totalRevenue * 100):0.1f}%" : "0%") + @"</td>
        </tr>
        <tr>
            <td colspan=""5"">Jami Cheklar Soni:</td>
            <td colspan=""8"" class=""kpi-val"">" + salesCount + @" ta chek</td>
        </tr>");

                if (debtSummary != null)
                {
                    double cashflow = items.Sum(i => i.CashAmount + i.CardAmount) + debtSummary.NetDebtCollectedTotalUz;
                    html.Append($@"
        <tr><td colspan=""13"" style=""border:none;""></td></tr>
        <tr>
            <td colspan=""5"" class=""kpi-title"" style=""background-color:#E2E8F0;"">QARZ VA CASHFLOW (PUL OQIMI)</td>
            <td colspan=""8"" class=""kpi-title"" style=""background-color:#E2E8F0;"">KO'RSATKICH</td>
        </tr>
        <tr>
            <td colspan=""5"">Yangi Berilgan Nasiya:</td>
            <td colspan=""8"" class=""kpi-val"">{debtSummary.NewDebtIssuedUz:N0} so'm</td>
        </tr>
        <tr>
            <td colspan=""5"">Undirilgan Qarz (Yig'im):</td>
            <td colspan=""8"" class=""kpi-val"">{debtSummary.NetDebtCollectedTotalUz:N0} so'm (Naqd: {debtSummary.NetDebtCashCollectedUz:N0}, Karta: {debtSummary.NetDebtCardCollectedUz:N0})</td>
        </tr>
        <tr>
            <td colspan=""5"">Undiruv Karta Komissiyasi:</td>
            <td colspan=""8"" class=""kpi-val"">{debtSummary.NetDebtFeeUz:N0} so'm</td>
        </tr>
        <tr>
            <td colspan=""5"">Qaytarishdan Qarz Yopilishi:</td>
            <td colspan=""8"" class=""kpi-val"">{debtSummary.DebtReturnOffsetUz:N0} so'm</td>
        </tr>
        <tr>
            <td colspan=""5"">Kassa Pul Oqimi (Savdo + Qarz Yig'imi):</td>
            <td colspan=""8"" class=""kpi-val"" style=""color:#0284C7;"">{cashflow:N0} so'm</td>
        </tr>
        <tr>
            <td colspan=""5"">Davr Yakunidagi Faol Qarz:</td>
            <td colspan=""8"" class=""kpi-val"" style=""color:#DC2626;"">{debtSummary.ClosingDebtUz:N0} so'm ({debtSummary.ActiveDebtorsCount} ta mijoz)</td>
        </tr>
        <tr>
            <td colspan=""5"">Davr Yakunidagi Mijoz Haqi (Kredit):</td>
            <td colspan=""8"" class=""kpi-val"" style=""color:#7C3AED;"">{debtSummary.ClosingCreditUz:N0} so'm</td>
        </tr>");
                }

                html.Append(@"
        <tr><td colspan=""13"" style=""border:none;""></td></tr>

        <!-- Jadval sarlavhasi -->
        <tr class=""header"">
            <td>№</td>
            <td>Sana va Vaqt</td>
            <td>Chek №</td>
            <td>Ombor</td>
            <td>Kategoriya</td>
            <td>Mahsulot Nomi</td>
            <td>Birligi</td>
            <td>Miqdori</td>
            <td>Tan Narxi (so'm)</td>
            <td>Sotish Narxi (so'm)</td>
            <td>Jami Summa (so'm)</td>
            <td>Nasiya (so'm)</td>
            <td>Foyda / ta’sir<br/>(so'm)</td>
        </tr>");

                for (int i = 0; i < items.Count; i++)
                {
                    var item = items[i];
                    var qtyStr = item.Quantity % 1.0 == 0 ? ((long)item.Quantity).ToString() : item.Quantity.ToString("0.##");
                    var costLabel = item.CostCurrency == "USD"
                        ? $"${item.OriginalCost:N2} ({item.CostPrice:N0})"
                        : item.CostPrice.ToString("N0");
                    var whLabel = string.IsNullOrWhiteSpace(item.WarehouseName) ? "Do'kondagi ombor" : item.WarehouseName;
                    var debtLabel = item.DebtAmount > 0 ? item.DebtAmount.ToString("N0") : "-";

                    html.Append($@"
        <tr>
            <td class=""center"">{i + 1}</td>
            <td>{item.DateTime:dd.MM.yyyy HH:mm}</td>
            <td class=""center"">#{item.ReceiptNumber}</td>
            <td>{whLabel}</td>
            <td>{item.Category}</td>
            <td><b>{item.ProductName}</b></td>
            <td class=""center"">{item.UnitDisplay}</td>
            <td class=""center"">{qtyStr}</td>
            <td class=""num"">{costLabel}</td>
            <td class=""num"">{item.SellingPrice:N0}</td>
            <td class=""num""><b>{item.TotalPrice:N0}</b></td>
            <td class=""num"">{debtLabel}</td>
            <td class=""num"" style=""color:#10B981;""><b>{item.Profit:N2}</b></td>
        </tr>");
                }

                html.Append(@"
    </table>
</body>
</html>");

                File.WriteAllText(filePath, html.ToString(), Encoding.UTF8);

                var result = MessageBox.Show(
                    $"Hisobot muvaffaqiyatli saqlandi!\n\nFaylni hozir Excelda ochishni xohlaysizmi?",
                    "Excel Eksport",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (result == MessageBoxResult.Yes)
                {
                    Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });
                }

                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Excel eksportda xatolik yuz berdi:\n{ex.Message}", "Xatolik", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        /// <summary>
        /// Kam qolgan tovarlar ichidan buyurtma uchun tanlangan ro'yxatni chiroyli Excel (.xls) qilib saqlash.
        /// </summary>
        public static bool ExportReorderList(List<Product> items, string warehouseTitle = "Barcha omborlar")
        {
            try
            {
                if (items == null || items.Count == 0)
                {
                    MessageBox.Show("Buyurtma uchun hech qanday tovar tanlanmagan!", "Ogohlantirish", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }

                var saveDialog = new SaveFileDialog
                {
                    Filter = "Excel fayli (*.xls)|*.xls|Barcha fayllar (*.*)|*.*",
                    FileName = $"LineKassa_Buyurtma_Royxati_{DateTime.Now:yyyyMMdd_HHmm}.xls",
                    Title = "Buyurtma ro'yxatini Excelga saqlash"
                };

                if (saveDialog.ShowDialog() != true)
                {
                    return false;
                }

                var filePath = saveDialog.FileName;
                var html = new StringBuilder();

                html.Append(@"<html xmlns:o=""urn:schemas-microsoft-com:office:office"" xmlns:x=""urn:schemas-microsoft-com:office:excel"" xmlns=""http://www.w3.org/TR/REC-html40"">
<head>
    <meta http-equiv=""Content-Type"" content=""text/html; charset=UTF-8"">
    <!--[if gte mso 9]>
    <xml>
    <x:ExcelWorkbook>
    <x:ExcelWorksheets>
    <x:ExcelWorksheet>
    <x:Name>Buyurtma Ro'yxati</x:Name>
    <x:WorksheetOptions><x:DisplayGridlines/></x:WorksheetOptions>
    </x:ExcelWorksheet>
    </x:ExcelWorksheets>
    </x:ExcelWorkbook>
    </xml>
    <![endif]-->
    <style>
        body { font-family: Calibri, Arial, sans-serif; }
        .title { font-size: 16pt; font-weight: bold; color: #0B6477; }
        .header { background-color: #0B6477; color: #ffffff; font-weight: bold; text-align: center; border: 1px solid #000000; }
        .kpi-title { font-weight: bold; background-color: #F1F5F9; }
        .kpi-val { font-weight: bold; color: #0284C7; }
        td { padding: 6px 10px; border: 1px solid #CBD5E1; }
        .num { text-align: right; }
        .center { text-align: center; }
        .reorder-box { background-color: #FEF3C7; text-align: center; font-weight: bold; }
    </style>
</head>
<body>
    <table>
        <tr>
            <td colspan=""9"" class=""title"" style=""border:none;"">LINE KASSA - TOVAR BUYURTMA RO'YXATI (ZAKAZ)</td>
        </tr>
        <tr>
            <td colspan=""9"" style=""border:none; font-size: 11pt;""><b>Ombor:</b> " + warehouseTitle + @"</td>
        </tr>
        <tr>
            <td colspan=""9"" style=""border:none; font-size: 10pt; color: #64748B;""><b>Yaratilgan sana:</b> " + DateTime.Now.ToString("dd.MM.yyyy HH:mm") + @" | <b>Jami tovarlar:</b> " + items.Count + @" ta</td>
        </tr>
        <tr><td colspan=""9"" style=""border:none;""></td></tr>
        
        <tr class=""header"">
            <th style=""width: 40px; background-color: #0B6477; color: #ffffff;"">T/r</th>
            <th style=""width: 140px; background-color: #0B6477; color: #ffffff;"">Shtrix-kod</th>
            <th style=""width: 300px; background-color: #0B6477; color: #ffffff;"">Mahsulot nomi</th>
            <th style=""width: 150px; background-color: #0B6477; color: #ffffff;"">Kategoriya</th>
            <th style=""width: 100px; background-color: #0B6477; color: #ffffff;"">Qoldiq</th>
            <th style=""width: 80px; background-color: #0B6477; color: #ffffff;"">Birlik</th>
            <th style=""width: 120px; background-color: #0B6477; color: #ffffff;"">Tan narxi</th>
            <th style=""width: 130px; background-color: #D97706; color: #ffffff;"">Buyurtma Miqdori</th>
            <th style=""width: 200px; background-color: #0B6477; color: #ffffff;"">Izoh / Yetkazib beruvchi</th>
        </tr>");

                for (int i = 0; i < items.Count; i++)
                {
                    var item = items[i];
                    var costDisplay = item.CostCurrency == "USD" ? $"${item.CostPrice:N2}" : $"{item.CostPrice:N0} so'm";
                    var barcodeStr = string.IsNullOrWhiteSpace(item.Barcode) ? "—" : item.Barcode;

                    html.Append($@"
        <tr>
            <td class=""center"">{i + 1}</td>
            <td class=""center"">{barcodeStr}</td>
            <td><b>{item.Name}</b></td>
            <td>{item.Category}</td>
            <td class=""num"" style=""color: #EF4444; font-weight: bold;"">{item.StockQuantity}</td>
            <td class=""center"">{item.UnitDisplay}</td>
            <td class=""num"">{costDisplay}</td>
            <td class=""reorder-box""></td>
            <td>{item.Note}</td>
        </tr>");
                }

                html.Append(@"
    </table>
</body>
</html>");

                File.WriteAllText(filePath, html.ToString(), Encoding.UTF8);

                var result = MessageBox.Show(
                    $"Buyurtma ro'yxati muvaffaqiyatli saqlandi!\n\nFaylni hozir Excelda ochishni xohlaysizmi?",
                    "Excel Eksport",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (result == MessageBoxResult.Yes)
                {
                    Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });
                }

                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Excel eksportda xatolik yuz berdi:\n{ex.Message}", "Xatolik", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }
            /// <summary>
        /// Ombordagi tovarlarning tan narxi va toifalar kesimidagi jamlanmasini Excel (.xls) qilib saqlash.
        /// </summary>
        public static bool ExportWarehouseCostReport(DatabaseContext db, CurrencyService currencyService)
        {
            try
            {
                var saveDialog = new SaveFileDialog
                {
                    Filter = "Excel fayli (*.xls)|*.xls|Barcha fayllar (*.*)|*.*",
                    FileName = $"LineKassa_Ombor_TanNarxi_{DateTime.Now:yyyyMMdd_HHmm}.xls",
                    Title = "Ombor tan narxi va toifalar hisobotini Excelga saqlash"
                };

                if (saveDialog.ShowDialog() != true)
                {
                    return false;
                }

                var filePath = saveDialog.FileName;
                var uzCulture = new System.Globalization.CultureInfo("uz-UZ");
                uzCulture.NumberFormat.NumberGroupSeparator = " ";

                double usdRate = currencyService != null ? currencyService.GetCachedUsdRate() : CurrencyService.DEFAULT_USD_RATE;
                if (usdRate <= 0) usdRate = CurrencyService.DEFAULT_USD_RATE;

                var allProducts = db.GetAllProducts(includeDeleted: false);
                var warehouses = db.GetWarehouses(includeDeleted: false);
                var allStocks = db.GetAllProductStocks();

                var whStockMap = new Dictionary<(string whGuid, string prodGuid), double>();
                foreach (var s in allStocks)
                {
                    whStockMap[(s.WarehouseGuid, s.ProductGuid)] = s.Quantity;
                }

                double GetProductCostInUzs(Product p)
                {
                    if (string.Equals(p.CostCurrency, "USD", StringComparison.OrdinalIgnoreCase))
                    {
                        return p.CostPrice * usdRate;
                    }
                    return p.CostPrice;
                }

                // Umumiy jamlanma (Barcha omborlar)
                double totalInventoryCostUzs = 0;
                double totalStockQty = 0;
                int totalDistinctProducts = allProducts.Count;

                // Kategoriya bo'yicha guruhlash (Barcha omborlar)
                var globalCatStats = new Dictionary<string, (int productCount, double totalQty, double totalCost)>(StringComparer.OrdinalIgnoreCase);

                foreach (var p in allProducts)
                {
                    var cat = string.IsNullOrWhiteSpace(p.Category) ? "Boshqa" : p.Category.Trim();
                    if (cat.Equals("Barchasi", StringComparison.OrdinalIgnoreCase))
                    {
                        cat = "Kategoriyasiz";
                    }

                    double qty = p.StockQuantity;
                    double costPerUnit = GetProductCostInUzs(p);
                    double totalCost = qty * costPerUnit;

                    totalInventoryCostUzs += totalCost;
                    totalStockQty += qty;

                    if (!globalCatStats.ContainsKey(cat))
                    {
                        globalCatStats[cat] = (0, 0, 0);
                    }
                    var cur = globalCatStats[cat];
                    globalCatStats[cat] = (cur.productCount + 1, cur.totalQty + qty, cur.totalCost + totalCost);
                }

                // Har bir ombor uchun hisob-kitoblar
                var whStats = new List<(Warehouse wh, int productCount, double totalQty, double totalCost, List<(string cat, int pCount, double qty, double cost)> catList)>();

                foreach (var wh in warehouses)
                {
                    int whPCount = 0;
                    double whTotalQty = 0;
                    double whTotalCost = 0;
                    var whCatMap = new Dictionary<string, (int pCount, double qty, double cost)>(StringComparer.OrdinalIgnoreCase);

                    foreach (var p in allProducts)
                    {
                        double qty = 0;
                        if (whStockMap.TryGetValue((wh.Guid, p.Guid), out var sQty))
                        {
                            qty = sQty;
                        }
                        else if (!string.IsNullOrWhiteSpace(p.WarehouseGuid) && p.WarehouseGuid == wh.Guid)
                        {
                            qty = p.StockQuantity;
                        }
                        else if (warehouses.Count == 1)
                        {
                            qty = p.StockQuantity;
                        }

                        if (qty != 0)
                        {
                            var cat = string.IsNullOrWhiteSpace(p.Category) ? "Boshqa" : p.Category.Trim();
                            if (cat.Equals("Barchasi", StringComparison.OrdinalIgnoreCase))
                            {
                                cat = "Kategoriyasiz";
                            }

                            double costPerUnit = GetProductCostInUzs(p);
                            double costVal = qty * costPerUnit;

                            whPCount++;
                            whTotalQty += qty;
                            whTotalCost += costVal;

                            if (!whCatMap.ContainsKey(cat))
                            {
                                whCatMap[cat] = (0, 0, 0);
                            }
                            var cur = whCatMap[cat];
                            whCatMap[cat] = (cur.pCount + 1, cur.qty + qty, cur.cost + costVal);
                        }
                    }

                    var sortedWhCats = whCatMap.Select(kv => (cat: kv.Key, pCount: kv.Value.pCount, qty: kv.Value.qty, cost: kv.Value.cost))
                                               .OrderByDescending(x => x.cost)
                                               .ToList();

                    whStats.Add((wh, whPCount, whTotalQty, whTotalCost, sortedWhCats));
                }

                var sortedGlobalCats = globalCatStats.Select(kv => (cat: kv.Key, pCount: kv.Value.productCount, qty: kv.Value.totalQty, cost: kv.Value.totalCost))
                                                     .OrderByDescending(x => x.cost)
                                                     .ToList();

                var html = new StringBuilder();
                html.Append(@"<html xmlns:o=""urn:schemas-microsoft-com:office:office"" xmlns:x=""urn:schemas-microsoft-com:office:excel"" xmlns=""http://www.w3.org/TR/REC-html40"">
<head>
    <meta http-equiv=""Content-Type"" content=""text/html; charset=UTF-8"">
    <!--[if gte mso 9]>
    <xml>
    <x:ExcelWorkbook>
    <x:ExcelWorksheets>
    <x:ExcelWorksheet>
    <x:Name>Ombor Tan Narxi</x:Name>
    <x:WorksheetOptions><x:DisplayGridlines/></x:WorksheetOptions>
    </x:ExcelWorksheet>
    </x:ExcelWorksheets>
    </x:ExcelWorkbook>
    </xml>
    <![endif]-->
    <style>
        body { font-family: Calibri, Arial, sans-serif; }
        .title { font-size: 16pt; font-weight: bold; color: #073B4C; }
        .meta { font-size: 10.5pt; color: #334155; }
        .section-title { font-size: 12pt; font-weight: bold; color: #0B6477; background-color: #F0FDFA; border: 1px solid #99F6E4; padding: 6px 10px; }
        .header { background-color: #0B6477; color: #FFFFFF; font-weight: bold; text-align: center; border: 1px solid #000000; }
        .header-blue { background-color: #0284C7; color: #FFFFFF; font-weight: bold; text-align: center; border: 1px solid #000000; }
        .header-sub { background-color: #0F766E; color: #FFFFFF; font-weight: bold; text-align: center; border: 1px solid #000000; }
        .kpi-title { font-weight: bold; background-color: #F1F5F9; border: 1px solid #CBD5E1; }
        .kpi-val { font-weight: bold; border: 1px solid #CBD5E1; color: #0F172A; }
        .kpi-val-highlight { font-weight: bold; border: 1px solid #CBD5E1; color: #059669; font-size: 11pt; }
        td { padding: 6px 10px; border: 1px solid #CBD5E1; }
        .num { text-align: right; }
        .center { text-align: center; }
        .bold { font-weight: bold; }
        .total-row { background-color: #F8FAFC; font-weight: bold; border-top: 2px solid #0B6477; }
    </style>
</head>
<body>
    <table>");

                int excelRow = 0;

                // Sarlavha qatorlari
                excelRow++;
                html.Append($@"
        <tr>
            <td colspan=""6"" class=""title"" style=""border:none;"">LINE KASSA - OMBORLAR VA KATEGORIYALAR TAN NARXI HISOBOTI</td>
        </tr>");

                excelRow++;
                html.Append($@"
        <tr>
            <td colspan=""6"" class=""meta"" style=""border:none;""><b>Hisobot sanasi:</b> {DateTime.Now:dd.MM.yyyy HH:mm} | <b>Amaldagi kurs:</b> 1 USD = {usdRate:N0} so'm</td>
        </tr>");

                excelRow++;
                html.Append(@"<tr><td colspan=""6"" style=""border:none;""></td></tr>");

                // Asosiy KPI Kartochkasi
                excelRow++;
                html.Append(@"
        <tr>
            <td colspan=""3"" class=""kpi-title"">ASOSIY OMBOR KO'RSATKICHLARI</td>
            <td colspan=""3"" class=""kpi-title"">QIYMATI</td>
        </tr>");

                excelRow++;
                html.Append($@"
        <tr>
            <td colspan=""3"">Barcha tovar turlari soni (xil):</td>
            <td colspan=""3"" class=""kpi-val"">{totalDistinctProducts:N0} xil</td>
        </tr>");

                excelRow++;
                html.Append($@"
        <tr>
            <td colspan=""3"">Jami mahsulot qoldig'i (barcha omborlar):</td>
            <td colspan=""3"" class=""kpi-val"">{totalStockQty:N0} dona/birlik</td>
        </tr>");

                excelRow++;
                html.Append($@"
        <tr>
            <td colspan=""3"">JAMI OMBOR TAN NARXI QIYMATI:</td>
            <td colspan=""3"" class=""kpi-val-highlight"">{totalInventoryCostUzs:N0} so'm</td>
        </tr>");

                excelRow++;
                html.Append(@"<tr><td colspan=""6"" style=""border:none;""></td></tr>");

                // JADVAL 1: OMBORLAR BO'YICHA TAN NARXI JAMLANMASI
                excelRow++;
                html.Append(@"
        <tr>
            <td colspan=""6"" class=""section-title"">1. OMBORLAR KESIMIDA TAN NARXI JAMLANMASI</td>
        </tr>");

                excelRow++;
                html.Append(@"
        <tr>
            <td class=""header"" style=""width: 50px;"">№</td>
            <td class=""header"" style=""width: 250px;"">Ombor Nomi</td>
            <td class=""header"" style=""width: 140px;"">Tovar turlari (xil)</td>
            <td class=""header"" style=""width: 150px;"">Jami qoldiq miqdori</td>
            <td class=""header"" style=""width: 180px;"">Tan narxi qiymati (so'm)</td>
            <td class=""header"" style=""width: 120px;"">Ulushi (%)</td>
        </tr>");

                int whStartRow = excelRow + 1;
                for (int i = 0; i < whStats.Count; i++)
                {
                    excelRow++;
                    var item = whStats[i];
                    double share = totalInventoryCostUzs > 0 ? (item.totalCost / totalInventoryCostUzs * 100.0) : 0;
                    var escapedWhName = System.Security.SecurityElement.Escape(item.wh.Name + (item.wh.IsPrimary ? " (Asosiy)" : string.Empty));
                    int totalRowTarget = whStartRow + whStats.Count;

                    html.Append($@"
        <tr>
            <td class=""center"">{i + 1}</td>
            <td class=""bold"">{escapedWhName}</td>
            <td class=""num"" x:num=""{item.productCount}"">{item.productCount:N0}</td>
            <td class=""num"" x:num=""{item.totalQty}"">{item.totalQty:N0}</td>
            <td class=""num"" x:num=""{item.totalCost}"">{item.totalCost:N0}</td>
            <td class=""num"" x:num=""{share / 100.0}"" x:fmla=""=IF($E${totalRowTarget}&gt;0, E{excelRow}/$E${totalRowTarget}, 0)"" style=""mso-number-format:'0.0%';"">{share:F1}%</td>
        </tr>");
                }
                int whEndRow = excelRow;

                // Omborlar Jami qatori
                excelRow++;
                html.Append($@"
        <tr class=""total-row"">
            <td colspan=""2"" class=""bold"" style=""text-align: right; padding-right: 15px;"">Jami:</td>
            <td class=""num"" x:num=""{totalDistinctProducts}"" x:fmla=""=SUM(C{whStartRow}:C{whEndRow})"">{totalDistinctProducts:N0}</td>
            <td class=""num"" x:num=""{totalStockQty}"" x:fmla=""=SUM(D{whStartRow}:D{whEndRow})"">{totalStockQty:N0}</td>
            <td class=""num"" x:num=""{totalInventoryCostUzs}"" x:fmla=""=SUM(E{whStartRow}:E{whEndRow})"">{totalInventoryCostUzs:N0}</td>
            <td class=""num"" style=""mso-number-format:'0.0%';"">100.0%</td>
        </tr>");

                excelRow++;
                html.Append(@"<tr><td colspan=""6"" style=""border:none;""></td></tr>");

                // JADVAL 2: BARCHA OMBORLAR BO'YICHA KATEGORIYALAR KESIMIDA
                excelRow++;
                html.Append(@"
        <tr>
            <td colspan=""6"" class=""section-title"">2. BARCHA OMBORLAR BO'YICHA KATEGORIYALAR (TOIFALAR) KESIMIDA</td>
        </tr>");

                excelRow++;
                html.Append(@"
        <tr>
            <td class=""header-blue"" style=""width: 50px;"">№</td>
            <td class=""header-blue"" style=""width: 250px;"">Kategoriya (Toifa)</td>
            <td class=""header-blue"" style=""width: 140px;"">Tovar turlari (xil)</td>
            <td class=""header-blue"" style=""width: 150px;"">Jami qoldiq miqdori</td>
            <td class=""header-blue"" style=""width: 180px;"">Tan narxi qiymati (so'm)</td>
            <td class=""header-blue"" style=""width: 120px;"">Ulushi (%)</td>
        </tr>");

                int catStartRow = excelRow + 1;
                for (int i = 0; i < sortedGlobalCats.Count; i++)
                {
                    excelRow++;
                    var c = sortedGlobalCats[i];
                    double share = totalInventoryCostUzs > 0 ? (c.cost / totalInventoryCostUzs * 100.0) : 0;
                    var escapedCat = System.Security.SecurityElement.Escape(c.cat);
                    int totalCatRowTarget = catStartRow + sortedGlobalCats.Count;

                    html.Append($@"
        <tr>
            <td class=""center"">{i + 1}</td>
            <td class=""bold"">{escapedCat}</td>
            <td class=""num"" x:num=""{c.pCount}"">{c.pCount:N0}</td>
            <td class=""num"" x:num=""{c.qty}"">{c.qty:N0}</td>
            <td class=""num"" x:num=""{c.cost}"">{c.cost:N0}</td>
            <td class=""num"" x:num=""{share / 100.0}"" x:fmla=""=IF($E${totalCatRowTarget}&gt;0, E{excelRow}/$E${totalCatRowTarget}, 0)"" style=""mso-number-format:'0.0%';"">{share:F1}%</td>
        </tr>");
                }
                int catEndRow = excelRow;

                // Kategoriyalar Jami qatori
                excelRow++;
                html.Append($@"
        <tr class=""total-row"">
            <td colspan=""2"" class=""bold"" style=""text-align: right; padding-right: 15px;"">Jami:</td>
            <td class=""num"" x:num=""{totalDistinctProducts}"" x:fmla=""=SUM(C{catStartRow}:C{catEndRow})"">{totalDistinctProducts:N0}</td>
            <td class=""num"" x:num=""{totalStockQty}"" x:fmla=""=SUM(D{catStartRow}:D{catEndRow})"">{totalStockQty:N0}</td>
            <td class=""num"" x:num=""{totalInventoryCostUzs}"" x:fmla=""=SUM(E{catStartRow}:E{catEndRow})"">{totalInventoryCostUzs:N0}</td>
            <td class=""num"" style=""mso-number-format:'0.0%';"">100.0%</td>
        </tr>");

                // JADVAL 3+: HAR BIR OMBOR UCHUN ALOHIDA TOIFALAR KESIMI (agar omborlar mavjud bo'lsa)
                if (warehouses.Count > 1)
                {
                    int whIdx = 0;
                    foreach (var ws in whStats)
                    {
                        whIdx++;
                        excelRow++;
                        html.Append(@"<tr><td colspan=""6"" style=""border:none;""></td></tr>");

                        excelRow++;
                        var escapedWhTitle = System.Security.SecurityElement.Escape(ws.wh.Name);
                        html.Append($@"
        <tr>
            <td colspan=""6"" class=""section-title"" style=""background-color: #F8FAFC; border: 1px solid #CBD5E1; color: #0F172A;"">3.{whIdx}. {escapedWhTitle.ToUpperInvariant()} OMBORI - TOIFALAR KESIMIDA</td>
        </tr>");

                        excelRow++;
                        html.Append(@"
        <tr>
            <td class=""header-sub"" style=""width: 50px;"">№</td>
            <td class=""header-sub"" style=""width: 250px;"">Kategoriya (Toifa)</td>
            <td class=""header-sub"" style=""width: 140px;"">Tovar turlari (xil)</td>
            <td class=""header-sub"" style=""width: 150px;"">Qoldiq miqdori</td>
            <td class=""header-sub"" style=""width: 180px;"">Tan narxi qiymati (so'm)</td>
            <td class=""header-sub"" style=""width: 120px;"">Ulushi (%)</td>
        </tr>");

                        int subStartRow = excelRow + 1;
                        for (int k = 0; k < ws.catList.Count; k++)
                        {
                            excelRow++;
                            var c = ws.catList[k];
                            double share = ws.totalCost > 0 ? (c.cost / ws.totalCost * 100.0) : 0;
                            var escapedCat = System.Security.SecurityElement.Escape(c.cat);
                            int subTotalRowTarget = subStartRow + ws.catList.Count;

                            html.Append($@"
        <tr>
            <td class=""center"">{k + 1}</td>
            <td class=""bold"">{escapedCat}</td>
            <td class=""num"" x:num=""{c.pCount}"">{c.pCount:N0}</td>
            <td class=""num"" x:num=""{c.qty}"">{c.qty:N0}</td>
            <td class=""num"" x:num=""{c.cost}"">{c.cost:N0}</td>
            <td class=""num"" x:num=""{share / 100.0}"" x:fmla=""=IF($E${subTotalRowTarget}&gt;0, E{excelRow}/$E${subTotalRowTarget}, 0)"" style=""mso-number-format:'0.0%';"">{share:F1}%</td>
        </tr>");
                        }
                        int subEndRow = excelRow;

                        excelRow++;
                        html.Append($@"
        <tr class=""total-row"">
            <td colspan=""2"" class=""bold"" style=""text-align: right; padding-right: 15px;"">Jami:</td>
            <td class=""num"" x:num=""{ws.productCount}"" x:fmla=""=SUM(C{subStartRow}:C{subEndRow})"">{ws.productCount:N0}</td>
            <td class=""num"" x:num=""{ws.totalQty}"" x:fmla=""=SUM(D{subStartRow}:D{subEndRow})"">{ws.totalQty:N0}</td>
            <td class=""num"" x:num=""{ws.totalCost}"" x:fmla=""=SUM(E{subStartRow}:E{subEndRow})"">{ws.totalCost:N0}</td>
            <td class=""num"" style=""mso-number-format:'0.0%';"">100.0%</td>
        </tr>");
                    }
                }

                html.Append(@"
    </table>
</body>
</html>");

                File.WriteAllText(filePath, html.ToString(), Encoding.UTF8);

                var result = MessageBox.Show(
                    $"Ombordagi tovarlar tan narxi va toifalar hisoboti muvaffaqiyatli saqlandi!\n\nFayl joylashuvi:\n{filePath}\n\nFaylni hozir Excelda ochishni xohlaysizmi?",
                    "Excel Eksport",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (result == MessageBoxResult.Yes)
                {
                    Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });
                }

                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ombor tan narxi hisobotini eksport qilishda xatolik yuz berdi:\n{ex.Message}", "Xatolik", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

    }
}