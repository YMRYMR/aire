using Aire.Data;

namespace Aire.Providers;

/// <summary>
/// Metadata surface used by settings and onboarding to render provider-specific fields and actions.
/// </summary>
public interface IProviderMetadata
{
    string ProviderType { get; }
    string DisplayName { get; }
    ProviderFieldHints FieldHints { get; }
    IReadOnlyList<ProviderAction> Actions { get; }
    /// <summary>Returns the curated default model list shipped with the app for this provider.</summary>
    List<ModelDefinition> GetDefaultModels();
    /// <summary>Fetches live models from the provider when the remote API supports it.</summary>
    /// <param name="apiKey">Optional API key or token needed by the provider.</param>
    /// <param name="baseUrl">Optional custom base URL for self-hosted or compatible APIs.</param>
    /// <param name="ct">Cancellation token for the remote lookup.</param>
    /// <returns>Live model metadata, or <see langword="null"/> when the provider does not support discovery.</returns>
    Task<List<ModelDefinition>?> FetchLiveModelsAsync(string? apiKey, string? baseUrl, CancellationToken ct);
}

/// <summary>
/// UI hints that control which provider configuration fields should be shown and how they are labeled.
/// </summary>
public class ProviderFieldHints
{
    public bool ShowApiKey { get; init; } = true;
    public string ApiKeyLabel { get; init; } = "API Key";
    public bool ApiKeyRequired { get; init; } = true;
    public bool ShowBaseUrl { get; init; } = true;
}

/// <summary>
/// Describes an extra provider-specific action exposed by onboarding or settings.
/// </summary>
public class ProviderAction
{
    public required string Id { get; init; }
    public required string Label { get; init; }
    public string? LocalizationKey { get; init; }
    public ProviderActionPlacement Placement { get; init; }
}

/// <summary>
/// Where a provider-specific action button should appear in the configuration UI.
/// </summary>
public enum ProviderActionPlacement
{
    ApiKeyArea,
    ModelArea
}
