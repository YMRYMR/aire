using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aire;
using System.Windows;
using Aire.Data;

namespace Aire.Services
{
    /// <summary>
    /// Hosts Aire's loopback-only local API and forwards validated requests onto the UI thread.
    /// </summary>
    internal sealed class LocalApiService : IDisposable
    {
        public const int Port = 51234;

        private readonly MainWindow _mainWindow;
        private CancellationTokenSource? _listenerCts;
        private Task? _listenerTask;
        private TcpListener? _listener;
        private int _consecutiveInvalidAuthCount;
        private bool _disposed;

        public LocalApiService(MainWindow mainWindow)
        {
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
        }

        public bool IsRunning => _listenerTask != null && !_listenerTask.IsCompleted;

        /// <summary>
        /// Starts the loopback listener if the service is not already running.
        /// </summary>
        public void Start()
        {
            if (_disposed || IsRunning) return;
            // Guarantee a token exists so the screenshots tool can authenticate immediately after
            // the window appears — even if a previous run wiped the persisted token.
            AppState.EnsureApiAccessToken();
            ApiTraceLog.Record("service", "start", $"Local API listener started on 127.0.0.1:{Port}", true);
            _listenerCts = new CancellationTokenSource();
            _listenerTask = Task.Run(() => ListenAsync(_listenerCts.Token));
        }

        /// <summary>
        /// Stops the listener and waits for any in-flight accept loop to exit.
        /// </summary>
        public async Task StopAsync()
        {
            if (_disposed) return;
            var cts = _listenerCts;
            var task = _listenerTask;
            _listenerCts = null;
            _listenerTask = null;

            if (cts != null)
                cts.Cancel();
            _listener?.Stop();

            if (task != null)
            {
                try { await task.ConfigureAwait(false); }
                catch (Exception ex) { AppLogger.Warn("LocalApiService.Stop", "Shutdown race while stopping local API", ex); }
            }

            cts?.Dispose();
            ApiTraceLog.Record("service", "stop", "Local API listener stopped", true);
        }

        /// <summary>
        /// Accepts loopback clients one at a time until cancellation or disposal.
        /// </summary>
        /// <param name="token">Cancellation token used to stop the listener loop.</param>
        private async Task ListenAsync(CancellationToken token)
        {
            _listener = new TcpListener(IPAddress.Loopback, Port);
            _listener.Start();

            while (!token.IsCancellationRequested)
            {
                TcpClient? client = null;
                try
                {
                    client = await _listener.AcceptTcpClientAsync(token).ConfigureAwait(false);
                    await HandleClientAsync(client, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    if (token.IsCancellationRequested)
                        break;
                    AppLogger.Warn("LocalApiService.Listen", "Unexpected listener error", ex);
                    await Task.Delay(100, token).ConfigureAwait(false);
                }
                finally
                {
                    client?.Dispose();
                }
            }
        }

        /// <summary>
        /// Reads a single line-delimited JSON request, authenticates it, dispatches it, and writes the JSON response.
        /// </summary>
        /// <param name="client">Connected loopback client.</param>
        /// <param name="token">Cancellation token used for throttling and shutdown.</param>
        internal async Task HandleClientAsync(TcpClient client, CancellationToken token)
        {
            client.NoDelay = true;
            using var stream = client.GetStream();
            using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
            using var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true)
            {
                AutoFlush = true
            };

            var line = await reader.ReadLineAsync().ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(line))
            {
                await WriteResponseAsync(writer, LocalApiResponse.Error("Empty request")).ConfigureAwait(false);
                return;
            }

