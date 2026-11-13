using System;
using System.Globalization;
using System.Windows.Data;

namespace PosElectro.Desktop.Views
{
    /// <summary>
    /// Qiymat ConverterParameter'ga teng bo'lsa true (masalan, tanlangan to'lov turi yoki chegirma tugmasini
    /// maketdagidek ajratib ko'rsatish uchun). Faqat ko'rinish uchun, bir tomonlama.
    /// </summary>
    public class EqualsConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => string.Equals(System.Convert.ToString(value, CultureInfo.InvariantCulture),
                             System.Convert.ToString(parameter, CultureInfo.InvariantCulture),
                             StringComparison.Ordinal);

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => Binding.DoNothing;
    }
}
