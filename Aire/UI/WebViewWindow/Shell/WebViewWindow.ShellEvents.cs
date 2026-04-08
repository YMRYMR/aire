using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Aire.Services;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;

namespace Aire.UI;

public partial class WebViewWindow
{
    private void BackButton_Click(object sender, RoutedEventArgs e)
        => ActiveTab?.WebView.CoreWebView2?.GoBack();

    private void ForwardButton_Click(object sender, RoutedEventArgs e)
        => ActiveTab?.WebView.CoreWebView2?.GoForward();

    private void ReloadButton_Click(object sender, RoutedEventArgs e)
        => ActiveTab?.WebView.CoreWebView2?.Reload();

    private void NewTabButton_Click(object sender, RoutedEventArgs e)
        => AddTab();

    private void CloseAllTabsButton_Click(object sender, RoutedEventArgs e)
    {
        foreach (var tab in _tabs.ToList())
        {
            _tabs.Remove(tab);
            BrowserTabs.Items.Remove(tab.TabItem);
        }

        AddTab("about:blank");
    }

    private void OpenExternalButton_Click(object sender, RoutedEventArgs e)
    {
        string? url = ActiveTab?.Url ?? UrlBar.Text;
        if (string.IsNullOrWhiteSpace(url) || url == "about:blank")
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch
        {
            // Ignore external browser launch failures.
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
        => Close();

    private void UrlBar_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        _ = NavigateActiveTabAsync(UrlBar.Text.Trim());
        ActiveTab?.WebView.Focus();
    }

    private void UrlBar_GotFocus(object sender, RoutedEventArgs e)
        => UrlBar.SelectAll();

    private void BrowserTabs_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        => SyncToolbarToActiveTab();

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }
        else
        {
            DragMove();
        }
    }

    private void Window_Closed(object sender, EventArgs e)
    {
        LocalizationService.LanguageChanged -= OnLanguageChanged;
        if (Current == this)
        {
            Current = null;
        }

        SaveWindowState();
        if (!Aire.Services.AppState.IsShuttingDown)
        {
            Aire.Services.AppState.SetBrowserOpen(false);
        }
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F5)
        {
            ActiveTab?.WebView.CoreWebView2?.Reload();
            e.Handled = true;
        }
        else if (e.Key == Key.T && Keyboard.Modifiers == ModifierKeys.Control)
        {
            AddTab();
            UrlBar.Focus();
            e.Handled = true;
        }
        else if (e.Key == Key.W && Keyboard.Modifiers == ModifierKeys.Control)
        {
            if (ActiveTab is { } tab)
            {
                CloseTab(tab);
            }

            e.Handled = true;
        }
        else if (e.Key == Key.L && Keyboard.Modifiers == ModifierKeys.Control)
        {
            UrlBar.Focus();
            UrlBar.SelectAll();
            e.Handled = true;
        }
    }
}
