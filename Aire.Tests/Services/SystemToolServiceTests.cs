using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Aire.Services;
using Xunit;

namespace Aire.Tests.Services;

public class SystemToolServiceTests
{
    private static readonly Type ServiceType = Type.GetType("Aire.Services.Tools.SystemToolService, Aire.Core", throwOnError: true)!;

    [Fact]
    public void ExecuteGetSystemInfo_ReturnsExpectedSections()
    {
        string textResult = GetTextResult(ServiceType.GetMethod("ExecuteGetSystemInfo", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, null)!);

        Assert.Contains("OS:", textResult, StringComparison.Ordinal);
        Assert.Contains("Machine:", textResult, StringComparison.Ordinal);
        Assert.Contains("CPU cores:", textResult, StringComparison.Ordinal);
        Assert.Contains("Drives:", textResult, StringComparison.Ordinal);
    }

    [Fact]
    public void ExecuteGetRunningProcesses_UsesFilterAndTopNClamp()
    {
        string currentProcessName = Process.GetCurrentProcess().ProcessName;
        ToolCallRequest request = CreateRequest(new { filter = currentProcessName, top_n = 1 });

        string textResult = GetTextResult(ServiceType.GetMethod("ExecuteGetRunningProcesses", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, [request])!);

        Assert.Contains("Process", textResult, StringComparison.Ordinal);
        Assert.Contains(currentProcessName, textResult, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExecuteOpenFile_RequiresPath()
    {
        string textResult = GetTextResult(ServiceType.GetMethod("ExecuteOpenFile", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, [CreateRequest(new { path = "" })])!);

        Assert.Equal("Error: path parameter is required.", textResult);
    }

    [Fact]
    public void ExecuteOpenFile_ReturnsNotFound_ForMissingPath()
    {
        string missingPath = Path.Combine(Path.GetTempPath(), "missing-file-" + Guid.NewGuid().ToString("N"));

        string textResult = GetTextResult(ServiceType.GetMethod("ExecuteOpenFile", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, [CreateRequest(new { path = missingPath })])!);

        Assert.Equal($"Error: Not found: {missingPath}", textResult);
    }

    [Fact]
    public void ExecuteGetActiveWindow_ReturnsTextInsteadOfThrowing()
    {
        string textResult = GetTextResult(ServiceType.GetMethod("ExecuteGetActiveWindow", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, null)!);

        Assert.False(string.IsNullOrWhiteSpace(textResult));
    }

    [Fact]
    public async Task ExecuteGetSelectedTextAsync_ReturnsWindowsOnlyMessage()
    {
        var task = (Task)ServiceType.GetMethod("ExecuteGetSelectedTextAsync", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, null)!;
        await task;
        object result = task.GetType().GetProperty("Result")!.GetValue(task)!;

        Assert.Equal("Get selected text is only supported on Windows.", GetTextResult(result));
    }

    [Fact]
    public void ShowSystemNotification_DoesNotThrow()
    {
        Exception? ex = Record.Exception(() => ServiceType.GetMethod("ShowSystemNotification", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, ["Aire", "Test"]));

        Assert.Null(ex);
    }

    private static string GetTextResult(object toolExecutionResult)
        => (string)toolExecutionResult.GetType().GetProperty("TextResult")!.GetValue(toolExecutionResult)!;

    private static ToolCallRequest CreateRequest(object parameters)
    {
        using JsonDocument doc = JsonDocument.Parse(JsonSerializer.Serialize(parameters));
        return new ToolCallRequest { Parameters = doc.RootElement.Clone() };
    }
}
