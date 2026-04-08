using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using Aire.Services;

namespace Aire.UI;

public partial class UpdateAvailableDialog
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ApplyLocalization();

        try
        {
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            if (hwnd != IntPtr.Zero)
            {
                var value = 1;
                DwmSetWindowAttribute(hwnd, 20, ref value, sizeof(int));
                DwmSetWindowAttribute(hwnd, 19, ref value, sizeof(int));
            }
        }
        catch
        {
            // Non-fatal. The dialog still works without the backdrop attribute.
        }
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }

    private void InstallButton_Click(object sender, RoutedEventArgs e)
    {
        InstallRequested = true;
        DialogResult = true;
        Close();
    }

    private void LaterButton_Click(object sender, RoutedEventArgs e)
    {
        InstallRequested = false;
        DialogResult = false;
        Close();
    }

    private void ReleaseNotesButton_Click(object sender, RoutedEventArgs e)
    {
        if (_update.ReleasePageUrl == null)
            return;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = _update.ReleasePageUrl.AbsoluteUri,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            AppLogger.Warn("App.Update", "Failed to open release page", ex);
        }
    }
}
