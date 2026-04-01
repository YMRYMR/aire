using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Aire.AppLayer.Providers;
using Aire.Services;
using ChatMessage = Aire.UI.MainWindow.Models.ChatMessage;
using Brushes = System.Windows.Media.Brushes;

namespace Aire
{
    public partial class MainWindow
    {
        private sealed partial class ProviderCoordinator
        {
            public void StartTokenUsageRefreshTimer()
            {
                StopTokenUsageRefreshTimer();
                if (_owner._currentProvider == null) return;

                _owner._tokenUsageRefreshTimer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromMinutes(5)
                };
                _owner._tokenUsageRefreshTimer.Tick += async (_, _) => await RefreshTokenUsageAsync();
                _owner._tokenUsageRefreshTimer.Start();
                _ = RefreshTokenUsageAsync();
            }

            public void StopTokenUsageRefreshTimer()
            {
                _owner._tokenUsageRefreshTimer?.Stop();
                _owner._tokenUsageRefreshTimer = null;
            }

            public async Task RefreshTokenUsageAsync()
            {
                if (_owner._currentProvider == null) return;

                try
                {
                    var usage = await TokenUsageService.GetTokenUsageAsync(
                        _owner._currentProvider,
                        forceRefresh: false,
                        System.Threading.CancellationToken.None);
                    _owner._cachedTokenUsage = usage;
                    var uiStateService = _owner._providerUiStateApplicationService ?? new ProviderUiStateApplicationService();
                    var state = uiStateService.BuildTokenUsageUiState(
                        usage,
                        _owner._currentProvider.GetType().Name,
                        _owner._limitReachedNotificationShown,
                        _owner._limitBubbleShown);
                    UpdateTokenUsageUi(state.InputToolTip);

                    if (state.ShouldShowLimitNotification)
                    {
                        _owner.TrayService?.ShowNotification(
                            state.NotificationTitle ?? "Token Limit Reached",
                            state.NotificationBody ?? string.Empty);
                        _owner._limitReachedNotificationShown = true;
                    }
                    else if (state.ResetLimitNotification)
                    {
                        _owner._limitReachedNotificationShown = false;
                    }

                    if (state.ShouldShowLimitBubble)
                    {
                        ShowTokenLimitBubble();
                        _owner._limitBubbleShown = true;
                    }
                    else if (state.ResetLimitBubble)
                    {
                        _owner._limitBubbleShown = false;
                    }
                }
                catch (Exception ex)
                {
                    AppLogger.Warn("ProviderCoordinator.UpdateTokenUsage", "Failed to update token usage display", ex);
                }
            }

            public void ShowTokenLimitBubble()
            {
                if (_owner._limitBubbleShown) return;

                var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
                var now = DateTime.Now;
                var msg = new ChatMessage
                {
                    Sender = "System",
                    Text = string.Empty,
                    Timestamp = now.ToString("HH:mm"),
                    MessageDate = now,
                    BackgroundBrush = Brushes.LightGoldenrodYellow,
                    SenderForeground = Brushes.Black,
                    FollowUpQuestion = "Token limit reached for the current model. Would you like to switch to another model?",
                    FollowUpOptions = new List<string> { "Switch model", "Keep using this model" },
                    AnswerTcs = tcs,
                };
                _owner.AddToUI(msg);
                _owner._limitBubbleShown = true;

                _ = Task.Run(async () =>
                {
                    var answer = await tcs.Task;
                    if (answer == "Switch model")
                        _owner.Dispatcher.Invoke(() => _owner.ProviderComboBox.IsDropDownOpen = true);
                });
            }

            public void UpdateTokenUsageUi(string? toolTip)
            {
                _owner.InputTextBox.ToolTip = toolTip;
            }
        }
    }
}
