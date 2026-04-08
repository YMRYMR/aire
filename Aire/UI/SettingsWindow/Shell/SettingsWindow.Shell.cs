using System;
using System.Windows;
using System.Windows.Input;

namespace Aire.UI
{
    public partial class SettingsWindow
    {
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
            {
                DragMove();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
    }
}
