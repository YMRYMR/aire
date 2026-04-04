using System;
using System.Linq;
using System.Threading.Tasks;
using Aire.Data;
using Aire.Providers;
using Aire.Services.Workflows;
using ChatMessage = Aire.UI.MainWindow.Models.ChatMessage;

namespace Aire
{
    public partial class MainWindow
    {
        private ProviderCoordinator? _providerCoordinator;
        private ProviderCoordinator ProvidersFlow => _providerCoordinator ??= new ProviderCoordinator(this);

        /// <summary>
        /// Owns provider loading and activation for the main window.
        /// The UI still controls the combobox, while selection rules are delegated to ProviderSelectionWorkflowService.
        /// </summary>
        private sealed partial class ProviderCoordinator
        {
            private readonly MainWindow _owner;
            private readonly ProviderSelectionWorkflowService _workflow = new();

            public ProviderCoordinator(MainWindow owner)
            {
                _owner = owner;
            }

            /// <summary>
            /// Loads enabled providers into the picker and activates the most appropriate one.
            /// </summary>
            /// <param name="autoSelect">Whether the first enabled provider should be chosen when there is no saved selection.</param>
            /// <param name="savedProviderId">Previously persisted provider id, if one exists.</param>
            public async Task LoadProvidersAsync(bool autoSelect = true, int? savedProviderId = null)
            {
                var catalog = await _owner._providerCatalogApplicationService.LoadProviderCatalogAsync(autoSelect, savedProviderId);
                _owner._providers = catalog.AllProviders.ToList();
                var supported = catalog.EnabledProviders;

                _owner._suppressProviderChange = true;
                _owner.ProviderComboBox.ItemsSource = supported;

                var resolvedSelection = catalog.SelectedProvider;
                if (resolvedSelection != null)
                {
                    _owner.ProviderComboBox.SelectedItem = resolvedSelection;
                    _owner._suppressProviderChange = false;
                    await UpdateCurrentProviderAsync(showSwitchedMessage: false);
                    return;
                }

                if (supported.Count == 0)
                {
                    _owner.AddToUI(new ChatMessage
                    {
                        Sender = "System",
                        Text = catalog.EmptyStateMessage ?? _workflow.BuildNoProviderMessage(),
                        Timestamp = DateTime.Now.ToString("HH:mm"),
                        BackgroundBrush = MainWindow.SystemBgBrush,
                        SenderForeground = MainWindow.SystemFgBrush
                    });
                }

                _owner._suppressProviderChange = false;
            }

            /// <summary>
            /// Rebuilds the provider picker after settings changes while trying to preserve the active provider.
            /// </summary>
            public async Task RefreshProvidersAsync()
            {
                var selected = _owner.ProviderComboBox.SelectedItem as Provider;
                int? selectedId = selected?.Id;

                _owner._suppressProviderChange = true;
                _owner._providerFactory.ClearCache();
                await LoadProvidersAsync(autoSelect: false);

                if (selectedId.HasValue)
                {
                    var providers = _owner.ProviderComboBox.ItemsSource as IEnumerable<Provider> ?? Enumerable.Empty<Provider>();
                    var same = _owner._providerCatalogApplicationService.ResolveSelectionAfterRefresh(providers, selectedId);
                    if (same != null)
                        _owner.ProviderComboBox.SelectedItem = same;
                }

                _owner._suppressProviderChange = false;
                await UpdateCurrentProviderAsync(showSwitchedMessage: false);
            }

            /// <summary>
            /// Adds the initial placeholder message shown before a provider is selected.
            /// </summary>
            public void LoadWelcomeMessage()
            {
                _owner.Messages.Add(new ChatMessage
                {
                    Sender = "System",
                    Text = "Welcome to Aire. Select a provider above and start chatting.",
                    Timestamp = DateTime.Now.ToString("HH:mm"),
                    BackgroundBrush = MainWindow.SystemBgBrush,
                    SenderForeground = MainWindow.SystemFgBrush
                });
            }

            /// <summary>
            /// Activates the selected provider, updates persistence, and refreshes the active conversation.
            /// </summary>
            /// <param name="showSwitchedMessage">Whether a chat message announcing the provider switch should be added.</param>
            public async Task UpdateCurrentProviderAsync(bool showSwitchedMessage = true)
            {
                var sel = _owner.ProviderComboBox.SelectedItem as Provider;
                if (sel == null) return;

                try
                {
                    var previousProviderId = _owner._currentProviderId;
                    _owner._currentProviderId = sel.Id;
                    var activation = await _owner._providerActivationApplicationService.ActivateProviderAsync(
                        sel,
                        previousProviderId,
                        _owner._currentConversationId,
                        showSwitchedMessage);
                    _owner._currentProvider = activation.ProviderInstance;

                    UpdateCapabilityUi();
                    StartTokenUsageRefreshTimer();
                    var plan = activation.ActivationPlan;

                    if (plan.ConversationAction == ProviderActivationWorkflowService.ConversationActionKind.KeepCurrentConversation)
                    {
                        if (plan.ShouldAnnounceSwitch)
                            _owner.AddSystemMessage(activation.SwitchedProviderMessage);

                        if (_owner._sidebarOpen)
                            await _owner.RefreshSidebarAsync();
                    }
                    else if (plan.ConversationAction == ProviderActivationWorkflowService.ConversationActionKind.LoadExistingConversation)
                    {
                        _owner._currentConversationId = plan.ConversationIdToLoad;
                        await _owner.LoadConversationMessages(plan.ConversationIdToLoad!.Value);
                    }
                    else
                    {
                        await _owner.ConversationFlow.CreateConversationAsync(
                            sel,
                            plan.NewConversationTitle ?? "Chat",
                            plan.NewConversationMessage ?? $"New conversation started with {sel.Name}.");
                    }

                    if (_owner._sidebarOpen)
                        await _owner.RefreshSidebarAsync();
                }
                catch
                {
                    _owner.AddErrorMessage($"Failed to initialize provider '{sel.Name}'.");
                }
            }

        }
    }
}
