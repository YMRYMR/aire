using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using Color = System.Windows.Media.Color;
using Application = System.Windows.Application;

namespace Aire.Services
{
    /// <summary>
    /// Manages the application's appearance state, including brightness, tint,
    /// accent styling, shared brushes, and font size.
    /// Modifies SolidColorBrush.Color in-place so every {StaticResource} binding
    /// in the UI repaints automatically without replacing resource objects.
    /// </summary>
    public static class AppearanceService
    {
        // â”€â”€ Current state â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        public static bool   UsesDarkPalette { get; private set; } = false;
        public static double Brightness   { get; private set; } = 0.9311106847612285; // default first-run palette
        public static double TintPosition { get; private set; } = 0.6277344324385377;
        public static double AccentBrightness   { get; private set; } = 0.6255924170616112;
        public static double AccentTintPosition { get; private set; } = 0.5817535545023695;
        public static double FontSize     { get; private set; } = 15.0;

        /// <summary>Raised on the UI thread after appearance or font-size updates complete.</summary>
        public static event Action? AppearanceChanged;

        [Obsolete("Use UsesDarkPalette instead.")]
        public static bool IsDark => UsesDarkPalette;

        [Obsolete("Use AppearanceChanged instead.")]
        public static event Action? ThemeChanged
        {
            add => AppearanceChanged += value;
            remove => AppearanceChanged -= value;
        }

        // â”€â”€ Message brushes (used directly by MainWindow chat rendering) â”€â”€â”€â”€â”€â”€
        // These are NOT in Application.Current.Resources â€” MainWindow references
        // them by name.  Apply() updates their Color so existing messages repaint.

        public static SolidColorBrush UserBgBrush { get; private set; } = new();
        public static SolidColorBrush UserFgBrush { get; private set; } = new();
        public static SolidColorBrush AiBgBrush { get; private set; } = new();
        public static SolidColorBrush AiFgBrush { get; private set; } = new();
        public static SolidColorBrush SystemBgBrush { get; private set; } = new();
        public static SolidColorBrush SystemFgBrush { get; private set; } = new();
        public static SolidColorBrush ErrorBgBrush { get; private set; } = new();
        public static SolidColorBrush ErrorFgBrush { get; private set; } = new();

        internal static void ResetForTesting()
        {
            UserBgBrush = new();
            UserFgBrush = new();
            AiBgBrush = new();
            AiFgBrush = new();
            SystemBgBrush = new();
            SystemFgBrush = new();
            ErrorBgBrush = new();
            ErrorFgBrush = new();
        }

        // â”€â”€ Palettes â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        // Each entry: (resourceKey, darkBaseRGB, lightBaseRGB)