            LocalApiRequest? request;
            try
            {
                request = JsonSerializer.Deserialize<LocalApiRequest>(line, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch (Exception ex)
            {
                ApiTraceLog.Record("error", "invalid_json", $"Invalid JSON payload: {ex.GetType().Name}", false);
                await WriteResponseAsync(writer, LocalApiResponse.Error("Invalid JSON payload.")).ConfigureAwait(false);
                return;
            }

            if (request == null || string.IsNullOrWhiteSpace(request.Method))
            {
                await WriteResponseAsync(writer, LocalApiResponse.Error("Missing method")).ConfigureAwait(false);
                return;
            }

            if (!AppState.GetApiAccessEnabled())
            {
                await WriteResponseAsync(writer, LocalApiResponse.Error("Local API access is disabled in Settings")).ConfigureAwait(false);
                return;
            }

            if (!IsAuthorized(request))
            {
                var delayMs = Math.Min(2000, Interlocked.Increment(ref _consecutiveInvalidAuthCount) * 250);
                if (delayMs > 0)
                    await Task.Delay(delayMs, token).ConfigureAwait(false);
                ApiTraceLog.Record("auth", request.Method, "Rejected invalid or missing auth token", false);
                await WriteResponseAsync(writer, LocalApiResponse.Error("Missing or invalid auth token")).ConfigureAwait(false);
                return;
            }
            Interlocked.Exchange(ref _consecutiveInvalidAuthCount, 0);

            ApiTraceLog.Record("request", request.Method, "Received request", true);
            var response = await DispatchAsync(request, token).ConfigureAwait(false);
            ApiTraceLog.Record(
                response.Ok ? "response" : "error",
                request.Method,
                response.Ok ? "Completed successfully" : (response.ErrorMessage ?? "Request failed"),
                response.Ok,
                response.Ok ? GetTraceDataForLogging(request.Method, response.Result) : null);
            await WriteResponseAsync(writer, response).ConfigureAwait(false);
        }

        /// <summary>
        /// Serializes the response object and writes it as one JSON line back to the caller.
        /// </summary>
        /// <param name="writer">Stream writer bound to the active client connection.</param>
        /// <param name="response">Response payload to serialize.</param>
        internal async Task WriteResponseAsync(StreamWriter writer, LocalApiResponse response)
        {
            var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            await writer.WriteLineAsync(json).ConfigureAwait(false);
        }

        /// <summary>
        /// Routes a validated API method to the corresponding MainWindow integration point.
        /// </summary>
        /// <param name="request">Parsed local API request.</param>
        /// <param name="token">Cancellation token from the listener loop.</param>
        /// <returns>A normalized local API response.</returns>
        internal async Task<LocalApiResponse> DispatchAsync(LocalApiRequest request, CancellationToken token)
        {
            try
            {
                var method = request.Method?.Trim().ToLowerInvariant() ?? string.Empty;
                return method switch
                {
                    "ping" => LocalApiResponse.OkResult(await InvokeOnUiAsync<ApiStateSnapshot>(() => _mainWindow.ApiGetStateAsync()).ConfigureAwait(false)),
                    "get_state" => LocalApiResponse.OkResult(await InvokeOnUiAsync<ApiStateSnapshot>(() => _mainWindow.ApiGetStateAsync()).ConfigureAwait(false)),
                    "show_main_window" => LocalApiResponse.OkResult(await InvokeOnUiAsync(() => _mainWindow.ShowMainWindowAsync()).ConfigureAwait(false)),
                    "hide_main_window" => LocalApiResponse.OkResult(await InvokeOnUiAsync(() => _mainWindow.HideMainWindowAsync()).ConfigureAwait(false)),
                    "open_settings" => LocalApiResponse.OkResult(await InvokeOnUiAsync(() => _mainWindow.ShowSettingsWindowAsync()).ConfigureAwait(false)),
                    "open_browser" => LocalApiResponse.OkResult(await InvokeOnUiAsync(() => _mainWindow.ShowBrowserWindowAsync()).ConfigureAwait(false)),
                    "list_providers" => LocalApiResponse.OkResult(await InvokeOnUiAsync<List<ApiProviderSnapshot>>(() => _mainWindow.ApiListProvidersAsync()).ConfigureAwait(false)),
                    "create_provider" => LocalApiResponse.OkResult(await InvokeOnUiAsync<ApiProviderSnapshot>(() => _mainWindow.ApiCreateProviderAsync(
                        GetNullableString(request.Parameters, "name"),
                        GetString(request.Parameters, "type"),
                        GetNullableString(request.Parameters, "apiKey"),
                        GetNullableString(request.Parameters, "baseUrl"),
                        GetString(request.Parameters, "model"),
                        GetBoolOrDefault(request.Parameters, "isEnabled", true),
                        GetNullableString(request.Parameters, "color"),
                        GetBoolOrDefault(request.Parameters, "selectAfterCreate", false),
                        GetNullableInt(request.Parameters, "inheritCredentialsFromProviderId"))).ConfigureAwait(false)),
                    "list_conversations" => LocalApiResponse.OkResult(await InvokeOnUiAsync<List<ConversationSummary>>(() => _mainWindow.ApiListConversationsAsync(GetString(request.Parameters, "search"))).ConfigureAwait(false)),
                    "create_conversation" => LocalApiResponse.OkResult(await InvokeOnUiAsync<int>(() => _mainWindow.ApiCreateConversationAsync(GetString(request.Parameters, "title"))).ConfigureAwait(false)),
                    "select_conversation" => LocalApiResponse.OkResult(await InvokeOnUiAsync<bool>(() => _mainWindow.ApiSelectConversationAsync(GetInt(request.Parameters, "conversationId"))).ConfigureAwait(false)),
                    "get_messages" => LocalApiResponse.OkResult(await InvokeOnUiAsync<List<Aire.Data.Message>>(() => _mainWindow.ApiGetMessagesAsync(GetInt(request.Parameters, "conversationId"))).ConfigureAwait(false)),
                    "set_provider" => LocalApiResponse.OkResult(await InvokeOnUiAsync<bool>(() => _mainWindow.ApiSetProviderAsync(GetInt(request.Parameters, "providerId"))).ConfigureAwait(false)),
                    "set_provider_model" => LocalApiResponse.OkResult(await InvokeOnUiAsync<bool>(() => _mainWindow.ApiSetProviderModelAsync(GetInt(request.Parameters, "providerId"), GetString(request.Parameters, "model"))).ConfigureAwait(false)),
                    "set_language" => LocalApiResponse.OkResult(await InvokeOnUiAsync(() => { LocalizationService.SetLanguage(GetString(request.Parameters, "languageCode")); return Task.CompletedTask; }).ConfigureAwait(false)),
                    "send_message" => LocalApiResponse.OkResult(await InvokeOnUiAsync<bool>(() => _mainWindow.ApiSendMessageAsync(GetString(request.Parameters, "text"))).ConfigureAwait(false)),
                    "list_pending_approvals" => LocalApiResponse.OkResult(await InvokeOnUiAsync<ApiPendingApproval[]>(() => _mainWindow.ApiListPendingApprovalsAsync()).ConfigureAwait(false)),
                    "wait_for_pending_approval" => LocalApiResponse.OkResult(await WaitForPendingApprovalAsync(
                        () => InvokeOnUiAsync<ApiPendingApproval?>(() => _mainWindow.ApiGetFirstPendingApprovalAsync()),
                        GetIntOrDefault(request.Parameters, "timeout_seconds", 30),
                        token).ConfigureAwait(false)),
                    "approve_tool_call" => LocalApiResponse.OkResult(await InvokeOnUiAsync<bool>(() => _mainWindow.ApiSetPendingApprovalAsync(GetInt(request.Parameters, "index"), true)).ConfigureAwait(false)),
                    "deny_tool_call" => LocalApiResponse.OkResult(await InvokeOnUiAsync<bool>(() => _mainWindow.ApiSetPendingApprovalAsync(GetInt(request.Parameters, "index"), false)).ConfigureAwait(false)),
                    "execute_tool" => LocalApiResponse.OkResult(await InvokeOnUiAsync<ApiToolExecutionResult>(() => _mainWindow.ApiExecuteToolAsync(
                        GetString(request.Parameters, "tool"),
                        GetObject(request.Parameters, "parameters"),
                        GetBoolOrDefault(request.Parameters, "wait_for_approval", true),
                        GetIntOrDefault(request.Parameters, "approval_timeout_seconds", 300))).ConfigureAwait(false)),
                    "get_trace" => LocalApiResponse.OkResult(ApiTraceLog.GetSince(GetLong(request.Parameters, "afterId"), GetIntOrDefault(request.Parameters, "limit", 100))),
                    "clear_trace" => LocalApiResponse.OkResult(ClearTrace()),
                    "get_appearance" => LocalApiResponse.OkResult(new {
                        brightness = AppearanceService.Brightness,
                        tintPosition = AppearanceService.TintPosition,
                        accentBrightness = AppearanceService.AccentBrightness,
                        accentTintPosition = AppearanceService.AccentTintPosition,
                        fontSize = AppearanceService.FontSize,
                    }),
                    "set_appearance" => LocalApiResponse.OkResult(await InvokeOnUiAsync(() =>
                    {
                        double brightness = GetDoubleOrDefault(request.Parameters, "brightness", AppearanceService.Brightness);
                        double tint       = GetDoubleOrDefault(request.Parameters, "tintPosition", AppearanceService.TintPosition);
                        double ab         = GetDoubleOrDefault(request.Parameters, "accentBrightness", AppearanceService.AccentBrightness);
                        double at_        = GetDoubleOrDefault(request.Parameters, "accentTintPosition", AppearanceService.AccentTintPosition);
                        AppearanceService.Apply(brightness, tint);
                        AppearanceService.ApplyAccent(ab, at_);
                        double fs = GetDoubleOrDefault(request.Parameters, "fontSize", -1);
                        if (fs > 0) AppearanceService.SetFontSize(fs);
                        return Task.CompletedTask;
                    }).ConfigureAwait(false)),
                    _ => LocalApiResponse.Error($"Unknown method: {request.Method}")
                };
            }
            catch (InvalidOperationException)
            {
                return LocalApiResponse.Error("Invalid local API request.");
            }
            catch (Exception ex)
            {
                ApiTraceLog.Record("error", request?.Method ?? "<null>", $"Unexpected local API failure: {ex.GetType().Name}", false);
                return LocalApiResponse.Error("An internal error occurred.");
            }
        }

        /// <summary>
        /// Executes an asynchronous UI operation that returns a value on the WPF dispatcher thread.
        /// </summary>
        /// <typeparam name="T">Result type returned by the UI operation.</typeparam>
        /// <param name="action">Asynchronous UI action to run.</param>
        /// <returns>The value returned by the UI action.</returns>
        private async Task<T> InvokeOnUiAsync<T>(Func<Task<T>> action)
        {
            var op = _mainWindow.Dispatcher.InvokeAsync(action);
            var task = await op.Task.ConfigureAwait(false);
            return await task.ConfigureAwait(false);
        }

        /// <summary>
        /// Executes an asynchronous UI operation with no meaningful return value on the WPF dispatcher thread.
        /// </summary>
        /// <param name="action">Asynchronous UI action to run.</param>
        /// <returns>Always returns <see langword="true"/> when the action completes.</returns>
        private async Task<bool> InvokeOnUiAsync(Func<Task> action)
        {
            var op = _mainWindow.Dispatcher.InvokeAsync(action);
            var task = await op.Task.ConfigureAwait(false);
            await task.ConfigureAwait(false);
            return true;
        }

        internal static async Task<ApiPendingApproval?> WaitForPendingApprovalAsync(
            Func<Task<ApiPendingApproval?>> getPendingApprovalAsync,
            int timeoutSeconds,
            CancellationToken token,
            int pollIntervalMs = 200)
        {
            timeoutSeconds = Math.Clamp(timeoutSeconds, 1, 3600);
            pollIntervalMs = Math.Clamp(pollIntervalMs, 50, 5000);

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

            while (!timeoutCts.IsCancellationRequested)
            {
                var pending = await getPendingApprovalAsync().ConfigureAwait(false);
                if (pending != null)
                    return pending;

                try
                {
                    await Task.Delay(pollIntervalMs, timeoutCts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }

            return null;
        }

        internal static int GetInt(JsonElement? element, string name)
        {
            if (element.HasValue &&
                element.Value.ValueKind == JsonValueKind.Object &&
                element.Value.TryGetProperty(name, out var prop) &&
                prop.TryGetInt32(out var value))
                return value;
            throw new InvalidOperationException($"Missing or invalid '{name}' parameter.");
        }

        internal static int GetIntOrDefault(JsonElement? element, string name, int defaultValue)
        {
            if (element.HasValue &&
                element.Value.ValueKind == JsonValueKind.Object &&
                element.Value.TryGetProperty(name, out var prop) &&
                prop.TryGetInt32(out var value))
                return value;
            return defaultValue;
        }

        internal static int? GetNullableInt(JsonElement? element, string name)
        {
            if (element.HasValue &&
                element.Value.ValueKind == JsonValueKind.Object &&
                element.Value.TryGetProperty(name, out var prop))
            {
                if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt32(out var value))
                    return value;
                if (prop.ValueKind == JsonValueKind.String && int.TryParse(prop.GetString(), out var parsed))
                    return parsed;
            }
            return null;
        }

        internal static bool GetBoolOrDefault(JsonElement? element, string name, bool defaultValue)
        {
            if (element.HasValue &&
                element.Value.ValueKind == JsonValueKind.Object &&
                element.Value.TryGetProperty(name, out var prop))
            {
                if (prop.ValueKind == JsonValueKind.True) return true;
                if (prop.ValueKind == JsonValueKind.False) return false;
                if (prop.ValueKind == JsonValueKind.String && bool.TryParse(prop.GetString(), out var parsed))
                    return parsed;
            }

            return defaultValue;
        }

        internal static double GetDoubleOrDefault(JsonElement? element, string name, double defaultValue)
        {
            if (element.HasValue &&
                element.Value.ValueKind == JsonValueKind.Object &&
                element.Value.TryGetProperty(name, out var prop) &&
                prop.TryGetDouble(out var value))
                return value;
            return defaultValue;
        }

        internal static long GetLong(JsonElement? element, string name)
        {
            if (element.HasValue &&
                element.Value.ValueKind == JsonValueKind.Object &&
                element.Value.TryGetProperty(name, out var prop) &&
                prop.TryGetInt64(out var value))
                return value;
            return 0;
        }

        internal static string GetString(JsonElement? element, string name)
        {
            if (element.HasValue &&
                element.Value.ValueKind == JsonValueKind.Object &&
                element.Value.TryGetProperty(name, out var prop))
            {
                var value = prop.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                    return value!;
            }
            return string.Empty;
        }

        internal static string? GetNullableString(JsonElement? element, string name)
        {
            var value = GetString(element, name);
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }

        internal static JsonElement GetObject(JsonElement? element, string name)
        {
            if (element.HasValue &&
                element.Value.ValueKind == JsonValueKind.Object &&
                element.Value.TryGetProperty(name, out var prop) &&
                prop.ValueKind == JsonValueKind.Object)
                return prop.Clone();

            using var doc = JsonDocument.Parse("{}");
            return doc.RootElement.Clone();
        }

        internal static object ClearTrace()
        {
            ApiTraceLog.Clear();
            return new { cleared = true };
        }

        /// <summary>
        /// Redacts or trims response payloads before they are copied into the in-memory trace log.
        /// </summary>
        /// <param name="method">Local API method name.</param>
        /// <param name="result">Raw result object returned to the caller.</param>
        /// <returns>A reduced trace payload, or <see langword="null"/> when the result should not be retained.</returns>
        internal static object? GetTraceDataForLogging(string method, object? result)
        {
            if (result == null) return null;

            return method.Trim().ToLowerInvariant() switch
            {
                "execute_tool" => result is ApiToolExecutionResult toolResult
                    ? new
                    {
                        toolResult.Status,
                        toolResult.PendingApprovalIndex,
                        HasDirectory = !string.IsNullOrEmpty(toolResult.DirectoryPath),
                        HasScreenshot = !string.IsNullOrEmpty(toolResult.ScreenshotPath)
                    }
                    : null,
                "send_message" or "get_messages" or "get_trace" => null,
                _ => result
            };
        }

        /// <summary>
        /// Validates the bearer-style token supplied with a local API request.
        /// </summary>
        /// <param name="request">Request whose token should be checked.</param>
        /// <returns><see langword="true"/> when the request token matches the current configured token.</returns>
        internal bool IsAuthorized(LocalApiRequest request)
        {
            var expected = AppState.GetApiAccessToken();
            if (string.IsNullOrWhiteSpace(expected))
                return false;

            return string.Equals(request.Token ?? string.Empty, expected, StringComparison.Ordinal);
        }

        /// <summary>
        /// Cancels and tears down the local API listener.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _listenerCts?.Cancel();
            _listenerCts?.Dispose();
            _listener?.Stop();
        }
    }

    internal sealed class LocalApiRequest
    {
        [JsonPropertyName("method")]
        public string Method { get; set; } = string.Empty;

        [JsonPropertyName("parameters")]
        public JsonElement? Parameters { get; set; }

        [JsonPropertyName("token")]
        public string? Token { get; set; }
    }

    internal sealed class LocalApiResponse
    {
        public bool Ok { get; set; }
        public object? Result { get; set; }
        public string? ErrorMessage { get; set; }

        public static LocalApiResponse OkResult(object? result) => new() { Ok = true, Result = result };
        public static LocalApiResponse Error(string error) => new() { Ok = false, ErrorMessage = error };
    }
}
