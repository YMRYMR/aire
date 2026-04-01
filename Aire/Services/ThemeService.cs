using System.Windows.Media;

namespace Aire.Services
{
    [System.Obsolete("Use AppearanceService instead.")]
    public static class ThemeService
    {
        [System.Obsolete("Use AppearanceService.UsesDarkPalette instead.")]
        public static bool IsDark => AppearanceService.UsesDarkPalette;
        public static bool UsesDarkPalette => AppearanceService.UsesDarkPalette;
        public static double Brightness => AppearanceService.Brightness;
        public static double TintPosition => AppearanceService.TintPosition;
        public static double AccentBrightness => AppearanceService.AccentBrightness;
        public static double AccentTintPosition => AppearanceService.AccentTintPosition;
        public static double FontSize => AppearanceService.FontSize;

        public static event System.Action? AppearanceChanged
        {
            add => AppearanceService.AppearanceChanged += value;
            remove => AppearanceService.AppearanceChanged -= value;
        }

        [System.Obsolete("Use AppearanceChanged instead.")]
        public static event System.Action? ThemeChanged
        {
            add => AppearanceService.AppearanceChanged += value;
            remove => AppearanceService.AppearanceChanged -= value;
        }

        public static SolidColorBrush UserBgBrush => AppearanceService.UserBgBrush;
        public static SolidColorBrush UserFgBrush => AppearanceService.UserFgBrush;
        public static SolidColorBrush AiBgBrush => AppearanceService.AiBgBrush;
        public static SolidColorBrush AiFgBrush => AppearanceService.AiFgBrush;
        public static SolidColorBrush SystemBgBrush => AppearanceService.SystemBgBrush;
        public static SolidColorBrush SystemFgBrush => AppearanceService.SystemFgBrush;
        public static SolidColorBrush ErrorBgBrush => AppearanceService.ErrorBgBrush;
        public static SolidColorBrush ErrorFgBrush => AppearanceService.ErrorFgBrush;

        public static void SetFontSize(double size) => AppearanceService.SetFontSize(size);
        public static void Apply(double brightness, double tintPosition) => AppearanceService.Apply(brightness, tintPosition);
        [System.Obsolete("Use Apply(double brightness, double tintPosition) instead.")]
        public static void Apply(bool isDark, double tintPosition) => AppearanceService.Apply(isDark, tintPosition);
        public static void ApplyAccent(double brightness, double tintPosition) => AppearanceService.ApplyAccent(brightness, tintPosition);
    }
}
