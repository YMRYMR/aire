using System;
using System.Threading;
using System.Threading.Tasks;
using Aire.AppLayer.Abstractions;
using Aire.AppLayer.Providers;
using Aire.Domain.Providers;
using Aire.Services;
using Xunit;

namespace Aire.Tests.Services;

public sealed class OllamaActionApplicationServiceTests
{
    private readonly FakeOllamaManagementClient _client = new();
    private readonly OllamaActionApplicationService _service;

    public OllamaActionApplicationServiceTests()
    {
        _service = new OllamaActionApplicationService(_client);
    }

    // --- InstallAsync ---

    [Fact]
    public async Task InstallAsync_Success_ReturnsSuccessResult()
    {
        // Act
        var result = await _service.InstallAsync();

        // Assert
        Assert.True(result.Succeeded);
        Assert.Contains("installed", result.UserMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task InstallAsync_Exception_ReturnsFailureResult()
    {
        // Arrange
        _client.ThrowOnInstall = true;

        // Act
        var result = await _service.InstallAsync();

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains("Could not install", result.UserMessage, StringComparison.OrdinalIgnoreCase);
    }

    // --- DownloadModelAsync ---

    [Fact]
    public async Task DownloadModelAsync_Success_ReturnsSuccessResult()
    {
        // Act
        var result = await _service.DownloadModelAsync("llama3");

        // Assert
        Assert.True(result.Succeeded);
        Assert.Contains("llama3", result.UserMessage);
        Assert.Contains("downloaded", result.UserMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DownloadModelAsync_Exception_ReturnsFailureResult()
    {
        // Arrange
        _client.ThrowOnPull = true;

        // Act
        var result = await _service.DownloadModelAsync("llama3");

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains("Could not download", result.UserMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DownloadModelAsync_Passes_BaseUrl_To_Client()
    {
        // Act
        await _service.DownloadModelAsync("llama3", "http://custom:11434");

        // Assert
        Assert.Equal("http://custom:11434", _client.LastPullBaseUrl);
    }

    // --- UninstallModelAsync ---

    [Fact]
    public async Task UninstallModelAsync_Success_ReturnsSuccessResult()
    {
        // Act
        var result = await _service.UninstallModelAsync("llama3");

        // Assert
        Assert.True(result.Succeeded);
        Assert.Contains("llama3", result.UserMessage);
        Assert.Contains("uninstalled", result.UserMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UninstallModelAsync_Exception_ReturnsFailureResult()
    {
        // Arrange
        _client.ThrowOnDelete = true;

        // Act
        var result = await _service.UninstallModelAsync("llama3");

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains("Could not uninstall", result.UserMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UninstallModelAsync_Passes_BaseUrl_To_Client()
    {
        // Act
        await _service.UninstallModelAsync("llama3", "http://custom:11434");

        // Assert
        Assert.Equal("http://custom:11434", _client.LastDeleteBaseUrl);
    }

    private sealed class FakeOllamaManagementClient : IOllamaManagementClient
    {
        public bool ThrowOnInstall { get; set; }
        public bool ThrowOnPull { get; set; }
        public bool ThrowOnDelete { get; set; }

        public string? LastPullBaseUrl { get; private set; }
        public string? LastDeleteBaseUrl { get; private set; }

        public Task InstallAsync(IProgress<int>? progress = null, CancellationToken cancellationToken = default)
        {
            if (ThrowOnInstall)
                throw new InvalidOperationException("Simulated install failure.");

            return Task.CompletedTask;
        }

        public Task PullModelAsync(
            string modelName,
            string? baseUrl = null,
            IProgress<OllamaService.OllamaPullProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            LastPullBaseUrl = baseUrl;

            if (ThrowOnPull)
                throw new InvalidOperationException("Simulated pull failure.");

            return Task.CompletedTask;
        }

        public Task DeleteModelAsync(
            string modelName,
            string? baseUrl = null,
            CancellationToken cancellationToken = default)
        {
            LastDeleteBaseUrl = baseUrl;

            if (ThrowOnDelete)
                throw new InvalidOperationException("Simulated delete failure.");

            return Task.CompletedTask;
        }
    }
}
