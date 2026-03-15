using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ACO_Optimizer.Converters
{
    public class GreaterThanZeroToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return Visibility.Collapsed;
            double d;
            if (value is double dd) d = dd;
            else if (!double.TryParse(value.ToString(), NumberStyles.Any, culture, out d)) return Visibility.Collapsed;
            return d > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}