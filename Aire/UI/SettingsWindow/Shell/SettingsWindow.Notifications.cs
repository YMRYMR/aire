using System;
using System.Threading.Tasks;
using System.Windows;

namespace Aire.UI
{
    public partial class SettingsWindow
    {
        private async void ShowToast(string message, bool isError = false)
        {
            _toastCts?.Cancel();
            _toastCts = new System.Threading.CancellationTokenSource();
            var cts = _toastCts;

            ToastText.Text = message;
            ToastText.ToolTip = message.Length > 120 ? message : null;

            if (isError)
            {
                ToastBorder.SetResourceReference(System.Windows.Controls.Border.BackgroundProperty, "Surface2Brush");
                ToastBorder.SetResourceReference(System.Windows.Controls.Border.BorderBrushProperty, "ErrorBrush");
                ToastBorder.BorderThickness = new Thickness(3, 1, 1, 1);
            }
            else
            {
                ToastBorder.SetResourceReference(System.Windows.Controls.Border.BackgroundProperty, "SurfaceBrush");
                ToastBorder.SetResourceReference(System.Windows.Controls.Border.BorderBrushProperty, "BorderBrush");
                ToastBorder.BorderThickness = new Thickness(1);
            }

            ToastBorder.Visibility = Visibility.Visible;

            try
            {
                var delayMs = isError
                    ? (message.Length > 120 ? 12000 : 8000)
                    : 4000;
                await Task.Delay(delayMs, cts.Token);
                ToastBorder.Visibility = Visibility.Collapsed;
            }
            catch (OperationCanceledException)
            {
            }
        }

        private void ToastDismiss_Click(object sender, RoutedEventArgs e)
        {
            _toastCts?.Cancel();
            ToastBorder.Visibility = Visibility.Collapsed;
        }
    }
}
