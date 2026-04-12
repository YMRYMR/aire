using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Aire.AppLayer.Abstractions;
using Aire.AppLayer.Providers;
using Aire.Services;
using Xunit;

namespace Aire.Tests.Services;

public sealed class ProviderCapabilityTestSessionServiceTests
{
    private readonly ProviderCapabilityTestSessionService _sut = new();

    // ── LoadAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task LoadAsync_NoSavedData_ReturnsNull()
    {
        var repo = new FakeSettingsRepository();

        var result = await _sut.LoadAsync(1, "gpt-4", repo);

        Assert.Null(result);
    }

    [Fact]
    public async Task LoadAsync_InvalidJson_ReturnsNull()
    {
        var repo = new FakeSettingsRepository();
        repo.Set("capability_tests_1", "not valid json {{{");

        var result = await _sut.LoadAsync(1, "gpt-4", repo);

        Assert.Null(result);
    }

    [Fact]
    public async Task LoadAsync_ModelNotFoundInDictionary_ReturnsNull()
    {
        var repo = new FakeSettingsRepository();
        repo.Set("capability_tests_1",
            """{"claude-3":{"model":"claude-3","testedAt":"2025-01-01T00:00:00","results":[]}}""");

        var result = await _sut.LoadAsync(1, "gpt-4", repo);

        Assert.Null(result);
    }

    [Fact]
    public async Task LoadAsync_SuccessfulLoad_ReturnsSession()
    {
        var repo = new FakeSettingsRepository();
        var testedAt = new DateTime(2025, 6, 15, 12, 0, 0, DateTimeKind.Utc);
        var session = new CapabilityTestSession
        {
            Model = "gpt-4",
            TestedAt = testedAt,
            Results = new List<CapabilityTestResult>
            {
                new("read_file", "Read file", "File System", true, "read_file", null, 120),
            },
        };
        var all = new Dictionary<string, CapabilityTestSession> { ["gpt-4"] = session };
        var json = System.Text.Json.JsonSerializer.Serialize(all);
        repo.Set("capability_tests_5", json);

        var result = await _sut.LoadAsync(5, "gpt-4", repo);

        Assert.NotNull(result);
        Assert.Equal("gpt-4", result.Model);
        Assert.Equal(testedAt, result.TestedAt);
        Assert.Single(result.Results);
        Assert.Equal("read_file", result.Results[0].Id);
        Assert.True(result.Results[0].Passed);
    }

    [Fact]
    public async Task LoadAsync_NullModel_TreatedAsEmptyString()
    {
        var repo = new FakeSettingsRepository();
        var session = new CapabilityTestSession
        {
            Model = "",
            TestedAt = DateTime.UtcNow,
            Results = new List<CapabilityTestResult>(),
        };
        var all = new Dictionary<string, CapabilityTestSession> { [""] = session };
        repo.Set("capability_tests_1", System.Text.Json.JsonSerializer.Serialize(all));

        var result = await _sut.LoadAsync(1, null!, repo);

        Assert.NotNull(result);
        Assert.Equal("", result.Model);
    }

    // ── SaveAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task SaveAsync_NoPriorData_CreatesNewDictionaryAndPersists()
    {
        var repo = new FakeSettingsRepository();
        var testedAt = new DateTime(2025, 3, 1, 10, 30, 0, DateTimeKind.Utc);
        var results = new List<CapabilityTestResult>
        {
            new("exec_cmd", "Execute command", "File System", true, "execute_command", null, 250),
        };

        await _sut.SaveAsync(7, "llama3", results, testedAt, repo);

        var stored = repo.Get("capability_tests_7");
        Assert.NotNull(stored);
        Assert.Contains("llama3", stored);
        Assert.Contains("exec_cmd", stored);
    }

    [Fact]
    public async Task SaveAsync_ExistingValidData_UpsertsModelEntry()
    {
        var repo = new FakeSettingsRepository();
        // Pre-populate with an existing model entry.
        var existing = new Dictionary<string, CapabilityTestSession>
        {
            ["gpt-4"] = new()
            {
                Model = "gpt-4",
                TestedAt = DateTime.UtcNow,
                Results = new List<CapabilityTestResult>(),
            },
        };
        repo.Set("capability_tests_3", System.Text.Json.JsonSerializer.Serialize(existing));

        var newResults = new List<CapabilityTestResult>
        {
            new("search_files", "Search files", "File System", false, "list_directory", "Expected: search_files", 300),
        };
        var testedAt = new DateTime(2025, 7, 1, 0, 0, 0, DateTimeKind.Utc);

        await _sut.SaveAsync(3, "gpt-4", newResults, testedAt, repo);

        var stored = repo.Get("capability_tests_3");
        Assert.NotNull(stored);
        Assert.Contains("search_files", stored);

        // Verify round-trip: load should return updated data.
        var loaded = await _sut.LoadAsync(3, "gpt-4", repo);
        Assert.NotNull(loaded);
        Assert.Single(loaded.Results);
        Assert.False(loaded.Results[0].Passed);
        Assert.Equal("search_files", loaded.Results[0].Id);
    }

