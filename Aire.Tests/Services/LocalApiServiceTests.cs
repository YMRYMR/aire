using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Aire.AppLayer.Api;
using Aire.Services;
using Xunit;

namespace Aire.Tests.Services
{
    [Collection("AppState Isolation")]
    public class LocalApiServiceTests : TestBase, IDisposable
    {
        private readonly string _tokenBackup;
        private readonly bool _apiEnabledBackup;

        public LocalApiServiceTests()
        {
            _tokenBackup = AppState.GetApiAccessToken();
            _apiEnabledBackup = AppState.GetApiAccessEnabled();
        }

        public void Dispose()
        {
            AppState.SetApiAccessToken(_tokenBackup);
            AppState.SetApiAccessEnabled(_apiEnabledBackup);
        }

        [Fact]
        public void LocalApiApplicationService_MapsDirectoryAndScreenshotFields()
        {
            LocalApiApplicationService localApiApplicationService = new LocalApiApplicationService();
            ToolExecutionResult obj = new ToolExecutionResult
            {
                TextResult = "done",
                ScreenshotPath = "C:/shot.png"
            };
            DirectoryListing obj2 = new DirectoryListing
            {
                Path = "C:/repo"
            };
            List<DirectoryEntry> list = new List<DirectoryEntry>
            {
                new DirectoryEntry { Name = "one.txt", IsDirectory = false },
                new DirectoryEntry { Name = "two.txt", IsDirectory = false }
            };
            obj2.Entries = list;
            obj.DirectoryListing = obj2;
            
            ApiToolExecutionResult result = localApiApplicationService.BuildCompletedToolResult(obj);
            Assert.Equal("completed", result.Status);
            Assert.Equal("done", result.TextResult);
            Assert.Equal("C:/repo", result.DirectoryPath);
            Assert.Equal("2 files", result.DirectorySummary);
            Assert.Equal("C:/shot.png", result.ScreenshotPath);
        }

        [Fact]
        public void RequestParsingHelpers_HandleTypedAndMissingValues()
        {
            using JsonDocument doc = JsonDocument.Parse("{\r\n  \"conversationId\": 42,\r\n  \"afterId\": 1234567890123,\r\n  \"enabled\": \"true\",\r\n  \"title\": \" Hello \",\r\n  \"parameters\": { \"path\": \"C:/repo\" }\r\n}");
            
            Assert.Equal(42, LocalApiService.GetInt(doc.RootElement, "conversationId"));
            Assert.Equal(42, LocalApiService.GetIntOrDefault(doc.RootElement, "conversationId", 9));
            Assert.Equal(9, LocalApiService.GetIntOrDefault(doc.RootElement, "missing", 9));
            Assert.True(LocalApiService.GetBoolOrDefault(doc.RootElement, "enabled", false));
            Assert.True(LocalApiService.GetBoolOrDefault(doc.RootElement, "missing", true));
            Assert.Equal(1234567890123L, LocalApiService.GetLong(doc.RootElement, "afterId"));
            Assert.Equal(" Hello ", LocalApiService.GetString(doc.RootElement, "title"));
            
            JsonElement parameters = LocalApiService.GetObject(doc.RootElement, "parameters");
            Assert.Equal(JsonValueKind.Object, parameters.ValueKind);
            Assert.Equal("C:/repo", parameters.GetProperty("path").GetString());
        }

        [Fact]
        public void RequestParsingHelpers_HandleInvalidAndFallbackValues()
        {
            using JsonDocument doc = JsonDocument.Parse("{\"conversationId\":\"oops\",\"enabled\":false,\"parameters\":\"not-an-object\"}");

            Assert.Throws<InvalidOperationException>(() => LocalApiService.GetInt(doc.RootElement, "conversationId"));
            Assert.False(LocalApiService.GetBoolOrDefault(doc.RootElement, "enabled", true));
            Assert.False(LocalApiService.GetBoolOrDefault(doc.RootElement, "missing", false));
            Assert.Equal(string.Empty, LocalApiService.GetString(doc.RootElement, "missing"));

            JsonElement fallback = LocalApiService.GetObject(doc.RootElement, "parameters");
            Assert.Equal(JsonValueKind.Object, fallback.ValueKind);
            Assert.Empty(fallback.EnumerateObject());
        }

        [Fact]
        public void TraceLoggingHelpers_RedactSensitiveMethodsAndShapeExecuteTool()
        {
            ApiToolExecutionResult result = new ApiToolExecutionResult
            {
                Status = "pending_approval",
                PendingApprovalIndex = 3,
                DirectoryPath = "C:/repo",
                ScreenshotPath = "C:/shot.png"
            };
            
            dynamic? traceData = LocalApiService.GetTraceDataForLogging("execute_tool", result);
            Assert.NotNull(traceData);
            Assert.Equal("pending_approval", traceData!.Status);
            Assert.Equal(3, (int)traceData!.PendingApprovalIndex);
            Assert.True((bool)traceData!.HasDirectory);
            Assert.True((bool)traceData!.HasScreenshot);
            
            Assert.Null(LocalApiService.GetTraceDataForLogging("send_message", new { ok = true }));
            Assert.Equal(7, (int)LocalApiService.GetTraceDataForLogging("ping", 7)!);
        }

