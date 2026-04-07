using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using Aire.Services;

namespace Aire.UI;

public partial class WebViewWindow
{
    private void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        ApplyLocalization();

        var (savedTabs, activeIdx) = LoadSavedTabUrls();

        if (savedTabs.Length > 0)
        {
            foreach (var (url, title) in savedTabs)
                AddTab(url, title);

            if (activeIdx >= 0 && activeIdx < _tabs.Count)
                BrowserTabs.SelectedItem = _tabs[activeIdx].TabItem;
        }
        else
        {
            AddTab("about:blank");
        }

        if (_queuedNavigate is not null)
            _ = NavigateActiveTabAsync(_queuedNavigate);
        else if (_queuedNewTab is not null)
            AddTab(_queuedNewTab);

        SizeChanged += (_, _) => SaveWindowState();
        LocationChanged += (_, _) => SaveWindowState();
    }

    private void LoadWindowState()
    {
        try
        {
            if (!File.Exists(StatePath)) return;
            var json = File.ReadAllText(StatePath);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var workArea = SystemParameters.WorkArea;

            if (root.TryGetProperty("width", out var w) && w.GetDouble() >= MinWidth)
                Width = Math.Min(w.GetDouble(), workArea.Width);
            if (root.TryGetProperty("height", out var h) && h.GetDouble() >= MinHeight)
                Height = Math.Min(h.GetDouble(), workArea.Height);

            if (root.TryGetProperty("left", out var l) &&
                l.ValueKind != JsonValueKind.Null &&
                l.TryGetDouble(out var lv) && lv >= workArea.Left)
            {
                WindowStartupLocation = WindowStartupLocation.Manual;
                Left = Math.Min(lv, workArea.Right - Width);
            }
            if (root.TryGetProperty("top", out var t) &&
                t.ValueKind != JsonValueKind.Null &&
                t.TryGetDouble(out var tv) && tv >= workArea.Top)
            {
                WindowStartupLocation = WindowStartupLocation.Manual;
                Top = Math.Min(tv, workArea.Bottom - Height);
            }
        }
        catch
        {
            AppLogger.Warn("WebViewWindow.LoadWindowState", "Failed to restore window position from state file");
        }
    }

    private ((string url, string title)[] tabs, int activeIdx) LoadSavedTabUrls()
    {
        try
        {
            if (!File.Exists(StatePath)) return ([], -1);
            var json = File.ReadAllText(StatePath);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            (string url, string title)[] tabs = [];
            int activeIdx = 0;

            if (root.TryGetProperty("tabs", out var tabsEl) &&
                tabsEl.ValueKind == JsonValueKind.Array)
            {
                var list = new System.Collections.Generic.List<(string url, string title)>();
                foreach (var el in tabsEl.EnumerateArray())
                {
                    string url = string.Empty, title = string.Empty;
                    if (el.ValueKind == JsonValueKind.String)
                    {
                        url = el.GetString() ?? string.Empty;
                    }
                    else if (el.ValueKind == JsonValueKind.Object)
                    {
                        if (el.TryGetProperty("url", out var u)) url = u.GetString() ?? string.Empty;
                        if (el.TryGetProperty("title", out var t)) title = t.GetString() ?? string.Empty;
                    }
                    if (!string.IsNullOrEmpty(url)) list.Add((url, title));
                }
                tabs = [.. list];
            }
            if (root.TryGetProperty("activeTabIndex", out var ai) &&
                ai.TryGetInt32(out var aiVal))
            {
                activeIdx = aiVal;
            }

            return (tabs, activeIdx);
        }
        catch
        {
            AppLogger.Warn("WebViewWindow.LoadSavedTabUrls", "Failed to restore saved tabs from state file");
        }
        return ([], -1);
    }

    private void SaveWindowState()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(StatePath)!);
            var activeIdx = ActiveTab is not null ? _tabs.IndexOf(ActiveTab) : 0;
            var json = JsonSerializer.Serialize(new
            {
                left = !double.IsNaN(Left) ? Left : (double?)null,
                top = !double.IsNaN(Top) ? Top : (double?)null,
                width = Width,
                height = Height,
                tabs = _tabs.Where(t => !t.IsBlank)
                    .Select(t => new { url = t.Url, title = t.Title })
                    .ToArray(),
                activeTabIndex = Math.Max(0, activeIdx),
            });
            File.WriteAllText(StatePath, json);
        }
        catch
        {
            AppLogger.Warn("WebViewWindow.SaveWindowState", "Failed to persist window state");
        }
    }
}
