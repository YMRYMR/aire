using System;
using System.Globalization;
using System.Windows.Data;

namespace Aire.UI
{
    /// <summary>
    /// Inflates the effective viewport size so the scrollbar thumb cannot shrink below a minimum length.
    /// </summary>
    public sealed class MinimumScrollViewportConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 3)
                return System.Windows.Data.Binding.DoNothing;

            if (!TryGetDouble(values[0], out var viewport) ||
                !TryGetDouble(values[1], out var maximum) ||
                !TryGetDouble(values[2], out var trackLength))
            {
                return System.Windows.Data.Binding.DoNothing;
            }

            var minThumbLength = 50.0;
            if (parameter != null && TryGetDouble(parameter, out var parsedMin))
                minThumbLength = parsedMin;

            if (trackLength <= minThumbLength || maximum <= 0)
                return viewport;

            var requiredViewport = (minThumbLength * maximum) / (trackLength - minThumbLength);
            if (double.IsNaN(requiredViewport) || double.IsInfinity(requiredViewport) || requiredViewport <= 0)
                return viewport;

            return Math.Max(viewport, requiredViewport);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotSupportedException();

        private static bool TryGetDouble(object value, out double result)
        {
            switch (value)
            {
                case double d:
                    result = d;
                    return true;
                case int i:
                    result = i;
                    return true;
                case float f:
                    result = f;
                    return true;
                case decimal dec:
                    result = (double)dec;
                    return true;
                case null:
                    result = 0;
                    return false;
                default:
                    return double.TryParse(value.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out result) ||
                           double.TryParse(value.ToString(), NumberStyles.Any, CultureInfo.CurrentCulture, out result);
            }
        }
    }
}