    [Fact]
    public async Task SaveAsync_ExistingInvalidJson_StartsFresh()
    {
        var repo = new FakeSettingsRepository();
        repo.Set("capability_tests_9", "broken json!!!");

        var results = new List<CapabilityTestResult>
        {
            new("web_search", "Web search", "Web", true, "open_url", null, 450),
        };
        var testedAt = DateTime.UtcNow;

        await _sut.SaveAsync(9, "mistral", results, testedAt, repo);

        var loaded = await _sut.LoadAsync(9, "mistral", repo);
        Assert.NotNull(loaded);
        Assert.Equal("mistral", loaded.Model);
        Assert.Single(loaded.Results);
        Assert.Equal("web_search", loaded.Results[0].Id);
    }

    [Fact]
    public async Task SaveAsync_NullModel_TreatedAsEmptyString()
    {
        var repo = new FakeSettingsRepository();
        var results = new List<CapabilityTestResult>
        {
            new("test", "Test", "Cat", true, "test", null, 10),
        };

        await _sut.SaveAsync(1, null!, results, DateTime.UtcNow, repo);

        var loaded = await _sut.LoadAsync(1, null!, repo);
        Assert.NotNull(loaded);
        Assert.Equal("", loaded.Model);
    }

    [Fact]
    public async Task SaveAsync_PreservesOtherModelsInDictionary()
    {
        var repo = new FakeSettingsRepository();
        var existing = new Dictionary<string, CapabilityTestSession>
        {
            ["model-a"] = new()
            {
                Model = "model-a",
                TestedAt = DateTime.UtcNow,
                Results = new List<CapabilityTestResult>
                {
                    new("t1", "T1", "Cat", true, "t1", null, 50),
                },
            },
        };
        repo.Set("capability_tests_2", System.Text.Json.JsonSerializer.Serialize(existing));

        await _sut.SaveAsync(2, "model-b",
            new List<CapabilityTestResult>(), DateTime.UtcNow, repo);

        // model-a should still be present.
        var loadedA = await _sut.LoadAsync(2, "model-a", repo);
        Assert.NotNull(loadedA);
        Assert.Equal("model-a", loadedA.Model);
        Assert.Single(loadedA.Results);

        // model-b should also be present.
        var loadedB = await _sut.LoadAsync(2, "model-b", repo);
        Assert.NotNull(loadedB);
        Assert.Equal("model-b", loadedB.Model);
        Assert.Empty(loadedB.Results);
    }

    // ── Settings key format ──────────────────────────────────────────────

    [Fact]
    public async Task SaveAsync_UsesCorrectSettingsKey()
    {
        var repo = new FakeSettingsRepository();

        await _sut.SaveAsync(42, "model-x",
            new List<CapabilityTestResult>(), DateTime.UtcNow, repo);

        Assert.Null(repo.Get("capability_tests_1"));
        Assert.Null(repo.Get("capability_tests_99"));
        Assert.NotNull(repo.Get("capability_tests_42"));
    }

    // ── Fake implementation ──────────────────────────────────────────────

    private sealed class FakeSettingsRepository : ISettingsRepository
    {
        private readonly Dictionary<string, string> _store = new();

        public Task<string?> GetSettingAsync(string key)
            => Task.FromResult(_store.TryGetValue(key, out var v) ? v : null);

        public Task SetSettingAsync(string key, string value)
        {
            _store[key] = value;
            return Task.CompletedTask;
        }

        public Task LogFileAccessAsync(string operation, string path, bool allowed)
            => Task.CompletedTask;

        // Test helper: direct get without async.
        public string? Get(string key)
            => _store.TryGetValue(key, out var v) ? v : null;

        // Test helper: direct set without async.
        public void Set(string key, string value) => _store[key] = value;
    }
}
