using System;
using System.Globalization;
using System.Windows;
using Aire.UI;
using Xunit;

namespace Aire.Tests.UI;

public class UiValueConverterTests
{
    [Fact]
    public void PercentageConverter_HandlesMultipleInputShapes()
    {
        PercentageConverter converter = new PercentageConverter();
        Assert.Equal(25.0, converter.Convert(50, typeof(double), 0.5, CultureInfo.InvariantCulture));
        Assert.Equal(12.5, converter.Convert("25", typeof(double), "0.5", CultureInfo.InvariantCulture));
        Assert.True(double.IsNaN((double)converter.Convert("bad", typeof(double), "0.5", CultureInfo.InvariantCulture)));
        Assert.Throws<NotImplementedException>(() => converter.ConvertBack(1, typeof(double), null, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void BooleanToVisibilityConverter_ConvertsForwardAndBackward()
    {
        BooleanToVisibilityConverter booleanToVisibilityConverter = new BooleanToVisibilityConverter();
        Assert.Equal(Visibility.Visible, booleanToVisibilityConverter.Convert(true, typeof(Visibility), null, CultureInfo.InvariantCulture));
        Assert.Equal(Visibility.Collapsed, booleanToVisibilityConverter.Convert(true, typeof(Visibility), "Invert", CultureInfo.InvariantCulture));
        Assert.Equal(true, booleanToVisibilityConverter.ConvertBack(Visibility.Visible, typeof(bool), null, CultureInfo.InvariantCulture));
        Assert.Equal(false, booleanToVisibilityConverter.ConvertBack(Visibility.Visible, typeof(bool), "Invert", CultureInfo.InvariantCulture));
        Assert.Equal(Visibility.Collapsed, booleanToVisibilityConverter.Convert("nope", typeof(Visibility), null, CultureInfo.InvariantCulture));
    }
}
