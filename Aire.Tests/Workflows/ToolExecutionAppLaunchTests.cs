using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Aire.Services;
using Xunit;

namespace Aire.Tests.Workflows;

public class ToolExecutionAppLaunchTests
{
    private readonly ToolExecutionService _svc = new ToolExecutionService(new FileSystemService(), new CommandExecutionService());

    private static readonly Regex PidRegex = new Regex("\\(PID:\\s*(\\d+)\\)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly string SampleImagePath;

    private static ToolCallRequest BuildExecRequest(string command)
    {
        return new ToolCallRequest
        {
            Tool = "execute_command",
            Parameters = JsonDocument.Parse("{\"tool\":\"execute_command\",\"command\":" + JsonSerializer.Serialize(command) + "}").RootElement.Clone(),
            RawJson = string.Empty
        };
    }

    private static List<int> ExtractProcessIds(string text)
    {
        List<int> list = new List<int>();
        foreach (Match item in PidRegex.Matches(text))
        {
            if (int.TryParse(item.Groups[1].Value, out var result))
            {
                list.Add(result);
            }
        }
        return list;
    }

    private static void CloseLaunchedProcesses(IEnumerable<int> processIds)
    {
        foreach (int item in processIds.Distinct())
        {
            try
            {
                using Process process = Process.GetProcessById(item);
                if (!process.HasExited)
                {
                    if (process.MainWindowHandle == IntPtr.Zero)
                    {
                        goto IL_005e;
                    }
                    process.CloseMainWindow();
                    if (!process.WaitForExit(2000))
                    {
                        goto IL_005e;
                    }
                }
                goto end_IL_0020;
IL_005e:
                process.Kill(entireProcessTree: true);
                process.WaitForExit(2000);
end_IL_0020:;
            }
            catch
            {
            }
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ExecuteCommand_Notepad_LaunchesAndReturnsSuccess()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        List<int> launchedProcessIds = new List<int>();
        try
        {
            ToolExecutionResult result = await _svc.ExecuteAsync(BuildExecRequest("notepad"));
            launchedProcessIds = ExtractProcessIds(result.TextResult);
            Assert.NotNull(result);
            Assert.NotNull(result.TextResult);
            Assert.True(result.TextResult.Contains("opened", StringComparison.OrdinalIgnoreCase) || result.TextResult.Contains("launched", StringComparison.OrdinalIgnoreCase) || result.TextResult.Contains("completed", StringComparison.OrdinalIgnoreCase), "Unexpected result: " + result.TextResult);
        }
        finally
        {
            CloseLaunchedProcesses(launchedProcessIds);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ExecuteCommand_GimpWithFile_ReturnsResultNotException()
    {
        List<int> launchedProcessIds = new List<int>();
        try
        {
            ToolExecutionResult result = await _svc.ExecuteAsync(BuildExecRequest("gimp \"" + SampleImagePath + "\""));
            launchedProcessIds = ExtractProcessIds(result.TextResult);
            Assert.NotNull(result);
            Assert.NotNull(result.TextResult);
        }
        finally
        {
            CloseLaunchedProcesses(launchedProcessIds);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ExecuteCommand_GimpWithFile_LaunchesGimpWhenInstalled()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        string gimpBin = "C:\\Program Files\\GIMP 2\\bin";
        if (!Directory.Exists(gimpBin) || !Directory.EnumerateFiles(gimpBin, "gimp*.exe").Any())
        {
            return;
        }
        List<int> launchedProcessIds = new List<int>();
        try
        {
            ToolExecutionResult result = await _svc.ExecuteAsync(BuildExecRequest("gimp \"" + SampleImagePath + "\""));
            launchedProcessIds = ExtractProcessIds(result.TextResult);
            Assert.NotNull(result);
            Assert.True(result.TextResult.Contains("opened", StringComparison.OrdinalIgnoreCase) || result.TextResult.Contains("launched", StringComparison.OrdinalIgnoreCase), "Unexpected result: " + result.TextResult);
            Assert.Contains("gimp", result.TextResult, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            CloseLaunchedProcesses(launchedProcessIds);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ExecuteCommand_FallsBackToDefaultAppWhenAppUnknown()
    {
        string existingFile = SampleImagePath;
        if (!File.Exists(existingFile))
        {
            return;
        }
        List<int> launchedProcessIds = new List<int>();
        try
        {
            ToolExecutionResult result = await _svc.ExecuteAsync(BuildExecRequest("unknown_app_xyz \"" + existingFile + "\""));
            launchedProcessIds = ExtractProcessIds(result.TextResult);
            Assert.NotNull(result);
        }
        finally
        {
            CloseLaunchedProcesses(launchedProcessIds);
        }
    }

    static ToolExecutionAppLaunchTests()
    {
        string repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        SampleImagePath = Path.Combine(repoRoot, "aire.png");
    }
}
