using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace Aire
{
    public partial class MainWindow
    {
        private static readonly HashSet<string> ImageExts = new(StringComparer.OrdinalIgnoreCase)
            { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".webp", ".tiff" };

        private static readonly HashSet<string> TextExts = new(StringComparer.OrdinalIgnoreCase)
            { ".txt", ".md", ".cs", ".js", ".ts", ".jsx", ".tsx", ".py", ".json", ".xml",
              ".html", ".htm", ".css", ".sql", ".yaml", ".yml", ".sh", ".ps1", ".bat", ".cmd",
              ".cpp", ".c", ".h", ".hpp", ".java", ".go", ".rs", ".rb", ".php", ".swift",
              ".kt", ".r", ".pl", ".lua", ".toml", ".ini", ".cfg", ".config", ".log", ".csv",
              ".tsv", ".gitignore", ".env", ".dockerfile", ".makefile" };

        private static string GetScreenshotsFolder()
        {
            var folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Aire", "Screenshots");
            Directory.CreateDirectory(folder);
            return folder;
        }

        private void InputTextBox_Drop(object sender, System.Windows.DragEventArgs e)
        {
            if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(System.Windows.DataFormats.FileDrop);
                if (files?.Length > 0)
                {
                    AttachFileOrImage(files[0]);
                    e.Handled = true;
                }
            }
        }

        private void AttachFileOrImage(string filePath)
        {
            if (ImageExts.Contains(Path.GetExtension(filePath)))
                AttachImage(filePath);
            else
                AttachNonImageFile(filePath);
        }

        private void AttachNonImageFile(string filePath)
        {
            var info = new FileInfo(filePath);
            _attachedFilePath  = filePath;
            _attachedFileName  = info.Name;
            _attachedImagePath = null;
            AttachedImagePreview.Source = null;

            ImageThumbnailBorder.Visibility = Visibility.Collapsed;
            FileChipBorder.Visibility       = Visibility.Visible;
            AttachedFileNameText.Text       = info.Name;
            AttachedFileSizeText.Text       = FormatFileSize(info.Length);
            LargeFileWarning.Visibility     = info.Length > 1_048_576 ? Visibility.Visible : Visibility.Collapsed;
            ImagePreviewPanel.Visibility    = Visibility.Visible;
        }

        private static string FormatFileSize(long bytes) => bytes switch
        {
            < 1024        => $"{bytes} B",
            < 1_048_576   => $"{bytes / 1024.0:F1} KB",
            _             => $"{bytes / 1_048_576.0:F1} MB"
        };

        private void FileChip_Click(object sender, MouseButtonEventArgs e)
        {
            if (_attachedFilePath != null)
                System.Diagnostics.Process.Start(
                    new System.Diagnostics.ProcessStartInfo { FileName = _attachedFilePath, UseShellExecute = true });
        }

        private void ShowImageChip()
        {
            _attachedFilePath  = null;
            _attachedFileName  = null;
            ImageThumbnailBorder.Visibility = Visibility.Visible;
            FileChipBorder.Visibility       = Visibility.Collapsed;
            LargeFileWarning.Visibility     = Visibility.Collapsed;
            ImagePreviewPanel.Visibility    = Visibility.Visible;
        }

        private void AttachImage(string filePath)
        {
            _attachedImagePath = filePath;
            try
            {
                var bitmap = new BitmapImage(new Uri(filePath));
                AttachedImagePreview.Source = bitmap;
                ShowImageChip();
            }
            catch (Exception ex)
            {
                UI.ConfirmationDialog.ShowAlert(this, "Error", $"Failed to load image: {ex.Message}");
                _attachedImagePath = null;
            }
        }

        private void AttachImageFromClipboard(BitmapSource bitmap)
        {
            try
            {
                // Encode to a temp PNG so the existing file-path pipeline works unchanged.
                var tempDir  = Path.Combine(Path.GetTempPath(), "Aire");
                Directory.CreateDirectory(tempDir);
                var tempPath = Path.Combine(tempDir, $"paste_{DateTime.Now:yyyyMMdd_HHmmss_fff}.png");

                using (var stream = new FileStream(tempPath, FileMode.Create))
                {
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    encoder.Save(stream);
                }

                _attachedImagePath          = tempPath;
                AttachedImagePreview.Source = bitmap;
                ShowImageChip();
            }
            catch (Exception ex)
            {
                UI.ConfirmationDialog.ShowAlert(this, "Error", $"Failed to paste image: {ex.Message}");
            }
        }

        private void AttachButton_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "All files (*.*)|*.*|Images (*.png;*.jpg;*.jpeg;*.bmp;*.gif)|*.png;*.jpg;*.jpeg;*.bmp;*.gif",
                Title  = "Attach a file"
            };
            if (dlg.ShowDialog() == true)
                AttachFileOrImage(dlg.FileName);
        }

        private void RemoveImageButton_Click(object sender, RoutedEventArgs e)
        {
            _attachedImagePath              = null;
            _attachedFilePath               = null;
            _attachedFileName               = null;
            AttachedImagePreview.Source     = null;
            ImagePreviewPanel.Visibility    = Visibility.Collapsed;
            ImageThumbnailBorder.Visibility = Visibility.Collapsed;
            FileChipBorder.Visibility       = Visibility.Collapsed;
            LargeFileWarning.Visibility     = Visibility.Collapsed;
        }

        private void ChatImage_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is System.Windows.Controls.Image img && img.Source is ImageSource src)
            {
                e.Handled = true;
                var viewer = new Aire.UI.ImageViewerWindow(src) { Owner = this };
                viewer.Show();
            }
        }
    }
}
