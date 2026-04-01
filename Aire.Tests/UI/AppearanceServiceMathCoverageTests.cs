using System.Windows.Media;
using Aire.Services;
using Xunit;

namespace Aire.Tests.UI;

public class AppearanceServiceMathCoverageTests
{
    [Fact]
    public void ColorMath_CoversHueAndHslBranches()
    {
        var (h1, s1, _) = AppearanceService.ToHsl(Color.FromRgb(48, 48, 48));
        var (h2, s2, _) = AppearanceService.ToHsl(Color.FromRgb(255, 0, 0));

        Assert.Equal(0.0, h1);
        Assert.Equal(0.0, s1);
        Assert.Equal(0.0, h2, precision: 0);
        Assert.Equal(1.0, s2, precision: 1);

        Color grey = AppearanceService.FromHsl(200.0, 0.0, 0.5);
        Assert.Equal(grey.R, grey.G);
        Assert.Equal(grey.G, grey.B);

        Color blue = AppearanceService.FromHsl(240.0, 0.9, 0.5);
        Assert.True(blue.B > blue.R && blue.B > blue.G);

        Color nearlyOriginal = AppearanceService.Tinted(Color.FromRgb(12, 34, 56), tintHue: 180.0, strength: 0.001);
        Assert.Equal(Color.FromRgb(12, 34, 56), nearlyOriginal);

        Color blueTinted = AppearanceService.Tinted(Color.FromRgb(128, 128, 128), tintHue: 240.0, strength: 1.0);
        Assert.True(blueTinted.B > blueTinted.R);

        Assert.InRange(AppearanceService.HueChannel(0.1, 0.6, -0.2), 0.09, 0.11);
        Assert.InRange(AppearanceService.HueChannel(0.1, 0.6,  1.2), 0.59, 0.61);
        Assert.InRange(AppearanceService.HueChannel(0.1, 0.6, 0.05), 0.24, 0.26);
        Assert.InRange(AppearanceService.HueChannel(0.1, 0.6,  0.4), 0.59, 0.61);
        Assert.InRange(AppearanceService.HueChannel(0.1, 0.6,  0.6), 0.29, 0.31);
        Assert.InRange(AppearanceService.HueChannel(0.1, 0.6,  0.9), 0.09, 0.11);

        Assert.Equal(  0.0, AppearanceService.LerpHue( 10.0, 350.0, 0.5), precision: 3);
        Assert.Equal(  0.0, AppearanceService.LerpHue(350.0,  10.0, 0.5), precision: 3);
        Assert.Equal( 45.0, AppearanceService.LerpHue( 30.0,  60.0, 0.5), precision: 3);
    }
}
