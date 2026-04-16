using Aire.AppLayer.Abstractions;
using Aire.AppLayer.Chat;
using Xunit;

namespace Aire.Tests.Services;

public sealed class CustomInstructionsServiceTests
{
    [Fact]
    public async Task LoadAsync_WithNoSavedValue_ReturnsEmpty()
    {
        var repo = new InMemorySettingsRepository();
        var svc = new CustomInstructionsService(repo);
        var result = await svc.LoadAsync();
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public async Task SaveAsync_PersistsAndLoads()
    {
        var repo = new InMemorySettingsRepository();
        var svc = new CustomInstructionsService(repo);
        await svc.SaveAsync("Always respond in French.");
        var result = await svc.LoadAsync();
        Assert.Equal("Always respond in French.", result);
    }

    [Fact]
    public async Task SaveAsync_WithNull_SavesEmptyString()
    {
        var repo = new InMemorySettingsRepository();
        var svc = new CustomInstructionsService(repo);
        await svc.SaveAsync(null!);
        var result = await svc.LoadAsync();
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public async Task SaveAsync_OverwritesPrevious()
    {
        var repo = new InMemorySettingsRepository();
        var svc = new CustomInstructionsService(repo);
        await svc.SaveAsync("First");
        await svc.SaveAsync("Second");
        var result = await svc.LoadAsync();
        Assert.Equal("Second", result);
    }

    [Fact]
    public async Task SaveAsync_MultilineInstructions_PersistedVerbatim()
    {
        var repo = new InMemorySettingsRepository();
        var svc = new CustomInstructionsService(repo);
        var instructions = "Rule 1: Be brief\nRule 2: Use code examples\nRule 3: Be friendly";
        await svc.SaveAsync(instructions);
        var result = await svc.LoadAsync();
        Assert.Equal(instructions, result);
    }

    [Fact]
    public async Task SaveAsync_EmptyString_ClearsValue()
    {
        var repo = new InMemorySettingsRepository();
        var svc = new CustomInstructionsService(repo);
        await svc.SaveAsync("Some instructions");
        await svc.SaveAsync("");
        var result = await svc.LoadAsync();
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public async Task LoadAsync_AfterMultipleSaves_ReturnsLatest()
    {
        var repo = new InMemorySettingsRepository();
        var svc = new CustomInstructionsService(repo);

        for (int i = 0; i < 5; i++)
            await svc.SaveAsync($"Version {i}");

        var result = await svc.LoadAsync();
        Assert.Equal("Version 4", result);
    }

    private sealed class InMemorySettingsRepository : ISettingsRepository
    {
        private readonly Dictionary<string, string> _values = new();

        public Task<string?> GetSettingAsync(string key)
            => Task.FromResult(_values.TryGetValue(key, out var value) ? value : null);

        public Task SetSettingAsync(string key, string value)
        {
            _values[key] = value;
            return Task.CompletedTask;
        }

        public Task LogFileAccessAsync(string operation, string path, bool allowed)
            => Task.CompletedTask;
    }
}
