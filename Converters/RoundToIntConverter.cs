using System;
using System.Globalization;
using System.Windows.Data;

namespace ACO_Optimizer.Converters
{
    public class RoundToIntConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return 0;
            if (value is double d) return (int)Math.Round(d);
            if (value is float f) return (int)Math.Round(f);
            if (value is decimal m) return (int)Math.Round((double)m);
            if (value is long l) return (int)l;
            if (value is int i) return i;
            if (value is TimeSpan ts) return (int)Math.Round(ts.TotalMinutes);
            if (double.TryParse(value.ToString(), NumberStyles.Any, culture, out var parsed))
                return (int)Math.Round(parsed);
            return 0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}