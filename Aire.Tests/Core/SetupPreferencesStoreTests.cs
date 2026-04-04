using System;
using System.IO;
using Aire.Bootstrap;
using Xunit;

namespace Aire.Tests.Core;

[Collection("NonParallelCoreUtilities")]
public sealed class SetupPreferencesStoreTests : IDisposable
{
    private readonly string? _originalPath;

    public SetupPreferencesStoreTests()
    {
        _originalPath = Environment.GetEnvironmentVariable("AIRE_SETUP_PREFERENCES_PATH");
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("AIRE_SETUP_PREFERENCES_PATH", _originalPath);
    }

    [Fact]
    public void Load_ReturnsDefaults_ForMalformedJson()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"aire_setup_prefs_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string path = Path.Combine(tempDir, "setup-preferences.json");
        File.WriteAllText(path, "{ not valid json");
        Environment.SetEnvironmentVariable("AIRE_SETUP_PREFERENCES_PATH", path);

        SetupPreferences preferences = SetupPreferencesStore.Load();

        Assert.Equal("en", preferences.LanguageCode);
        Assert.Equal("general", preferences.DefaultAssistantMode);
        Assert.False(preferences.VoiceInputEnabled);
        Assert.False(preferences.VoiceOutputEnabled);
    }

    [Fact]
    public void Save_Throws_WhenTargetPathIsInvalid()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"aire_setup_prefs_blocked_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        Environment.SetEnvironmentVariable("AIRE_SETUP_PREFERENCES_PATH", tempDir);

        Assert.ThrowsAny<Exception>(() => SetupPreferencesStore.Save(new SetupPreferences()));
    }

    [Fact]
    public void GetPath_UsesEnvironmentOverride_WhenPresent()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"aire_setup_prefs_override_{Guid.NewGuid():N}.json");
        Environment.SetEnvironmentVariable("AIRE_SETUP_PREFERENCES_PATH", tempFile);

        string path = SetupPreferencesStore.GetPath();

        Assert.Equal(tempFile, path);
    }

    [Fact]
    public void Save_AndLoad_NormalizeBlankValues_AndClampVoiceRate()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"aire_setup_prefs_roundtrip_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string path = Path.Combine(tempDir, "setup-preferences.json");
        Environment.SetEnvironmentVariable("AIRE_SETUP_PREFERENCES_PATH", path);

        SetupPreferencesStore.Save(new SetupPreferences
        {
            LanguageCode = " ",
            DefaultAssistantMode = "",
            VoiceRate = 99
        });

        SetupPreferences preferences = SetupPreferencesStore.Load();

        Assert.Equal("en", preferences.LanguageCode);
        Assert.Equal("general", preferences.DefaultAssistantMode);
        Assert.Equal(10, preferences.VoiceRate);
    }
}
