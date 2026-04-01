using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Aire.Services.Mcp
{
    /// <summary>
    /// Manages one MCP server process: spawns it, drives the JSON-RPC 2.0 handshake,
    /// lists tools, and executes tool calls.  Thread-safe.
    /// </summary>
    public sealed class McpClient : IDisposable
    {
        private readonly McpServerConfig _config;
        private Process?      _process;
        private StreamWriter? _stdin;
        private StreamReader? _stdout;
        private readonly CancellationTokenSource _cts = new();
        private readonly SemaphoreSlim _writeLock = new(1, 1);
        private readonly ConcurrentDictionary<int, TaskCompletionSource<JsonElement>> _pending = new();
        private int  _nextId;
        private bool _initialized;
        private bool _disposed;

        public string ServerName => _config.Name;
        public bool   IsRunning  => _process is { HasExited: false };

        public McpClient(McpServerConfig config) => _config = config;

        // ── Startup ───────────────────────────────────────────────────────────

        public async Task StartAsync()
        {
            var command = _config.Command;
            var args    = _config.Arguments;

            if (OperatingSystem.IsWindows() &&
                (command.Equals("npx",     StringComparison.OrdinalIgnoreCase) ||
                 command.Equals("node",    StringComparison.OrdinalIgnoreCase) ||
                 command.Equals("python",  StringComparison.OrdinalIgnoreCase) ||
                 command.Equals("python3", StringComparison.OrdinalIgnoreCase)))
            {
                args    = $"/c {command} {args}";
                command = "cmd.exe";
            }

            var psi = new ProcessStartInfo
            {
                FileName               = command,
                Arguments              = args,
                UseShellExecute        = false,
                CreateNoWindow         = true,
                RedirectStandardInput  = true,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                WorkingDirectory       = string.IsNullOrWhiteSpace(_config.WorkingDirectory)
                                          ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
                                          : _config.WorkingDirectory,
            };

            foreach (var (k, v) in _config.EnvVars)
                psi.EnvironmentVariables[k] = v;

            _process = Process.Start(psi) ?? throw new InvalidOperationException($"Failed to start MCP server '{_config.Name}'.");
            _stdin   = _process.StandardInput;
            _stdout  = _process.StandardOutput;

            _ = Task.Run(ReadLoopAsync);
        }

        // ── Protocol handshake ────────────────────────────────────────────────

        public async Task InitializeAsync(CancellationToken ct = default)
        {
            var result = await SendRequestAsync(new McpRpcRequest
            {
                Method = "initialize",
                Params = new
                {
                    protocolVersion = "2024-11-05",
                    capabilities    = new { },
                    clientInfo      = new { name = "Aire", version = "1.0.0" }
                }
            }, ct);

            // Send the initialized notification (no id = fire and forget)
            await WriteLineAsync(JsonSerializer.Serialize(new McpRpcRequest
            {
                Method = "notifications/initialized"
            }));

            _initialized = true;
        }

        // ── Tool discovery ────────────────────────────────────────────────────

        public async Task<List<McpToolDefinition>> ListToolsAsync(CancellationToken ct = default)
        {
            if (!_initialized) throw new InvalidOperationException("Call InitializeAsync first.");

            var result = await SendRequestAsync(new McpRpcRequest { Method = "tools/list" }, ct);

            var tools = new List<McpToolDefinition>();
            if (result.TryGetProperty("tools", out var toolsEl))
            {
                foreach (var t in toolsEl.EnumerateArray())
                {
                    tools.Add(new McpToolDefinition
                    {
                        ServerName  = _config.Name,
                        Name        = t.TryGetProperty("name",        out var n) ? n.GetString() ?? "" : "",
                        Description = t.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "",
                        InputSchema = t.TryGetProperty("inputSchema",  out var s) ? s.Clone() : default,
                    });
                }
            }
            return tools;
        }

        // ── Tool execution ────────────────────────────────────────────────────

        public async Task<McpCallResult> CallToolAsync(string toolName, JsonElement args, CancellationToken ct = default)
        {
            if (!_initialized) throw new InvalidOperationException("MCP server not initialized.");

            var result = await SendRequestAsync(new McpRpcRequest
            {
                Method = "tools/call",
                Params = new { name = toolName, arguments = args }
            }, ct);

            var isError = result.TryGetProperty("isError", out var errEl) && errEl.GetBoolean();
            var text    = new System.Text.StringBuilder();

            if (result.TryGetProperty("content", out var content))
            {
                foreach (var block in content.EnumerateArray())
                {
                    if (block.TryGetProperty("text", out var textEl))
                        text.AppendLine(textEl.GetString());
                }
            }

            return new McpCallResult { IsError = isError, Text = text.ToString().Trim() };
        }

        // ── Internal JSON-RPC plumbing ────────────────────────────────────────

        private async Task<JsonElement> SendRequestAsync(McpRpcRequest request, CancellationToken ct = default)
        {
            var id = Interlocked.Increment(ref _nextId);
            request.Id = id;

            var tcs = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pending[id] = tcs;

            try
            {
                await WriteLineAsync(JsonSerializer.Serialize(request, new JsonSerializerOptions
                {
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                }));

                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct, _cts.Token);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(30));

                var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(Timeout.Infinite, timeoutCts.Token));
                if (completedTask != tcs.Task)
                    throw new TimeoutException($"MCP server '{_config.Name}' did not respond within 30 s.");

                return await tcs.Task;
            }
            finally
            {
                _pending.TryRemove(id, out _);
            }
        }

        private async Task WriteLineAsync(string line)
        {
            if (_stdin == null) return;
            await _writeLock.WaitAsync();
            try
            {
                await _stdin.WriteLineAsync(line);
                await _stdin.FlushAsync();
            }
            finally
            {
                _writeLock.Release();
            }
        }

        private async Task ReadLoopAsync()
        {
            try
            {
                while (!_cts.IsCancellationRequested && _stdout != null)
                {
                    var line = await _stdout.ReadLineAsync(_cts.Token);
                    if (line == null) break;

                    line = line.Trim();
                    if (string.IsNullOrEmpty(line)) continue;

                    McpRpcResponse? response;
                    try { response = JsonSerializer.Deserialize<McpRpcResponse>(line); }
                    catch { continue; }

                    if (response?.Id == null) continue;

                    if (_pending.TryRemove(response.Id.Value, out var tcs2))
                    {
                        if (response.Error != null)
                            tcs2.TrySetException(new Exception($"MCP error {response.Error.Code}: {response.Error.Message}"));
                        else
                            tcs2.TrySetResult(response.Result ?? default);
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch { /* process died — pending requests will time out */ }
            finally
            {
                foreach (var tcs2 in _pending.Values)
                    tcs2.TrySetException(new Exception($"MCP server '{_config.Name}' disconnected."));
                _pending.Clear();
            }
        }

        // ── Lifecycle ─────────────────────────────────────────────────────────

        public void Stop()
        {
            _cts.Cancel();
            try { _process?.Kill(entireProcessTree: true); } catch { }
            _initialized = false;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Stop();
            _writeLock.Dispose();
            _cts.Dispose();
            _process?.Dispose();
        }
    }
}
