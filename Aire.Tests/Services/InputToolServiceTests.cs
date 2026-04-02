using System.Text.Json;
using System.Threading.Tasks;
using Aire.Services;
using Aire.Services.Tools;
using Xunit;

namespace Aire.Tests.Services;

public sealed class InputToolServiceTests
{
    [Fact]
    public async Task ExecuteAsync_UnknownTool_ReturnsFriendlyError()
    {
        var service = new InputToolService();

        var result = await service.ExecuteAsync(new ToolCallRequest
        {
            Tool = "unknown_input_tool",
            Parameters = JsonDocument.Parse("{}").RootElement.Clone()
        });

        if (OperatingSystem.IsWindows())
            Assert.Equal("Unknown input tool: unknown_input_tool", result.TextResult);
        else
            Assert.Contains("only supported on Windows", result.TextResult);
    }

    [Fact]
    public async Task ExecuteAsync_KeyComboWithoutKeys_ReturnsValidationMessage()
    {
        var service = new InputToolService();

        var result = await service.ExecuteAsync(new ToolCallRequest
        {
            Tool = "key_combo",
            Parameters = JsonDocument.Parse("{}").RootElement.Clone()
        });

        if (OperatingSystem.IsWindows())
            Assert.Equal("key_combo: no keys specified.", result.TextResult);
        else
            Assert.Contains("only supported on Windows", result.TextResult);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidMouseCoordinates_FallBackToZeroes()
    {
        var service = new InputToolService();

        var result = await service.ExecuteAsync(new ToolCallRequest
        {
            Tool = "mouse_move",
            Parameters = JsonDocument.Parse("{\"x\":\"oops\"}").RootElement.Clone()
        });

        if (OperatingSystem.IsWindows())
            Assert.Equal("Mouse moved to (0, 0).", result.TextResult);
        else
            Assert.Contains("only supported on Windows", result.TextResult);
    }
}
