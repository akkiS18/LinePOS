using System;
using System.Globalization;
using System.Windows.Data;

namespace PosElectro.Desktop.Views
{
    /// <summary>
    /// Ikki qiymat (masalan, kartadagi ombor Guid va tanlangan ombor Guid) teng bo'lsa true.
    /// Faqat ko'rsatish uchun (tanlangan kartani ajratib ko'rsatish).
    /// </summary>
    public class SameValueMultiConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length < 2 || values[0] == null || values[1] == null) return false;
            return string.Equals(System.Convert.ToString(values[0], CultureInfo.InvariantCulture),
                                 System.Convert.ToString(values[1], CultureInfo.InvariantCulture),
                                 StringComparison.Ordinal);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
