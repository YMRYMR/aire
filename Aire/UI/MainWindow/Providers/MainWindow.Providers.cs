using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Aire.AppLayer.Chat;
using Aire.AppLayer.Providers;
using Aire.Data;
using Aire.Providers;
using Aire.Services;
using ChatMessage = Aire.UI.MainWindow.Models.ChatMessage;
using ProviderChatMessage = Aire.Providers.ChatMessage;

namespace Aire
{
    public partial class MainWindow
    {
        private async Task LoadProviders(bool autoSelect = true, int? savedProviderId = null)
            => await ProvidersFlow.LoadProvidersAsync(autoSelect, savedProviderId);

        public async Task RefreshProvidersAsync()
            => await ProvidersFlow.RefreshProvidersAsync();

        private void LoadWelcomeMessage() => ProvidersFlow.LoadWelcomeMessage();

        private async Task UpdateCurrentProvider(bool showSwitchedMessage = true)
            => await ProvidersFlow.UpdateCurrentProviderAsync(showSwitchedMessage);

        private async void ProviderComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressProviderChange) return;
            await UpdateCurrentProvider(showSwitchedMessage: true);
        }

        private void UpdateCapabilityUI() => ProvidersFlow.UpdateCapabilityUi();

        private void StartTokenUsageRefreshTimer() => ProvidersFlow.StartTokenUsageRefreshTimer();

        private void StopTokenUsageRefreshTimer() => ProvidersFlow.StopTokenUsageRefreshTimer();

        private async Task RefreshTokenUsageAsync() => await ProvidersFlow.RefreshTokenUsageAsync();

        private void ShowTokenLimitBubble() => ProvidersFlow.ShowTokenLimitBubble();

        private void UpdateTokenUsageUI()
            => ProvidersFlow.UpdateTokenUsageUi(
                (_providerUiStateApplicationService ?? new ProviderUiStateApplicationService()).BuildTokenUsageUiState(
                    _cachedTokenUsage,
                    _currentProvider?.GetType().Name ?? "AI",
                    _limitReachedNotificationShown,
                    _limitBubbleShown).InputToolTip);

        private static List<ProviderChatMessage> WindowConversation(
            List<ProviderChatMessage> history,
            ContextWindowSettings? settings = null,
            int maxMessages = 40)
            => ProviderCoordinator.WindowConversation(history, settings, maxMessages);

        internal string BuildModelListSection() => ProvidersFlow.BuildModelListSection();

        internal void RefreshProviderAvailabilityUI() => ProvidersFlow.RefreshProviderAvailabilityUi();

        private void ProviderComboBox_DropDownOpened(object sender, EventArgs e)
            => ProvidersFlow.OnProviderComboBoxDropDownOpened();

        internal void CheckAgainButton_Click(object sender, RoutedEventArgs e)
            => ProvidersFlow.ClearSelectedProviderCooldown();
    }
}
