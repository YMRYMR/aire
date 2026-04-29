using System.Collections.Generic;
using System.Threading.Tasks;
using Aire.AppLayer.Abstractions;
using Aire.AppLayer.Tools;
using Xunit;

namespace Aire.Tests.Services;

public class AutoAcceptProfilesApplicationServiceTests
{
    [Fact]
    public async Task LoadProfilesAsync_IncludesBuiltIns_AndCustomProfiles()
    {
        var repo = new InMemorySettingsRepository();
        var service = new AutoAcceptProfilesApplicationService(repo);

        await service.SaveProfileAsync("My profile", new(
            true,
            new[] { "read_file", "read_file", "write_to_file" },
            false,
            false));

        var profiles = await service.LoadProfilesAsync();

        Assert.Contains(profiles, p => p.Name == "Developer" && p.IsBuiltIn);
        Assert.Contains(profiles, p => p.Name == "News browser" && p.IsBuiltIn);
        Assert.Contains(profiles, p => p.Name == "My profile" && !p.IsBuiltIn);
        Assert.Contains(profiles, p => p.Name == "My profile" && p.Configuration.AllowedTools.Count == 2);
        Assert.Contains(profiles, p => p.Name == "Developer" && p.Configuration.AllowedTools.Contains("edit_file_text"));
    }

    [Fact]
    public async Task SaveAndLoadActiveConfiguration_RoundTrips()
    {
        var repo = new InMemorySettingsRepository();
        var service = new AutoAcceptProfilesApplicationService(repo);

        await service.SaveActiveConfigurationAsync(new(
            true,
            new[] { "write_to_file", "read_file", "edit_file_text" },
            true,
            false));

        var loaded = await service.LoadActiveConfigurationAsync();

        Assert.True(loaded.Enabled);
        Assert.True(loaded.AllowMouseTools);
        Assert.Contains("write_to_file", loaded.AllowedTools);
        Assert.Contains("edit_file_text", loaded.AllowedTools);
    }

    [Fact]
    public async Task DeleteProfileAsync_RemovesCustomProfile()
    {
        var repo = new InMemorySettingsRepository();
        var service = new AutoAcceptProfilesApplicationService(repo);

        await service.SaveProfileAsync("Temporary", new(false, new[] { "read_file" }, false, false));
        await service.DeleteProfileAsync("Temporary");

        var profiles = await service.LoadProfilesAsync();

        Assert.DoesNotContain(profiles, p => p.Name == "Temporary");
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
