using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using Aire.Services;
using Clipboard = System.Windows.Clipboard;
using Key = System.Windows.Input.Key;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using Keyboard = System.Windows.Input.Keyboard;
using ModifierKeys = System.Windows.Input.ModifierKeys;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace Aire.UI;

public partial class ImageViewerWindow
{
    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                Close();
                break;
            case Key.F:
                FitToWindow();
                break;
            case Key.Add:
            case Key.OemPlus:
                ZoomAt(ViewportCenter, ZoomStep);
                break;
            case Key.Subtract:
            case Key.OemMinus:
                ZoomAt(ViewportCenter, 1.0 / ZoomStep);
                break;
            case Key.C when (Keyboard.Modifiers & ModifierKeys.Control) != 0:
                CopyToClipboard();
                e.Handled = true;
                break;
            case Key.S when (Keyboard.Modifiers & ModifierKeys.Control) != 0:
                SaveImage();
                e.Handled = true;
                break;
        }
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
        => CopyToClipboard();

    private void SaveButton_Click(object sender, RoutedEventArgs e)
        => SaveImage();

    private void CopyToClipboard()
    {
        try
        {
            Clipboard.SetImage(_bitmap);
        }
        catch
        {
            ConfirmationDialog.ShowAlert(this, "Error", "Copy failed.");
        }
    }

    private void SaveImage()
    {
        var dialog = new SaveFileDialog
        {
            Title = "Save Image",
            Filter = "PNG image|*.png|JPEG image|*.jpg;*.jpeg|BMP image|*.bmp",
            DefaultExt = ".png",
            FileName = "image",
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            BitmapEncoder encoder = Path.GetExtension(dialog.FileName).ToLowerInvariant() switch
            {
                ".jpg" or ".jpeg" => new JpegBitmapEncoder { QualityLevel = 95 },
                ".bmp" => new BmpBitmapEncoder(),
                _ => new PngBitmapEncoder(),
            };

            encoder.Frames.Add(BitmapFrame.Create(_bitmap));
            using var stream = File.Create(dialog.FileName);
            encoder.Save(stream);
        }
        catch
        {
            ConfirmationDialog.ShowAlert(this, "Error", "Save failed.");
        }
    }
}
