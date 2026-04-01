using System.IO;
using System.Reflection;
using System.Text.Json;

namespace Aire.Data;

/// <summary>
/// Loads model definitions from JSON files in <c>%LOCALAPPDATA%\Aire\Models\</c>.
/// On first run (or when a built-in file is missing), default JSON files are
/// extracted from embedded resources. Users can import additional JSON files.
/// </summary>
public static class ModelCatalog
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    public static void EnsureDefaults()
    {
        var modelsFolder = GetModelsFolder();
        try
        {
            Directory.CreateDirectory(modelsFolder);
        }
        catch
        {
            return;
        }

        var assembly = Assembly.GetExecutingAssembly();
        const string prefix = "Aire.Data.Models.";

        foreach (var resourceName in assembly.GetManifestResourceNames())
        {
            if (!resourceName.StartsWith(prefix, StringComparison.Ordinal) ||
                !resourceName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                continue;

            var fileName = resourceName[prefix.Length..];
            var destPath = Path.Combine(modelsFolder, fileName);

            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
                continue;

            using var reader = new StreamReader(stream);
            var embeddedJson = reader.ReadToEnd();

            if (!File.Exists(destPath))
            {
                File.WriteAllText(destPath, embeddedJson);
                continue;
            }

            TryMergeCatalogFile(destPath, embeddedJson);
        }
    }

    public static List<ModelDefinition> GetDefaults(string providerType)
    {
        var modelsFolder = GetModelsFolder();
        try
        {
            Directory.CreateDirectory(modelsFolder);
        }
        catch
        {
            return [];
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<ModelDefinition>();

        IEnumerable<string> files;
        try
        {
            files = Directory.EnumerateFiles(modelsFolder, "*.json");
        }
        catch
        {
            return [];
        }

        foreach (var file in files)
        {
            try
            {
                var json = File.ReadAllText(file);
                var catalog = JsonSerializer.Deserialize<ModelCatalogFile>(json, JsonOpts);
                if (catalog == null)
                    continue;

                if (!string.Equals(catalog.ProviderType, providerType, StringComparison.OrdinalIgnoreCase))
                    continue;

                foreach (var model in catalog.Models)
                {
                    if (seen.Add(model.Id))
                        result.Add(model);
                }
            }
            catch
            {
                // Skip malformed files silently.
            }
        }

        return result;
    }

    public static int ImportFile(string sourcePath)
    {
        try
        {
            var json = File.ReadAllText(sourcePath);
            var catalog = JsonSerializer.Deserialize<ModelCatalogFile>(json, JsonOpts);
            if (catalog == null || string.IsNullOrWhiteSpace(catalog.ProviderType) || catalog.Models.Count == 0)
                return -1;

            var modelsFolder = GetModelsFolder();
            Directory.CreateDirectory(modelsFolder);
            var destName = $"models-custom-{DateTime.Now:yyyyMMdd-HHmmss}.json";
            var destPath = Path.Combine(modelsFolder, destName);
            File.Copy(sourcePath, destPath, overwrite: true);
            return catalog.Models.Count;
        }
        catch
        {
            return -1;
        }
    }

    public static string GetModelsFolder()
    {
        var envOverride = Environment.GetEnvironmentVariable("AIRE_MODELS_FOLDER");
        if (!string.IsNullOrWhiteSpace(envOverride))
            return envOverride;

        try
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (!string.IsNullOrWhiteSpace(localAppData))
                return Path.Combine(localAppData, "Aire", "Models");
        }
        catch
        {
            // Fall through to local fallback.
        }

        return Path.Combine(AppContext.BaseDirectory, "Models");
    }

    private static void TryMergeCatalogFile(string destPath, string embeddedJson)
    {
        try
        {
            var embeddedCatalog = JsonSerializer.Deserialize<ModelCatalogFile>(embeddedJson, JsonOpts);
            if (embeddedCatalog == null || string.IsNullOrWhiteSpace(embeddedCatalog.ProviderType))
                return;

            var existingJson = File.ReadAllText(destPath);
            var existingCatalog = JsonSerializer.Deserialize<ModelCatalogFile>(existingJson, JsonOpts);
            if (existingCatalog == null ||
                !string.Equals(existingCatalog.ProviderType, embeddedCatalog.ProviderType, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var knownIds = existingCatalog.Models
                .Select(model => model.Id)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            bool changed = false;
            foreach (var embeddedModel in embeddedCatalog.Models)
            {
                if (string.IsNullOrWhiteSpace(embeddedModel.Id) || !knownIds.Add(embeddedModel.Id))
                    continue;

                existingCatalog.Models.Add(embeddedModel);
                changed = true;
            }

            if (!changed)
                return;

            var mergedJson = JsonSerializer.Serialize(existingCatalog, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(destPath, mergedJson);
        }
        catch
        {
            // Keep the user's existing file untouched if we can't safely merge it.
        }
    }
}
