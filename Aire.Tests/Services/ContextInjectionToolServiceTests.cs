using System.Text.Json;
using System.Threading.Tasks;
using Aire.Services;
using Aire.Services.Tools;
using Xunit;

namespace Aire.Tests.Services;

public sealed class ContextInjectionToolServiceTests
{
    private readonly ContextInjectionToolService _svc = new();

    private static ToolCallRequest MakeRequest(string type)
    {
        var json = $"{{\"type\":\"{type}\"}}";
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
        // Either empty or has content — just verify no crash.
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
}
