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
        InstallButton.Focus();

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

    private System.Windows.Controls.Button[] _buttons => [LaterButton, ReleaseNotesButton, InstallButton];

    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                e.Handled = true;
                LaterButton_Click(sender, e);
                break;

            case Key.Enter:
                e.Handled = true;
                if (Keyboard.FocusedElement is System.Windows.Controls.Button focused)
                    focused.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
                else
                    InstallButton.Focus();
                break;

            case Key.Left:
                e.Handled = true;
                CycleFocus(-1);
                break;

            case Key.Right:
                e.Handled = true;
                CycleFocus(1);
                break;
        }
    }

    private void CycleFocus(int direction)
    {
        var current = Keyboard.FocusedElement as System.Windows.Controls.Button;
        var index = current != null ? Array.IndexOf(_buttons, current) : -1;
        if (index < 0) index = _buttons.Length - 1;
        var next = (index + direction + _buttons.Length) % _buttons.Length;
        _buttons[next].Focus();
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
