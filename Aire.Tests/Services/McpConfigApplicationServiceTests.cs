using Aire.AppLayer.Abstractions;
using Aire.AppLayer.Mcp;
using Aire.Services.Mcp;
using Xunit;

namespace Aire.Tests.Services;

public sealed class McpConfigApplicationServiceTests
{
    private readonly FakeMcpConfigRepository _repo = new();
    private readonly McpConfigApplicationService _service;

    public McpConfigApplicationServiceTests()
    {
        _service = new McpConfigApplicationService(_repo);
    }

    [Fact]
    public async Task GetMcpServersAsync_DelegatesToRepository()
    {
        // Arrange
        var config = new McpServerConfig { Id = 1, Name = "test-server" };
        _repo.StoredConfigs.Add(config);

        // Act
        var result = await _service.GetMcpServersAsync();

        // Assert
        Assert.Same(_repo.StoredConfigs, result);
        Assert.Single(result);
        Assert.Equal("test-server", result[0].Name);
    }

    [Fact]
    public async Task GetMcpServersAsync_ReturnsEmptyList_WhenNoneConfigured()
    {
        // Act
        var result = await _service.GetMcpServersAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task InsertMcpServerAsync_DelegatesToRepository()
    {
        // Arrange
        var config = new McpServerConfig { Name = "new-server", Command = "npx" };

        // Act
        var id = await _service.InsertMcpServerAsync(config);

        // Assert
        Assert.Equal(1, _repo.InsertCallCount);
        Assert.Equal(42, id);
    }

    [Fact]
    public async Task InsertMcpServerAsync_PassesConfigToRepository()
    {
        // Arrange
        var config = new McpServerConfig { Name = "observed-server", Command = "node" };
        McpServerConfig? captured = null;
        _repo.OnInsert = c => captured = c;

        // Act
        await _service.InsertMcpServerAsync(config);

        // Assert
        Assert.NotNull(captured);
        Assert.Same(config, captured);
    }

    [Fact]
    public async Task UpdateMcpServerAsync_DelegatesToRepository()
    {
        // Arrange
        var config = new McpServerConfig { Id = 5, Name = "updated-server" };

        // Act
        await _service.UpdateMcpServerAsync(config);

        // Assert
        Assert.Equal(1, _repo.UpdateCallCount);
    }

    [Fact]
    public async Task UpdateMcpServerAsync_PassesConfigToRepository()
    {
        // Arrange
        var config = new McpServerConfig { Id = 3, Name = "observed-update" };
        McpServerConfig? captured = null;
        _repo.OnUpdate = c => captured = c;

        // Act
        await _service.UpdateMcpServerAsync(config);

        // Assert
        Assert.NotNull(captured);
        Assert.Same(config, captured);
        Assert.Equal(3, captured.Id);
    }

    [Fact]
    public async Task DeleteMcpServerAsync_DelegatesToRepository()
    {
        // Act
        await _service.DeleteMcpServerAsync(7);

        // Assert
        Assert.Equal(1, _repo.DeleteCallCount);
    }

    [Fact]
    public async Task DeleteMcpServerAsync_PassesIdToRepository()
    {
        // Arrange
        int capturedId = 0;
        _repo.OnDelete = id => capturedId = id;

        // Act
        await _service.DeleteMcpServerAsync(99);

        // Assert
        Assert.Equal(99, capturedId);
    }

    // ── Inline fake ────────────────────────────────────────────────────────

    private sealed class FakeMcpConfigRepository : IMcpConfigRepository
    {
        public List<McpServerConfig> StoredConfigs { get; } = new();
        public int InsertCallCount { get; private set; }
        public int UpdateCallCount { get; private set; }
        public int DeleteCallCount { get; private set; }

        public Action<McpServerConfig>? OnInsert { get; set; }
        public Action<McpServerConfig>? OnUpdate { get; set; }
        public Action<int>? OnDelete { get; set; }

        public Task<List<McpServerConfig>> GetMcpServersAsync()
            => Task.FromResult(StoredConfigs);

        public Task<int> InsertMcpServerAsync(McpServerConfig config)
        {
            InsertCallCount++;
            OnInsert?.Invoke(config);
            return Task.FromResult(42);
        }

        public Task UpdateMcpServerAsync(McpServerConfig config)
        {
            UpdateCallCount++;
            OnUpdate?.Invoke(config);
            return Task.CompletedTask;
        }

        public Task DeleteMcpServerAsync(int id)
        {
            DeleteCallCount++;
            OnDelete?.Invoke(id);
            return Task.CompletedTask;
        }
    }
}