        private static readonly (string Key, Color Dark, Color Light)[] ResourceSlots =
        [
            ("BackgroundBrush",           C(0x19,0x19,0x19), C(0xF2,0xF2,0xF2)),
            ("SurfaceBrush",              C(0x20,0x20,0x20), C(0xE8,0xE8,0xE8)),
            ("Surface2Brush",             C(0x28,0x28,0x28), C(0xDE,0xDE,0xDE)),
            ("Surface3Brush",             C(0x32,0x32,0x32), C(0xD0,0xD0,0xD0)),
            ("AccentSurfaceBrush",        C(0x1D,0x24,0x30), C(0xD9,0xE3,0xEE)),
            ("AccentSurface2Brush",       C(0x24,0x2D,0x3A), C(0xCF,0xDB,0xE8)),
            ("AccentBorderBrush",         C(0x34,0x41,0x52), C(0xB8,0xC7,0xD8)),
            ("PrimaryBrush",              C(0x4D,0x4D,0x4D), C(0x9E,0x9E,0x9E)),
            ("PrimaryHoverBrush",         C(0x5C,0x5C,0x5C), C(0xB2,0xB2,0xB2)),
            ("PrimaryPressedBrush",       C(0x3D,0x3D,0x3D), C(0x8A,0x8A,0x8A)),
            ("SecondaryBrush",            C(0x28,0x28,0x28), C(0xDE,0xDE,0xDE)),
            ("TextBrush",                 C(0xD2,0xD2,0xD2), C(0x28,0x28,0x28)),
            ("TextSecondaryBrush",        C(0x70,0x70,0x70), C(0x68,0x68,0x68)),
            ("BorderBrush",               C(0x3A,0x3A,0x3A), C(0xC0,0xC0,0xC0)),
            ("UserMessageBrush",          C(0x32,0x32,0x32), C(0xD0,0xD0,0xD0)),
            ("UserMessageTextBrush",      C(0xE2,0xE2,0xE2), C(0x28,0x28,0x28)),
            ("AssistantMessageBrush",     C(0x24,0x24,0x24), C(0xE6,0xE6,0xE6)),
            ("AssistantMessageTextBrush", C(0xCA,0xCA,0xCA), C(0x28,0x28,0x28)),
            ("WarningBrush",              C(0xE8,0xA0,0x20), C(0x7A,0x50,0x00)),
            ("WarningBackgroundBrush",    C(0x30,0x22,0x00), C(0xFF,0xF0,0xC8)),
            ("WarningBorderBrush",        C(0x70,0x50,0x00), C(0xC8,0x90,0x00)),
            // Status text (between bubbles): must always contrast against the background
            ("StatusTextBrush",           C(0xC0,0xC0,0xC0), C(0x30,0x30,0x30)),
            // Links: muted sky-blue on dark â†’ deep blue on light
            ("LinkBrush",                 C(0x6B,0x9F,0xD4), C(0x1A,0x5C,0xB8)),
            // Inline code: warm tan on dark â†’ burnt sienna on light
            ("CodeForegroundBrush",       C(0xE0,0xA8,0x72), C(0x7A,0x38,0x10)),
            // Inline code background: very dark on dark â†’ light warm gray on light
            ("CodeBackgroundBrush",       C(0x22,0x1D,0x19), C(0xD8,0xD2,0xCC)),
        ];

        private static readonly (Color Dark, Color Light)[] MsgSlots =
        [
            /* UserBg   */ (C(0x32,0x32,0x32), C(0xD0,0xD0,0xD0)),
            /* UserFg   */ (C(0xE2,0xE2,0xE2), C(0x28,0x28,0x28)),
            /* AiBg     */ (C(0x24,0x24,0x24), C(0xE6,0xE6,0xE6)),
            /* AiFg     */ (C(0xCA,0xCA,0xCA), C(0x28,0x28,0x28)),
            /* SystemBg */ (C(0x28,0x28,0x28), C(0xDE,0xDE,0xDE)),
            /* SystemFg */ (C(0xB8,0xB8,0xB8), C(0x38,0x38,0x38)),
            /* ErrorBg  */ (C(0x38,0x20,0x20), C(0xF5,0xD5,0xD5)),
            /* ErrorFg  */ (C(0xAA,0x66,0x66), C(0x88,0x22,0x22)),
        ];

        // â”€â”€ Public API â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        /// <summary>
        /// Reads appearance settings from the saved window-state JSON and calls
        /// <see cref="Apply"/> + <see cref="ApplyAccent"/> so brushes are correct
        /// before any window is shown.  Safe to call before <see cref="MainWindow"/>
        /// is constructed; falls back to defaults when no save file exists.
        /// </summary>
        public static void ApplySaved()
        {
            try
            {
                var path = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Aire", "windowstate.json");

                if (!System.IO.File.Exists(path))
                {
                    Apply(Brightness, TintPosition);
                    ApplyAccent(AccentBrightness, AccentTintPosition);
                    return;
                }

                var json = System.IO.File.ReadAllText(path);
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                var root = doc.RootElement;

                double brightness       = Brightness;
                double tintPosition     = TintPosition;
                double accentBrightness = AccentBrightness;
                double accentTintPos    = AccentTintPosition;

                if (root.TryGetProperty("brightness", out var b) && b.GetDouble() is >= 0 and <= 1)
                    brightness = b.GetDouble();
                else if (root.TryGetProperty("usesDarkPalette", out var udp))
                    brightness = udp.GetBoolean() ? 0.0 : 1.0;
                else if (root.TryGetProperty("isDark", out var d))
                    brightness = d.GetBoolean() ? 0.0 : 1.0;

                if (root.TryGetProperty("tintPosition", out var t) && t.GetDouble() is >= 0 and <= 1)
                    tintPosition = t.GetDouble();
                if (root.TryGetProperty("accentBrightness", out var ab) && ab.GetDouble() is >= 0 and <= 1)
                    accentBrightness = ab.GetDouble();
                if (root.TryGetProperty("accentTintPosition", out var at) && at.GetDouble() is >= 0 and <= 1)
                    accentTintPos = at.GetDouble();

                Apply(brightness, tintPosition);
                ApplyAccent(accentBrightness, accentTintPos);
            }
            catch
            {
                // If anything fails, apply defaults so the UI is never unpainted.
                Apply(Brightness, TintPosition);
                ApplyAccent(AccentBrightness, AccentTintPosition);
            }
        }

