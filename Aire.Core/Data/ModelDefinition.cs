namespace Aire.Data;

/// <summary>
/// Represents a single AI model entry in the catalog.
/// Used by both JSON files and provider metadata for model dropdowns.
/// </summary>
public class ModelDefinition
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public long SizeBytes { get; set; }

    /// <summary>Runtime flag — true when the model is locally installed (Ollama).</summary>
    public bool IsInstalled { get; set; }

    /// <summary>Optional list of capabilities this model supports (e.g. "vision", "tools", "mouse", "keyboard").</summary>
    public List<string>? Capabilities { get; set; }
}

/// <summary>
/// Root object in each models-*.json file.
/// </summary>
public class ModelCatalogFile
{
    public string ProviderType { get; set; } = string.Empty;
    public List<ModelDefinition> Models { get; set; } = new();
}
