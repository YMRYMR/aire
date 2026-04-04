using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using Aire.Services;

namespace Aire.UI;

public partial class UpdateAvailableDialog : Window
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private readonly GitHubReleaseUpdateInfo _update;

    public bool InstallRequested { get; private set; }

    public UpdateAvailableDialog(GitHubReleaseUpdateInfo update)
    {
        _update = update ?? throw new ArgumentNullException(nameof(update));
        InitializeComponent();
        FontSize = AppearanceService.FontSize;
        AppearanceService.AppearanceChanged += OnThemeChanged;
        Closed += (_, _) => AppearanceService.AppearanceChanged -= OnThemeChanged;
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        TitleText.Text = $"Aire {_update.LatestVersion} is available";
        VersionText.Text = string.IsNullOrWhiteSpace(_update.ReleaseName)
            ? $"Current version: {_update.CurrentVersion}"
            : $"{_update.ReleaseName} • Current version: {_update.CurrentVersion}";
        NotesText.Text = string.IsNullOrWhiteSpace(_update.ReleaseNotes)
            ? "Install the latest update to get the newest fixes and improvements."
            : _update.ReleaseNotes;

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

    private void OnThemeChanged() => Dispatcher.Invoke(() => FontSize = AppearanceService.FontSize);

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

    public static bool? ShowDialog(Window? owner, GitHubReleaseUpdateInfo update)
    {
        var dialog = new UpdateAvailableDialog(update)
        {
            Owner = owner,
            Topmost = owner?.Topmost ?? true,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };

        return dialog.ShowDialog();
    }
}
