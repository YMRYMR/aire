using Aire.AppLayer.Abstractions;
using Aire.AppLayer.Settings;
using Xunit;

namespace Aire.Tests.Services;

public sealed class AppSettingsApplicationServiceTests
{
    /// <summary>
    /// In-memory fake that records calls so tests can verify delegation.
    /// </summary>
    private sealed class FakeSettingsRepository : ISettingsRepository
    {
        private readonly Dictionary<string, string> _store = new();

        public string? LastSetKey { get; private set; }
        public string? LastSetValue { get; private set; }

        public Task<string?> GetSettingAsync(string key)
            => Task.FromResult(_store.TryGetValue(key, out var v) ? v : (string?)null);

        public Task SetSettingAsync(string key, string value)
        {
            _store[key] = value;
            LastSetKey = key;
            LastSetValue = value;
            return Task.CompletedTask;
        }

        public Task LogFileAccessAsync(string operation, string path, bool allowed)
            => Task.CompletedTask;
    }

    // ── GetSettingAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetSettingAsync_ReturnsValueFromRepository()
    {
        var repo = new FakeSettingsRepository();
        await repo.SetSettingAsync("language", "en");
        var svc = new AppSettingsApplicationService(repo);

        var result = await svc.GetSettingAsync("language");

        Assert.Equal("en", result);
    }

    [Fact]
    public async Task GetSettingAsync_ReturnsNullForMissingKey()
    {
        var svc = new AppSettingsApplicationService(new FakeSettingsRepository());

        var result = await svc.GetSettingAsync("nonexistent");

        Assert.Null(result);
    }

    // ── SaveSettingAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task SaveSettingAsync_DelegatesWithCorrectKeyAndValue()
    {
        var repo = new FakeSettingsRepository();
        var svc = new AppSettingsApplicationService(repo);

        await svc.SaveSettingAsync("theme", "dark");

        Assert.Equal("theme", repo.LastSetKey);
        Assert.Equal("dark", repo.LastSetValue);
    }

    [Fact]
    public async Task SaveSettingAsync_OverwritesExistingKey()
    {
        var repo = new FakeSettingsRepository();
        var svc = new AppSettingsApplicationService(repo);

        await svc.SaveSettingAsync("theme", "light");
        await svc.SaveSettingAsync("theme", "dark");

        Assert.Equal("dark", await svc.GetSettingAsync("theme"));
    }

    [Fact]
    public async Task SaveSettingAsync_PreservesOtherKeys()
    {
        var repo = new FakeSettingsRepository();
        var svc = new AppSettingsApplicationService(repo);

        await svc.SaveSettingAsync("theme", "dark");
        await svc.SaveSettingAsync("language", "en");

        Assert.Equal("dark", await svc.GetSettingAsync("theme"));
        Assert.Equal("en", await svc.GetSettingAsync("language"));
    }
}
