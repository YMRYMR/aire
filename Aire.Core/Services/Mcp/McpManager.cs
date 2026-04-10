using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Aire.Services;

namespace Aire.Services.Mcp
{
    /// <summary>
    /// Singleton that owns all MCP server connections.
    /// Call StartAllAsync() with a list of enabled configs at startup.
    /// </summary>
    public sealed class McpManager
    {
        public static readonly McpManager Instance = new();

        private readonly Dictionary<string, McpClient>   _clients      = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string>      _toolToServer = new(StringComparer.OrdinalIgnoreCase);
        private volatile List<McpToolDefinition>         _toolCache    = new();
        private readonly SemaphoreSlim                   _lock         = new(1, 1);

        private McpManager() { }

        // ── Startup ───────────────────────────────────────────────────────────

        public async Task StartAllAsync(IEnumerable<McpServerConfig> configs, CancellationToken ct = default)
        {
            var allTools = new List<McpToolDefinition>();

            foreach (var config in configs.Where(c => c.IsEnabled))
            {
                try
                {
                    var client = new McpClient(config);
                    await client.StartAsync();
                    using var initCts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                    await client.InitializeAsync(initCts.Token);
                    var tools = await client.ListToolsAsync(initCts.Token);

                    await _lock.WaitAsync(ct);
                    try
                    {
                        _clients[config.Name] = client;
                        foreach (var t in tools)
                            _toolToServer[t.Name] = config.Name;
                        allTools.AddRange(tools);
                    }
                    finally { _lock.Release(); }
                }
                catch (Exception ex)
                {
                    AppLogger.Warn(nameof(McpManager), $"Failed to start '{config.Name}'", ex);
                }
            }

            _toolCache = allTools;
        }

        public async Task StartSingleAsync(McpServerConfig config)
        {
            try
            {
                await _lock.WaitAsync();
                try
                {
                    if (_clients.TryGetValue(config.Name, out var old))
                    {
                        old.Stop();
                        old.Dispose();
                        _clients.Remove(config.Name);
                    }
                }
                finally { _lock.Release(); }

                var client = new McpClient(config);
                await client.StartAsync();
                using var initCts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                await client.InitializeAsync(initCts.Token);
                var tools = await client.ListToolsAsync(initCts.Token);

                await _lock.WaitAsync();
                try
                {
                    _clients[config.Name] = client;
                    foreach (var t in tools)
                        _toolToServer[t.Name] = config.Name;
                    var updated = new List<McpToolDefinition>(_toolCache);
                    updated.RemoveAll(t => t.ServerName == config.Name);
                    updated.AddRange(tools);
                    _toolCache = updated;
                }
                finally { _lock.Release(); }
            }
            catch (Exception ex)
            {
                AppLogger.Warn($"{nameof(McpManager)}.StartSingleAsync", $"Failed to start '{config.Name}'", ex);
                throw;
            }
        }

        public void StopSingle(string serverName)
        {
            _lock.Wait();
            try
            {
                if (_clients.TryGetValue(serverName, out var client))
                {
                    client.Stop();
                    client.Dispose();
                    _clients.Remove(serverName);
                    var updated = new List<McpToolDefinition>(_toolCache);
                    updated.RemoveAll(t => t.ServerName == serverName);
                    _toolCache = updated;
                    foreach (var key in _toolToServer.Where(kv => kv.Value == serverName).Select(kv => kv.Key).ToList())
                        _toolToServer.Remove(key);
                }
            }
            finally { _lock.Release(); }
        }

        public async Task StopAllAsync()
        {
            await _lock.WaitAsync();
            try
            {
                foreach (var client in _clients.Values)
                {
                    client.Stop();
                    client.Dispose();
                }
                _clients.Clear();
                _toolToServer.Clear();
                _toolCache = new();
            }
            finally { _lock.Release(); }
        }

        // ── Tool access ───────────────────────────────────────────────────────

        public IReadOnlyList<McpToolDefinition> GetAllTools() => _toolCache;

        public bool IsToolMcp(string toolName) => _toolToServer.ContainsKey(toolName);

        public string? GetServerNameForTool(string toolName) =>
            _toolToServer.TryGetValue(toolName, out var s) ? s : null;

        // ── Execution ─────────────────────────────────────────────────────────

        public async Task<McpCallResult> ExecuteToolAsync(
            string toolName, JsonElement args, CancellationToken ct = default)
        {
            var serverName = GetServerNameForTool(toolName);
            if (serverName == null)
                return new McpCallResult { IsError = true, Text = $"MCP tool '{toolName}' not found." };

            McpClient? client;
            await _lock.WaitAsync(ct);
            try { _clients.TryGetValue(serverName, out client); }
            finally { _lock.Release(); }

            if (client == null || !client.IsRunning)
                return new McpCallResult { IsError = true, Text = $"MCP server '{serverName}' is not running." };

            return await client.CallToolAsync(toolName, args, ct);
        }

        // ── Status ────────────────────────────────────────────────────────────

        public bool IsServerRunning(string serverName) =>
            _clients.TryGetValue(serverName, out var c) && c.IsRunning;
    }
}
