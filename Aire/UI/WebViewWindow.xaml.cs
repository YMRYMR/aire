using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Web.WebView2.Wpf;
using WpfButton      = System.Windows.Controls.Button;
using WpfOrientation = System.Windows.Controls.Orientation;
using WpfHAlign      = System.Windows.HorizontalAlignment;
using WpfVAlign      = System.Windows.VerticalAlignment;

namespace Aire.UI;

/// <summary>
/// Tabbed in-app browser window. The AI can inspect open tabs via list_browser_tabs
/// and read_browser_tab tools. Only one instance is kept open at a time.
/// </summary>
public partial class WebViewWindow : Window
{
    // ── Persisted state path ─────────────────────────────────────────────────
    internal static readonly string StatePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Aire", "browserstate.json");

    // ── Singleton ─────────────────────────────────────────────────────────────
    /// <summary>The single open browser window, or null if closed.</summary>
    public static WebViewWindow? Current { get; private set; }

    /// <summary>
    /// Opens (or focuses) the browser window and navigates the active tab to
    /// <paramref name="url"/>. Used by the open_browser_tab AI tool.
    /// </summary>
    public static void OpenUrl(string url)
    {
        if (Current is { IsLoaded: true })
        {
            Current.Activate();
            _ = Current.NavigateActiveTabAsync(url);
        }
        else
        {
            var win = new WebViewWindow { _queuedNavigate = url };
            win.Show();
        }
    }

    /// <summary>
    /// Opens (or focuses) the browser window and opens <paramref name="url"/>
    /// in a NEW tab. Used when the user clicks a hyperlink in the chat.
    /// </summary>
    public static void OpenInNewTab(string url)
    {
        if (Current is { IsLoaded: true })
        {
            Current.Activate();
            Current.AddTab(url);
        }
        else
        {
            var win = new WebViewWindow { _queuedNewTab = url };
            win.Show();
        }
    }

    // ── Tab bookkeeping ───────────────────────────────────────────────────────
    private sealed class Tab
    {
        public WebView2   WebView    { get; init; } = null!;
        public TabItem    TabItem    { get; init; } = null!;
        public TextBlock  TitleBlock { get; init; } = null!;
        public string     Url        { get; set; }  = string.Empty;
        public string     Title      { get; set; }  = "New Tab";
        /// <summary>
        /// True only for a brand-new blank tab that has never been navigated to a
        /// real URL. Cleared immediately (synchronously) when blank-tab reuse
        /// decides to use this slot, so no timing race with NavigationCompleted.
        /// </summary>
        public bool       IsBlank    { get; set; }  = false;
    }

    private readonly List<Tab> _tabs = [];

    // URLs to open after saved tabs are restored (set before Show())
    private string? _queuedNavigate;
    private string? _queuedNewTab;

    // ── Constructor ───────────────────────────────────────────────────────────
    public WebViewWindow()
    {
        InitializeComponent();
        Current = this;
        Aire.Services.AppState.SetBrowserOpen(true);

        LoadWindowState();

        Loaded         += OnWindowLoaded;
        PreviewKeyDown += Window_PreviewKeyDown;
    }

    // ── Tab management ────────────────────────────────────────────────────────

    // ── Toolbar handlers ──────────────────────────────────────────────────────

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
        // Clear all tabs from the UI and internal list
        foreach (var tab in _tabs.ToList())
        {
            _tabs.Remove(tab);
            BrowserTabs.Items.Remove(tab.TabItem);
        }
        // Open a fresh blank tab rather than closing the window
        AddTab("about:blank");
    }

    private void OpenExternalButton_Click(object sender, RoutedEventArgs e)
    {
        var url = ActiveTab?.Url ?? UrlBar.Text;
        if (string.IsNullOrWhiteSpace(url) || url == "about:blank") return;
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch { /* ignore */ }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void UrlBar_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        _ = NavigateActiveTabAsync(UrlBar.Text.Trim());
        ActiveTab?.WebView.Focus();
    }

    private void UrlBar_GotFocus(object sender, RoutedEventArgs e)
        => UrlBar.SelectAll();

    private void BrowserTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        => SyncToolbarToActiveTab();

    // ── Window handlers ───────────────────────────────────────────────────────

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        else
            DragMove();
    }

    private void Window_Closed(object sender, EventArgs e)
    {
        if (Current == this) Current = null;
        SaveWindowState();
        // Only record "closed" when the user closes the window manually.
        // During app shutdown the state was already snapshotted by OnExitRequested.
        if (!Aire.Services.AppState.IsShuttingDown)
            Aire.Services.AppState.SetBrowserOpen(false);
    }

    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
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
            if (ActiveTab is { } t) CloseTab(t);
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
