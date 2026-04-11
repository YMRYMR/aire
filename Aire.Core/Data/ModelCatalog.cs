using System.IO;
using System.Reflection;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace Aire.Data;

/// <summary>
/// Result of syncing a provider's live models into the local catalog cache.
/// </summary>
public sealed record ModelCatalogSyncResult(
    bool CreatedCatalog,
    IReadOnlyList<string> AddedModelIds);

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

    /// <summary>
    /// Returns the context length (in tokens) of a specific model, if defined in the catalog.
    /// </summary>
    /// <param name="providerType">Provider type (e.g., "OpenAI", "Anthropic").</param>
    /// <param name="modelId">Model identifier (e.g., "gpt-4-turbo").</param>
    /// <returns>
    /// The model's context length, or null if the model is not found or does not have a
    /// <see cref="ModelDefinition.ContextLength"/> defined.
    /// </returns>
    public static int? GetContextLength(string providerType, string modelId)
    {
        if (string.IsNullOrWhiteSpace(providerType) || string.IsNullOrWhiteSpace(modelId))
            return null;

        var models = GetDefaults(providerType);
        var match = models.FirstOrDefault(m => string.Equals(m.Id, modelId, StringComparison.OrdinalIgnoreCase));
        return match?.ContextLength;
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

    /// <summary>
    /// Merges a provider's live models into a dedicated local cache file.
    /// Existing entries are updated in place, and new model ids are appended.
    /// </summary>
    public static ModelCatalogSyncResult SyncLiveModels(string providerType, IReadOnlyList<ModelDefinition> liveModels)
    {
        if (string.IsNullOrWhiteSpace(providerType) || liveModels.Count == 0)
            return new ModelCatalogSyncResult(false, Array.Empty<string>());

        var modelsFolder = GetModelsFolder();
        try
        {
            Directory.CreateDirectory(modelsFolder);
        }
        catch
        {
            return new ModelCatalogSyncResult(false, Array.Empty<string>());
        }

        var destPath = Path.Combine(modelsFolder, $"models-live-{SanitizeToken(providerType)}.json");
        var createdCatalog = !File.Exists(destPath);

        var catalog = createdCatalog
            ? new ModelCatalogFile { ProviderType = providerType }
            : LoadCatalog(destPath) ?? new ModelCatalogFile { ProviderType = providerType };

        if (string.IsNullOrWhiteSpace(catalog.ProviderType))
            catalog.ProviderType = providerType;

        var orderedModels = new List<ModelDefinition>();
        var byId = new Dictionary<string, ModelDefinition>(StringComparer.OrdinalIgnoreCase);

        foreach (var existing in catalog.Models)
        {
            if (string.IsNullOrWhiteSpace(existing.Id) || byId.ContainsKey(existing.Id))
                continue;

            orderedModels.Add(existing);
            byId[existing.Id] = existing;
        }

        catalog.Models = orderedModels;

        var addedIds = new List<string>();
        var changed = createdCatalog;
        foreach (var liveModel in liveModels)
        {
            if (string.IsNullOrWhiteSpace(liveModel.Id))
                continue;

            if (byId.TryGetValue(liveModel.Id, out var existing))
            {
                changed |= MergeModel(existing, liveModel);
                continue;
            }

            var copy = CloneModel(liveModel);
            catalog.Models.Add(copy);
            byId[copy.Id] = copy;
            addedIds.Add(copy.Id);
            changed = true;
        }

        if (changed)
        {
            try
            {
                WriteCatalog(destPath, catalog);
            }
            catch
            {
                return new ModelCatalogSyncResult(createdCatalog, Array.Empty<string>());
            }
        }

        return new ModelCatalogSyncResult(createdCatalog, addedIds);
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

    private static ModelCatalogFile? LoadCatalog(string path)
    {
        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<ModelCatalogFile>(json, JsonOpts);
        }
        catch
        {
            return null;
        }
    }

    private static bool MergeModel(ModelDefinition target, ModelDefinition source)
    {
        var changed = false;

        if (!string.IsNullOrWhiteSpace(source.DisplayName) &&
            !string.Equals(target.DisplayName, source.DisplayName, StringComparison.Ordinal))
        {
            target.DisplayName = source.DisplayName;
            changed = true;
        }

        if (source.SizeBytes > 0 && target.SizeBytes != source.SizeBytes)
        {
            target.SizeBytes = source.SizeBytes;
            changed = true;
        }

        if (source.IsInstalled != target.IsInstalled)
        {
            target.IsInstalled = source.IsInstalled;
            changed = true;
        }

        if (source.Capabilities != null)
        {
            var sourceCaps = source.Capabilities
                .Where(cap => !string.IsNullOrWhiteSpace(cap))
                .ToList();
            var currentCaps = target.Capabilities ?? [];
            if (!currentCaps.SequenceEqual(sourceCaps, StringComparer.OrdinalIgnoreCase))
            {
                target.Capabilities = sourceCaps;
                changed = true;
            }
        }

        return changed;
    }

    private static ModelDefinition CloneModel(ModelDefinition model) => new()
    {
        Id = model.Id,
        DisplayName = model.DisplayName,
        SizeBytes = model.SizeBytes,
        IsInstalled = model.IsInstalled,
        Capabilities = model.Capabilities?.ToList()
    };

    private static string SanitizeToken(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(value.Length);
        foreach (var ch in value.Trim())
        {
            builder.Append(Array.IndexOf(invalid, ch) >= 0 ? '_' : ch);
        }

        var token = builder.ToString().Trim();
        return string.IsNullOrWhiteSpace(token) ? "provider" : token;
    }

    private static void WriteCatalog(string path, ModelCatalogFile catalog)
    {
        var json = JsonSerializer.Serialize(catalog, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        var tempPath = $"{path}.tmp";
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, path, overwrite: true);
    }
}
