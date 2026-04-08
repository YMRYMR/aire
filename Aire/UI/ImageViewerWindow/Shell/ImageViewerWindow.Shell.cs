using System;
using System.Windows;
using System.Windows.Interop;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;

namespace Aire.UI;

public partial class ImageViewerWindow
{
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        int dark = 1;
        DwmSetWindowAttribute(hwnd, 20, ref dark, sizeof(int));
        HwndSource.FromHwnd(hwnd).AddHook(WndProc);

        // ContentRendered is the first point when all child ActualWidth/Height values are stable.
        ContentRendered += OnContentRendered;

        ViewportBorder.Focus();
    }

    private void OnContentRendered(object? sender, EventArgs e)
    {
        ContentRendered -= OnContentRendered;
        FitToWindow();
        _hasInitialFit = true;
    }

    /// <summary>Lets WPF handle resize on all four edges despite WindowStyle=None.</summary>
    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int WM_NCHITTEST = 0x0084;
        const int HTTOP = 12;
        const int ResizePixels = 6;

        if (msg == WM_NCHITTEST)
        {
            int screenY = unchecked((short)(lParam.ToInt32() >> 16));
            GetWindowRect(hwnd, out var rect);
            if (screenY >= rect.Top && screenY < rect.Top + ResizePixels)
            {
                handled = true;
                return new IntPtr(HTTOP);
            }
        }

        return IntPtr.Zero;
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        => DragMove();

    private void CloseButton_Click(object sender, RoutedEventArgs e)
        => Close();
}
