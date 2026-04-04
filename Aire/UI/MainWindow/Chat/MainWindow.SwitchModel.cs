using System;
using System.Linq;
using System.Threading.Tasks;
using Aire.AppLayer.Providers;
using Aire.Services;
using ChatMessage = Aire.UI.MainWindow.Models.ChatMessage;

namespace Aire
{
    public partial class MainWindow
    {
        private async Task HandleSwitchModelAsync(ParsedAiResponse parsed)
            => await HandleSwitchModelAsync(parsed.TextContent, parsed.ToolCall!);

        private async Task HandleSwitchModelAsync(string assistantText, ToolCallRequest toolCall)
        {
            var switchService = _switchModelApplicationService
                ?? new SwitchModelApplicationService(_providerFactory, _chatService, _chatSessionApplicationService);
            var result = await switchService.ExecuteAsync(
                assistantText,
                toolCall,
                ProviderComboBox.Items.OfType<Aire.Data.Provider>(),
                id => _availabilityTracker.IsOnCooldown(id),
                _currentConversationId);

            _conversationHistory.Add(result.AssistantHistoryMessage);
            _conversationHistory.Add(result.ResultHistoryMessage);

            if (!result.Succeeded || result.TargetProvider == null)
                return;

            _suppressProviderChange = true;
            ProviderComboBox.SelectedItem = result.TargetProvider;
            _suppressProviderChange = false;

            _currentProviderId = result.TargetProvider.Id;
            _currentProvider = result.ProviderInstance;
            UpdateCapabilityUI();

            if (!string.IsNullOrEmpty(result.UserFacingMessage))
            {
                AddToUI(new ChatMessage
                {
                    Sender = "System",
                    Text = result.UserFacingMessage,
                    Timestamp = DateTime.Now.ToString("HH:mm"),
                    SenderForeground = SystemFgBrush
                });
            }
        }
    }
}
