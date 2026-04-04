using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Aire.Providers;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using MessageBox = System.Windows.MessageBox;

namespace Aire.UI
{
    public partial class ClaudeAiLoginWindow : Window
    {
        private WebView2? _webView;

        public ClaudeAiLoginWindow()
        {
            InitializeComponent();
            _webView = new WebView2();
            WebViewHost.Children.Add(_webView);
            Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_webView == null)
                    throw new InvalidOperationException("WebView2 control was not initialized.");

                await _webView.EnsureCoreWebView2Async(null);
                _webView.CoreWebView2.NavigationCompleted += OnNavigationCompleted;
                _webView.CoreWebView2.Navigate("https://claude.ai/login");
            }
            catch (Exception)
            {
                ConfirmationDialog.ShowAlert(this, "WebView2 Required",
                    "The WebView2 runtime is required but not installed.\n\n" +
                    "Download it from: https://developer.microsoft.com/en-us/microsoft-edge/webview2/\n\n" +
                    "Unable to initialize the login window. Please try again or restart the application.");
                Close();
            }
        }

        private async void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (_webView == null || _webView.CoreWebView2 == null)
                return;

            var cookies = await _webView.CoreWebView2.CookieManager
                .GetCookiesAsync("https://claude.ai");

            var sessionCookie = cookies.FirstOrDefault(c => c.Name == "sessionKey");
            if (sessionCookie == null || string.IsNullOrEmpty(sessionCookie.Value))
            {
                StatusText.Text = "Waiting for login…";
                return;
            }

            StatusText.Text = "Logged in — setting up session…";

            // Copy cookies into the session's own permanent WebView2, then this
            // window (and its WebView2) can be safely disposed.
            await ClaudeAiSession.Instance.AttachAsync(_webView);

            DialogResult = true;
            Close();
        }
    }
}
