using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Aire.AppLayer.Chat;
using Aire.AppLayer.Providers;
using Aire.Data;
using Aire.Providers;
using Aire.Services;
using Aire.Services.Workflows;
using ComboBox = System.Windows.Controls.ComboBox;
using Color = System.Windows.Media.Color;
using ProviderChatMessage = Aire.Providers.ChatMessage;

namespace Aire
{
    public partial class MainWindow
    {
        private sealed partial class ProviderCoordinator
        {
            private readonly ProviderPresentationWorkflowService _presentationWorkflow = new();

            public void UpdateCapabilityUi()
            {
                var uiStateService = _owner._providerUiStateApplicationService ?? new ProviderUiStateApplicationService();
                var state = uiStateService.BuildCapabilityUiState(
                    _owner._currentProvider,
                    _owner._speechService.HasMic,
                    _owner._speechService.ModelExists,
                    _owner._speechService.UnavailableReason);

                if (!state.CanImages && _owner._attachedImagePath != null)
                {
                    _owner._attachedImagePath = null;
                    _owner.ImagePreviewPanel.Visibility = Visibility.Collapsed;
                }

                _owner.MicButton.IsEnabled = state.MicEnabled;
                _owner.MicButton.ToolTip = state.MicToolTip;
                _owner.ProviderComboBox.ToolTip = state.ProviderToolTip;

                bool canTools = _owner._currentProvider?.Has(ProviderCapabilities.ToolCalling) == true;
                _owner._toolsSupportedByProvider = canTools;
                _owner.UpdateToolsButtonState();
                _owner.RefreshToolsCategoryMenuChecks();
            }

            public static List<ProviderChatMessage> WindowConversation(
                List<ProviderChatMessage> history,
                ContextWindowSettings? settings = null,
                int maxMessages = 40)
                => settings != null
                    ? new ProviderPresentationWorkflowService().TrimConversation(history, settings)
                    : new ProviderPresentationWorkflowService().TrimConversation(history, maxMessages);

            public string BuildModelListSection()
                => _presentationWorkflow.BuildModelListSection(
                    _owner.ProviderComboBox.Items.OfType<Provider>(),
                    id => _owner._availabilityTracker.IsOnCooldown(id));

            public void RefreshProviderAvailabilityUi()
            {
                var uiStateService = _owner._providerUiStateApplicationService ?? new ProviderUiStateApplicationService();
                var state = uiStateService.BuildAvailabilityUiState(
                    _owner._currentProviderId,
                    _owner._availabilityTracker);
                _owner.CheckAgainButton.Visibility = state.IsOnCooldown ? Visibility.Visible : Visibility.Collapsed;

                if (state.IsOnCooldown)
                {
                    _owner.ProviderComboBox.BorderBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0xBB, 0x00));
                    _owner.ProviderComboBox.BorderThickness = new Thickness(2);
                    _owner.ProviderComboBox.ToolTip = state.CooldownMessage;
                    _owner.CheckAgainButton.ToolTip = state.CheckAgainToolTip;
                }
                else
                {
                    _owner.ProviderComboBox.ClearValue(ComboBox.BorderBrushProperty);
                    _owner.ProviderComboBox.ClearValue(ComboBox.BorderThicknessProperty);
                    _owner.ProviderComboBox.ToolTip = null;
                    _owner.CheckAgainButton.ToolTip = state.CheckAgainToolTip;
                }
            }

            public void OnProviderComboBoxDropDownOpened()
            {
                for (int i = 0; i < _owner.ProviderComboBox.Items.Count; i++)
                {
                    var provider = _owner.ProviderComboBox.Items[i] as Provider;
                    if (provider == null) continue;

                    var container = _owner.ProviderComboBox.ItemContainerGenerator.ContainerFromIndex(i) as ComboBoxItem;
                    if (container == null) continue;

                    bool onCooldown = _owner._availabilityTracker.IsOnCooldown(provider.Id);
                    var icon = FindVisualChild<TextBlock>(container, "ProviderWarningIcon");
                    if (icon == null) continue;

                    icon.Visibility = onCooldown ? Visibility.Visible : Visibility.Collapsed;
                    if (onCooldown)
                    {
                        var cd = _owner._availabilityTracker.GetCooldown(provider.Id);
                        if (cd != null)
                            icon.ToolTip = cd.Message;
                    }
                    else
                    {
                        icon.ToolTip = LocalizationService.S("tooltip.providerCooldown", "Provider is on cooldown");
                    }
                }
            }

            public void ClearSelectedProviderCooldown()
            {
                var sel = _owner.ProviderComboBox.SelectedItem as Provider;
                if (sel == null) return;
                _owner._availabilityTracker.ClearCooldown(sel.Id);
            }

            private static T? FindVisualChild<T>(DependencyObject parent, string name)
                where T : FrameworkElement
            {
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
                {
                    var child = VisualTreeHelper.GetChild(parent, i);
                    if (child is T fe && fe.Name == name)
                        return fe;

                    var found = FindVisualChild<T>(child, name);
                    if (found != null)
                        return found;
                }

                return null;
            }
        }
    }
}
