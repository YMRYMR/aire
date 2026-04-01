using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Web.WebView2.Wpf;
using WpfButton = System.Windows.Controls.Button;
using WpfHAlign = System.Windows.HorizontalAlignment;
using WpfOrientation = System.Windows.Controls.Orientation;
using WpfVAlign = System.Windows.VerticalAlignment;

namespace Aire.UI;

public partial class WebViewWindow
{
    private static string DeriveTitle(string url, string savedTitle = "")
    {
        if (!string.IsNullOrEmpty(savedTitle)) return savedTitle;
        if (url == "about:blank") return "New Tab";
        try { return new Uri(url).Host; }
        catch { return url; }
    }

    private void AddTab(string url = "about:blank", string savedTitle = "")
    {
        url = NormalizeUrl(url);

        if (url != "about:blank")
        {
            var blankTab = _tabs.FirstOrDefault(t => t.IsBlank);
            if (blankTab is not null)
            {
                blankTab.IsBlank = false;
                blankTab.Url = url;
                blankTab.Title = DeriveTitle(url);
                blankTab.TitleBlock.Text = blankTab.Title;
                BrowserTabs.SelectedItem = blankTab.TabItem;
                if (blankTab.WebView.CoreWebView2 is { } cw)
                    cw.Navigate(url);
                return;
            }
        }

        var webView = new WebView2 { HorizontalAlignment = WpfHAlign.Stretch, VerticalAlignment = WpfVAlign.Stretch };
        var titleBlock = new TextBlock
        {
            Text = DeriveTitle(url, savedTitle),
            MaxWidth = 140,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var closeBtn = new WpfButton
        {
            Content = "×",
            FontSize = 11,
            Width = 18,
            Height = 18,
            Padding = new Thickness(0),
            Margin = new Thickness(6, 0, 0, 0),
            BorderThickness = new Thickness(0),
            Background = System.Windows.Media.Brushes.Transparent,
            Cursor = System.Windows.Input.Cursors.Hand,
            VerticalAlignment = WpfVAlign.Center,
        };

        var header = new StackPanel { Orientation = WpfOrientation.Horizontal };
        header.Children.Add(titleBlock);
        header.Children.Add(closeBtn);

        var tabItem = new TabItem { Header = header, Content = webView };
        var tab = new Tab
        {
            WebView = webView,
            TabItem = tabItem,
            TitleBlock = titleBlock,
            Url = url,
            Title = DeriveTitle(url, savedTitle),
            IsBlank = url == "about:blank",
        };

        closeBtn.Click += (_, _) => CloseTab(tab);
        _tabs.Add(tab);
        BrowserTabs.Items.Add(tabItem);
        BrowserTabs.SelectedItem = tabItem;
        SaveWindowState();
        _ = InitTabAsync(tab, url);
    }

    private async Task InitTabAsync(Tab tab, string url)
    {
        try
        {
            await tab.WebView.EnsureCoreWebView2Async();
            tab.WebView.CoreWebView2.NavigationCompleted += (_, _) => Dispatcher.Invoke(() => OnTabNavigated(tab));
            tab.WebView.CoreWebView2.Navigate(tab.Url);
        }
        catch (Exception ex)
        {
            tab.TitleBlock.Text = "Error";
            tab.Url = url;
            if (BrowserTabs.SelectedItem == tab.TabItem)
                UrlBar.Text = $"Error: {ex.Message}";
        }
    }

    private void OnTabNavigated(Tab tab)
    {
        var cw = tab.WebView.CoreWebView2;
        tab.Url = cw.Source;
        tab.IsBlank = false;
        tab.Title = string.IsNullOrEmpty(cw.DocumentTitle) ? tab.Url : cw.DocumentTitle;
        tab.TitleBlock.Text = tab.Title;

        if (BrowserTabs.SelectedItem == tab.TabItem)
            SyncToolbarToActiveTab();

        SaveWindowState();
    }

    private void CloseTab(Tab tab)
    {
        if (_tabs.Count == 1)
        {
            Close();
            return;
        }

        var idx = _tabs.IndexOf(tab);
        _tabs.Remove(tab);
        BrowserTabs.Items.Remove(tab.TabItem);
        if (idx >= _tabs.Count) idx = _tabs.Count - 1;
        BrowserTabs.SelectedItem = _tabs[idx].TabItem;
        SaveWindowState();
    }

    private Tab? ActiveTab => _tabs.FirstOrDefault(t => t.TabItem == BrowserTabs.SelectedItem);

    private void SyncToolbarToActiveTab()
    {
        var tab = ActiveTab;
        if (tab is null) return;
        var cw = tab.WebView.CoreWebView2;
        if (cw is null) return;

        UrlBar.Text = tab.Url;
        BackButton.IsEnabled = cw.CanGoBack;
        ForwardButton.IsEnabled = cw.CanGoForward;
        Title = $"{tab.Title} — Aire";
    }

    private static string NormalizeUrl(string url)
    {
        if (url.StartsWith("http", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("file:", StringComparison.OrdinalIgnoreCase) ||
            url == "about:blank")
            return url;

        if (url.Length > 2 && url[1] == ':')
        {
            try { return new Uri(System.IO.Path.GetFullPath(url)).AbsoluteUri; }
            catch { /* not a valid absolute path — fall through to https:// prefix below */ }
        }

        return "https://" + url;
    }

    private async Task NavigateActiveTabAsync(string url)
    {
        url = NormalizeUrl(url);
        var tab = ActiveTab;
        if (tab is null) { AddTab(url); return; }
        await tab.WebView.EnsureCoreWebView2Async();
        tab.WebView.CoreWebView2.Navigate(url);
    }
}
