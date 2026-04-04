extern alias AireCore;
extern alias AireWpf;
using System;
using System.IO;
using System.Threading.Tasks;
using Aire.Data;
using Aire.Providers;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Aire.Tests.Providers;

public class ProviderFactoryAppAssemblyTests : IAsyncLifetime, IDisposable
{
    private readonly string _dbPath;

    private readonly DatabaseService _db;

    private readonly ProviderFactory _factory;

    public ProviderFactoryAppAssemblyTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"aire_test_{Guid.NewGuid():N}.db");
        _db = new DatabaseService(_dbPath);
        _factory = new ProviderFactory(_db);
    }

    public async Task InitializeAsync()
    {
        await _db.InitializeAsync();
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _db.Dispose();
        SqliteConnection.ClearAllPools();
        try
        {
            if (File.Exists(_dbPath))
            {
                File.Delete(_dbPath);
            }
        }
        catch
        {
        }
    }

    [Fact]
    public void CreateProvider_Ollama_ReturnsWpfOllamaProviderAndCachesByTypeAndId()
    {
        Provider providerConfig = new Provider
        {
            Id = 50,
            Type = "Ollama",
            Name = "Local",
            Model = "qwen",
            IsEnabled = true
        };
        IAiProvider aiProvider = _factory.CreateProvider(providerConfig);
        IAiProvider actual = _factory.CreateProvider(providerConfig);
        Assert.IsType<OllamaProvider>(aiProvider);
        Assert.Same(aiProvider, actual);
    }

    [Fact]
    public void GetMetadata_ReturnsWpfSpecificMetadataForAppProviders()
    {
        Assert.IsType<ClaudeAiProvider>(ProviderFactory.GetMetadata("Anthropic"));
        Assert.IsType<AireWpf::Aire.Providers.ClaudeWebProvider>(ProviderFactory.GetMetadata("ClaudeWeb"));
        Assert.IsType<PortableOllamaProvider>(ProviderFactory.GetMetadata("Ollama"));
    }

    [Fact]
    public async Task GetCurrentProviderAsync_ReturnsFirstEnabledProvider_AndNullWhenNoneEnabled()
    {
        int disabledId = await _db.InsertProviderAsync(new Provider
        {
            Name = "Disabled Test",
            Type = "OpenAI",
            ApiKey = "sk-disabled",
            Model = "gpt-4o",
            IsEnabled = false,
            Color = "#111111"
        });
        int enabledId = await _db.InsertProviderAsync(new Provider
        {
            Name = "Enabled Test",
            Type = "OpenAI",
            ApiKey = "sk-enabled",
            Model = "gpt-4o",
            IsEnabled = true,
            Color = "#222222"
        });
        IAiProvider byId = await _factory.GetCurrentProviderAsync(enabledId);
        Assert.NotNull(byId);
        Assert.Equal("OpenAI", byId.ProviderType);
        foreach (Provider provider in await _db.GetProvidersAsync())
        {
            provider.IsEnabled = false;
            await _db.UpdateProviderAsync(provider);
        }
        Assert.Null(await _factory.GetCurrentProviderAsync());
        Assert.True(disabledId > 0);
    }
}
