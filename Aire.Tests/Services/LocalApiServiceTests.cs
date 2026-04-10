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
        public void LocalApiApplicationService_NormalizeProviderType_UsesCanonicalCatalogRules()
        {
            LocalApiApplicationService localApiApplicationService = new LocalApiApplicationService();

            Assert.Equal("GoogleAIImage", localApiApplicationService.NormalizeProviderType("google ai images"));
            Assert.Equal("ClaudeWeb", localApiApplicationService.NormalizeProviderType("claude.ai"));
            Assert.Equal("Zai", localApiApplicationService.NormalizeProviderType("z.ai"));
        }

        [Fact]
        public void RequestParsingHelpers_GetNullableString_HandlesMissingAndBlankValues()
        {
            using JsonDocument doc = JsonDocument.Parse("{\"name\":\"  \",\"type\":\"GoogleAIImage\"}");

            Assert.Null(LocalApiService.GetNullableString(doc.RootElement, "name"));
            Assert.Equal("GoogleAIImage", LocalApiService.GetNullableString(doc.RootElement, "type"));
            Assert.Null(LocalApiService.GetNullableString(doc.RootElement, "missing"));
        }

        [Fact]
        public void RequestParsingHelpers_GetNullableInt_HandlesPresentAndMissingValues()
        {
            using JsonDocument doc = JsonDocument.Parse("{\"providerId\":13,\"blank\":\"x\"}");

            Assert.Equal(13, LocalApiService.GetNullableInt(doc.RootElement, "providerId"));
            Assert.Null(LocalApiService.GetNullableInt(doc.RootElement, "missing"));
            Assert.Null(LocalApiService.GetNullableInt(doc.RootElement, "blank"));
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
        public void TraceLoggingHelpers_ShapesWindowCaptureAndSelectionMethods()
        {
            var captureResult = new WindowCaptureResult
            {
                Ok = true,
                WindowId = "0000000000000123",
                WindowTitle = "Settings — Aire",
                ProcessName = "Aire",
                PngPath = "C:/shot.png",
                PngBase64 = "abc"
            };

            dynamic? captureTrace = LocalApiService.GetTraceDataForLogging("capture_window", captureResult);
            Assert.NotNull(captureTrace);
            Assert.True((bool)captureTrace!.Ok);
            Assert.Equal("0000000000000123", captureTrace!.WindowId);
            Assert.Equal("Settings — Aire", captureTrace!.WindowTitle);
            Assert.True((bool)captureTrace!.HasPath);
            Assert.True((bool)captureTrace!.HasBase64);

            dynamic? listTrace = LocalApiService.GetTraceDataForLogging(
                "list_windows",
                new[]
                {
                    new TopLevelWindowInfo { WindowId = "1", Title = "A", ProcessName = "P" }
                });
            Assert.NotNull(listTrace);
            Assert.Equal(1, (int)listTrace!.Count);

            dynamic? selectedTrace = LocalApiService.GetTraceDataForLogging(
                "get_selected_window",
                new TopLevelWindowInfo
                {
                    WindowId = "0000000000000123",
                    Title = "Settings — Aire",
                    ProcessName = "Aire"
                });
            Assert.NotNull(selectedTrace);
            Assert.Equal("Settings — Aire", selectedTrace!.Title);
        }

        [Fact]
        public void IsAuthorized_DeniesMissingAndWrongTokens()
        {
            RunOnStaThread(() =>
            {
                AppState.SetApiAccessEnabled(true);
                AppState.SetApiAccessToken("expected-token");
                
                var window = new MainWindow(initializeUi: false);
                var service = new LocalApiService(window); 
                Assert.False(service.IsAuthorized(new LocalApiRequest { Method = "ping", Token = null }));
                Assert.False(service.IsAuthorized(new LocalApiRequest { Method = "ping", Token = "wrong" }));
                Assert.True(service.IsAuthorized(new LocalApiRequest { Method = "ping", Token = "expected-token" }));
            });
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
        public void WriteResponseAsync_SerializesCamelCaseJson()
        {
            RunOnStaThread(async () =>
            {
                var window = new MainWindow(initializeUi: false);
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
            });
        }

        [Fact]
        public void DispatchAsync_ReturnsUnknownMethodError_WithoutTouchingUi()
        {
            RunOnStaThread(async () =>
            {
                var window = new MainWindow(initializeUi: false);
                var service = new LocalApiService(window);

                var response = await service.DispatchAsync(new LocalApiRequest
                {
                    Method = "  unknown_method  "
                }, CancellationToken.None);

                Assert.False(response.Ok);
                Assert.Contains("Unknown method", response.ErrorMessage, StringComparison.OrdinalIgnoreCase);
            });
        }

        [Fact]
        public void DispatchAsync_ReturnsGenericError_ForUnexpectedExceptions()
        {
            RunOnStaThread(async () =>
            {
                var window = new MainWindow(initializeUi: false);
                var service = new LocalApiService(window);

                var response = await service.DispatchAsync(null!, CancellationToken.None);

                Assert.False(response.Ok);
                Assert.Equal("An internal error occurred.", response.ErrorMessage);
                Assert.DoesNotContain("Object reference", response.ErrorMessage, StringComparison.OrdinalIgnoreCase);
            });
        }

        [Fact]
        public async Task WaitForPendingApprovalAsync_ReturnsApproval_WhenOneAppearsBeforeTimeout()
        {
            var attempts = 0;

            var result = await LocalApiService.WaitForPendingApprovalAsync(
                () =>
                {
                    attempts++;
                    return Task.FromResult(attempts >= 3
                        ? new ApiPendingApproval
                        {
                            Index = 7,
                            Tool = "read_file",
                            Description = "Read file",
                            RawJson = "{}",
                            Timestamp = "12:34"
                        }
                        : null);
                },
                timeoutSeconds: 1,
                token: CancellationToken.None,
                pollIntervalMs: 10);

            Assert.NotNull(result);
            Assert.Equal(7, result!.Index);
            Assert.True(attempts >= 3);
        }

        [Fact]
        public async Task WaitForPendingApprovalAsync_ReturnsNull_OnTimeout()
        {
            var result = await LocalApiService.WaitForPendingApprovalAsync(
                () => Task.FromResult<ApiPendingApproval?>(null),
                timeoutSeconds: 1,
                token: CancellationToken.None,
                pollIntervalMs: 10);

            Assert.Null(result);
        }

        [Fact]
        public void IsAuthorized_DeniesWhenExpectedTokenMissing()
        {
            RunOnStaThread(() =>
            {
                AppState.SetApiAccessEnabled(true);
                AppState.SetApiAccessToken(string.Empty);

                var window = new MainWindow(initializeUi: false);
                var service = new LocalApiService(window);

                Assert.False(service.IsAuthorized(new LocalApiRequest { Method = "ping", Token = "anything" }));
            });
        }

        [Fact]
        public void HandleClientAsync_EmptyRequest_ReturnsEmptyRequestError()
        {
            RunOnStaThread(async () =>
            {
                AppState.SetApiAccessEnabled(true);
                AppState.SetApiAccessToken("token");

                var window = new MainWindow(initializeUi: false);
                var service = new LocalApiService(window);

                var response = await RoundTripAsync(service, string.Empty + Environment.NewLine);

                Assert.Contains("\"ok\":false", response, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("Empty request", response, StringComparison.OrdinalIgnoreCase);
            });
        }

        [Fact]
        public void HandleClientAsync_InvalidJson_ReturnsJsonError()
        {
            RunOnStaThread(async () =>
            {
                AppState.SetApiAccessEnabled(true);
                AppState.SetApiAccessToken("token");

                var window = new MainWindow(initializeUi: false);
                var service = new LocalApiService(window);

                var response = await RoundTripAsync(service, "{not-json}" + Environment.NewLine);

                Assert.Contains("\"ok\":false", response, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("Invalid JSON", response, StringComparison.OrdinalIgnoreCase);
            });
        }

        [Fact]
        public void HandleClientAsync_DisabledApi_ReturnsDisabledError()
        {
            RunOnStaThread(async () =>
            {
                AppState.SetApiAccessEnabled(false);
                AppState.SetApiAccessToken("token");

                var window = new MainWindow(initializeUi: false);
                var service = new LocalApiService(window);

                var response = await RoundTripAsync(service, "{\"method\":\"ping\",\"token\":\"token\"}" + Environment.NewLine);

                Assert.Contains("\"ok\":false", response, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("disabled", response, StringComparison.OrdinalIgnoreCase);
            });
        }

        [Fact]
        public void HandleClientAsync_InvalidToken_ReturnsAuthError()
        {
            RunOnStaThread(async () =>
            {
                AppState.SetApiAccessEnabled(true);
                AppState.SetApiAccessToken("expected-token");

                var window = new MainWindow(initializeUi: false);
                var service = new LocalApiService(window);

                var response = await RoundTripAsync(service, "{\"method\":\"ping\",\"token\":\"wrong\"}" + Environment.NewLine);

                Assert.Contains("\"ok\":false", response, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("invalid auth token", response, StringComparison.OrdinalIgnoreCase);
            });
        }

        [Fact]
        public void DispatchAsync_GetTraceAndClearTrace_WorkWithoutUiDispatch()
        {
            RunOnStaThread(async () =>
            {
                var window = new MainWindow(initializeUi: false);
                var service = new LocalApiService(window);

                LocalApiService.ClearTrace();
                var traceResponse = await service.DispatchAsync(new LocalApiRequest
                {
                    Method = "get_trace",
                    Parameters = JsonDocument.Parse("{\"afterId\":0,\"limit\":5}").RootElement.Clone()
                }, CancellationToken.None);

                var clearResponse = await service.DispatchAsync(new LocalApiRequest
                {
                    Method = "clear_trace"
                }, CancellationToken.None);

                Assert.True(traceResponse.Ok);
                Assert.NotNull(traceResponse.Result);
                Assert.True(clearResponse.Ok);
                Assert.Contains("cleared", clearResponse.Result!.ToString(), StringComparison.OrdinalIgnoreCase);
            });
        }

        [Fact]
        public void DispatchAsync_ApproveAndDenyToolCalls_RouteThroughMainWindowApprovalState()
        {
            RunOnStaThread(async () =>
            {
                AppStartupState.MarkReady();
                var window = new MainWindow(initializeUi: false);
                var service = new LocalApiService(window);

                var approveTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                var denyTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                window.Messages = new System.Collections.ObjectModel.ObservableCollection<Aire.UI.MainWindow.Models.ChatMessage>
                {
                    new()
                    {
                        IsApprovalPending = true,
                        PendingToolCall = new ToolCallRequest
                        {
                            Tool = "read_file",
                            Description = "read",
                            RawJson = "{}"
                        },
                        ApprovalTcs = approveTcs,
                        Timestamp = "10:00"
                    },
                    new()
                    {
                        IsApprovalPending = true,
                        PendingToolCall = new ToolCallRequest
                        {
                            Tool = "write_file",
                            Description = "write",
                            RawJson = "{}"
                        },
                        ApprovalTcs = denyTcs,
                        Timestamp = "10:01"
                    }
                };

                var approve = await service.DispatchAsync(new LocalApiRequest
                {
                    Method = "approve_tool_call",
                    Parameters = JsonDocument.Parse("{\"index\":0}").RootElement.Clone()
                }, CancellationToken.None);

                var deny = await service.DispatchAsync(new LocalApiRequest
                {
                    Method = "deny_tool_call",
                    Parameters = JsonDocument.Parse("{\"index\":1}").RootElement.Clone()
                }, CancellationToken.None);

                Assert.True(approve.Ok);
                Assert.True(deny.Ok);
                Assert.True(approveTcs.Task.IsCompletedSuccessfully);
                Assert.True(denyTcs.Task.IsCompletedSuccessfully);
                Assert.True(approveTcs.Task.Result);
                Assert.False(denyTcs.Task.Result);
            });
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
