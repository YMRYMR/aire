using System;
using System.Windows.Input;
using Aire.Services;

namespace Aire
{
    public partial class MainWindow
    {
        private void MainWindow_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.Modifiers != ModifierKeys.Control)
                return;

            e.Handled = true;
            AdjustFontSize(e.Delta > 0 ? 1 : -1);
        }

        private void AdjustFontSize(double delta)
        {
            var newSize = Math.Clamp(Aire.Services.AppearanceService.FontSize + delta, 8, 24);
            if (Math.Abs(newSize - Aire.Services.AppearanceService.FontSize) < 0.1)
                return;

            Aire.Services.AppearanceService.SetFontSize(newSize);
            SaveWindowSize();
        }
    }
}

