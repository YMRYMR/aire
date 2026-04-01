using System;
using System.Globalization;
using System.Windows.Data;

namespace Aire.UI
{
    /// <summary>
    /// Converts a numeric value to a percentage of that value.
    /// Parameter can be a double (e.g., 0.8 for 80%) or a string that can be parsed as double.
    /// </summary>
    public class PercentageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return double.NaN;

            double originalValue;
            if (value is double d)
                originalValue = d;
            else if (value is int i)
                originalValue = i;
            else if (value is float f)
                originalValue = f;
            else if (value is decimal dec)
                originalValue = (double)dec;
            else
            {
                if (!double.TryParse(value.ToString(), out originalValue))
                    return double.NaN;
            }

            double factor = 1.0;
            if (parameter != null)
            {
                if (parameter is double paramDouble)
                    factor = paramDouble;
                else if (parameter is string paramString)
                {
                    if (!double.TryParse(paramString, NumberStyles.Any, culture, out factor))
                        factor = 1.0;
                }
                else
                {
                    if (!double.TryParse(parameter.ToString(), out factor))
                        factor = 1.0;
                }
            }

            return originalValue * factor;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}