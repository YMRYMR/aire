using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Aire.UI
{
    /// <summary>
    /// Converts a boolean value to a Visibility (True = Visible, False = Collapsed).
    /// Optionally supports inversion via parameter "Invert".
    /// </summary>
    public class BooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool boolValue = false;
            if (value is bool b)
                boolValue = b;
            else if (value is bool?)
                boolValue = ((bool?)value) ?? false;
            else
                return Visibility.Collapsed;

            bool invert = false;
            if (parameter is string paramStr && paramStr.Equals("Invert", StringComparison.OrdinalIgnoreCase))
                invert = true;

            bool visible = invert ? !boolValue : boolValue;
            return visible ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility visibility)
            {
                bool invert = false;
                if (parameter is string paramStr && paramStr.Equals("Invert", StringComparison.OrdinalIgnoreCase))
                    invert = true;

                bool visible = visibility == Visibility.Visible;
                return invert ? !visible : visible;
            }
            return false;
        }
    }
}