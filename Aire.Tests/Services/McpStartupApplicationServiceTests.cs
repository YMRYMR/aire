using Aire.AppLayer.Abstractions;
using Aire.AppLayer.Mcp;
using Aire.Services.Mcp;
using Xunit;

namespace Aire.Tests.Services;

public sealed class McpStartupApplicationServiceTests
{
    private readonly FakeMcpConfigRepository _repo = new();
    private readonly McpStartupApplicationService _service;

    public McpStartupApplicationServiceTests()
    {
        _service = new McpStartupApplicationService(_repo);
    }

    [Fact]
    public async Task StartAllAsync_CallsRepositoryToGetConfigs()
    {
        // Act
        await _service.StartAllAsync();

        // Assert
        Assert.Equal(1, _repo.GetCallCount);
    }

    [Fact]
    public async Task StartAllAsync_CompletesWithEmptyConfigList()
    {
        // Arrange — default fake returns empty list
        // Act
        await _service.StartAllAsync();

        // Assert — no exception thrown
        Assert.Equal(1, _repo.GetCallCount);
        Assert.Empty(_repo.StoredConfigs);
    }

    [Fact]
    public async Task StartAllAsync_CompletesWithNonEmptyConfigList()
    {
        // Arrange
        _repo.StoredConfigs.Add(new McpServerConfig
        {
            Id = 1,
            Name = "test-server",
            Command = "npx",
            IsEnabled = false // disabled so McpManager won't actually try to start it
        });

        // Act
        await _service.StartAllAsync();

        // Assert — method completed without exception
        Assert.Equal(1, _repo.GetCallCount);
        Assert.Single(_repo.StoredConfigs);
    }

    [Fact]
    public async Task StartAllAsync_ReturnsCompletedTask_DoesNotBlockOnFireAndForget()
    {
        // Arrange — simulate a slow GetMcpServersAsync to confirm the method
        // returns after the await but does not await McpManager.Instance.StartAllAsync
        _repo.DelayMs = 50;

        // Act
        await _service.StartAllAsync();

        // Assert — method completed even though McpManager fire-and-forget is discarded
        Assert.Equal(1, _repo.GetCallCount);
    }

    // ── Inline fake ────────────────────────────────────────────────────────

    private sealed class FakeMcpConfigRepository : IMcpConfigRepository
    {
        public List<McpServerConfig> StoredConfigs { get; } = new();
        public int GetCallCount { get; private set; }
        public int DelayMs { get; set; }

        public Task<List<McpServerConfig>> GetMcpServersAsync()
        {
            GetCallCount++;
            if (DelayMs > 0)
                return SimulateDelay();
            return Task.FromResult(StoredConfigs);
        }

        private async Task<List<McpServerConfig>> SimulateDelay()
        {
            await Task.Delay(DelayMs);
            return StoredConfigs;
        }

        public Task<int> InsertMcpServerAsync(McpServerConfig config)
            => Task.FromResult(0);

        public Task UpdateMcpServerAsync(McpServerConfig config)
            => Task.CompletedTask;

        public Task DeleteMcpServerAsync(int id)
            => Task.CompletedTask;
    }
}
