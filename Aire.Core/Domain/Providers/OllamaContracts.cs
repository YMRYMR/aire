using System.Collections.Generic;

namespace Aire.Domain.Providers
{
    /// <summary>
    /// Normalized result of an Ollama management action.
    /// </summary>
    public sealed record OllamaActionResult(
        bool Succeeded,
        string UserMessage);

    /// <summary>
    /// View-neutral representation of one Ollama model entry.
    /// </summary>
    public sealed record OllamaCatalogItem(
        string DisplayName,
        string ModelName,
        bool IsInstalled,
        string SizeText,
        bool IsRecommended,
        string RecommendationLabel);

    /// <summary>
    /// Result of loading the combined installed and available Ollama catalog.
    /// </summary>
    public sealed record OllamaCatalogResult(
        bool OllamaDetected,
        int InstalledCount,
        IReadOnlyList<OllamaCatalogItem> Items);

    /// <summary>
    /// Hardware guidance shown when onboarding evaluates whether Ollama is a good fit for the machine.
    /// </summary>
    public sealed record OllamaHardwareGuidance(
        string SummaryLine,
        string? WarningLine);

    /// <summary>
    /// View-neutral model entry shown in the onboarding Ollama picker.
    /// </summary>
    public sealed record OnboardingOllamaEntry(
        string ModelName,
        bool IsInstalled,
        string SizeText,
        string ParameterSize,
        string[] Tags,
        bool IsRecommended);

    /// <summary>
    /// Result of building the onboarding Ollama picker state.
    /// </summary>
    public sealed record OnboardingOllamaCatalog(
        string ReadyText,
        string HintText,
        IReadOnlyList<OnboardingOllamaEntry> Entries);
}
