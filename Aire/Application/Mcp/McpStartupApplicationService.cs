using Aire.AppLayer.Abstractions;
using Aire.Services.Mcp;

namespace Aire.AppLayer.Mcp
{
    /// <summary>
    /// Application-layer use cases for MCP server configuration loading at startup.
    /// </summary>
    public sealed class McpStartupApplicationService
    {
        private readonly IMcpConfigRepository _mcpConfigs;

        /// <summary>
        /// Creates the service over the MCP configuration persistence boundary.
        /// </summary>
        /// <param name="mcpConfigs">Repository used to load persisted MCP server definitions.</param>
        public McpStartupApplicationService(IMcpConfigRepository mcpConfigs)
        {
            _mcpConfigs = mcpConfigs;
        }

        /// <summary>
        /// Loads all configured MCP servers and starts them via <see cref="McpManager"/>.
        /// </summary>
        public async Task StartAllAsync()
        {
            var configs = await _mcpConfigs.GetMcpServersAsync();
            _ = McpManager.Instance.StartAllAsync(configs);
        }
    }
}
