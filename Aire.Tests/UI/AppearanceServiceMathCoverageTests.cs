using System;
using System.Windows;
using System.Windows.Media;
using Aire.Services;
using Xunit;

namespace Aire.Tests.UI;

public class AppearanceServiceMathCoverageTests : TestBase
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

    // Contrast enforcement is tested behaviorally via Apply() since the
    // internal EnsureContrast/ContrastRatio helpers are private in Codex's design.

    [Theory]
    [InlineData(0.0, 0.0)]   // full dark, no tint
    [InlineData(0.0, 0.5)]   // full dark, max tint
    [InlineData(1.0, 0.0)]   // full light, no tint
    [InlineData(1.0, 0.5)]   // full light, max tint
    [InlineData(0.3, 0.3)]   // arbitrary dark-ish
    public void Apply_TextBrushContrastsAgainstBackground(double brightness, double tintPosition)
    {
        // TextBrush must satisfy WCAG AA (4.5:1) against BackgroundBrush for any palette setting.
        RunOnStaThread(() =>
        {
            EnsureApplication();
            AppearanceService.ResetForTesting();
            AppearanceService.Apply(brightness, tintPosition);

            var res = Application.Current?.Resources;
            if (res?["TextBrush"] is SolidColorBrush tb &&
                res["BackgroundBrush"] is SolidColorBrush bg)
            {
                static double Ch(byte v) { double s = v / 255.0; return s <= 0.04045 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4); }
                static double Lum(Color c) => 0.2126 * Ch(c.R) + 0.7152 * Ch(c.G) + 0.0722 * Ch(c.B);

                double l1 = Lum(tb.Color);
                double l2 = Lum(bg.Color);
                double lighter = Math.Max(l1, l2);
                double darker  = Math.Min(l1, l2);
                double ratio = (lighter + 0.05) / (darker + 0.05);

                Assert.True(ratio >= 4.5,
                    $"brightness={brightness} tint={tintPosition}: " +
                    $"TextBrush(lum={l1:F3}) vs BackgroundBrush(lum={l2:F3}) contrast={ratio:F2} < 4.5");
            }
        });
    }
}
