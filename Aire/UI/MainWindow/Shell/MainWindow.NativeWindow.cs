using System;
using System.Windows;
using System.Windows.Interop;

namespace Aire
{
    public partial class MainWindow
    {
        private const int DwmUseImmersiveDarkMode = 20;

        private void InitializeNativeWindow()
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            HwndSource.FromHwnd(hwnd).AddHook(WndProc);

            int dark = 1;
            DwmSetWindowAttribute(hwnd, DwmUseImmersiveDarkMode, ref dark, sizeof(int));
        }
    }
}
