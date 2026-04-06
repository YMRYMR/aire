using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aire.Screenshots;

internal static class LanguageHelper
{
    private static readonly string TranslationsPath = Path.Combine(Directory.GetCurrentDirectory(), "Aire", "Translations");
    private static readonly string AppStatePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Aire",
        "appstate_strings.json");

    /// <summary>
    /// Sets the application language by writing the language code to the appstate_strings.json file.
    /// </summary>
    /// <param name="languageCode">ISO language code (e.g., "en", "zh")</param>
    public static void SetAppStateLanguage(string languageCode)
    {
        try
        {
            var directory = Path.GetDirectoryName(AppStatePath);
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory!);

            // Merge into the existing file so other keys (e.g. apiAccessToken) are preserved.
            var existing = new Dictionary<string, JsonElement>();
            if (File.Exists(AppStatePath))
            {
                try
                {
                    using var doc = JsonDocument.Parse(File.ReadAllText(AppStatePath));
                    foreach (var prop in doc.RootElement.EnumerateObject())
                        existing[prop.Name] = prop.Value.Clone();
                }
                catch
                {
                    // If the existing file is corrupt, start fresh.
                }
            }

            // Build merged JSON: keep all existing properties, set/overwrite "language".
            using var ms = new MemoryStream();
            using (var writer = new Utf8JsonWriter(ms, new JsonWriterOptions { Indented = false }))
            {
                writer.WriteStartObject();
                writer.WriteString("language", languageCode);
                foreach (var (key, value) in existing)
                {
                    if (key == "language") continue; // already written above
                    writer.WritePropertyName(key);
                    value.WriteTo(writer);
                }
                writer.WriteEndObject();
            }

            var json = Encoding.UTF8.GetString(ms.ToArray());
            File.WriteAllText(AppStatePath, json, Encoding.UTF8);

            Console.WriteLine($"Language set to '{languageCode}' in {AppStatePath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to set language: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Scans the Translations directory and returns a list of available language codes.
    /// </summary>
    public static List<string> GetAvailableLanguageCodes()
    {
        var codes = new List<string>();
        if (!Directory.Exists(TranslationsPath))
        {
            Console.WriteLine($"Translations directory not found: {TranslationsPath}");
            return codes;
        }

        foreach (var file in Directory.EnumerateFiles(TranslationsPath, "*.json"))
        {
            try
            {
                var json = File.ReadAllText(file);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("code", out var codeProperty))
                {
                    var code = codeProperty.GetString();
                    if (!string.IsNullOrEmpty(code))
                        codes.Add(code);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to parse translation file {file}: {ex.Message}");
            }
        }

        Console.WriteLine($"Found {codes.Count} language codes: {string.Join(", ", codes)}");
        return codes;
    }

    /// <summary>
    /// Gets the localized title for a given English title and language code.
    /// Currently a stub - would need to map English keys to translation strings.
    /// </summary>
    public static string GetLocalizedTitle(string englishTitle, string languageCode)
    {
        // For now, return the English title as a placeholder.
        // Future implementation would look up the translation dictionary.
        return englishTitle;
    }
}