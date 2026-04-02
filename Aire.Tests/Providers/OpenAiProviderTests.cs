using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using Aire.Providers;
using OpenAI.ObjectModels.RequestModels;
using Xunit;

namespace Aire.Tests.Providers
{
    public class OpenAiProviderTests : TestBase
    {
        private sealed class InspectableOpenAiProvider : OpenAiProvider
        {
            public IList<ToolDefinition> ExposeFunctionDefinitions() => GetFunctionDefinitions();

            public OpenAI.ObjectModels.RequestModels.ChatMessage ExposeConvert(Aire.Providers.ChatMessage message)
                => ConvertToOpenAiMessage(message);

            public string? ExposeExtractApiErrorMessage(string json)
                => TryExtractApiErrorMessage(json);
        }

        private sealed class UsageCapableOpenAiProvider : OpenAiProvider
        {
            protected override bool SupportsTokenUsageEndpoint(string baseUrl) => true;
        }

        [Fact]
        public void OpenAiProvider_HelperPaths_Work()
        {
            var (host, version) = OpenAiProvider.SplitSdkUrl("https://api.groq.com/openai/");
            Assert.Equal("https://api.groq.com", host);
            Assert.Equal("openai/v1", version);

            string toolCallStr = OpenAiProvider.ConvertFunctionCallToToolCall("execute_command", "{\"command\":\"dir\",\"timeout_seconds\":5,\"shell\":null,\"interactive\":false}");
            Assert.StartsWith("<tool_call>", toolCallStr, StringComparison.Ordinal);
            
            using (JsonDocument doc = JsonDocument.Parse(toolCallStr.Replace("<tool_call>", "").Replace("</tool_call>", "")))
            {
                Assert.Equal("execute_command", doc.RootElement.GetProperty("tool").GetString());
                Assert.Equal("dir", doc.RootElement.GetProperty("command").GetString());
                Assert.Equal(5, doc.RootElement.GetProperty("timeout_seconds").GetInt32());
                Assert.False(doc.RootElement.GetProperty("interactive").GetBoolean());
                Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("shell").ValueKind);
            }

            InspectableOpenAiProvider provider = new InspectableOpenAiProvider();
            provider.Initialize(new ProviderConfig
            {
                ApiKey = "sk-test-key",
                Model = "gpt-4o",
                ModelCapabilities = new List<string> { "tools", "mouse" }
            });

            var definitions = provider.ExposeFunctionDefinitions();
            Assert.NotEmpty(definitions);
            Assert.Contains(definitions, d => d.Function.Name == "begin_mouse_session");
            Assert.DoesNotContain(definitions, d => d.Function.Name == "type_text");

            provider.SetEnabledToolCategories(new[] { "filesystem" });
            var filesystemOnly = provider.ExposeFunctionDefinitions();
            Assert.Contains(filesystemOnly, d => d.Function.Name == "read_file");
            Assert.DoesNotContain(filesystemOnly, d => d.Function.Name == "begin_mouse_session");
            Assert.DoesNotContain(filesystemOnly, d => d.Function.Name == "list_browser_tabs");

            var chatMessage = provider.ExposeConvert(new Aire.Providers.ChatMessage
            {
                Role = "user",
                Content = "describe",
                ImageBytes = new byte[] { 1, 2, 3, 4 },
                ImageMimeType = "image/jpeg"
            });

            Assert.Null(chatMessage.Content);
            Assert.NotNull(chatMessage.Contents);
            Assert.Equal(2, chatMessage.Contents.Count);
            Assert.Equal("text", chatMessage.Contents[0].Type);
            Assert.Equal("image_url", chatMessage.Contents[1].Type);
        }

        [Fact]
        public void OpenAiProvider_ExtractsApiErrors_AndHandlesMissingMessage()
        {
            var provider = new InspectableOpenAiProvider();

            Assert.Equal(
                "Rate limit exceeded.",
                provider.ExposeExtractApiErrorMessage("{\"error\":{\"message\":\"Rate limit exceeded.\",\"code\":429}}"));
            Assert.Null(provider.ExposeExtractApiErrorMessage("{\"error\":{\"code\":429}}"));
            Assert.Null(provider.ExposeExtractApiErrorMessage("not json"));
        }

