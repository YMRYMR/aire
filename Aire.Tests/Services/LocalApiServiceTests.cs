using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text.Json;
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
    }
}
