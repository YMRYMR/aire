using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Aire.Domain.Providers;
using Aire.Services.Providers;
using Xunit;

namespace Aire.Tests.Services;

public sealed class CodexManagementClientTests
{
    [Fact]
    public async Task GetStatusAsync_ReturnsInjectedStatus()
    {
        var client = new CodexManagementClient(
            () => new CodexCliStatus(true, "C:\\tools\\codex.exe", false, "ok"),
            () => null,
            (_, _) => Task.FromResult((0, string.Empty, string.Empty)));

        var status = await client.GetStatusAsync();

        Assert.True(status.IsInstalled);
        Assert.Equal("C:\\tools\\codex.exe", status.CliPath);
    }

    [Fact]
    public async Task InstallAsync_ShortCircuitsWhenAlreadyInstalled()
    {
        var progress = new RecordingProgress();
        var client = new CodexManagementClient(
            () => new CodexCliStatus(true, "C:\\tools\\codex.exe", false, "installed"),
            () => throw new InvalidOperationException("should not look for npm"),
            (_, _) => throw new InvalidOperationException("should not run install"));

        await client.InstallAsync(progress);

        Assert.Contains(progress.Updates, message => message.Contains("already installed", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task InstallAsync_ThrowsWhenNpmIsMissing()
    {
        var client = new CodexManagementClient(
            () => new CodexCliStatus(false, null, false, "missing"),
            () => null,
            (_, _) => Task.FromResult((0, string.Empty, string.Empty)));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => client.InstallAsync());

        Assert.Contains("npm was not found", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task InstallAsync_ReportsProgressAndSucceeds()
    {
        var progress = new RecordingProgress();
        var client = new CodexManagementClient(
            () => new CodexCliStatus(false, null, false, "missing"),
            () => "C:\\Program Files\\nodejs\\npm.cmd",
            (_, _) => Task.FromResult((0, "installed", string.Empty)));

        await client.InstallAsync(progress);

        Assert.Contains(progress.Updates, message => message.Contains("Installing Codex CLI", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(progress.Updates, message => message.Contains("installed", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task InstallAsync_UsesStdErrForFailureMessage()
    {
        var client = new CodexManagementClient(
            () => new CodexCliStatus(false, null, false, "missing"),
            () => "C:\\Program Files\\nodejs\\npm.cmd",
            (_, _) => Task.FromResult((1, "stdout noise", "stderr failure")));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => client.InstallAsync());

        Assert.Contains("stderr failure", ex.Message, StringComparison.Ordinal);
    }

    private sealed class RecordingProgress : IProgress<string>
    {
        public List<string> Updates { get; } = new();

        public void Report(string value)
        {
            Updates.Add(value);
        }
    }
}
