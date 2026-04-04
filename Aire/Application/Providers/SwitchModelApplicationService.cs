using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Aire.AppLayer.Chat;
using Aire.Data;
using Aire.Providers;
using Aire.Services;
using Aire.Services.Workflows;
using ProviderChatMessage = Aire.Providers.ChatMessage;

namespace Aire.AppLayer.Providers
{
    /// <summary>
    /// Application-layer workflow for the <c>switch_model</c> tool call.
    /// </summary>
    public sealed class SwitchModelApplicationService
    {
        /// <summary>
        /// Result of handling a switch-model tool call.
        /// </summary>
        public sealed record SwitchModelResult(
            bool Succeeded,
            string? RequestedModel,
            string? Reason,
            Provider? TargetProvider,
            IAiProvider? ProviderInstance,
            ProviderChatMessage AssistantHistoryMessage,
            ProviderChatMessage ResultHistoryMessage,
            string? UserFacingMessage = null);

        private readonly ProviderFactory _providerFactory;
        private readonly ChatService _chatService;
        private readonly ChatSessionApplicationService _chatSessionService;
        private readonly ToolFollowUpWorkflowService _toolWorkflow = new();

        /// <summary>
        /// Creates the switch-model application service over provider runtime and session-persistence seams.
        /// </summary>
        public SwitchModelApplicationService(
            ProviderFactory providerFactory,
            ChatService chatService,
            ChatSessionApplicationService chatSessionService)
        {
            _providerFactory = providerFactory;
            _chatService = chatService;
            _chatSessionService = chatSessionService;
        }

        /// <summary>
        /// Applies a <c>switch_model</c> request against the available provider list and persists the selected provider when successful.
        /// </summary>
        /// <param name="parsed">Parsed AI response containing the switch-model tool call.</param>
        /// <param name="availableProviders">Enabled providers available in the picker.</param>
        /// <param name="onCooldown">Predicate that returns whether a provider is currently unavailable.</param>
        /// <param name="currentConversationId">Current conversation id, if one exists.</param>
        /// <returns>The resolved target provider plus normalized provider-history entries.</returns>
        public async Task<SwitchModelResult> ExecuteAsync(
            ParsedAiResponse parsed,
            IEnumerable<Provider> availableProviders,
            Func<int, bool> onCooldown,
            int? currentConversationId)
            => await ExecuteAsync(parsed.TextContent, parsed.ToolCall!, availableProviders, onCooldown, currentConversationId);

        /// <summary>
        /// Applies a <c>switch_model</c> request against the available provider list and persists the selected provider when successful.
        /// </summary>
        public async Task<SwitchModelResult> ExecuteAsync(
            string assistantText,
            ToolCallRequest toolCall,
            IEnumerable<Provider> availableProviders,
            Func<int, bool> onCooldown,
            int? currentConversationId)
        {
            var parameters = toolCall.Parameters;
            var modelName = parameters.TryGetProperty("model_name", out var mn)
                ? mn.GetString() ?? string.Empty
                : string.Empty;
            var reason = parameters.TryGetProperty("reason", out var rs)
                ? rs.GetString() ?? string.Empty
                : string.Empty;

            var assistantHistoryMessage = new ProviderChatMessage
            {
                Role = "assistant",
                Content = _toolWorkflow.BuildAssistantToolCallContent(assistantText, toolCall.RawJson)
            };

            if (string.IsNullOrWhiteSpace(modelName))
            {
                return new SwitchModelResult(
                    false,
                    null,
                    reason,
                    null,
                    null,
                    assistantHistoryMessage,
                    new ProviderChatMessage
                    {
                        Role = "user",
                        Content = "[switch_model result]: ERROR — model_name parameter is required."
                    });
            }

            var target = availableProviders.FirstOrDefault(p =>
                string.Equals(p.Model, modelName, StringComparison.OrdinalIgnoreCase) &&
                p.IsEnabled &&
                !onCooldown(p.Id));

            if (target == null)
            {
                return new SwitchModelResult(
                    false,
                    modelName,
                    reason,
                    null,
                    null,
                    assistantHistoryMessage,
                    new ProviderChatMessage
                    {
                        Role = "user",
                        Content = $"[switch_model result]: ERROR — no available provider with model '{modelName}'. Use a model_name that exactly matches one of the models listed in the system prompt."
                    });
            }

            IAiProvider? providerInstance;
            try { providerInstance = _providerFactory.CreateProvider(target); }
            catch { providerInstance = null; }

            await _chatService.SetProviderAsync(target.Id);
            await _chatSessionService.SaveSelectedProviderAsync(target.Id);
            if (currentConversationId.HasValue)
                await _chatSessionService.UpdateConversationProviderAsync(currentConversationId.Value, target.Id);

            var reasonText = string.IsNullOrWhiteSpace(reason) ? string.Empty : $" — {reason}";
            return new SwitchModelResult(
                true,
                modelName,
                reason,
                target,
                providerInstance,
                assistantHistoryMessage,
                new ProviderChatMessage
                {
                    Role = "user",
                    Content = $"[switch_model result]: SUCCESS — now using {target.Name} ({modelName})."
                },
                $"Switched to {target.Name} ({modelName}){reasonText}");
        }
    }
}
