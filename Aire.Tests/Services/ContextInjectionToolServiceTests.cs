using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Aire.Services;
using Aire.Services.Tools;
using Xunit;

namespace Aire.Tests.Services;

public sealed class ContextInjectionToolServiceTests
{
    private readonly ContextInjectionToolService _svc = new();

    private static ToolCallRequest MakeRequest(string type, string? extraParam = null, string? extraValue = null)
    {
        string json;
        if (extraParam != null && extraValue != null)
        {
            var escapedValue = extraValue.Replace("\\", "\\\\").Replace("\"", "\\\"");
            json = $"{{\"type\":\"{type}\",\"{extraParam}\":\"{escapedValue}\"}}";
        }
        else
            json = $"{{\"type\":\"{type}\"}}";

        return new ToolCallRequest
        {
            Tool = "request_context",
            Parameters = JsonDocument.Parse(json).RootElement,
            RawJson = json
        };
    }

    [Fact]
    public async Task ExecuteAsync_UnknownType_ReturnsError()
    {
        var result = await _svc.ExecuteAsync(MakeRequest("unknown"));
        Assert.Contains("Unknown context type", result.TextResult);
        Assert.Contains("file", result.TextResult);
        Assert.Contains("url", result.TextResult);
    }

    [Fact]
    public async Task ExecuteAsync_Datetime_ReturnsCurrentTime()
    {
        var result = await _svc.ExecuteAsync(MakeRequest("datetime"));
        Assert.Contains("Current date and time", result.TextResult);
        Assert.Contains(System.DateTime.Now.ToString("yyyy-MM-dd"), result.TextResult);
    }

    [Fact]
    public async Task ExecuteAsync_Date_ReturnsCurrentTime()
    {
        var result = await _svc.ExecuteAsync(MakeRequest("date"));
        Assert.Contains("Current date and time", result.TextResult);
    }

    [Fact]
    public async Task ExecuteAsync_Time_ReturnsCurrentTime()
    {
        var result = await _svc.ExecuteAsync(MakeRequest("time"));
        Assert.Contains("Current date and time", result.TextResult);
    }

    [Fact]
    public async Task ExecuteAsync_Environment_ReturnsSystemInfo()
    {
        var result = await _svc.ExecuteAsync(MakeRequest("environment"));
        Assert.Contains("OS:", result.TextResult);
        Assert.Contains("Machine:", result.TextResult);
        Assert.Contains(".NET:", result.TextResult);
        Assert.Contains("Processors:", result.TextResult);
    }

    [Fact]
    public async Task ExecuteAsync_Clipboard_ReturnsResult()
    {
        var result = await _svc.ExecuteAsync(MakeRequest("clipboard"));
        Assert.NotNull(result.TextResult);
    }

    [Fact]
    public async Task ExecuteAsync_NullType_ReturnsUnknownError()
    {
        var json = "{}";
        var request = new ToolCallRequest
        {
            Tool = "request_context",
            Parameters = JsonDocument.Parse(json).RootElement,
            RawJson = json
        };
        var result = await _svc.ExecuteAsync(request);
        Assert.Contains("Unknown context type", result.TextResult);
    }

    [Fact]
    public async Task ExecuteAsync_File_MissingPath_ReturnsError()
    {
        var result = await _svc.ExecuteAsync(MakeRequest("file"));
        Assert.Contains("path", result.TextResult);
    }

    [Fact]
    public async Task ExecuteAsync_File_NonexistentFile_ReturnsNotFound()
    {
        var result = await _svc.ExecuteAsync(MakeRequest("file", "path", "/nonexistent/file.txt"));
        Assert.Contains("not found", result.TextResult);
    }

    [Fact]
    public async Task ExecuteAsync_File_ExistingFile_ReturnsContent()
    {
        var path = Path.Combine(Path.GetTempPath(), $"aire-ctx-test-{System.Guid.NewGuid():N}.txt");
        try
        {
            await File.WriteAllTextAsync(path, "hello world from test");
            var result = await _svc.ExecuteAsync(MakeRequest("file", "path", path));
            Assert.Contains("hello world from test", result.TextResult);
            Assert.Contains("File:", result.TextResult);
        }
        finally
        {
            try { File.Delete(path); } catch { }
        }
    }

    [Fact]
    public async Task ExecuteAsync_File_BinaryExtension_Skips()
    {
        var path = Path.Combine(Path.GetTempPath(), $"aire-ctx-test-{System.Guid.NewGuid():N}.exe");
        try
        {
            await File.WriteAllTextAsync(path, "fake binary");
            var result = await _svc.ExecuteAsync(MakeRequest("file", "path", path));
            Assert.Contains("Binary file skipped", result.TextResult);
        }
        finally
        {
            try { File.Delete(path); } catch { }
        }
    }

    [Fact]
    public async Task ExecuteAsync_Url_MissingUrl_ReturnsError()
    {
        var result = await _svc.ExecuteAsync(MakeRequest("url"));
        Assert.Contains("url", result.TextResult);
    }

    [Fact]
    public async Task ExecuteAsync_Url_InvalidUrl_ReturnsError()
    {
        var result = await _svc.ExecuteAsync(MakeRequest("url", "url", "not-a-url"));
        Assert.Contains("Invalid URL", result.TextResult);
    }

    [Fact]
    public async Task ExecuteAsync_Url_InvalidScheme_ReturnsError()
    {
        var result = await _svc.ExecuteAsync(MakeRequest("url", "url", "ftp://example.com"));
        Assert.Contains("Invalid URL", result.TextResult);
    }

    [Fact]
    public async Task ExecuteAsync_Url_NonexistentDomain_ReturnsError()
    {
        var result = await _svc.ExecuteAsync(MakeRequest("url", "url", "https://this-domain-does-not-exist-12345.example.com"));
        Assert.Contains("HTTP error", result.TextResult);
    }
}