        public static void SetFontSize(double size)
        {
            FontSize = Math.Clamp(size, 8, 24);
            AppearanceChanged?.Invoke();
        }

        /// <summary>
        /// Applies the chosen theme and tint to all shared resource brushes.
        /// brightness 0-1: 0 = full dark, 1 = full light; intermediate values smoothly interpolate.
        /// tintPosition 0-1: 0/1 = neutral (no extra tint), 0.5 = maximum tint.
        /// Hue sweeps 0Â°â†’360Â° as position goes 0â†’1.
        /// </summary>
        public static void Apply(double brightness, double tintPosition)
        {
            Brightness   = Math.Clamp(brightness, 0, 1);
            UsesDarkPalette = Brightness < 0.5;
            TintPosition = Math.Clamp(tintPosition, 0, 1);

            double tintHue      = TintPosition * 360.0;
            double tintStrength = Math.Sin(TintPosition * Math.PI); // 0 at ends, 1 at 0.5

            // Resource brushes â€” replace the object so {DynamicResource} bindings update
            if (Application.Current?.Resources is ResourceDictionary res)
            {
                foreach (var (key, dark, light) in ResourceSlots)
                {
                    if (key.StartsWith("Accent", StringComparison.Ordinal))
                        continue;
                    var base_ = LerpColor(dark, light, Brightness);
                    res[key] = new SolidColorBrush(Tinted(base_, tintHue, tintStrength));
                }

                ApplyAccentResources(res);
            }

            // Message brushes
            var msg = MsgSlots;
            SetBrush(UserBgBrush,   Tinted(LerpColor(msg[0].Dark, msg[0].Light, Brightness), tintHue, tintStrength));
            SetBrush(UserFgBrush,   Tinted(LerpColor(msg[1].Dark, msg[1].Light, Brightness), tintHue, tintStrength));
            SetBrush(AiBgBrush,     Tinted(LerpColor(msg[2].Dark, msg[2].Light, Brightness), tintHue, tintStrength));
            SetBrush(AiFgBrush,     Tinted(LerpColor(msg[3].Dark, msg[3].Light, Brightness), tintHue, tintStrength));
            SetBrush(SystemBgBrush, Tinted(LerpColor(msg[4].Dark, msg[4].Light, Brightness), tintHue, tintStrength));
            SetBrush(SystemFgBrush, LerpColor(msg[5].Dark, msg[5].Light, Brightness));   // no tint â€” must stay readable against any background
            // Error brushes: preserve their red identity; tint background only slightly
            SetBrush(ErrorBgBrush,  Tinted(LerpColor(msg[6].Dark, msg[6].Light, Brightness), tintHue, tintStrength * 0.25));
            SetBrush(ErrorFgBrush,  LerpColor(msg[7].Dark, msg[7].Light, Brightness));   // no tint â€” always red-ish

            AppearanceChanged?.Invoke();
        }

        // Legacy convenience overload for callers that still think in dark/light presets.
        [Obsolete("Use Apply(double brightness, double tintPosition) instead.")]
        public static void Apply(bool isDark, double tintPosition) =>
            Apply(isDark ? 0.0 : 1.0, tintPosition);

        public static void ApplyAccent(double brightness, double tintPosition)
        {
            AccentBrightness = Math.Clamp(brightness, 0, 1);
            AccentTintPosition = Math.Clamp(tintPosition, 0, 1);

            if (Application.Current?.Resources is ResourceDictionary res)
                ApplyAccentResources(res);

            AppearanceChanged?.Invoke();
        }

