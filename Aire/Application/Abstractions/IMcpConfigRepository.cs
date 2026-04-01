using Aire.Services.Mcp;

namespace Aire.AppLayer.Abstractions
{
    /// <summary>
    /// Persistence boundary for MCP server configuration.
    /// </summary>
    public interface IMcpConfigRepository
    {
        Task<List<McpServerConfig>> GetMcpServersAsync();
        Task<int> InsertMcpServerAsync(McpServerConfig config);
        Task UpdateMcpServerAsync(McpServerConfig config);
        Task DeleteMcpServerAsync(int id);
    }
}
