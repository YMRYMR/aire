using System;
using System.Windows;
using System.Windows.Controls;
using WpfColor     = System.Windows.Media.Color;
using WpfBrush     = System.Windows.Media.SolidColorBrush;
using WpfRect      = System.Windows.Shapes.Rectangle;
using WpfEllipse   = System.Windows.Shapes.Ellipse;
using WpfLine      = System.Windows.Shapes.Line;
using WpfPolygon   = System.Windows.Shapes.Polygon;
using WpfPoint     = System.Windows.Point;
using WpfPointColl = System.Windows.Media.PointCollection;

namespace Aire.UI
{
    /// <summary>
    /// Draws simplified flag graphics using WPF shapes.
    /// WPF on Windows cannot render Unicode regional-indicator flag emoji,
    /// so this class provides actual drawn flag representations instead.
    /// </summary>
    internal static class FlagPainter
    {
        /// <summary>
        /// Returns a WPF <see cref="UIElement"/> representing the flag for the given language code.
        /// Default size: 24 × 16 px (3:2 ratio).
        /// </summary>
        public static UIElement Create(string langCode, double w = 24, double h = 16)
        {
            var canvas = new Canvas { Width = w, Height = h };
            Draw(canvas, langCode, w, h);
            return new Border
            {
                Width           = w,
                Height          = h,
                BorderBrush     = new WpfBrush(WpfColor.FromArgb(0x55, 0, 0, 0)),
                BorderThickness = new Thickness(0.5),
                Child           = canvas,
                ClipToBounds    = true,
            };
        }

        private static void Draw(Canvas c, string code, double w, double h)
        {
            switch (code.ToLowerInvariant())
            {
                case "en": DrawGB(c, w, h); break;
                case "es": DrawES(c, w, h); break;
                case "fr": DrawFR(c, w, h); break;
                case "de": DrawDE(c, w, h); break;
                case "da": DrawDK(c, w, h); break;
                case "pt": DrawPT(c, w, h); break;
                case "it": DrawIT(c, w, h); break;
                case "ja": DrawJA(c, w, h); break;
                case "zh": DrawZH(c, w, h); break;
                case "ko": DrawKO(c, w, h); break;
                case "uk": DrawUA(c, w, h); break;
                case "ar": DrawAR(c, w, h); break;
                case "hi": DrawHI(c, w, h); break;
                default:   R(c, 0, 0, w, h, "#888888"); break;
            }
        }

        // ── Drawing helpers ───────────────────────────────────────────────────

        private static void R(Canvas c, double x, double y, double rw, double rh, string hex)
        {
            var r = new WpfRect { Width = rw, Height = rh, Fill = B(hex) };
            Canvas.SetLeft(r, x);
            Canvas.SetTop(r, y);
            c.Children.Add(r);
        }

        private static void L(Canvas c, double x1, double y1, double x2, double y2, string hex, double thick)
        {
            c.Children.Add(new WpfLine
            {
                X1 = x1, Y1 = y1, X2 = x2, Y2 = y2,
                Stroke = B(hex), StrokeThickness = thick,
            });
        }

        private static void E(Canvas c, double cx, double cy, double rx, double ry, string hex)
        {
            var e = new WpfEllipse { Width = rx * 2, Height = ry * 2, Fill = B(hex) };
            Canvas.SetLeft(e, cx - rx);
            Canvas.SetTop(e, cy - ry);
            c.Children.Add(e);
        }

        private static void Star(Canvas c, double cx, double cy, double r, string hex)
        {
            var pts = new WpfPointColl();
            for (int i = 0; i < 10; i++)
            {
                double a   = Math.PI * i / 5 - Math.PI / 2;
                double rad = (i % 2 == 0) ? r : r * 0.4;
                pts.Add(new WpfPoint(cx + rad * Math.Cos(a), cy + rad * Math.Sin(a)));
            }
            c.Children.Add(new WpfPolygon { Points = pts, Fill = B(hex) });
        }

        private static WpfBrush B(string hex)
        {
            hex = hex.TrimStart('#');
            if (hex.Length == 6) hex = "FF" + hex;
            return new WpfBrush(WpfColor.FromArgb(
                Convert.ToByte(hex[..2], 16),
                Convert.ToByte(hex.Substring(2, 2), 16),
                Convert.ToByte(hex.Substring(4, 2), 16),
                Convert.ToByte(hex.Substring(6, 2), 16)));
        }

        // ── Flag drawings ─────────────────────────────────────────────────────

        // 🇬🇧 United Kingdom (for "en")
        private static void DrawGB(Canvas c, double w, double h)
        {
            R(c, 0, 0, w, h, "#012169");           // blue background
            L(c, 0, 0, w, h, "#FFFFFF", h * 0.34); // white diagonal \
            L(c, w, 0, 0, h, "#FFFFFF", h * 0.34); // white diagonal /
            L(c, 0, 0, w, h, "#C8102E", h * 0.16); // red diagonal \
            L(c, w, 0, 0, h, "#C8102E", h * 0.16); // red diagonal /
            R(c, 0, h * 0.35, w, h * 0.30, "#FFFFFF"); // white horizontal bar
            R(c, w * 0.38, 0, w * 0.24, h, "#FFFFFF"); // white vertical bar
            R(c, 0, h * 0.40, w, h * 0.20, "#C8102E"); // red horizontal bar
            R(c, w * 0.42, 0, w * 0.16, h, "#C8102E"); // red vertical bar
        }

