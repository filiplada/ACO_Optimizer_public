using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;

namespace ACO_Optimizer.Converters
{
    public class NotNullToVisibilityConverter : MarkupExtension, IValueConverter
    {
        private static NotNullToVisibilityConverter _instance;

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            return _instance ?? (_instance = new NotNullToVisibilityConverter());
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // If value is not null, return Visible, otherwise Collapsed
            return value != null ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}