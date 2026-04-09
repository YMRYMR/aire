using System.Threading.Tasks;
using Aire.AppLayer.Providers;
using Aire.Data;
using Aire.Services.Workflows;

namespace Aire
{
    public partial class MainWindow
    {
        private sealed partial class ProviderCoordinator
        {
            private async Task ResetClearedProviderSelectionAsync()
            {
                await _owner._chatService.ClearProviderAsync();
                _owner._currentProvider = null;
                _owner._currentProviderId = null;
                _owner.UpdateCapabilityUI();
            }

            private async Task ApplyActivatedProviderAsync(
                Provider selectedProvider,
                ProviderActivationApplicationService.ProviderActivationResult activation,
                bool showSwitchedMessage)
            {
                _owner._currentProvider = activation.ProviderInstance;

                UpdateCapabilityUi();
                StartTokenUsageRefreshTimer();

                var plan = activation.ActivationPlan;

                if (plan.ConversationAction == ProviderActivationWorkflowService.ConversationActionKind.KeepCurrentConversation)
                {
                    if (plan.ShouldAnnounceSwitch)
                        await _owner.AddSystemMessageAsync(activation.SwitchedProviderMessage);

                    if (_owner._sidebarOpen)
                        await _owner.RefreshSidebarAsync();

                    return;
                }

                if (plan.ConversationAction == ProviderActivationWorkflowService.ConversationActionKind.LoadExistingConversation)
                {
                    _owner._currentConversationId = plan.ConversationIdToLoad;
                    await _owner.LoadConversationMessages(plan.ConversationIdToLoad!.Value, syncProviderSelection: false);
                }
                else
                {
                    await _owner.ConversationFlow.CreateConversationAsync(
                        selectedProvider,
                        plan.NewConversationTitle ?? "Chat",
                        plan.NewConversationMessage ?? $"New conversation started with {selectedProvider.Name}.");
                }

                if (_owner._sidebarOpen)
                    await _owner.RefreshSidebarAsync();
            }
        }
    }
}
