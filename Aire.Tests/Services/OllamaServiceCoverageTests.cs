using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Aire.Services;
using Xunit;

namespace Aire.Tests.Services;

public class OllamaServiceCoverageTests : IDisposable
{
    private readonly OllamaService _service = new OllamaService();

    public void Dispose()
    {
        _service.Dispose();
    }

    [Fact]
    public void DefaultBaseUrl_IsLocalOllamaEndpoint()
    {
        Assert.Equal("http://localhost:11434", OllamaService.DefaultBaseUrl);
    }

    [Fact]
    public void KnownModelMeta_ContainsWellKnownModelsAndCapabilities()
    {
        Assert.True(OllamaService.KnownModelMeta.ContainsKey("qwen3:4b"));
        Assert.True(OllamaService.KnownModelMeta.ContainsKey("llama3.1:8b"));
        Assert.True(OllamaService.KnownModelMeta.ContainsKey("granite4:latest"));
        OllamaService.OllamaModelMeta ollamaModelMeta = OllamaService.KnownModelMeta["qwen3:4b"];
        OllamaService.OllamaModelMeta ollamaModelMeta2 = OllamaService.KnownModelMeta["llama3.1:8b"];
        Assert.True(ollamaModelMeta.Recommended);
        Assert.Contains("tools", (IEnumerable<string>)ollamaModelMeta.Tags);
        Assert.Contains("thinking", (IEnumerable<string>)ollamaModelMeta.Tags);
        Assert.Equal("4B", ollamaModelMeta.ParamSize);
        Assert.True(ollamaModelMeta.SizeBytes >= 0);
        Assert.True(ollamaModelMeta2.Recommended);
        Assert.Contains("tools", (IEnumerable<string>)ollamaModelMeta2.Tags);
        Assert.Equal("8B", ollamaModelMeta2.ParamSize);
    }

    [Fact]
    public void OllamaModelMeta_Record_StoresProvidedValues()
    {
        OllamaService.OllamaModelMeta ollamaModelMeta = new OllamaService.OllamaModelMeta(new string[] { "tools", "code" }, Recommended: true, "7B", 1234L);
        Assert.Equal<string[]>(new string[] { "tools", "code" }, ollamaModelMeta.Tags);
        Assert.True(ollamaModelMeta.Recommended);
        Assert.Equal("7B", ollamaModelMeta.ParamSize);
        Assert.Equal(1234L, ollamaModelMeta.SizeBytes);
    }

    [Fact]
    public void NestedDtoTypes_AreSerializableAndUsable()
    {
        OllamaService.OllamaModel ollamaModel = new OllamaService.OllamaModel
        {
            Name = "qwen3:4b",
            Size = 123L,
            Digest = "abc"
        };
        Assert.Equal("qwen3:4b", ollamaModel.Name);
        Assert.Equal(123L, ollamaModel.Size);
        Assert.Equal("abc", ollamaModel.Digest);
        OllamaService.OllamaPullProgress ollamaPullProgress = new OllamaService.OllamaPullProgress
        {
            Status = "downloading",
            Digest = "xyz",
            Total = 10L,
            Completed = 4L
        };
        Assert.Equal("downloading", ollamaPullProgress.Status);
        Assert.Equal("xyz", ollamaPullProgress.Digest);
        Assert.Equal(10L, ollamaPullProgress.Total);
        Assert.Equal(4L, ollamaPullProgress.Completed);
    }

    [Theory]
    [InlineData(new object[] { "http://127.0.0.1:1" })]
    [InlineData(new object[] { "http://localhost:1" })]
    public async Task IsOllamaReachableAsync_ReturnsFalseForUnavailableLocalEndpoints(string baseUrl)
    {
        Assert.False(await _service.IsOllamaReachableAsync(baseUrl, CancellationToken.None));
    }

    [Fact]
    public async Task IsOllamaReachableAsync_ReturnsFalseForInvalidUrl()
    {
        Assert.False(await _service.IsOllamaReachableAsync("http://[invalid", CancellationToken.None));
    }

