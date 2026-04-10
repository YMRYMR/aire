using System;
using System.IO;
using System.Diagnostics;
using System.Text.Json;
using Aire.Services;

namespace Aire.Bootstrap;

public sealed class SetupPreferences
{
    public string LanguageCode { get; set; } = "en";
    public string DefaultAssistantMode { get; set; } = "general";
    public bool VoiceInputEnabled { get; set; }
    public bool VoiceOutputEnabled { get; set; }
    public bool VoiceGuidanceEnabled { get; set; }
    public bool UseLocalVoicesOnly { get; set; }
    public string? SelectedVoice { get; set; }
    public int VoiceRate { get; set; }
}

public static class SetupPreferencesStore
{
    private const string OverrideEnvVar = "AIRE_SETUP_PREFERENCES_PATH";

    public static string GetPath()
    {
        string? overridePath = Environment.GetEnvironmentVariable(OverrideEnvVar);
        if (!string.IsNullOrWhiteSpace(overridePath))
        {
            return overridePath;
        }

        string basePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Aire");
        return Path.Combine(basePath, "setup-preferences.json");
    }

    public static SetupPreferences Load()
    {
        try
        {
            string path = GetPath();
            if (!File.Exists(path))
            {
                return new SetupPreferences();
            }

            string json = File.ReadAllText(path);
            SetupPreferences? preferences = JsonSerializer.Deserialize<SetupPreferences>(json);
            return Normalize(preferences ?? new SetupPreferences());
        }
        catch (Exception ex)
        {
            AppLogger.Warn(nameof(SetupPreferencesStore) + ".Load", "Failed to load setup preferences; using defaults", ex);
            return new SetupPreferences();
        }
    }

    public static void Save(SetupPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);

        string path = GetPath();
        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string json = JsonSerializer.Serialize(Normalize(preferences), new JsonSerializerOptions
        {
            WriteIndented = true,
        });
        try
        {
            File.WriteAllText(path, json);
        }
        catch (Exception ex)
        {
            AppLogger.Warn(nameof(SetupPreferencesStore) + ".Save", "Failed to save setup preferences", ex);
            throw;
        }
    }

    private static SetupPreferences Normalize(SetupPreferences preferences)
    {
        preferences.LanguageCode = string.IsNullOrWhiteSpace(preferences.LanguageCode)
            ? "en"
            : preferences.LanguageCode.Trim();
        preferences.DefaultAssistantMode = string.IsNullOrWhiteSpace(preferences.DefaultAssistantMode)
            ? "general"
            : preferences.DefaultAssistantMode.Trim();
        preferences.VoiceRate = Math.Clamp(preferences.VoiceRate, -10, 10);
        return preferences;
    }
}
