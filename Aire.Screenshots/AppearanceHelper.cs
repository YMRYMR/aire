using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aire.Screenshots;

/// <summary>
/// Writes appearance settings to windowstate.json so Aire starts with the
/// correct dark theme when screenshots are captured.
/// </summary>
internal static class AppearanceHelper
{
    private static readonly string AppDataDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Aire");

    private static readonly string WindowStatePath = Path.Combine(AppDataDir, "windowstate.json");

    // Must match App.xaml.cs UiDefaultsVersion so the app doesn't delete windowstate.json on startup.
    private const string CurrentUiDefaultsVersion = "2";

    /// <summary>
    /// Ensures windowstate.json has dark-theme appearance values and pins the UI
    /// defaults version so the app doesn't reset the file on startup.
    /// Preserves existing dark settings so user tweaks aren't overwritten.
    /// </summary>
    public static void SetDarkTheme()
    {
        try
        {
            Directory.CreateDirectory(AppDataDir);

            // Pin the defaults version so EnsureUiDefaultsApplied() won't delete windowstate.json.
            var versionPath = Path.Combine(AppDataDir, "ui-defaults.version");
            File.WriteAllText(versionPath, CurrentUiDefaultsVersion, new UTF8Encoding(false));

            var existing = new Dictionary<string, JsonElement>();
            if (File.Exists(WindowStatePath))
            {
                try
                {
                    using var doc = JsonDocument.Parse(File.ReadAllText(WindowStatePath));
                    foreach (var prop in doc.RootElement.EnumerateObject())
                        existing[prop.Name] = prop.Value.Clone();
                }
                catch
                {
                    // Corrupt file — start fresh.
                }
            }

            // If the user already set a dark palette (brightness < 0.5), keep their values.
            if (existing.TryGetValue("brightness", out var brightnessEl) && brightnessEl.GetDouble() < 0.5)
            {
                Console.WriteLine($"Existing dark theme preserved (brightness={brightnessEl.GetDouble():F3})");
                return;
            }

            using var ms = new MemoryStream();
            using (var writer = new Utf8JsonWriter(ms, new JsonWriterOptions { Indented = false }))
            {
                writer.WriteStartObject();

                // Dark theme: brightness 0.0 = full dark. Tint 0 = neutral gray (no color).
                writer.WriteNumber("brightness", 0.0);
                writer.WriteNumber("tintPosition", 0.0);
                writer.WriteNumber("accentBrightness", 0.5);
                writer.WriteNumber("accentTintPosition", 0.5);

                foreach (var (key, value) in existing)
                {
                    if (key is "brightness" or "tintPosition" or "accentBrightness" or "accentTintPosition")
                        continue; // already written above
                    writer.WritePropertyName(key);
                    value.WriteTo(writer);
                }

                writer.WriteEndObject();
            }

            File.WriteAllText(WindowStatePath, Encoding.UTF8.GetString(ms.ToArray()), new UTF8Encoding(false));
            Console.WriteLine($"Dark theme written to {WindowStatePath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to set dark theme: {ex.Message}");
            throw;
        }
    }
}
