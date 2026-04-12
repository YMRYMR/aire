using System;
using System.Threading;
using System.Threading.Tasks;
using Aire.AppLayer.Abstractions;
using Aire.AppLayer.Providers;
using Aire.Domain.Providers;
using Xunit;

namespace Aire.Tests.Services;

public sealed class CodexActionApplicationServiceTests
{
    private readonly FakeCodexManagementClient _client = new();
    private readonly CodexActionApplicationService _service;

    public CodexActionApplicationServiceTests()
    {
        _service = new CodexActionApplicationService(_client);
    }

    [Fact]
    public async Task GetStatusAsync_Delegates_To_Client()
    {
        // Arrange
        var expected = new CodexCliStatus(true, "/usr/local/bin/codex", false, "Ready");
        _client.StatusToReturn = expected;

        // Act
        var result = await _service.GetStatusAsync();

        // Assert
        Assert.Same(expected, result);
        Assert.Equal(expected, _client.LastStatusCall);
    }

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

    private sealed class FakeCodexManagementClient : ICodexManagementClient
    {
        public CodexCliStatus? StatusToReturn { get; set; }
        public CodexCliStatus? LastStatusCall { get; private set; }
        public bool ThrowOnInstall { get; set; }

        public Task<CodexCliStatus> GetStatusAsync(CancellationToken cancellationToken = default)
        {
            LastStatusCall = StatusToReturn;
            return Task.FromResult(StatusToReturn!);
        }

        public Task InstallAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default)
        {
            if (ThrowOnInstall)
                throw new InvalidOperationException("Simulated install failure.");

            return Task.CompletedTask;
        }
    }
}
