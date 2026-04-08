using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Aire.Services;
using Microsoft.Web.WebView2.Wpf;

namespace Aire.UI;

/// <summary>
/// Tabbed in-app browser window. The AI can inspect open tabs via list_browser_tabs
/// and read_browser_tab tools. Only one instance is kept open at a time.
/// </summary>
public partial class WebViewWindow : Window
{
    internal static readonly string StatePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Aire", "browserstate.json");

    public static WebViewWindow? Current { get; private set; }

    private sealed class Tab
    {
        public WebView2 WebView { get; init; } = null!;
        public TabItem TabItem { get; init; } = null!;
        public TextBlock TitleBlock { get; init; } = null!;
        public string Url { get; set; } = string.Empty;
        public string Title { get; set; } = "New Tab";
        public bool IsBlank { get; set; }
    }

    private readonly List<Tab> _tabs = [];
    private string? _queuedNavigate;
    private string? _queuedNewTab;

    public WebViewWindow()
    {
        InitializeComponent();
        Current = this;
        Aire.Services.AppState.SetBrowserOpen(true);
        LocalizationService.LanguageChanged += OnLanguageChanged;

        LoadWindowState();

        Loaded += OnWindowLoaded;
        PreviewKeyDown += Window_PreviewKeyDown;
    }
}
