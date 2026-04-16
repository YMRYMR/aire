using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Aire.Services.Mcp;

namespace Aire.Services.Policies
{
    /// <summary>
    /// Evaluates the persisted auto-accept settings used to decide whether a tool can run without prompting.
    /// </summary>
    public sealed class ToolAutoAcceptPolicyService
    {
        private readonly Func<Task<string?>> _loadSettingsJsonAsync;
        private readonly Func<Task<IReadOnlyList<string>?>> _loadRuntimeToolsAsync;

        /// <summary>
        /// Creates the policy service with a callback that returns the current serialized settings payload.
        /// </summary>
        /// <param name="loadSettingsJsonAsync">Callback used to load the saved auto-accept settings JSON.</param>
        public ToolAutoAcceptPolicyService(Func<Task<string?>> loadSettingsJsonAsync)
            : this(loadSettingsJsonAsync, null)
        {
        }

        /// <summary>
        /// Creates the policy service with a callback that returns the current serialized settings payload.
        /// </summary>
        /// <param name="loadSettingsJsonAsync">Callback used to load the saved auto-accept settings JSON.</param>
        /// <param name="loadRuntimeToolsAsync">Optional callback that returns runtime tool names to treat as auto-accepted.</param>
        public ToolAutoAcceptPolicyService(
            Func<Task<string?>> loadSettingsJsonAsync,
            Func<Task<IReadOnlyList<string>?>>? loadRuntimeToolsAsync)
        {
            _loadSettingsJsonAsync = loadSettingsJsonAsync;
            _loadRuntimeToolsAsync = loadRuntimeToolsAsync ?? LoadInstalledMcpToolNamesAsync;
        }

        /// <summary>
        /// Checks whether a tool is allowed to run automatically under the saved policy.
        /// </summary>
        /// <param name="toolName">Canonical tool name being evaluated.</param>
        /// <param name="cachedJson">Optional preloaded settings JSON to avoid re-reading it for repeated checks.</param>
        /// <returns><see langword="true"/> when the policy allows the tool to auto-run.</returns>
        public async Task<bool> IsAutoAcceptedAsync(string toolName, string? cachedJson = null)
        {
            try
            {
                toolName = ToolExecutionService.NormalizeToolName(toolName);
                var json = cachedJson ?? await _loadSettingsJsonAsync();
                if (string.IsNullOrEmpty(json))
                    return await IsRuntimeToolAutoAcceptedAsync(toolName);

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (!root.TryGetProperty("Enabled", out var enabledProp) || !enabledProp.GetBoolean())
                    return false;

                if (root.TryGetProperty("AllowedTools", out var allowedToolsProp) &&
                    allowedToolsProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var tool in allowedToolsProp.EnumerateArray())
                    {
                        var saved = tool.GetString();
                        if (saved == toolName || AreEquivalentToolNames(saved, toolName))
                            return true;
                    }
                }

                if (IsMouseTool(toolName) &&
                    root.TryGetProperty("AllowMouseTools", out var mouseProp) &&
                    mouseProp.GetBoolean())
                {
                    return true;
                }

                if (IsKeyboardTool(toolName) &&
                    root.TryGetProperty("AllowKeyboardTools", out var keyboardProp) &&
                    keyboardProp.GetBoolean())
                {
                    return true;
                }

                return await IsRuntimeToolAutoAcceptedAsync(toolName);
            }
            catch
            {
                return false;
            }
        }

        private async Task<bool> IsRuntimeToolAutoAcceptedAsync(string toolName)
        {
            if (string.IsNullOrWhiteSpace(toolName))
                return false;

            var runtimeTools = await _loadRuntimeToolsAsync().ConfigureAwait(false);
            if (runtimeTools == null || runtimeTools.Count == 0)
                return false;

            return runtimeTools.Contains(toolName, StringComparer.OrdinalIgnoreCase);
        }

        private static Task<IReadOnlyList<string>?> LoadInstalledMcpToolNamesAsync()
            => Task.FromResult<IReadOnlyList<string>?>(McpManager.Instance
                .GetAllTools()
                .Select(tool => tool.Name)
                .ToList());

        /// <summary>
        /// Treats known aliases as equivalent so saved policy entries survive naming variations.
        /// </summary>
        /// <param name="left">Saved tool name from policy.</param>
        /// <param name="right">Incoming tool name being evaluated.</param>
        /// <returns><see langword="true"/> when both names should be treated as the same tool.</returns>
        private static bool AreEquivalentToolNames(string? left, string right)
        {
            if (string.IsNullOrWhiteSpace(left))
                return false;

            return (left, right) switch
            {
                ("write_file", "write_to_file") => true,
                ("write_to_file", "write_file") => true,
                ("list_directory", "list_files") => true,
                ("list_files", "list_directory") => true,
                _ => false
            };
        }

        /// <summary>
        /// Determines whether a tool belongs to the mouse-input family.
        /// </summary>
        private static bool IsMouseTool(string toolName)
            => toolName.StartsWith("mouse_", StringComparison.Ordinal)
            || toolName is "move_mouse" or "click" or "scroll" or "take_screenshot";

        /// <summary>
        /// Determines whether a tool belongs to the keyboard-input family.
        /// </summary>
        private static bool IsKeyboardTool(string toolName)
            => toolName.StartsWith("keyboard_", StringComparison.Ordinal)
            || toolName == "type_text";
    }
}