        // 🇪🇸 Spain
        private static void DrawES(Canvas c, double w, double h)
        {
            R(c, 0, 0,       w, h / 3,         "#AA151B"); // red
            R(c, 0, h / 3,   w, h / 3,         "#F1BF00"); // yellow
            R(c, 0, h * 2/3, w, h / 3 + 0.5,   "#AA151B"); // red
        }

        // 🇫🇷 France
        private static void DrawFR(Canvas c, double w, double h)
        {
            R(c, 0,       0, w / 3,       h, "#002395"); // blue
            R(c, w / 3,   0, w / 3,       h, "#FFFFFF"); // white
            R(c, w * 2/3, 0, w / 3 + 0.5, h, "#ED2939"); // red
        }

        // 🇩🇪 Germany
        private static void DrawDE(Canvas c, double w, double h)
        {
            R(c, 0, 0,       w, h / 3,         "#000000"); // black
            R(c, 0, h / 3,   w, h / 3,         "#DD0000"); // red
            R(c, 0, h * 2/3, w, h / 3 + 0.5,   "#FFCE00"); // gold
        }

        // 🇩🇰 Denmark
        private static void DrawDK(Canvas c, double w, double h)
        {
            R(c, 0, 0, w, h, "#C60C30");                        // red background
            R(c, 0, h * 0.38, w, h * 0.24, "#FFFFFF");         // white horizontal bar
            R(c, w * 0.29, 0, w * 0.16, h, "#FFFFFF");         // white vertical bar (offset left)
        }

        // 🇧🇷 Brazil / Portugal
        private static void DrawPT(Canvas c, double w, double h)
        {
            R(c, 0,       0, w * 0.4, h, "#009C3B"); // green
            R(c, w * 0.4, 0, w * 0.6, h, "#EF3340"); // red
        }

        // 🇮🇹 Italy
        private static void DrawIT(Canvas c, double w, double h)
        {
            R(c, 0,       0, w / 3,       h, "#009246"); // green
            R(c, w / 3,   0, w / 3,       h, "#FFFFFF"); // white
            R(c, w * 2/3, 0, w / 3 + 0.5, h, "#CE2B37"); // red
        }

        // 🇯🇵 Japan
        private static void DrawJA(Canvas c, double w, double h)
        {
            R(c, 0, 0, w, h, "#FFFFFF");
            E(c, w / 2, h / 2, h * 0.28, h * 0.28, "#BC002D");
        }

        // 🇨🇳 China
        private static void DrawZH(Canvas c, double w, double h)
        {
            R(c, 0, 0, w, h, "#DE2910");
            Star(c, w * 0.22, h * 0.30, h * 0.20, "#FFDE00");
        }

        // 🇰🇷 South Korea
        private static void DrawKO(Canvas c, double w, double h)
        {
            R(c, 0, 0, w, h, "#FFFFFF");
            double r = h * 0.26, cx = w / 2, cy = h / 2;

            var red = new WpfEllipse
            {
                Width = r * 2, Height = r * 2, Fill = B("#CD2E3A"),
                Clip = new System.Windows.Media.RectangleGeometry(new Rect(0, 0, r * 2, r))
            };
            Canvas.SetLeft(red, cx - r); Canvas.SetTop(red, cy - r);
            c.Children.Add(red);

            var blue = new WpfEllipse
            {
                Width = r * 2, Height = r * 2, Fill = B("#003478"),
                Clip = new System.Windows.Media.RectangleGeometry(new Rect(0, r, r * 2, r))
            };
            Canvas.SetLeft(blue, cx - r); Canvas.SetTop(blue, cy - r);
            c.Children.Add(blue);
        }

        // 🇺🇦 Ukraine
        private static void DrawUA(Canvas c, double w, double h)
        {
            R(c, 0, 0,   w, h / 2,         "#005BBB"); // blue
            R(c, 0, h/2, w, h / 2 + 0.5,   "#FFD500"); // yellow
        }

        // 🇸🇦 Saudi Arabia (Arabic)
        private static void DrawAR(Canvas c, double w, double h)
        {
            R(c, 0, 0, w, h, "#006C35");                              // green
            R(c, w * 0.1, h * 0.43, w * 0.8, h * 0.14, "#FFFFFF");  // white band
        }

        // 🇮🇳 India
        private static void DrawHI(Canvas c, double w, double h)
        {
            R(c, 0, 0,       w, h / 3,         "#FF9933"); // saffron
            R(c, 0, h / 3,   w, h / 3,         "#FFFFFF"); // white
            R(c, 0, h * 2/3, w, h / 3 + 0.5,   "#138808"); // green
            E(c, w / 2, h / 2, h * 0.08, h * 0.08, "#000080"); // Ashoka chakra (dot)
        }
    }
}
