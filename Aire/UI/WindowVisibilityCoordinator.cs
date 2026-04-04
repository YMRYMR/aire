using WebViewWin = Aire.UI.WebViewWindow;

namespace Aire.UI;

/// <summary>
/// Tracks the auxiliary windows that should hide with the main shell and restores them when the shell returns.
/// Keeps the visibility bookkeeping out of App.xaml.cs.
/// </summary>
public sealed class WindowVisibilityCoordinator
{
    private bool _settingsWasVisible;
    private bool _browserWasVisible;

    /// <summary>
    /// Applies the current main-window visibility transition to the satellite windows.
    /// </summary>
    public void HandleMainWindowVisibilityChanged(bool nowVisible, SettingsWindow? settingsWindow, WebViewWin? browserWindow)
    {
        if (nowVisible)
        {
            if (_settingsWasVisible)
            {
                settingsWindow?.Show();
            }

            if (_browserWasVisible)
            {
                browserWindow?.Show();
            }

            return;
        }

        _settingsWasVisible = settingsWindow?.IsVisible == true;
        _browserWasVisible = browserWindow?.IsVisible == true;
        settingsWindow?.Hide();
        browserWindow?.Hide();
    }
}
