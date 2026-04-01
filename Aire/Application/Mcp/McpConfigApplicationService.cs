using Aire.AppLayer.Abstractions;
using Aire.Services.Mcp;

namespace Aire.AppLayer.Mcp
{
    /// <summary>
    /// Application-layer use cases for managing MCP server configuration in the Settings UI.
    /// </summary>
    public sealed class McpConfigApplicationService
    {
        private readonly IMcpConfigRepository _mcpConfigs;

        /// <summary>
        /// Creates the service over the MCP configuration persistence boundary.
        /// </summary>
        /// <param name="mcpConfigs">Repository used to persist MCP server definitions.</param>
        public McpConfigApplicationService(IMcpConfigRepository mcpConfigs)
        {
            _mcpConfigs = mcpConfigs;
        }

        /// <summary>Loads all configured MCP servers.</summary>
        public Task<List<McpServerConfig>> GetMcpServersAsync()
            => _mcpConfigs.GetMcpServersAsync();

        /// <summary>Inserts a new MCP server and returns its generated id.</summary>
        public Task<int> InsertMcpServerAsync(McpServerConfig config)
            => _mcpConfigs.InsertMcpServerAsync(config);

        /// <summary>Updates an existing MCP server configuration.</summary>
        public Task UpdateMcpServerAsync(McpServerConfig config)
            => _mcpConfigs.UpdateMcpServerAsync(config);

        /// <summary>Deletes an MCP server by id.</summary>
        public Task DeleteMcpServerAsync(int id)
            => _mcpConfigs.DeleteMcpServerAsync(id);
    }
}
