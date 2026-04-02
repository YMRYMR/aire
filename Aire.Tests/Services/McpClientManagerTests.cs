using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Aire.Services.Mcp;
using Xunit;

namespace Aire.Tests.Services;

public sealed class McpClientManagerTests : IDisposable
{
    private readonly string _scriptPath;

    public McpClientManagerTests()
    {
        _scriptPath = Path.Combine(Path.GetTempPath(), $"aire-fake-mcp-{Guid.NewGuid():N}.ps1");
        File.WriteAllText(_scriptPath,
            """
            $stdin = [Console]::In
            while (($line = $stdin.ReadLine()) -ne $null) {
              if ([string]::IsNullOrWhiteSpace($line)) { continue }
              $req = $line | ConvertFrom-Json
              if (-not $req.id) { continue }
              switch ($req.method) {
                'initialize' {
                  $resp = @{
                    jsonrpc = '2.0'
                    id = $req.id
                    result = @{
                      protocolVersion = '2024-11-05'
                      capabilities = @{}
                      serverInfo = @{ name = 'FakeMcp'; version = '1.0.0' }
                    }
                  }
                }
                'tools/list' {
                  $resp = @{
                    jsonrpc = '2.0'
                    id = $req.id
                    result = @{
                      tools = @(
                        @{
                          name = 'fake_tool'
                          description = 'Fake tool'
                          inputSchema = @{ type = 'object' }
                        }
                      )
                    }
                  }
                }
                'tools/call' {
                  if ($req.params.name -eq 'boom') {
                    $resp = @{
                      jsonrpc = '2.0'
                      id = $req.id
                      error = @{
                        code = 123
                        message = 'Boom'
                      }
                    }
                  } else {
                    $resp = @{
                      jsonrpc = '2.0'
                      id = $req.id
                      result = @{
                        isError = $false
                        content = @(
                          @{ text = "ran $($req.params.name)" }
                        )
                      }
                    }
                  }
                }
                default {
                  $resp = @{
                    jsonrpc = '2.0'
                    id = $req.id
                    error = @{
                      code = -32601
                      message = 'Unknown method'
                    }
                  }
                }
              }

              $resp | ConvertTo-Json -Compress -Depth 10
            }
            """);
    }

    [Fact]
    public async Task McpClient_CanInitializeListToolsAndCallTool()
    {
        var client = new McpClient(CreateConfig("ClientTest"));
        try
        {
            await client.StartAsync();
            await client.InitializeAsync(CancellationToken.None);

            var tools = await client.ListToolsAsync(CancellationToken.None);
            using var emptyArgs = JsonDocument.Parse("{}");
            var result = await client.CallToolAsync("fake_tool", emptyArgs.RootElement.Clone(), CancellationToken.None);

            Assert.True(client.IsRunning);
            Assert.Equal("ClientTest", client.ServerName);
            Assert.Single(tools);
            Assert.Equal("fake_tool", tools[0].Name);
            Assert.False(result.IsError);
            Assert.Equal("ran fake_tool", result.Text);
        }
        finally
        {
            client.Dispose();
        }
    }

    [Fact]
    public async Task McpClient_PropagatesRpcErrors()
    {
        var client = new McpClient(CreateConfig("ClientError"));
        try
        {
            await client.StartAsync();
            await client.InitializeAsync(CancellationToken.None);
            using var emptyArgs = JsonDocument.Parse("{}");

            var ex = await Assert.ThrowsAsync<Exception>(() =>
                client.CallToolAsync("boom", emptyArgs.RootElement.Clone(), CancellationToken.None));

            Assert.Contains("MCP error 123: Boom", ex.Message, StringComparison.Ordinal);
        }
        finally
        {
            client.Dispose();
        }
    }

    [Fact]
    public async Task McpManager_StartExecuteAndStopSingle_Works()
    {
        var manager = McpManager.Instance;
        await manager.StopAllAsync();

        var config = CreateConfig("ManagerTest");
        try
        {
            await manager.StartSingleAsync(config);

            using var emptyArgs = JsonDocument.Parse("{}");
            var result = await manager.ExecuteToolAsync("fake_tool", emptyArgs.RootElement.Clone(), CancellationToken.None);

            Assert.True(manager.IsServerRunning("ManagerTest"));
            Assert.True(manager.IsToolMcp("fake_tool"));
            Assert.Equal("ManagerTest", manager.GetServerNameForTool("fake_tool"));
            Assert.Contains(manager.GetAllTools(), t => t.Name == "fake_tool" && t.ServerName == "ManagerTest");
            Assert.False(result.IsError);
            Assert.Equal("ran fake_tool", result.Text);

            manager.StopSingle("ManagerTest");

            Assert.False(manager.IsServerRunning("ManagerTest"));
            Assert.False(manager.IsToolMcp("fake_tool"));
            Assert.DoesNotContain(manager.GetAllTools(), t => t.ServerName == "ManagerTest");
        }
        finally
        {
            manager.StopSingle("ManagerTest");
            await manager.StopAllAsync();
        }
    }

    [Fact]
    public async Task McpManager_ReturnsReadableErrors_ForMissingOrStoppedTools()
    {
        var manager = McpManager.Instance;
        await manager.StopAllAsync();

        using var emptyArgs = JsonDocument.Parse("{}");
        var missing = await manager.ExecuteToolAsync("missing_tool", emptyArgs.RootElement.Clone(), CancellationToken.None);

        Assert.True(missing.IsError);
        Assert.Contains("not found", missing.Text, StringComparison.OrdinalIgnoreCase);

        await manager.StartSingleAsync(CreateConfig("StoppedServer"));
        manager.StopSingle("StoppedServer");

        var afterStop = await manager.ExecuteToolAsync("fake_tool", emptyArgs.RootElement.Clone(), CancellationToken.None);
        Assert.True(afterStop.IsError);
        Assert.Contains("not found", afterStop.Text, StringComparison.OrdinalIgnoreCase);

        await manager.StopAllAsync();
    }

    private McpServerConfig CreateConfig(string name) => new()
    {
        Name = name,
        Command = "powershell.exe",
        Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{_scriptPath}\"",
        WorkingDirectory = Path.GetTempPath(),
        IsEnabled = true
    };

    public void Dispose()
    {
        try { if (File.Exists(_scriptPath)) File.Delete(_scriptPath); } catch { }
    }
}
