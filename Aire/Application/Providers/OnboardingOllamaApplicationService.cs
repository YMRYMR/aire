using System;
using System.Collections.Generic;
using System.Linq;
using Aire.Domain.Providers;
using Aire.Services;

namespace Aire.AppLayer.Providers
{
    /// <summary>
    /// Application-layer planning for the onboarding Ollama step.
    /// This keeps hardware messaging and recommended-model ordering out of the wizard UI.
    /// </summary>
    public sealed class OnboardingOllamaApplicationService
    {
        private readonly OllamaModelCatalogApplicationService _catalogService = new();

        /// <summary>
        /// Builds the onboarding hardware guidance for the local machine.
        /// </summary>
        /// <param name="profile">Detected machine profile used for local-model recommendations.</param>
        /// <returns>Summary and optional warning text for the onboarding Ollama step.</returns>
        public OllamaHardwareGuidance BuildHardwareGuidance(OllamaService.OllamaSystemProfile profile)
        {
            string summaryLine;
            if (profile.TotalRamGb <= 0)
            {
                summaryLine = "Aire will recommend smaller local models by default.";
            }
            else
            {
                var fit = profile.TotalRamGb switch
                {
                    < 8 => "best for smaller local models",
                    < 16 => "good for small and medium local models",
                    < 32 => "good for medium and some larger local models",
                    _ => "good for medium and larger local models"
                };

                if (profile.VideoRamGb > 0)
                {
                    var gpuLabel = string.IsNullOrWhiteSpace(profile.PrimaryGpuName)
                        ? $"{profile.VideoRamGb:0.#} GB VRAM"
                        : $"{profile.VideoRamGb:0.#} GB VRAM on {profile.PrimaryGpuName}";

                    summaryLine = $"Detected {profile.TotalRamGb:0.#} GB RAM and {gpuLabel}. This PC is {fit}.";
                }
                else
                {
                    summaryLine = $"Detected {profile.TotalRamGb:0.#} GB RAM. This PC is {fit}.";
                }
            }

            string? warningLine = null;
            if (profile.TotalRamGb > 0 && profile.TotalRamGb < 4)
            {
                warningLine =
                    $"⚠  Only {profile.TotalRamGb:0.0} GB RAM detected. Most AI models need at least 4 GB. " +
                    "Ollama may not run well on this machine. Consider using OpenAI or Claude (cloud-based) instead.";
            }
            else if (profile.TotalRamGb <= 0)
            {
                summaryLine = "Could not detect RAM. Most models need at least 4 GB.";
            }

            return new OllamaHardwareGuidance(summaryLine, warningLine);
        }

        /// <summary>
        /// Builds the onboarding model picker from installed and available Ollama models.
        /// Installed models stay first, followed by recommended downloadable models, then the rest.
        /// </summary>
        /// <param name="installed">Models already installed in the local Ollama runtime.</param>
        /// <param name="available">Models known in the curated Ollama catalog.</param>
        /// <param name="profile">Detected machine profile used for recommendations.</param>
        /// <returns>Ready-state text, legend hint, and ordered onboarding entries.</returns>
        public OnboardingOllamaCatalog BuildCatalog(
            IReadOnlyList<OllamaService.OllamaModel> installed,
            IReadOnlyList<OllamaService.OllamaModel> available,
            OllamaService.OllamaSystemProfile profile)
        {
            var profileSummary = BuildHardwareGuidance(profile).SummaryLine;
            var combined = _catalogService.BuildCatalog(installed, available, profile);

            var ordered = combined
                .OrderByDescending(item => item.IsInstalled)
                .ThenByDescending(item => item.IsRecommended)
                .ThenBy(item => item.ModelName, StringComparer.OrdinalIgnoreCase)
                .Select(item =>
                {
                    OllamaService.KnownModelMeta.TryGetValue(item.ModelName, out var meta);
                    var recommendation = OllamaService.GetModelRecommendation(item.ModelName, ParseSizeBytes(item.SizeText), profile);
                    return new OnboardingOllamaEntry(
                        item.ModelName,
                        item.IsInstalled,
                        item.SizeText,
                        meta?.ParamSize ?? string.Empty,
                        recommendation.Badges,
                        item.IsRecommended);
                })
                .ToList();

            var readyText = installed.Count > 0
                ? $"Ollama is running. {installed.Count} model{(installed.Count == 1 ? "" : "s")} installed. {profileSummary}"
                : $"Ollama is running on your machine. {profileSummary}";

            var hintText = installed.Count > 0
                ? "✓ = installed  ·  ★ = recommended for this PC and for Aire  ·  Type to filter."
                : "No models installed yet. ★ marks the models that look like the best fit for this PC and for Aire.";

            return new OnboardingOllamaCatalog(readyText, hintText, ordered);
        }

        private static long ParseSizeBytes(string sizeText)
        {
            if (string.IsNullOrWhiteSpace(sizeText))
                return 0;

            var normalized = sizeText.Replace(',', '.');
            if (normalized.EndsWith(" GB", StringComparison.OrdinalIgnoreCase)
                && double.TryParse(normalized[..^3], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var gb))
            {
                return (long)(gb * 1_073_741_824.0);
            }

            if (normalized.EndsWith(" MB", StringComparison.OrdinalIgnoreCase)
                && double.TryParse(normalized[..^3], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var mb))
            {
                return (long)(mb * 1_048_576.0);
            }

            return 0;
        }
    }
}
