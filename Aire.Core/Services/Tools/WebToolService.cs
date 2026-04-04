using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using static Aire.Services.Tools.ToolHelpers;

namespace Aire.Services.Tools
{
    /// <summary>
    /// Handles open_url and http_request tools.
    /// </summary>
    public class WebToolService
    {
        private readonly WebFetchService _webFetchService;
        private static readonly HttpClient _httpClient = new();

        public WebToolService(WebFetchService webFetchService)
        {
            _webFetchService = webFetchService ?? throw new ArgumentNullException(nameof(webFetchService));
        }

        public async Task<ToolExecutionResult> ExecuteOpenUrlAsync(ToolCallRequest request)
        {
            var url = GetString(request, "url");
            if (string.IsNullOrWhiteSpace(url))
                return new ToolExecutionResult { TextResult = "Error: url parameter is required." };

            int maxChars    = 12_000;
            var maxCharsStr = GetString(request, "max_chars");
            if (!string.IsNullOrEmpty(maxCharsStr) && int.TryParse(maxCharsStr, out int parsed))
                maxChars = Math.Clamp(parsed, 500, 50_000);

            try
            {
                var result = await _webFetchService.FetchAsync(url, maxChars);
                return new ToolExecutionResult { TextResult = result.ToToolResponseString() };
            }
            catch
            {
            return new ToolExecutionResult { TextResult = "Error fetching URL." };
            }
        }

        public async Task<ToolExecutionResult> ExecuteHttpRequestAsync(ToolCallRequest request)
        {
            var url    = GetString(request, "url");
            var method = GetString(request, "method").ToUpperInvariant();
            var body   = GetString(request, "body");
            var hdrs   = GetString(request, "headers");

            if (string.IsNullOrWhiteSpace(url))
                return new ToolExecutionResult { TextResult = "Error: url is required." };

            if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                url = "https://" + url;

            if (string.IsNullOrEmpty(method)) method = "GET";

            try
            {
                var httpMethod = new HttpMethod(method);
                var req        = new HttpRequestMessage(httpMethod, url);

                if (!string.IsNullOrEmpty(hdrs))
                {
                    try
                    {
                        var headerMap = JsonSerializer.Deserialize<Dictionary<string, string>>(hdrs);
                        if (headerMap != null)
                            foreach (var (k, v) in headerMap)
                                req.Headers.TryAddWithoutValidation(k, v);
                    }
                    catch { /* ignore malformed headers */ }
                }

                if (!string.IsNullOrEmpty(body))
                    req.Content = new StringContent(body, Encoding.UTF8, "application/json");

                var response     = await _httpClient.SendAsync(req);
                var responseBody = await response.Content.ReadAsStringAsync();

                const int maxBody = 20_000;
                if (responseBody.Length > maxBody)
                    responseBody = responseBody[..maxBody] + "\n[...truncated...]";

                return new ToolExecutionResult
                {
                    TextResult = $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}\n\n{responseBody}"
                };
            }
            catch
            {
            return new ToolExecutionResult { TextResult = "Web operation failed." };
            }
        }
    }
}