    [Fact]
    public void StaticHelperMetadata_IsCompleteForCommonModelShapes()
    {
        IReadOnlyDictionary<string, OllamaService.OllamaModelMeta> knownModelMeta = OllamaService.KnownModelMeta;
        Assert.Contains<string>("qwen2.5-coder:7b", knownModelMeta.Keys, StringComparer.OrdinalIgnoreCase);
        Assert.Contains<string>("mistral-nemo", knownModelMeta.Keys, StringComparer.OrdinalIgnoreCase);
        Assert.Contains<string>("llava:7b", knownModelMeta.Keys, StringComparer.OrdinalIgnoreCase);
        Assert.Contains<string>("nomic-embed-text", knownModelMeta.Keys, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("vision", (IEnumerable<string>)knownModelMeta["llava:7b"].Tags);
        Assert.Contains("embedding", (IEnumerable<string>)knownModelMeta["nomic-embed-text"].Tags);
        Assert.Contains("code", (IEnumerable<string>)knownModelMeta["qwen2.5-coder:7b"].Tags);
    }

    [Fact]
    public void GetModelRecommendation_PrefersBalancedToolModelsForMidrangeSystems()
    {
        OllamaService.OllamaSystemProfile profile = new OllamaService.OllamaSystemProfile(16.0, 120.0, 8.0, "Test GPU", "balanced", "test");
        OllamaService.OllamaModelRecommendation modelRecommendation = OllamaService.GetModelRecommendation("qwen3:4b", 0L, profile);
        Assert.True(modelRecommendation.RecommendedForThisPc);
        Assert.True(modelRecommendation.AireFriendly);
        Assert.False(modelRecommendation.TooLargeForThisPc);
        Assert.Contains<string>("best fit", modelRecommendation.Badges, StringComparer.OrdinalIgnoreCase);
        Assert.Contains<string>("tools", modelRecommendation.Badges, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetModelRecommendation_FlagsModelsThatAreTooLargeForLowRamSystems()
    {
        OllamaService.OllamaSystemProfile profile = new OllamaService.OllamaSystemProfile(8.0, 200.0, 4.0, "Test GPU", "starter", "test");
        OllamaService.OllamaModelRecommendation modelRecommendation = OllamaService.GetModelRecommendation("qwen3:32b", 0L, profile);
        Assert.False(modelRecommendation.RecommendedForThisPc);
        Assert.True(modelRecommendation.TooLargeForThisPc);
        Assert.Equal("too large", modelRecommendation.SummaryLabel);
    }

    [Fact]
    public void GetModelRecommendation_DemotesEmbeddingOnlyModelsForAire()
    {
        OllamaService.OllamaSystemProfile profile = new OllamaService.OllamaSystemProfile(32.0, 200.0, 12.0, "Test GPU", "strong", "test");
        OllamaService.OllamaModelRecommendation modelRecommendation = OllamaService.GetModelRecommendation("nomic-embed-text", 0L, profile);
        Assert.False(modelRecommendation.RecommendedForThisPc);
        Assert.False(modelRecommendation.AireFriendly);
        Assert.Equal("specialized", modelRecommendation.SummaryLabel);
    }

    [Fact]
    public void GetModelRecommendation_FlagsDiskPressure()
    {
        OllamaService.OllamaSystemProfile profile = new OllamaService.OllamaSystemProfile(32.0, 5.0, 12.0, "Test GPU", "strong", "test");
        OllamaService.OllamaModelRecommendation modelRecommendation = OllamaService.GetModelRecommendation("gemma3:4b", 0L, profile);
        Assert.False(modelRecommendation.RecommendedForThisPc);
        Assert.True(modelRecommendation.DiskSpaceLikelyInsufficient);
        Assert.Equal("needs more disk", modelRecommendation.SummaryLabel);
    }

    [Fact]
    public void FormatHardwareSummary_IncludesGpuNameWhenAvailable()
    {
        OllamaService.OllamaSystemProfile profile = new OllamaService.OllamaSystemProfile(32.0, 251.3, 8.0, "NVIDIA GeForce RTX 3050", "strong", "test");
        string actualString = OllamaService.FormatHardwareSummary(profile);
        Assert.Contains("32 GB RAM", actualString);
        Assert.Contains("8 GB VRAM (NVIDIA GeForce RTX 3050)", actualString);
        Assert.Contains("GB free disk", actualString);
    }

    [Fact]
    public async Task PullModelAsync_ReportsProgressFromStreamingResponse()
    {
        using var server = new OllamaHttpServer((method, path, _) =>
        {
            if (method == "POST" && path == "/api/pull")
            {
                return OllamaHttpServer.Lines(200,
                [
                    """{"status":"pulling manifest","total":100,"completed":25}""",
                    """{"status":"downloading","total":100,"completed":100}"""
                ]);
            }

            return OllamaHttpServer.Text(404, "missing");
        });

        var progress = new List<OllamaService.OllamaPullProgress>();
        await _service.PullModelAsync("qwen3:4b", server.BaseUrl, new Progress<OllamaService.OllamaPullProgress>(p => progress.Add(p)), CancellationToken.None);

        Assert.Equal(2, progress.Count);
        Assert.Equal("pulling manifest", progress[0].Status);
        Assert.Equal(100, progress[1].Completed);
    }

    [Fact]
    public async Task PullModelAsync_ThrowsReadableError_WhenServerRejectsRequest()
    {
        using var server = new OllamaHttpServer((method, path, _) =>
            method == "POST" && path == "/api/pull"
                ? OllamaHttpServer.Text(500, "bad pull")
                : OllamaHttpServer.Text(404, "missing"));

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() =>
            _service.PullModelAsync("qwen3:4b", server.BaseUrl, null, CancellationToken.None));

        Assert.Contains("Failed to pull model: bad pull", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DeleteModelAsync_UsesDeleteEndpoint_AndThrowsReadableError()
    {
        var deleteCalls = 0;
        using var server = new OllamaHttpServer((method, path, _) =>
        {
            if (method == "DELETE" && path == "/api/delete")
            {
                deleteCalls++;
                return deleteCalls == 1
                    ? OllamaHttpServer.Text(200, "ok")
                    : OllamaHttpServer.Text(500, "delete failed");
            }

            return OllamaHttpServer.Text(404, "missing");
        });

        await _service.DeleteModelAsync("qwen3:4b", server.BaseUrl, CancellationToken.None);

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() =>
            _service.DeleteModelAsync("qwen3:4b", server.BaseUrl, CancellationToken.None));

        Assert.Contains("Failed to delete model: delete failed", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(2, deleteCalls);
    }

    [Fact]
    public void IsOllamaInPath_SeesExecutableInPathEnvironment()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "aire-ollama-path-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        string exePath = Path.Combine(tempDir, "ollama.exe");
        File.WriteAllText(exePath, string.Empty);
        string? originalPath = Environment.GetEnvironmentVariable("PATH");

        try
        {
            Environment.SetEnvironmentVariable("PATH", tempDir);
            Assert.True(OllamaService.IsOllamaInPath());
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", originalPath);
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public void IsOllamaServiceRunning_ReturnsFalse_WhenScCommandCannotFindService()
    {
        Assert.False(OllamaService.IsOllamaServiceRunning());
    }

    private sealed class OllamaHttpServer : IDisposable
    {
        private readonly TcpListener _listener;
        private readonly Func<string, string, string, Response> _handler;
        private readonly Task _serveLoop;

        public OllamaHttpServer(Func<string, string, string, Response> handler)
        {
            _handler = handler;
            _listener = new TcpListener(IPAddress.Loopback, 0);
            _listener.Start();
            int port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            BaseUrl = $"http://127.0.0.1:{port}";
            _serveLoop = Task.Run(ServeAsync);
        }

        public string BaseUrl { get; }

        public static Response Text(int statusCode, string text) =>
            new(statusCode, "text/plain", Encoding.UTF8.GetBytes(text));

        public static Response Lines(int statusCode, IEnumerable<string> lines) =>
            new(statusCode, "application/x-ndjson", Encoding.UTF8.GetBytes(string.Join("\n", lines) + "\n"));

        private async Task ServeAsync()
        {
            try
            {
                while (true)
                {
                    using TcpClient client = await _listener.AcceptTcpClientAsync();
                    using NetworkStream stream = client.GetStream();
                    using StreamReader reader = new StreamReader(stream, leaveOpen: true);

                    string? requestLine = await reader.ReadLineAsync();
                    if (requestLine == null)
                        continue;

                    string[] parts = requestLine.Split(' ');
                    string method = parts[0];
                    string path = parts[1];
                    int contentLength = 0;

                    string? line;
                    while (!string.IsNullOrEmpty(line = await reader.ReadLineAsync()))
                    {
                        if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                            contentLength = int.Parse(line[15..].Trim());
                    }

                    string body = string.Empty;
                    if (contentLength > 0)
                    {
                        char[] buffer = new char[contentLength];
                        int read = 0;
                        while (read < contentLength)
                            read += await reader.ReadAsync(buffer, read, contentLength - read);
                        body = new string(buffer);
                    }

                    Response response = _handler(method, path, body);
                    string header =
                        $"HTTP/1.1 {response.StatusCode} {(response.StatusCode == 200 ? "OK" : "Error")}\r\n" +
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
