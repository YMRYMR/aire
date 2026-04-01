extern alias AireCore;
extern alias AireWpf;
using System;
using System.IO;
using System.Threading.Tasks;
using Aire.Data;
using Aire.Providers;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Aire.Tests.Core;

public class ProviderFactoryTests : IAsyncLifetime, IDisposable
{
    private readonly string _dbPath;

    private readonly DatabaseService _db;

    private readonly ProviderFactory _factory;

    public ProviderFactoryTests()
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
    public void CreateProvider_OpenAI_ReturnsOpenAiProvider()
    {
        Provider providerConfig = new Provider
        {
            Id = 1,
            Type = "OpenAI",
            Model = "gpt-4o",
            ApiKey = "sk-test",
            Color = "#000000",
            Name = "Test"
        };
        IAiProvider aiProvider = _factory.CreateProvider(providerConfig);
        Assert.IsType<OpenAiProvider>(aiProvider);
        Assert.Equal("OpenAI", aiProvider.ProviderType);
    }

    [Fact]
    public void CreateProvider_Codex_ReturnsCodexProvider()
    {
        Provider providerConfig = new Provider
        {
            Id = 11,
            Type = "Codex",
            Model = "default",
            ApiKey = null,
            Color = "#000000",
            Name = "Codex"
        };
        IAiProvider aiProvider = _factory.CreateProvider(providerConfig);
        Assert.IsType<CodexProvider>(aiProvider);
        Assert.Equal("Codex", aiProvider.ProviderType);
    }

    [Fact]
    public void CreateProvider_MercuryType_ThrowsNotSupportedException()
    {
        Provider config = new Provider
        {
            Id = 2,
            Type = "Mercury",
            Model = "mercury-latest",
            ApiKey = "sk-test",
            Color = "#000000",
            Name = "Test"
        };
        Assert.Throws<NotSupportedException>(() => _factory.CreateProvider(config));
    }

    [Fact]
    public void CreateProvider_UnsupportedType_ThrowsNotSupportedException()
    {
        Provider config = new Provider
        {
            Id = 3,
            Type = "Unknown",
            Model = "x",
            ApiKey = "sk-test",
            Color = "#000000",
            Name = "Test"
        };
        Assert.Throws<NotSupportedException>(() => _factory.CreateProvider(config));
    }

    [Fact]
    public void CreateProvider_NullConfig_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _factory.CreateProvider(null));
    }

    [Fact]
    public void CreateProvider_SameIdAndType_ReturnsCachedInstance()
    {
        Provider providerConfig = new Provider
        {
            Id = 10,
            Type = "OpenAI",
            Model = "gpt-4o",
            ApiKey = "sk-test",
            Color = "#000000",
            Name = "Test"
        };
        IAiProvider expected = _factory.CreateProvider(providerConfig);
        IAiProvider actual = _factory.CreateProvider(providerConfig);
        Assert.Same(expected, actual);
    }

    [Fact]
    public void ClearCache_AllowsNewInstanceCreation()
    {
        Provider providerConfig = new Provider
        {
            Id = 10,
            Type = "OpenAI",
            Model = "gpt-4o",
            ApiKey = "sk-test",
            Color = "#000000",
            Name = "Test"
        };
        IAiProvider expected = _factory.CreateProvider(providerConfig);
        _factory.ClearCache();
        IAiProvider actual = _factory.CreateProvider(providerConfig);
        Assert.NotSame(expected, actual);
    }

    [Fact]
    public async Task GetConfiguredProvidersAsync_ReturnsSeededProviders()
    {
        Assert.NotEmpty(await _factory.GetConfiguredProvidersAsync());
    }

    [Fact]
    public void GetMetadata_Codex_ReturnsCodexMetadata()
    {
        IProviderMetadata metadata = ProviderFactory.GetMetadata("Codex");
        Assert.IsType<CodexProvider>(metadata);
        Assert.Equal("Codex", metadata.ProviderType);
    }

    [Fact]
    public void CreateProvider_ClaudeWeb_ReturnsClaudeWebProvider()
    {
        Provider providerConfig = new Provider
        {
            Id = 12,
            Type = "ClaudeWeb",
            Model = "claude-sonnet-4-5",
            ApiKey = "claude.ai-session",
            Color = "#000000",
            Name = "Claude.ai"
        };
        IAiProvider aiProvider = _factory.CreateProvider(providerConfig);
        Assert.Equal("ClaudeWeb", aiProvider.ProviderType); // Type is from Aire assembly
        Assert.Equal("ClaudeWeb", aiProvider.ProviderType);
    }
}
