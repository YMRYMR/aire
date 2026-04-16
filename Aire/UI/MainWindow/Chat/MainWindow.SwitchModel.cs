using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Aire.AppLayer.Providers;
using Aire.Providers;
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
            {
                if (_agentModeService?.IsActive == true)
                {
                    var preferImageInput = ShouldPreferImageFallback(assistantText, result.Reason, result.UserFacingMessage);
                    var switched = await TrySwitchOrchestratorFallbackProviderAsync(
                        _currentProviderId ?? -1,
                        preferImageInput);

                    if (switched)
                    {
                        var fallbackMessage = string.IsNullOrWhiteSpace(result.Reason)
                            ? "The requested model was unavailable, so I switched to a different model and will continue."
                            : $"The requested model was unavailable ({result.Reason}), so I switched to a different model and will continue.";
                        await AddOrchestratorNarrativeAsync(fallbackMessage);
                        _agentModeService?.ClearTaskFailures("switch_model");
                        return;
                    }
                }

                _agentModeService?.RecordTaskFailure(
                    "switch_model",
                    string.IsNullOrWhiteSpace(result.UserFacingMessage) ? result.Reason ?? "switch_model failed" : result.UserFacingMessage);
                return;
            }

            _agentModeService?.ClearTaskFailures("switch_model");

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

        private static bool TryParseFailureProviderId(string signature, out int providerId)
        {
            providerId = default;
            if (string.IsNullOrWhiteSpace(signature))
                return false;

            var separator = signature.IndexOf(':');
            var providerPart = separator > 0 ? signature[..separator] : signature;
            return int.TryParse(providerPart, out providerId);
        }

        private async Task<bool> TrySwitchOrchestratorFallbackProviderAsync(int excludeProviderId)
            => await TrySwitchOrchestratorFallbackProviderAsync(excludeProviderId, preferImageInput: false);

        private async Task<bool> TrySwitchOrchestratorFallbackProviderAsync(int excludeProviderId, bool preferImageInput)
        {
            var failedProviderIds = new HashSet<int>();
            if (_agentModeService?.IsActive == true)
            {
                foreach (var signature in _agentModeService.GetTaskFailureSignatures("provider-response"))
                {
                    if (TryParseFailureProviderId(signature, out var failedProviderId))
                        failedProviderIds.Add(failedProviderId);
                }
            }

            var candidates = ProviderComboBox.Items
                .OfType<Aire.Data.Provider>()
                .Where(provider => provider.IsEnabled)
                .Where(provider => provider.Id != excludeProviderId)
                .Where(provider => !failedProviderIds.Contains(provider.Id))
                .Where(provider => !_availabilityTracker.IsOnCooldown(provider.Id))
                .ToList();

            if (candidates.Count == 0)
                return false;

            var currentModel = (_currentProviderId.HasValue
                ? ProviderComboBox.Items.OfType<Aire.Data.Provider>().FirstOrDefault(provider => provider.Id == _currentProviderId.Value)?.Model
                : null) ?? string.Empty;

            var resolvedCandidates = new List<(Aire.Data.Provider Provider, IAiProvider Instance, bool SupportsImage)>();

            foreach (var target in candidates)
            {
                IAiProvider? providerInstance;
                try
                {
                    providerInstance = _providerFactory.CreateProvider(target);
                }
                catch
                {
                    continue;
                }

                if (providerInstance == null)
                    continue;

                if (providerInstance is BaseAiProvider baseProvider)
                {
                    var configuredMaxTokens = baseProvider.GetConfiguredMaxTokens();
                    var boostedMaxTokens = Math.Min(Math.Max(configuredMaxTokens * 2, 8192), 32768);
                    baseProvider.SetMaxTokens(boostedMaxTokens);
                }

                resolvedCandidates.Add((target, providerInstance, providerInstance.Has(Providers.ProviderCapabilities.ImageInput)));
            }

            if (resolvedCandidates.Count == 0)
                return false;

            var orderedCandidates = preferImageInput
                ? resolvedCandidates
                    .OrderByDescending(candidate => candidate.SupportsImage)
                    .ThenBy(candidate => string.Equals(candidate.Provider.Model, currentModel, StringComparison.OrdinalIgnoreCase) ? 1 : 0)
                    .ToList()
                : resolvedCandidates
                    .OrderBy(candidate => string.Equals(candidate.Provider.Model, currentModel, StringComparison.OrdinalIgnoreCase) ? 1 : 0)
                    .ThenByDescending(candidate => candidate.SupportsImage)
                    .ToList();

            foreach (var candidate in orderedCandidates)
            {
                _suppressProviderChange = true;
                ProviderComboBox.SelectedItem = candidate.Provider;
                _suppressProviderChange = false;

                _currentProviderId = candidate.Provider.Id;
                _currentProvider = candidate.Instance;
                UpdateCapabilityUI();

                await _chatService.SetProviderAsync(candidate.Instance);
                await _chatSessionApplicationService.SaveSelectedProviderAsync(candidate.Provider.Id);
                if (_currentConversationId.HasValue)
                    await _chatSessionApplicationService.UpdateConversationProviderAsync(_currentConversationId.Value, candidate.Provider.Id);

                return true;
            }

            return false;
        }

        private static bool ShouldPreferImageFallback(string? assistantText, string? reason, string? userFacingMessage)
        {
            var text = string.Join(' ', new[] { assistantText, reason, userFacingMessage }
                .Where(value => !string.IsNullOrWhiteSpace(value)));

            if (string.IsNullOrWhiteSpace(text))
                return false;

            return text.Contains("vision", StringComparison.OrdinalIgnoreCase)
                || text.Contains("image", StringComparison.OrdinalIgnoreCase)
                || text.Contains("screenshot", StringComparison.OrdinalIgnoreCase)
                || text.Contains("browser", StringComparison.OrdinalIgnoreCase)
                || text.Contains("picture", StringComparison.OrdinalIgnoreCase)
                || text.Contains("photo", StringComparison.OrdinalIgnoreCase);
        }
    }
}