        // â”€â”€ Colour math â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        /// <summary>
        /// Shifts the base colour's hue toward tintHue while preserving luminance.
        /// strength 0 = original colour, 1 = fully tinted.
        /// </summary>
        internal static Color Tinted(Color base_, double tintHue, double strength)
        {
            if (strength < 0.002) return base_;

            var (h, s, l) = ToHsl(base_);

            // Achromatic bases have an undefined hue (ToHsl returns 0Â°).
            // Using LerpHue from 0Â° to e.g. 240Â° takes the short arc backward
            // (0Â° â†’ -120Â° â†’ 240Â°), landing at ~256Â° (purple) instead of blue.
            // Fix: jump straight to tintHue for near-gray colors.
            double newH = s < 0.01 ? tintHue : LerpHue(h, tintHue, strength);
            // Saturation: nudge up so the tint is actually visible
            double targetS = Math.Min(1.0, s + 0.22);
            double newS    = s + (targetS - s) * strength;

            return FromHsl(newH, newS, l); // L unchanged
        }

        private static Color LerpColor(Color a, Color b, double t) => Color.FromRgb(
            (byte)Math.Round(a.R + (b.R - a.R) * t),
            (byte)Math.Round(a.G + (b.G - a.G) * t),
            (byte)Math.Round(a.B + (b.B - a.B) * t));

        private static void SetBrush(SolidColorBrush b, Color c)
        {
            if (!b.IsFrozen) b.Color = c;
        }

        private static void ApplyAccentResources(ResourceDictionary res)
        {
            double tintHue = AccentTintPosition * 360.0;
            double tintStrength = Math.Sin(AccentTintPosition * Math.PI);

            foreach (var (key, dark, light) in ResourceSlots)
            {
                if (!key.StartsWith("Accent", StringComparison.Ordinal))
                    continue;

                var base_ = LerpColor(dark, light, AccentBrightness);
                res[key] = new SolidColorBrush(Tinted(base_, tintHue, tintStrength));
            }
        }

        private static Color C(byte r, byte g, byte b) => Color.FromRgb(r, g, b);

        // â”€â”€ HSL conversions â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        internal static (double h, double s, double l) ToHsl(Color c)
        {
            double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
            double max = Math.Max(r, Math.Max(g, b));
            double min = Math.Min(r, Math.Min(g, b));
            double l   = (max + min) / 2.0;

            if (max == min) return (0, 0, l);

            double d = max - min;
            double s = l > 0.5 ? d / (2 - max - min) : d / (max + min);

            double h;
            if      (max == r) h = (g - b) / d + (g < b ? 6 : 0);
            else if (max == g) h = (b - r) / d + 2;
            else               h = (r - g) / d + 4;
            h *= 60;

            return (h, s, l);
        }

        internal static Color FromHsl(double h, double s, double l)
        {
            if (s < 0.001)
            {
                byte v = ClampByte(l);
                return Color.FromRgb(v, v, v);
            }
            double q = l < 0.5 ? l * (1 + s) : l + s - l * s;
            double p = 2 * l - q;
            return Color.FromRgb(
                ClampByte(HueChannel(p, q, h / 360 + 1.0 / 3)),
                ClampByte(HueChannel(p, q, h / 360)),
                ClampByte(HueChannel(p, q, h / 360 - 1.0 / 3)));
        }

        internal static double HueChannel(double p, double q, double t)
        {
            if (t < 0) t += 1;
            if (t > 1) t -= 1;
            if (t < 1.0 / 6) return p + (q - p) * 6 * t;
            if (t < 0.5)      return q;
            if (t < 2.0 / 3)  return p + (q - p) * (2.0 / 3 - t) * 6;
            return p;
        }

        internal static double LerpHue(double h0, double h1, double t)
        {
            double diff = h1 - h0;
            if (diff >  180) diff -= 360;
            if (diff < -180) diff += 360;
            double r = h0 + diff * t;
            return r < 0 ? r + 360 : r >= 360 ? r - 360 : r;
        }

        private static byte ClampByte(double v) =>
            (byte)Math.Round(Math.Clamp(v, 0, 1) * 255);
    }
}
