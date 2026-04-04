using System;
using System.Windows;
using System.Windows.Threading;
using Aire.Services;

using WebViewWin = Aire.UI.WebViewWindow;

namespace Aire.UI;

/// <summary>
/// Owns the startup-time settings window lifecycle and window-state snapshotting.
/// Keeps the App shell from handling settings/browser restoration directly.
/// </summary>
public sealed class StartupWindowCoordinator
{
    private SettingsWindow? _settingsWindow;

    /// <summary>
    /// Gets the currently open settings window, if any.
    /// </summary>
    public SettingsWindow? CurrentSettingsWindow => _settingsWindow;

    /// <summary>
    /// Shows the single settings window instance or re-activates it if already open.
    /// </summary>
    public void ShowSettingsWindow(Window? owner, global::Aire.MainWindow? mainWindow)
    {
        if (_settingsWindow != null)
        {
            _settingsWindow.Activate();
            return;
        }

        _settingsWindow = new SettingsWindow { Owner = owner };
        _settingsWindow.ProvidersChanged += async () =>
        {
            if (mainWindow != null)
            {
                await mainWindow.RefreshProvidersAsync();
            }
        };
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
    }

    /// <summary>
    /// Shows the main window after splash/startup has completed.
    /// </summary>
    public void ShowInitialMainWindow(global::Aire.MainWindow? mainWindow, TrayIconService? trayService, Dispatcher dispatcher)
    {
        if (mainWindow == null)
            return;

        mainWindow.WindowState = WindowState.Normal;

        if (!mainWindow.IsVisible)
            mainWindow.Show();

        if (trayService?.IsAttachedToTray == true)
        {
            trayService.ShowMainWindow();
        }
        else
        {
            mainWindow.Activate();
            mainWindow.Focus();
        }

        dispatcher.BeginInvoke(() =>
        {
            if (mainWindow == null)
                return;

            mainWindow.WindowState = WindowState.Normal;
            if (trayService?.IsAttachedToTray == true)
            {
                trayService.ShowMainWindow();
            }
            else
            {
                mainWindow.Activate();
                mainWindow.Topmost = true;
                mainWindow.Topmost = false;
                mainWindow.Focus();
            }
        }, DispatcherPriority.ApplicationIdle);
    }

    /// <summary>
    /// Restores windows that were left open during the previous session.
    /// </summary>
    public void RestoreWindowsFromState(Window? owner, global::Aire.MainWindow? mainWindow)
    {
        try
        {
            if (global::Aire.Services.AppState.GetSettingsOpen())
            {
                ShowSettingsWindow(owner, mainWindow);
            }

            if (global::Aire.Services.AppState.GetBrowserOpen())
            {
                new WebViewWin().Show();
            }
        }
        catch
        {
            // Never crash startup because window state restoration failed.
        }
    }

    /// <summary>
    /// Persists the currently open auxiliary windows before shutdown.
    /// </summary>
    public void SnapshotOpenWindows()
    {
        global::Aire.Services.AppState.SetBrowserOpen(WebViewWin.Current != null);
        global::Aire.Services.AppState.SetSettingsOpen(_settingsWindow != null);
    }
}