        [Fact]
        public void IsAuthorized_DeniesMissingAndWrongTokens()
        {
            AppState.SetApiAccessEnabled(true);
            AppState.SetApiAccessToken("expected-token");
            
            var window = (MainWindow)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(MainWindow));
            var service = new LocalApiService(window); 
            Assert.False(service.IsAuthorized(new LocalApiRequest { Method = "ping", Token = null }));
            Assert.False(service.IsAuthorized(new LocalApiRequest { Method = "ping", Token = "wrong" }));
            Assert.True(service.IsAuthorized(new LocalApiRequest { Method = "ping", Token = "expected-token" }));
        }

        [Fact]
        public void ResponseFactories_And_ClearTrace_Work()
        {
            var ok = LocalApiResponse.OkResult(12);
            var err = LocalApiResponse.Error("bad request");
            dynamic cleared = LocalApiService.ClearTrace();
            
            Assert.True(ok.Ok);
            Assert.Equal(12, (int)ok.Result!);
            Assert.False(err.Ok);
            Assert.Equal("bad request", err.ErrorMessage);
            Assert.True((bool)cleared.cleared);
        }

        [Fact]
        public async Task WriteResponseAsync_SerializesCamelCaseJson()
        {
            var window = (MainWindow)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(MainWindow));
            var service = new LocalApiService(window);
            using var stream = new MemoryStream();
            using var writer = new StreamWriter(stream, leaveOpen: true);

            await service.WriteResponseAsync(writer, new LocalApiResponse
            {
                Ok = false,
                ErrorMessage = "bad request"
            });
            await writer.FlushAsync();
            stream.Position = 0;
            using var reader = new StreamReader(stream);
            var json = await reader.ReadToEndAsync();

            Assert.Contains("\"ok\":false", json, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("\"errorMessage\":\"bad request\"", json, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task DispatchAsync_ReturnsUnknownMethodError_WithoutTouchingUi()
        {
            var window = (MainWindow)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(MainWindow));
            var service = new LocalApiService(window);

            var response = await service.DispatchAsync(new LocalApiRequest
            {
                Method = "  unknown_method  "
            }, CancellationToken.None);

            Assert.False(response.Ok);
            Assert.Contains("Unknown method", response.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void IsAuthorized_DeniesWhenExpectedTokenMissing()
        {
            AppState.SetApiAccessEnabled(true);
            AppState.SetApiAccessToken(string.Empty);

            var window = (MainWindow)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(MainWindow));
            var service = new LocalApiService(window);

            Assert.False(service.IsAuthorized(new LocalApiRequest { Method = "ping", Token = "anything" }));
        }

        [Fact]
        public async Task HandleClientAsync_EmptyRequest_ReturnsEmptyRequestError()
        {
            AppState.SetApiAccessEnabled(true);
            AppState.SetApiAccessToken("token");

            var window = (MainWindow)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(MainWindow));
            var service = new LocalApiService(window);

            var response = await RoundTripAsync(service, string.Empty + Environment.NewLine);

            Assert.Contains("\"ok\":false", response, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Empty request", response, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task HandleClientAsync_InvalidJson_ReturnsJsonError()
        {
            AppState.SetApiAccessEnabled(true);
            AppState.SetApiAccessToken("token");

            var window = (MainWindow)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(MainWindow));
            var service = new LocalApiService(window);

            var response = await RoundTripAsync(service, "{not-json}" + Environment.NewLine);

            Assert.Contains("\"ok\":false", response, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Invalid JSON", response, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task HandleClientAsync_DisabledApi_ReturnsDisabledError()
        {
            AppState.SetApiAccessEnabled(false);
            AppState.SetApiAccessToken("token");

            var window = (MainWindow)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(MainWindow));
            var service = new LocalApiService(window);

            var response = await RoundTripAsync(service, "{\"method\":\"ping\",\"token\":\"token\"}" + Environment.NewLine);

            Assert.Contains("\"ok\":false", response, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("disabled", response, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task HandleClientAsync_InvalidToken_ReturnsAuthError()
        {
            AppState.SetApiAccessEnabled(true);
            AppState.SetApiAccessToken("expected-token");

            var window = (MainWindow)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(MainWindow));
            var service = new LocalApiService(window);

            var response = await RoundTripAsync(service, "{\"method\":\"ping\",\"token\":\"wrong\"}" + Environment.NewLine);

            Assert.Contains("\"ok\":false", response, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("invalid auth token", response, StringComparison.OrdinalIgnoreCase);
        }

        private static async Task<string> RoundTripAsync(LocalApiService service, string requestLine)
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            try
            {
                var port = ((IPEndPoint)listener.LocalEndpoint).Port;
                using var client = new TcpClient();
                var connectTask = client.ConnectAsync(IPAddress.Loopback, port);
                using var serverClient = await listener.AcceptTcpClientAsync();
                await connectTask;

                var handleTask = service.HandleClientAsync(serverClient, CancellationToken.None);

                using var clientStream = client.GetStream();
                using var writer = new StreamWriter(clientStream, leaveOpen: true) { AutoFlush = true };
                using var reader = new StreamReader(clientStream, leaveOpen: true);

                await writer.WriteAsync(requestLine);
                var response = await reader.ReadLineAsync();
                await handleTask;

                return response ?? string.Empty;
            }
            finally
            {
                listener.Stop();
            }
        }
    }
}
