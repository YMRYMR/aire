using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using Aire.Services;
using System.Windows.Input;
using System.Windows.Interop;

namespace Aire.UI
{
    public partial class HelpWindow
    {
        internal void LoadWindowState()
        {
            try
            {
                if (!File.Exists(StatePath)) return;
                var json = File.ReadAllText(StatePath);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                var workArea = SystemParameters.WorkArea;

                if (root.TryGetProperty("width", out var w) && w.GetDouble() >= MinWidth)
                    Width = Math.Min(w.GetDouble(), workArea.Width);
                if (root.TryGetProperty("height", out var h) && h.GetDouble() >= MinHeight)
                    Height = Math.Min(h.GetDouble(), workArea.Height);

                if (root.TryGetProperty("left", out var l) &&
                    l.ValueKind != JsonValueKind.Null &&
                    l.TryGetDouble(out var lv) && lv >= workArea.Left)
                {
                    WindowStartupLocation = WindowStartupLocation.Manual;
                    Left = Math.Min(lv, workArea.Right - Width);
                }
                if (root.TryGetProperty("top", out var t) &&
                    t.ValueKind != JsonValueKind.Null &&
                    t.TryGetDouble(out var tv) && tv >= workArea.Top)
                {
                    WindowStartupLocation = WindowStartupLocation.Manual;
                    Top = Math.Min(tv, workArea.Bottom - Height);
                }
            }
            catch
            {
                AppLogger.Warn("HelpWindow.LoadWindowState", "Failed to restore window state");
            }
        }

        internal void SaveWindowState()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(StatePath)!);
                var json = JsonSerializer.Serialize(new
                {
                    left = !double.IsNaN(Left) ? Left : (double?)null,
                    top = !double.IsNaN(Top) ? Top : (double?)null,
                    width = Width,
                    height = Height,
                });
                File.WriteAllText(StatePath, json);
            }
            catch
            {
                AppLogger.Warn("HelpWindow.SaveWindowState", "Failed to persist window state");
            }
        }

        private void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            FontSize = AppearanceService.FontSize;
            AppearanceService.AppearanceChanged += OnThemeChanged;
            LocalizationService.LanguageChanged += OnLanguageChanged;
            Closed += (_, _) =>
            {
                SaveWindowState();
                AppearanceService.AppearanceChanged -= OnThemeChanged;
                LocalizationService.LanguageChanged -= OnLanguageChanged;
            };

            var hwnd = new WindowInteropHelper(this).Handle;
            int dark = 1;
            DwmSetWindowAttribute(hwnd, 20, ref dark, sizeof(int));
            HwndSource.FromHwnd(hwnd).AddHook(WndProc);

            ApplyLocalization();
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_NCHITTEST = 0x0084;
            const int HTTOP = 12;
            const int resizePx = 6;

            if (msg == WM_NCHITTEST && ResizeMode == ResizeMode.CanResize)
            {
                int screenY = unchecked((short)(lParam.ToInt32() >> 16));
                GetWindowRect(hwnd, out var r);
                if (screenY >= r.Top && screenY < r.Top + resizePx)
                {
                    handled = true;
                    return new IntPtr(HTTOP);
                }
            }
            return IntPtr.Zero;
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
    }
}
