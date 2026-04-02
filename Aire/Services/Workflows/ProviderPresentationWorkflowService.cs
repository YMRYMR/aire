using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Aire.AppLayer.Chat;
using Aire.Data;
using Aire.Providers;
using Aire.Services;
using ProviderChatMessage = Aire.Providers.ChatMessage;

namespace Aire.Services.Workflows
{
    /// <summary>
    /// Owns provider-facing presentation rules that do not need direct access to WPF controls.
    /// </summary>
    public sealed class ProviderPresentationWorkflowService
    {
        private readonly ConversationContextApplicationService _contextService = new();

        /// <summary>
        /// Trims provider history to the system messages plus the latest non-system turns.
        /// </summary>
        /// <param name="history">Current provider conversation history.</param>
        /// <param name="maxMessages">Maximum number of non-system messages to retain.</param>
        /// <returns>A trimmed provider-history list ready for the next request.</returns>
        public List<ProviderChatMessage> TrimConversation(IReadOnlyList<ProviderChatMessage> history, int maxMessages = 40)
            => _contextService.BuildContextWindow(history, maxMessages);

        /// <summary>
        /// Trims provider history using the persisted context-window settings.
        /// </summary>
        public List<ProviderChatMessage> TrimConversation(
            IReadOnlyList<ProviderChatMessage> history,
            ContextWindowSettings settings)
            => _contextService.BuildContextWindow(
                history,
                settings.MaxMessages,
                settings.AnchorMessages,
                settings.UncachedRecentMessages,
                settings.EnablePromptCaching,
                settings.EnableConversationSummaries,
                settings.SummaryMaxCharacters);

        /// <summary>
        /// Builds the model-switch helper text presented to tool-calling models.
        /// </summary>
        /// <param name="providers">Enabled providers visible in the picker.</param>
        /// <param name="isOnCooldown">Delegate used to mark unavailable providers.</param>
        /// <returns>A plain-text list of available model names and status notes.</returns>
        public string BuildModelListSection(IEnumerable<Provider> providers, System.Func<int, bool> isOnCooldown)
        {
            var sb = new StringBuilder();
            sb.AppendLine("\n\nAVAILABLE MODELS FOR switch_model:");
            sb.AppendLine("Use model_name exactly as shown (the model ID, not the provider name).");
            sb.AppendLine("Only switch when you have a clear reason (e.g. the task needs vision, or you hit a rate limit).");

            int i = 1;
            foreach (var provider in providers.Where(p => p.IsEnabled))
            {
                var tags = new List<string>();
                try
                {
                    var metadata = ProviderFactory.GetMetadata(provider.Type) as IAiProvider;
                    if (metadata != null)
                    {
                        if (metadata.Has(ProviderCapabilities.ImageInput)) tags.Add("vision");
                        if (metadata.Has(ProviderCapabilities.ToolCalling)) tags.Add("tools");
                    }
                }
                catch (Exception ex)
                {
                    AppLogger.Warn("ProviderPresentation.BuildList", $"Failed to read capabilities for provider type '{provider.Type}': {ex.Message}");
                }

                string status = isOnCooldown(provider.Id)
                    ? " [UNAVAILABLE — on cooldown]"
                    : " [available]";
                string tagString = tags.Count > 0 ? $" ({string.Join(", ", tags)})" : string.Empty;
                sb.AppendLine($"{i++}. model_name=\"{provider.Model}\"  provider={provider.Name}{tagString}{status}");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Builds the short tooltip text that describes a provider's visible capabilities.
        /// </summary>
        /// <param name="capabilities">Capability flags reported by the current provider.</param>
        /// <returns>A tooltip string, or an empty string when nothing special should be shown.</returns>
        public string BuildCapabilityTooltip(ProviderCapabilities capabilities)
        {
            if (capabilities == ProviderCapabilities.None)
                return string.Empty;

            var parts = new List<string>();
            if ((capabilities & ProviderCapabilities.ImageInput) != 0) parts.Add("images");
            if ((capabilities & ProviderCapabilities.ToolCalling) != 0) parts.Add("tool calling");
            if ((capabilities & ProviderCapabilities.Streaming) != 0) parts.Add("streaming");
            return parts.Count == 0 ? string.Empty : "Supports: " + string.Join(", ", parts);
        }
    }
}
