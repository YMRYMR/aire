using System.Threading.Tasks;
using Aire.Data;
using Aire.Providers;
using Aire.Services;
using Aire.Services.Workflows;

namespace Aire.AppLayer.Providers
{
    /// <summary>
    /// Application-layer workflow for activating one provider and planning the conversation transition that follows.
    /// </summary>
    public sealed class ProviderActivationApplicationService
    {
        /// <summary>
        /// Result of activating a provider and planning the next conversation state.
        /// </summary>
        public sealed record ProviderActivationResult(
            IAiProvider? ProviderInstance,
            ProviderActivationWorkflowService.ProviderActivationPlan ActivationPlan,
            string SwitchedProviderMessage);

        private readonly ChatService _chatService;
        private readonly ProviderFactory _providerFactory;
        private readonly Chat.ChatSessionApplicationService _chatSessionService;
        private readonly ProviderActivationWorkflowService _activationWorkflow = new();
        private readonly ProviderSelectionWorkflowService _selectionWorkflow = new();

        /// <summary>
        /// Creates the provider-activation application service over the runtime provider and persistence seams.
        /// </summary>
        public ProviderActivationApplicationService(
            ChatService chatService,
            ProviderFactory providerFactory,
            Chat.ChatSessionApplicationService chatSessionService)
        {
            _chatService = chatService;
            _providerFactory = providerFactory;
            _chatSessionService = chatSessionService;
        }

        /// <summary>
        /// Activates the selected provider, persists the selection, and builds the next conversation plan.
        /// </summary>
        public async Task<ProviderActivationResult> ActivateProviderAsync(
            Provider provider,
            int? previousProviderId,
            int? currentConversationId,
            bool showSwitchedMessage)
        {
            await _chatService.SetProviderAsync(provider.Id);
            await _chatSessionService.SaveSelectedProviderAsync(provider.Id);

            IAiProvider? providerInstance;
            try { providerInstance = _providerFactory.CreateProvider(provider); }
            catch { providerInstance = null; }

            var latestConversation = currentConversationId.HasValue
                ? null
                : await _chatSessionService.GetLatestConversationAsync(provider.Id);

            var plan = _activationWorkflow.BuildPlan(
                previousProviderId,
                provider.Id,
                currentConversationId,
                latestConversation,
                provider.Name,
                showSwitchedMessage);

            if (plan.ConversationAction == ProviderActivationWorkflowService.ConversationActionKind.KeepCurrentConversation &&
                currentConversationId.HasValue)
            {
                await _chatSessionService.UpdateConversationProviderAsync(currentConversationId.Value, provider.Id);
            }

            return new ProviderActivationResult(
                providerInstance,
                plan,
                _selectionWorkflow.BuildSwitchedProviderMessage(provider.Name));
        }
    }
}
