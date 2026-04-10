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

    [Theory]
    [InlineData(0.06, 0.12)]
    [InlineData(0.93, 0.63)]
    public void Apply_ProducesReadableThemeContrasts(double brightness, double tintPosition)
    {
        RunOnStaThread(() =>
        {
            EnsureApplication();
            AppearanceService.ResetForTesting();

            AppearanceService.Apply(brightness, tintPosition);

            var resources = Application.Current!.Resources;
            Color background = ((SolidColorBrush)resources["BackgroundBrush"]).Color;
            Color sidebarBackground = ((SolidColorBrush)resources["AccentSurface2Brush"]).Color;
            Color text = ((SolidColorBrush)resources["TextBrush"]).Color;
            Color secondaryText = ((SolidColorBrush)resources["TextSecondaryBrush"]).Color;
            Color sidebarText = ((SolidColorBrush)resources["SidebarTextBrush"]).Color;
            Color sidebarSecondary = ((SolidColorBrush)resources["SidebarTextSecondaryBrush"]).Color;
            Color statusText = ((SolidColorBrush)resources["StatusTextBrush"]).Color;
            Color userBubble = ((SolidColorBrush)resources["UserMessageBrush"]).Color;
            Color assistantBubble = ((SolidColorBrush)resources["AssistantMessageBrush"]).Color;
            Color userBubbleText = ((SolidColorBrush)resources["UserMessageTextBrush"]).Color;
            Color assistantBubbleText = ((SolidColorBrush)resources["AssistantMessageTextBrush"]).Color;

            Assert.True(ContrastRatio(text, background) >= 7.0);
            Assert.True(ContrastRatio(secondaryText, background) >= 4.5);
            Assert.True(ContrastRatio(sidebarText, sidebarBackground) >= 7.0);
            Assert.True(ContrastRatio(sidebarSecondary, sidebarBackground) >= 4.5);
            Assert.True(ContrastRatio(statusText, background) >= 4.5);
            Assert.True(ContrastRatio(userBubbleText, userBubble) >= 4.5);
            Assert.True(ContrastRatio(assistantBubbleText, assistantBubble) >= 4.5);
        });
    }

    private static double ContrastRatio(Color a, Color b)
    {
        static double Channel(byte v)
        {
            double x = v / 255.0;
            return x <= 0.03928 ? x / 12.92 : Math.Pow((x + 0.055) / 1.055, 2.4);
        }

        double l1 = 0.2126 * Channel(a.R) + 0.7152 * Channel(a.G) + 0.0722 * Channel(a.B);
        double l2 = 0.2126 * Channel(b.R) + 0.7152 * Channel(b.G) + 0.0722 * Channel(b.B);
        if (l1 < l2)
        {
            (l1, l2) = (l2, l1);
        }

        return (l1 + 0.05) / (l2 + 0.05);
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
            AppearanceService.ResetForTesting();
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
            Assert.True(raised >= 1);
            void Handler()
            {
                raised++;
            }
        });
    }
}
