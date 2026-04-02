using System.Threading.Tasks;
using Aire.AppLayer.Chat;
using Aire.AppLayer.Abstractions;
using Xunit;

namespace Aire.Tests.Services;

public class ContextSettingsApplicationServiceTests
{
    [Fact]
    public async Task LoadAsync_ReturnsDefaults_WhenMissingOrInvalid()
    {
        var repo = new InMemorySettingsRepository();
        var service = new ContextSettingsApplicationService(repo);

        var defaults = await service.LoadAsync();
        Assert.Equal(ContextWindowSettings.Default, defaults);

        await repo.SetSettingAsync("context_window_settings", "{invalid");
        var invalid = await service.LoadAsync();
        Assert.Equal(ContextWindowSettings.Default, invalid);
    }

    [Fact]
    public async Task SaveAsync_NormalizesValues_AndRoundTrips()
    {
        var repo = new InMemorySettingsRepository();
        var service = new ContextSettingsApplicationService(repo);

        await service.SaveAsync(new ContextWindowSettings(
            MaxMessages: 500,
            AnchorMessages: 80,
            UncachedRecentMessages: 0,
            EnablePromptCaching: false,
            EnableConversationSummaries: false,
            SummaryMaxCharacters: 50));

        var loaded = await service.LoadAsync();
        Assert.Equal(200, loaded.MaxMessages);
        Assert.Equal(40, loaded.AnchorMessages);
        Assert.Equal(1, loaded.UncachedRecentMessages);
        Assert.False(loaded.EnablePromptCaching);
        Assert.False(loaded.EnableConversationSummaries);
        Assert.Equal(160, loaded.SummaryMaxCharacters);
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
