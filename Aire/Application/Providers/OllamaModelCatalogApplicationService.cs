using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Aire.Domain.Providers;
using Aire.Services;

namespace Aire.AppLayer.Providers
{
    /// <summary>
    /// Application-layer workflow for building the combined Ollama model catalog shown in the settings editor.
    /// This keeps recommendation and installed-vs-available catalog shaping out of the WPF layer.
    /// </summary>
    public sealed class OllamaModelCatalogApplicationService
    {
        /// <summary>
        /// Loads the current Ollama state and returns a combined list of installed and available models.
        /// </summary>
        /// <param name="baseUrl">Optional Ollama base URL for the selected provider.</param>
        /// <returns>Combined catalog plus install-detection state.</returns>
        public async Task<OllamaCatalogResult> LoadCatalogAsync(string? baseUrl)
        {
            using var ollamaService = new OllamaService();
            var ollamaRunning = await ollamaService.IsOllamaReachableAsync(baseUrl);
            var ollamaDetected = ollamaRunning || OllamaService.IsOllamaInPath();

            var installed = await ollamaService.GetInstalledModelsAsync(baseUrl);
            var available = await ollamaService.GetAvailableModelsAsync();
            var items = BuildCatalog(installed, available, OllamaService.GetLocalSystemProfile());

            return new OllamaCatalogResult(ollamaDetected, installed.Count, items);
        }

        /// <summary>
        /// Loads installed Ollama models with a timeout and falls back to an empty catalog if the local endpoint hangs.
        /// </summary>
        /// <param name="baseUrl">Optional Ollama base URL for the selected provider.</param>
        /// <param name="timeout">Maximum time to wait for the installed-model query.</param>
        /// <returns>Combined catalog items built from the installed and available model lists.</returns>
        public async Task<IReadOnlyList<OllamaCatalogItem>> LoadCatalogWithTimeoutAsync(string? baseUrl, TimeSpan timeout)
        {
            using var ollamaService = new OllamaService();
            var installedTask = ollamaService.GetInstalledModelsAsync(baseUrl);
            if (await Task.WhenAny(installedTask, Task.Delay(timeout)) != installedTask)
                return Array.Empty<OllamaCatalogItem>();

            var installed = await installedTask;
            var available = await ollamaService.GetAvailableModelsAsync();
            return BuildCatalog(installed, available, OllamaService.GetLocalSystemProfile());
        }

        /// <summary>
        /// Builds the combined installed and available catalog and annotates each model for Aire-specific recommendations.
        /// </summary>
        /// <param name="installed">Models already installed in the local Ollama runtime.</param>
        /// <param name="available">Models known in the curated Ollama catalog.</param>
        /// <param name="profile">Detected local hardware profile used for recommendations.</param>
        /// <returns>Ordered combined catalog entries for the UI layer.</returns>
        public IReadOnlyList<OllamaCatalogItem> BuildCatalog(
            IReadOnlyList<OllamaService.OllamaModel> installed,
            IReadOnlyList<OllamaService.OllamaModel> available,
            OllamaService.OllamaSystemProfile profile)
        {
            var items = new List<OllamaCatalogItem>();
            var installedNames = installed.Select(m => m.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

            OllamaCatalogItem BuildItem(OllamaService.OllamaModel model, bool isInstalled)
            {
                var sizeText = FormatModelSize(model.Size);
                var recommendation = OllamaService.GetModelRecommendation(model.Name, model.Size, profile);
                var prefix = isInstalled ? "✓ " : recommendation.RecommendedForThisPc ? "★ " : string.Empty;
                var sizePart = string.IsNullOrEmpty(sizeText) ? string.Empty : $"  ({sizeText})";
                var labelPart = string.IsNullOrEmpty(recommendation.SummaryLabel) ? string.Empty : $"  ·  {recommendation.SummaryLabel}";

                return new OllamaCatalogItem(
                    $"{prefix}{model.Name}{sizePart}{labelPart}",
                    model.Name,
                    isInstalled,
                    sizeText,
                    recommendation.RecommendedForThisPc,
                    recommendation.SummaryLabel);
            }

            foreach (var model in installed.OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase))
                items.Add(BuildItem(model, isInstalled: true));

            foreach (var model in available.Where(m => !installedNames.Contains(m.Name))
                                           .OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase))
                items.Add(BuildItem(model, isInstalled: false));

            return items;
        }

        /// <summary>
        /// Formats a raw model size into a compact MB/GB label for the UI.
        /// </summary>
        public static string FormatModelSize(long bytes)
        {
            if (bytes <= 0)
                return string.Empty;

            double gb = bytes / 1_073_741_824.0;
            if (gb >= 1.0)
                return $"{gb:0.0} GB";

            double mb = bytes / 1_048_576.0;
            return $"{mb:0} MB";
        }
    }
}
