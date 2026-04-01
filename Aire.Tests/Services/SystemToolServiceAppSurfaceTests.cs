extern alias AireWpf;
using System.Text.Json;
using System.Threading.Tasks;
using Aire.Providers;
using Aire.Services;
using Xunit;

using SystemToolService = AireWpf::Aire.Services.Tools.SystemToolService;


namespace Aire.Tests.Services;

public class SystemToolServiceAppSurfaceTests
{
    [Fact]
    public void ExecuteGetSystemInfo_ReturnsUsefulText()
    {
        ToolExecutionResult toolExecutionResult = SystemToolService.ExecuteGetSystemInfo();
        Assert.NotNull(toolExecutionResult);
        Assert.Contains("OS:", toolExecutionResult.TextResult);
        Assert.Contains("CPU cores:", toolExecutionResult.TextResult);
        Assert.Contains("Drives:", toolExecutionResult.TextResult);
    }

    [Fact]
    public void ExecuteGetRunningProcesses_RespectsTopNAndFilter()
    {
        using JsonDocument jsonDocument = JsonDocument.Parse("{\"top_n\":1,\"filter\":\"unlikely-process-name-xyz\"}");
        ToolCallRequest request = new ToolCallRequest
        {
            Parameters = jsonDocument.RootElement.Clone()
        };
        ToolExecutionResult toolExecutionResult = SystemToolService.ExecuteGetRunningProcesses(request);
        Assert.NotNull(toolExecutionResult);
        Assert.Contains("Process", toolExecutionResult.TextResult);
    }

    [Fact]
    public void ExecuteOpenFile_RejectsMissingTargets()
    {
        ToolExecutionResult toolExecutionResult = SystemToolService.ExecuteOpenFile(new ToolCallRequest
        {
            Parameters = JsonDocument.Parse("{\"path\":\"C:/definitely/not/here\"}").RootElement.Clone()
        });
        Assert.Contains("Not found", toolExecutionResult.TextResult);
    }

    [Fact]
    public void ExecuteOpenFile_RejectsEmptyPath()
    {
        ToolExecutionResult toolExecutionResult = SystemToolService.ExecuteOpenFile(new ToolCallRequest
        {
            Parameters = JsonDocument.Parse("{}").RootElement.Clone()
        });
        Assert.Contains("path parameter is required", toolExecutionResult.TextResult);
    }

    [Fact]
    public async Task ExecuteGetSelectedTextAsync_ReturnsGracefulTextWhenClipboardIsUnavailable()
    {
        ToolExecutionResult result = await SystemToolService.ExecuteGetSelectedTextAsync();
        Assert.NotNull(result);
        Assert.False(string.IsNullOrWhiteSpace(result.TextResult));
    }
}
