using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Aire.AppLayer.Tools;
using Aire.AppLayer.Abstractions;
using Aire.Domain.Tools;
using Xunit;

namespace Aire.Tests.Services;

public sealed class ToolCategorySettingsApplicationServiceTests
{
    [Fact]
    public async Task LoadAsync_ReturnsAllCategories_WhenSettingIsMissingOrMalformed()
    {
        var repository = new InMemorySettingsRepository();
        var service = new ToolCategorySettingsApplicationService(repository);

        var defaults = await service.LoadAsync();
        Assert.Equal(ToolCategoryCatalog.KnownCategories.OrderBy(x => x), defaults.EnabledCategories);
        Assert.True(defaults.ToolsEnabled);

        await repository.SetSettingAsync(ToolCategorySettingsApplicationService.SettingsKey, "{not-json");
        var malformed = await service.LoadAsync();
        Assert.Equal(ToolCategoryCatalog.KnownCategories.OrderBy(x => x), malformed.EnabledCategories);
    }

    [Fact]
    public async Task SaveAsync_NormalizesUnknownCategories_AndPersistsSortedValues()
    {
        var repository = new InMemorySettingsRepository();
        var service = new ToolCategorySettingsApplicationService(repository);

        await service.SaveAsync(["keyboard", "email", "keyboard", "unknown", "browser"]);

        var saved = await service.LoadAsync();
        Assert.Equal(["browser", "email", "keyboard"], saved.EnabledCategories);
        Assert.Equal("[\"browser\",\"email\",\"keyboard\"]", await repository.GetSettingAsync(ToolCategorySettingsApplicationService.SettingsKey));
    }

    [Fact]
    public void Parse_ReturnsEmptySelection_WhenNormalizedInputHasNoKnownCategories()
    {
        var selection = ToolCategorySettingsApplicationService.Parse("[\"not-a-category\"]");

        Assert.Empty(selection.EnabledCategories);
        Assert.False(selection.ToolsEnabled);
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
