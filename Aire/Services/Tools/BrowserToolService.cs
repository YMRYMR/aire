using System.Text.Json;
using System.Threading.Tasks;
using static Aire.Services.Tools.ToolHelpers;

namespace Aire.Services.Tools
{
    /// <summary>
    /// Handles all browser tab operations: open, list, read, switch, close, get HTML,
    /// execute script, and get cookies.
    /// </summary>
    internal class BrowserToolService
    {
        /// <summary>
        /// Opens a URL or local file path in Aire's in-app browser window.
        /// </summary>
        /// <param name="request">Tool request containing the target <c>url</c>.</param>
        /// <returns>A success or validation result for the open-tab request.</returns>
        public ToolExecutionResult ExecuteOpenBrowserTab(ToolCallRequest request)
        {
            var url = GetString(request, "url");
            if (string.IsNullOrWhiteSpace(url))
                return new ToolExecutionResult { TextResult = "Error: url parameter is required." };

            // Normalize local file paths to a proper file:/// URI so WebView2 doesn't
            // prepend https:// and produce a broken address like https://file///C:/...
            if (!url.Contains("://", StringComparison.Ordinal))
            {
                try { url = new Uri(System.IO.Path.GetFullPath(url)).AbsoluteUri; }
                catch { /* leave url as-is if path is unparseable */ }
            }

            System.Windows.Application.Current.Dispatcher.Invoke(
                () => Aire.UI.WebViewWindow.OpenInNewTab(url));

            return new ToolExecutionResult { TextResult = $"SUCCESS: Opened {url} in the Aire browser window." };
        }

        /// <summary>
        /// Lists the tabs currently open in the shared browser window.
        /// </summary>
        public ToolExecutionResult ExecuteListBrowserTabs()
        {
            var browser = System.Windows.Application.Current.Dispatcher.Invoke(
                () => Aire.UI.WebViewWindow.Current);

            if (browser is null)
                return new ToolExecutionResult
                {
                    TextResult = "The browser window is not open. " +
                                 "The user can open it by clicking the 🌐 button in the Aire toolbar."
                };

            var list = System.Windows.Application.Current.Dispatcher.Invoke(
                () => browser.ListTabs());

            return new ToolExecutionResult { TextResult = list };
        }

        /// <summary>
        /// Reads the rendered text content of the selected browser tab.
        /// </summary>
        /// <param name="request">Tool request that may include a tab index.</param>
        /// <returns>Text extracted from the requested browser tab.</returns>
        public async Task<ToolExecutionResult> ExecuteReadBrowserTabAsync(ToolCallRequest request)
        {
            var browser = System.Windows.Application.Current.Dispatcher.Invoke(
                () => Aire.UI.WebViewWindow.Current);

            if (browser is null)
                return new ToolExecutionResult
                {
                    TextResult = "The browser window is not open. " +
                                 "The user can open it by clicking the 🌐 button in the Aire toolbar."
                };

            int index = GetIndexParam(request);

            var text = await System.Windows.Application.Current.Dispatcher.InvokeAsync(
                () => browser.ReadTabContentAsync(index));
            return new ToolExecutionResult { TextResult = await text };
        }

        /// <summary>
        /// Switches the active tab in the shared browser window.
        /// </summary>
        public ToolExecutionResult ExecuteSwitchBrowserTab(ToolCallRequest request)
        {
            var browser = System.Windows.Application.Current.Dispatcher.Invoke(
                () => Aire.UI.WebViewWindow.Current);
            if (browser is null)
                return new ToolExecutionResult { TextResult = "The browser window is not open." };

            int index  = GetInt(request, "index");
            var result = System.Windows.Application.Current.Dispatcher.Invoke(
                () => browser.SwitchTab(index));
            return new ToolExecutionResult { TextResult = result };
        }

        /// <summary>
        /// Closes the selected tab in the shared browser window.
        /// </summary>
        public ToolExecutionResult ExecuteCloseBrowserTab(ToolCallRequest request)
        {
            var browser = System.Windows.Application.Current.Dispatcher.Invoke(
                () => Aire.UI.WebViewWindow.Current);
            if (browser is null)
                return new ToolExecutionResult { TextResult = "The browser window is not open." };

            int index = GetIndexParam(request);

            var result = System.Windows.Application.Current.Dispatcher.Invoke(
                () => browser.CloseTabByIndex(index));
            return new ToolExecutionResult { TextResult = result };
        }

        /// <summary>
        /// Retrieves the raw HTML for the selected browser tab.
        /// </summary>
        public async Task<ToolExecutionResult> ExecuteGetBrowserHtmlAsync(ToolCallRequest request)
        {
            var browser = System.Windows.Application.Current.Dispatcher.Invoke(
                () => Aire.UI.WebViewWindow.Current);
            if (browser is null)
                return new ToolExecutionResult { TextResult = "The browser window is not open." };

            int index = GetIndexParam(request);

            var text = await System.Windows.Application.Current.Dispatcher.InvokeAsync(
                () => browser.GetTabHtmlAsync(index));
            return new ToolExecutionResult { TextResult = await text };
        }

        /// <summary>
        /// Runs JavaScript in the selected browser tab and returns the script result.
        /// </summary>
        /// <param name="request">Tool request containing the script text and optional tab index.</param>
        /// <returns>Result returned by the browser execution environment.</returns>
        public async Task<ToolExecutionResult> ExecuteBrowserScriptAsync(ToolCallRequest request)
        {
            var browser = System.Windows.Application.Current.Dispatcher.Invoke(
                () => Aire.UI.WebViewWindow.Current);
            if (browser is null)
                return new ToolExecutionResult { TextResult = "The browser window is not open." };

            var script = GetString(request, "script");
            if (string.IsNullOrWhiteSpace(script))
                return new ToolExecutionResult { TextResult = "Error: script parameter is required." };

            int index = GetIndexParam(request);

            var task = await System.Windows.Application.Current.Dispatcher.InvokeAsync(
                () => browser.ExecuteScriptInTabAsync(index, script));
            return new ToolExecutionResult { TextResult = await task };
        }

        /// <summary>
        /// Returns cookies visible to the selected browser tab.
        /// </summary>
        public async Task<ToolExecutionResult> ExecuteGetBrowserCookiesAsync(ToolCallRequest request)
        {
            var browser = System.Windows.Application.Current.Dispatcher.Invoke(
                () => Aire.UI.WebViewWindow.Current);
            if (browser is null)
                return new ToolExecutionResult { TextResult = "The browser window is not open." };

            int index = GetIndexParam(request);

            var task = await System.Windows.Application.Current.Dispatcher.InvokeAsync(
                () => browser.GetTabCookiesAsync(index));
            return new ToolExecutionResult { TextResult = await task };
        }
    }
}
