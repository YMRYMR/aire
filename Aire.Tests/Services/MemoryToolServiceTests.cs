using System.Text.Json;
using Aire.Services;
using Aire.Services.Tools;
using Xunit;

namespace Aire.Tests.Services;

public class MemoryToolServiceTests
{
    private readonly MemoryToolService _service = new();

    [Fact]
    public void ExecuteRemember_BlankKey_ReturnsError()
    {
        var request = CreateRequest(new { key = "   ", value = "secret" });
        var result  = _service.ExecuteRemember(request);
        Assert.Equal("Error: key is required.", result.TextResult);
    }

    [Fact]
    public void ExecuteSetReminder_MissingMessage_ReturnsError()
    {
        var request = CreateRequest(new { message = "" });
        var result  = _service.ExecuteSetReminder(request);
        Assert.Equal("Error: message is required.", result.TextResult);
    }

    [Fact]
    public void ExecuteSetReminder_NegativeDelay_ClampsToZero()
    {
        var request = CreateRequest(new { message = "ping", delay_minutes = "-2.5" });
        var result  = _service.ExecuteSetReminder(request);

        Assert.Contains("Reminder set for", result.TextResult);
        Assert.Contains("min from now", result.TextResult);
        Assert.DoesNotContain("(-", result.TextResult, StringComparison.Ordinal);
        Assert.Contains("\"ping\"", result.TextResult);
    }

    [Fact]
    public void ProtectJson_ThenUnprotectStoredJson_RoundTrips()
    {
        string protected_ = MemoryToolService.ProtectJson("{\"token\":\"value\"}");
        string restored   = MemoryToolService.UnprotectStoredJson(protected_);

        Assert.StartsWith("dpapi:", protected_, StringComparison.Ordinal);
        Assert.Equal("{\"token\":\"value\"}", restored);
    }

    [Fact]
    public void UnprotectStoredJson_InvalidProtectedPayload_ReturnsEmptyObject()
    {
        string result = MemoryToolService.UnprotectStoredJson("dpapi:not-base64");
        Assert.Equal("{}", result);
    }

    [Fact]
    public void UnprotectStoredJson_PlaintextValue_IsReturnedUnchanged()
    {
        string result = MemoryToolService.UnprotectStoredJson("{\"plain\":true}");
        Assert.Equal("{\"plain\":true}", result);
    }

    private static ToolCallRequest CreateRequest(object parameters)
    {
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(parameters));
        return new ToolCallRequest { Parameters = doc.RootElement.Clone() };
    }
}
