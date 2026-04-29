using System;
using System.Globalization;
using System.Windows.Data;

namespace Aire.UI
{
    /// <summary>
    /// Inflates a ScrollBar viewport just enough to keep the draggable thumb above a
    /// minimum pixel length.
    /// </summary>
    public sealed class MinimumScrollViewportConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            var viewport = GetDouble(values, 0);
            var maximum = GetDouble(values, 1);
            var trackLength = GetDouble(values, 2);
            var desiredThumbLength = GetParameter(parameter, 50.0);

            if (viewport <= 0 || maximum <= 0 || trackLength <= 0 || desiredThumbLength <= 0)
                return viewport;

            // If the track is shorter than the requested thumb length, just keep the
            // viewport inflated so the thumb fills the available space.
            if (trackLength <= desiredThumbLength + 1.0)
                return Math.Max(viewport, maximum);

            // Solve thumb = track * viewport / (maximum + viewport) for viewport.
            var requiredViewport = (desiredThumbLength * maximum) / Math.Max(1.0, trackLength - desiredThumbLength);
            if (double.IsNaN(requiredViewport) || double.IsInfinity(requiredViewport) || requiredViewport <= 0)
                return viewport;

            return Math.Max(viewport, requiredViewport);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotSupportedException();

        private static double GetDouble(object[] values, int index)
        {
            if (values.Length <= index || values[index] == null)
                return 0;

            return values[index] switch
            {
                double d => d,
                float f => f,
                int i => i,
                long l => l,
                decimal m => (double)m,
                _ when double.TryParse(values[index].ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) => parsed,
                _ => 0
            };
        }

        private static double GetParameter(object? parameter, double fallback)
        {
            if (parameter == null)
                return fallback;

            return parameter switch
            {
                double d => d,
                float f => f,
                int i => i,
                long l => l,
                decimal m => (double)m,
                _ when double.TryParse(parameter.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) => parsed,
                _ => fallback
            };
        }
    }
}
