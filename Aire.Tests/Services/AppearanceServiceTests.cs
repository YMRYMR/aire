using System;
using System.Windows;
using System.Windows.Media;
using Aire.Services;
using Xunit;

namespace Aire.Tests.Services;

public class AppearanceServiceTests : TestBase
{
    [Fact]
    public void Apply_UpdatesThemeStateAndMessageBrushes()
    {
        RunOnStaThread(() =>
        {
            EnsureApplication();
            AppearanceService.ResetForTesting();
            Color color = AppearanceService.UserBgBrush.Color;
            int raised = 0;
            AppearanceService.AppearanceChanged += Handler;
            try
            {
                AppearanceService.Apply(0.0, 0.5);
            }
            finally
            {
                AppearanceService.AppearanceChanged -= Handler;
            }
            Assert.True(AppearanceService.UsesDarkPalette);
            Assert.Equal(0.0, AppearanceService.Brightness);
            Assert.Equal(0.5, AppearanceService.TintPosition);
            Assert.NotEqual(color, AppearanceService.UserBgBrush.Color);
            Assert.True(raised >= 1);
            void Handler()
            {
                raised++;
            }
        });
    }

    [Fact]
    public void Apply_KeepsTextBrushesNeutralWhenTintChanges()
    {
        RunOnStaThread(() =>
        {
            EnsureApplication();
            AppearanceService.ResetForTesting();

            AppearanceService.Apply(0.1, 0.0);
            Color textStart = ((SolidColorBrush)Application.Current!.Resources["TextBrush"]).Color;
            Color userTextStart = ((SolidColorBrush)Application.Current.Resources["UserMessageTextBrush"]).Color;
            Color surfaceStart = ((SolidColorBrush)Application.Current.Resources["SurfaceBrush"]).Color;

            AppearanceService.Apply(0.1, 0.75);
            Color textEnd = ((SolidColorBrush)Application.Current.Resources["TextBrush"]).Color;
            Color userTextEnd = ((SolidColorBrush)Application.Current.Resources["UserMessageTextBrush"]).Color;
            Color surfaceEnd = ((SolidColorBrush)Application.Current.Resources["SurfaceBrush"]).Color;

            Assert.Equal(textStart, textEnd);
            Assert.Equal(userTextStart, userTextEnd);
            Assert.NotEqual(surfaceStart, surfaceEnd);
        });
    }

    [Fact]
    public void Apply_BooleanOverloadAndSetFontSize_ClampValues()
    {
        RunOnStaThread(() =>
        {
            AppearanceService.ResetForTesting();
            AppearanceService.Apply(1.0, 1.2);
            AppearanceService.SetFontSize(100.0);
            Assert.False(AppearanceService.UsesDarkPalette);
            Assert.Equal(1.0, AppearanceService.Brightness);
            Assert.Equal(1.0, AppearanceService.TintPosition);
            Assert.Equal(24.0, AppearanceService.FontSize);
        });
    }

    [Fact]
    public void ApplyAccent_UpdatesAccentState()
    {
        RunOnStaThread(() =>
        {
            EnsureApplication();
            AppearanceService.ResetForTesting();
            AppearanceService.Apply(0.15, 0.25);

            Color textStart = ((SolidColorBrush)Application.Current!.Resources["TextBrush"]).Color;
            Color accentSurfaceStart = ((SolidColorBrush)Application.Current.Resources["AccentSurfaceBrush"]).Color;

            int raised = 0;
            AppearanceService.AppearanceChanged += Handler;
            try
            {
                AppearanceService.ApplyAccent(-1.0, 2.0);
            }
            finally
            {
                AppearanceService.AppearanceChanged -= Handler;
            }
            Assert.Equal(0.0, AppearanceService.AccentBrightness);
            Assert.Equal(1.0, AppearanceService.AccentTintPosition);
            Assert.Equal(textStart, ((SolidColorBrush)Application.Current.Resources["TextBrush"]).Color);
            Assert.NotEqual(accentSurfaceStart, ((SolidColorBrush)Application.Current.Resources["AccentSurfaceBrush"]).Color);
            Assert.True(ContrastRatio(
                ((SolidColorBrush)Application.Current.Resources["AccentTextBrush"]).Color,
                ((SolidColorBrush)Application.Current.Resources["AccentSurfaceBrush"]).Color) >= 4.5);
            Assert.True(ContrastRatio(
                ((SolidColorBrush)Application.Current.Resources["SidebarTextBrush"]).Color,
                ((SolidColorBrush)Application.Current.Resources["AccentSurface2Brush"]).Color) >= 4.5);
            Assert.True(ContrastRatio(
                ((SolidColorBrush)Application.Current.Resources["SidebarTextBrush"]).Color,
                ((SolidColorBrush)Application.Current.Resources["AccentSurfaceBrush"]).Color) >= 4.5);
            Assert.True(ContrastRatio(
                ((SolidColorBrush)Application.Current.Resources["AccentTextBrush"]).Color,
                ((SolidColorBrush)Application.Current.Resources["AccentSurface2Brush"]).Color) >= 4.5);
            Assert.True(raised >= 1);
            void Handler()
            {
                raised++;
            }
        });
    }

    private static double ContrastRatio(Color foreground, Color background)
    {
        static double Channel(byte v)
        {
            double x = v / 255.0;
            return x <= 0.03928 ? x / 12.92 : Math.Pow((x + 0.055) / 1.055, 2.4);
        }

        static double RelativeLuminance(Color c)
            => 0.2126 * Channel(c.R) + 0.7152 * Channel(c.G) + 0.0722 * Channel(c.B);

        double l1 = RelativeLuminance(foreground);
        double l2 = RelativeLuminance(background);
        if (l1 < l2)
            (l1, l2) = (l2, l1);
        return (l1 + 0.05) / (l2 + 0.05);
    }
}
