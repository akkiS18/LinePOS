using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using Microsoft.Win32;
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
            string warehouseTitle = "Barcha omborlar")
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
            <td colspan=""12"" class=""title"" style=""border:none;"">LINE KASSA - SAVDO VA FOYDA HISOBOTI</td>
        </tr>
        <tr>
            <td colspan=""12"" style=""border:none; font-size: 11pt;""><b>Hisobot davri:</b> " + periodTitle + @"</td>
        </tr>
        <tr>
            <td colspan=""12"" style=""border:none; font-size: 10pt;""><b>Kategoriya:</b> " + categoryTitle + @" | <b>Ombor:</b> " + warehouseTitle + @"</td>
        </tr>
        <tr>
            <td colspan=""12"" style=""border:none; font-size: 10pt; color: #64748B;""><b>Yaratilgan vaqt:</b> " + DateTime.Now.ToString("dd.MM.yyyy HH:mm") + @"</td>
        </tr>
        <tr><td colspan=""12"" style=""border:none;""></td></tr>
        
        <!-- KPI Xulosa -->
        <tr>
            <td colspan=""5"" class=""kpi-title"">UMUMIY MOLIYAVIY XULOSA</td>
            <td colspan=""7"" class=""kpi-title"">KO'RSATKICH</td>
        </tr>
        <tr>
            <td colspan=""5"">Jami Tushum:</td>
            <td colspan=""7"" class=""kpi-val"">" + totalRevenue.ToString("N0") + @" so'm</td>
        </tr>
        <tr>
            <td colspan=""5"">Jami Tan Narxi:</td>
            <td colspan=""7"" class=""kpi-val"">" + totalCost.ToString("N0") + @" so'm</td>
        </tr>
        <tr>
            <td colspan=""5"">Sof Foyda:</td>
            <td colspan=""7"" class=""profit-val"">+" + netProfit.ToString("N0") + @" so'm</td>
        </tr>
        <tr>
            <td colspan=""5"">Rentabellik (Marja):</td>
            <td colspan=""7"" class=""kpi-val"">" + (totalRevenue > 0 ? $"{(netProfit / totalRevenue * 100):0.1f}%" : "0%") + @"</td>
        </tr>
        <tr>
            <td colspan=""5"">Jami Cheklar Soni:</td>
            <td colspan=""7"" class=""kpi-val"">" + salesCount + @" ta chek</td>
        </tr>
        <tr><td colspan=""12"" style=""border:none;""></td></tr>

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
            <td>Sof Foyda (so'm)</td>
        </tr>");

                for (int i = 0; i < items.Count; i++)
                {
                    var item = items[i];
                    var qtyStr = item.Quantity % 1.0 == 0 ? ((long)item.Quantity).ToString() : item.Quantity.ToString("0.##");
                    var costLabel = item.CostCurrency == "USD"
                        ? $"${item.OriginalCost:N2} ({item.CostPrice:N0})"
                        : item.CostPrice.ToString("N0");
                    var whLabel = string.IsNullOrWhiteSpace(item.WarehouseName) ? "Do'kondagi ombor" : item.WarehouseName;

                    html.Append($@"
        <tr>
            <td class=""center"">{i + 1}</td>
            <td>{item.DateTime:dd.MM.yyyy HH:mm}</td>
            <td class=""center"">#{item.SaleId}</td>
            <td>{whLabel}</td>
            <td>{item.Category}</td>
            <td><b>{item.ProductName}</b></td>
            <td class=""center"">{item.UnitDisplay}</td>
            <td class=""center"">{qtyStr}</td>
            <td class=""num"">{costLabel}</td>
            <td class=""num"">{item.SellingPrice:N0}</td>
            <td class=""num""><b>{item.TotalPrice:N0}</b></td>
            <td class=""num"" style=""color:#10B981;""><b>+{item.Profit:N0}</b></td>
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
    }
}