        [Fact]
        public void SplitSdkUrl_DefaultsToV1_WhenBaseUrlHasNoPathPrefix()
        {
            var (host, version) = OpenAiProvider.SplitSdkUrl("https://api.openai.com/");

            Assert.Equal("https://api.openai.com", host);
            Assert.Equal("v1", version);
        }

        [Fact]
        public async Task OpenAiProvider_UninitializedAndNoKeyPaths_Work()
        {
            OpenAiProvider provider = new OpenAiProvider();
            var validation = await provider.ValidateConfigurationAsync(CancellationToken.None);
            Assert.False(validation.IsValid);
            Assert.NotNull(validation.Error);
            await Assert.ThrowsAsync<InvalidOperationException>(() => provider.SendChatAsync(Array.Empty<Aire.Providers.ChatMessage>(), CancellationToken.None));
            
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await foreach (var item in provider.StreamChatAsync(Array.Empty<Aire.Providers.ChatMessage>(), CancellationToken.None))
                {
                }
            });
        }

        [Fact]
        public async Task FetchLiveModelsAsync_FiltersKnownModelPrefixes_AndSortsDescending()
        {
            using var server = new OpenAiTestServer((method, path, _) =>
            {
                if (method == "GET" && path == "/v1/models")
                {
                    return OpenAiTestServer.Json(200,
                        """
                        {
                          "data": [
                            { "id": "whisper-1" },
                            { "id": "gpt-4.1-mini" },
                            { "id": "o4-mini" },
                            { "id": "gpt-4o" }
                          ]
                        }
                        """);
                }

                return OpenAiTestServer.Json(404, """{"error":{"message":"missing"}}""");
            });

            var provider = new OpenAiProvider();

            var models = await provider.FetchLiveModelsAsync("sk-test", server.BaseUrl, CancellationToken.None);

            Assert.NotNull(models);
            string[] ids = models!.Select(m => m.Id).ToArray();
            Assert.Equal(new[] { "o4-mini", "gpt-4o", "gpt-4.1-mini" }, ids);
        }

        [Fact]
        public async Task ValidateConfigurationAsync_UsesChatProbe_WhenModelsEndpointIsForbidden()
        {
            using var server = new OpenAiTestServer((method, path, _) =>
            {
                if (method == "GET" && path == "/v1/models")
                    return OpenAiTestServer.Json(403, """{"error":{"message":"model list disabled"}}""");

                if (method == "POST" && path == "/v1/chat/completions")
                    return OpenAiTestServer.Json(429, """{"error":{"message":"billing required"}}""");

                return OpenAiTestServer.Json(404, """{"error":{"message":"missing"}}""");
            });

            var provider = new OpenAiProvider();
            provider.Initialize(new ProviderConfig
            {
                ApiKey = "sk-test",
                BaseUrl = server.BaseUrl,
                Model = "gpt-4o-mini"
            });

            var validation = await provider.ValidateConfigurationAsync(CancellationToken.None);

            Assert.True(validation.IsValid);
        }

        [Fact]
        public async Task ValidateConfigurationAsync_FailsWhenChatProbeAlsoReturnsUnauthorized()
        {
            using var server = new OpenAiTestServer((method, path, _) =>
            {
                if (method == "GET" && path == "/v1/models")
                    return OpenAiTestServer.Json(401, """{"error":{"message":"listing blocked"}}""");

                if (method == "POST" && path == "/v1/chat/completions")
                    return OpenAiTestServer.Json(401, """{"error":{"message":"bad api key"}}""");

                return OpenAiTestServer.Json(404, """{"error":{"message":"missing"}}""");
            });

            var provider = new OpenAiProvider();
            provider.Initialize(new ProviderConfig
            {
                ApiKey = "sk-test",
                BaseUrl = server.BaseUrl,
                Model = "gpt-4o-mini"
            });

            var validation = await provider.ValidateConfigurationAsync(CancellationToken.None);

            Assert.False(validation.IsValid);
            Assert.Equal("bad api key", validation.Error);
        }

        [Fact]
        public async Task GetTokenUsageAsync_ParsesUsagePayload()
        {
            using var server = new OpenAiTestServer((method, path, _) =>
            {
                if (method == "GET" && path == "/dashboard/billing/usage")
                {
                    return OpenAiTestServer.Json(200,
                        """{"total_usage":1234,"hard_limit":9999,"end_date":"2026-04-30T00:00:00Z"}""");
                }

                return OpenAiTestServer.Json(404, """{"error":{"message":"missing"}}""");
            });

            var provider = new UsageCapableOpenAiProvider();
            provider.Initialize(new ProviderConfig
            {
                ApiKey = "sk-test",
                BaseUrl = server.BaseUrl,
                Model = "gpt-4o-mini"
            });

            var usage = await provider.GetTokenUsageAsync(CancellationToken.None);

            Assert.NotNull(usage);
            Assert.Equal(1234, usage!.Used);
            Assert.Equal(9999, usage.Limit);
            Assert.Equal("USD", usage.Unit);
            Assert.Equal(new DateTime(2026, 4, 30, 0, 0, 0, DateTimeKind.Utc), usage.ResetDate?.ToUniversalTime());
        }

        private sealed class OpenAiTestServer : IDisposable
        {
            private readonly TcpListener _listener;
            private readonly Func<string, string, string, Response> _handler;
            private readonly Task _serveLoop;

            public OpenAiTestServer(Func<string, string, string, Response> handler)
            {
                _handler = handler;
                _listener = new TcpListener(IPAddress.Loopback, 0);
                _listener.Start();
                var port = ((IPEndPoint)_listener.LocalEndpoint).Port;
                BaseUrl = $"http://127.0.0.1:{port}";
                _serveLoop = Task.Run(ServeAsync);
            }

            public string BaseUrl { get; }

            public static Response Json(int statusCode, string json) =>
                new(statusCode, "application/json", Encoding.UTF8.GetBytes(json));

            private async Task ServeAsync()
            {
                try
                {
                    while (true)
                    {
                        using var client = await _listener.AcceptTcpClientAsync();
                        using var stream = client.GetStream();
                        using var reader = new StreamReader(stream, leaveOpen: true);

                        var requestLine = await reader.ReadLineAsync();
                        if (requestLine == null)
                            continue;

                        var parts = requestLine.Split(' ');
                        var method = parts[0];
                        var path = parts[1];
                        var contentLength = 0;

                        string? line;
                        while (!string.IsNullOrEmpty(line = await reader.ReadLineAsync()))
                        {
                            if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                                contentLength = int.Parse(line[15..].Trim());
                        }

                        var body = string.Empty;
                        if (contentLength > 0)
                        {
                            var buffer = new char[contentLength];
                            var read = 0;
                            while (read < contentLength)
                                read += await reader.ReadAsync(buffer, read, contentLength - read);
                            body = new string(buffer);
                        }

                        var response = _handler(method, path, body);
                        var statusText = response.StatusCode == 200 ? "OK" : "Error";
                        var header =
                            $"HTTP/1.1 {response.StatusCode} {statusText}\r\n" +
                            $"Content-Type: {response.ContentType}\r\n" +
                            $"Content-Length: {response.Body.Length}\r\n" +
                            "Connection: close\r\n\r\n";

                        await stream.WriteAsync(Encoding.ASCII.GetBytes(header));
                        await stream.WriteAsync(response.Body);
                        await stream.FlushAsync();
                    }
                }
                catch
                {
                }
            }

            public void Dispose()
            {
                try { _listener.Stop(); } catch { }
                try { _serveLoop.Wait(1000); } catch { }
            }

            public sealed record Response(int StatusCode, string ContentType, byte[] Body);
        }
    }
}
