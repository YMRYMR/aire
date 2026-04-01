extern alias AireWpf;
using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Aire.Providers;
using Aire.Services;
using Xunit;

using SystemToolService = AireWpf::Aire.Services.Tools.SystemToolService;


namespace Aire.Tests.Services
{
    public class ToolExecutionServiceTests : TestBase
    {
        [Fact]
        public async Task ToolExecutionAndSystemTool_HelperPaths_Work()
        {
            ToolExecutionService service = new ToolExecutionService(new FileSystemService(), new CommandExecutionService());
            string tempDir = Path.Combine(Path.GetTempPath(), "aire-tool-exec-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            
            string readPath = Path.Combine(tempDir, "sample.txt");
            File.WriteAllText(readPath, "hello from file");
            
            string unsafePath = Path.Combine(tempDir, "launch.ps1");
            File.WriteAllText(unsafePath, "Write-Host test");
            
            try
            {
                ToolCallRequest listRequest = new ToolCallRequest
                {
                    Tool = "list_directory",
                    Parameters = JsonElementFor(new { path = tempDir })
                };
                var listResult = await service.ExecuteAsync(listRequest);
                Assert.Contains("sample.txt", listResult.TextResult);
                
                ToolCallRequest readRequest = new ToolCallRequest
                {
                    Tool = "read_file",
                    Parameters = JsonElementFor(new { path = readPath })
                };
                var readResult = await service.ExecuteAsync(readRequest);
                Assert.Contains("hello from file", readResult.TextResult);

                // Descriptions
                Assert.Equal("Read command output", service.GetToolDescription(new ToolCallRequest { Tool = "read_command_output" }));
                Assert.Equal("Write to: /tmp/out.txt", service.GetToolDescription(new ToolCallRequest { Tool = "write_to_file", Parameters = JsonElementFor(new { path = "/tmp/out.txt" }) }));
                
                // Paths
                Assert.Equal(tempDir, service.GetToolPath(listRequest));
                Assert.Equal(readPath, service.GetToolPath(readRequest));

                // SystemToolService static methods (now internal/public)
                var processes = SystemToolService.ExecuteGetRunningProcesses(new ToolCallRequest
                {
                    Parameters = JsonElementFor(new { top_n = 5, filter = Process.GetCurrentProcess().ProcessName })
                });
                Assert.Contains(Process.GetCurrentProcess().ProcessName, processes.TextResult, StringComparison.OrdinalIgnoreCase);

                var openMissing = SystemToolService.ExecuteOpenFile(new ToolCallRequest
                {
                    Parameters = JsonElementFor(new { path = Path.Combine(tempDir, "missing.txt") })
                });
                Assert.Contains("Not found", openMissing.TextResult);

                var openUnsafe = SystemToolService.ExecuteOpenFile(new ToolCallRequest
                {
                    Parameters = JsonElementFor(new { path = unsafePath })
                });
                Assert.Contains("refusing", openUnsafe.TextResult, StringComparison.OrdinalIgnoreCase);

                // IsPotentiallyExecutableTarget is internal
                Assert.True(SystemToolService.IsPotentiallyExecutableTarget(unsafePath));
                Assert.False(SystemToolService.IsPotentiallyExecutableTarget(readPath));
            }
            finally
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }

    }
}
