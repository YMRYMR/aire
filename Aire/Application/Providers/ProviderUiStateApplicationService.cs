using Aire.Providers;
using Aire.Services;
using Aire.Services.Workflows;

namespace Aire.AppLayer.Providers
{
    /// <summary>
    /// Computes provider-related UI state from application/runtime data without depending on WPF types.
    /// </summary>
    public sealed class ProviderUiStateApplicationService
    {
        /// <summary>
        /// Presentation state for provider capabilities and microphone availability.
        /// </summary>
        public sealed record CapabilityUiState(
            bool CanImages,
            bool MicEnabled,
            string MicToolTip,
            string ProviderToolTip);

        /// <summary>
        /// Presentation state for provider cooldown UI.
        /// </summary>
        public sealed record AvailabilityUiState(
            bool IsOnCooldown,
            string? CooldownMessage,
            string CheckAgainToolTip);

        /// <summary>
        /// Presentation state for token usage and limit-reached UX.
        /// </summary>
        public sealed record TokenUsageUiState(
            string? InputToolTip,
            bool ShouldShowLimitNotification,
            bool ResetLimitNotification,
            bool ShouldShowLimitBubble,
            bool ResetLimitBubble,
            string? NotificationTitle = null,
            string? NotificationBody = null);

        private readonly ProviderPresentationWorkflowService _presentationWorkflow = new();

        /// <summary>
        /// Builds capability-related UI state for the active provider.
        /// </summary>
        public CapabilityUiState BuildCapabilityUiState(
            IAiProvider? provider,
            bool speechHasMic,
            bool speechModelExists,
            string? speechUnavailableReason)
        {
            var cap = provider?.Capabilities ?? ProviderCapabilities.None;
            bool canImages = (cap & ProviderCapabilities.ImageInput) != 0;

            string micToolTip;
            bool micEnabled;
            if (!speechHasMic)
            {
                micEnabled = false;
                micToolTip = speechUnavailableReason ?? "No microphone found.";
            }
            else if (!speechModelExists)
            {
                micEnabled = true;
                micToolTip = "Click to download the Whisper speech model (~150 MB)";
            }
            else
            {
                micEnabled = true;
                micToolTip = "Start voice input";
            }

            return new CapabilityUiState(
                canImages,
                micEnabled,
                micToolTip,
                _presentationWorkflow.BuildCapabilityTooltip(cap));
        }

        /// <summary>
        /// Builds cooldown-related UI state for the currently selected provider.
        /// </summary>
        public AvailabilityUiState BuildAvailabilityUiState(int? currentProviderId, ProviderAvailabilityTracker tracker)
        {
            bool isOnCooldown = currentProviderId.HasValue && tracker.IsOnCooldown(currentProviderId.Value);
            if (!isOnCooldown)
            {
                return new AvailabilityUiState(
                    false,
                    null,
                    LocalizationService.S("tooltip.checkAvailability", "Check if provider is available again"));
            }

            var entry = currentProviderId.HasValue ? tracker.GetCooldown(currentProviderId.Value) : null;
            var message = entry?.Message ?? "Provider on cooldown";
            var suffix = LocalizationService.S("cooldown.suffix", "Click ↻ to clear the cooldown and retry.");
            return new AvailabilityUiState(true, $"{message}\n\n{suffix}", $"{message}\n\n{suffix}");
        }

        /// <summary>
        /// Builds token-usage UI state and one-shot notification decisions.
        /// </summary>
        public TokenUsageUiState BuildTokenUsageUiState(
            TokenUsage? usage,
            string providerDisplayName,
            bool limitNotificationShown,
            bool limitBubbleShown)
        {
            string? inputToolTip = null;
            if (usage != null && usage.Limit.HasValue && usage.Limit.Value > 0)
            {
                double percentage = usage.Percentage * 100;
                var usedFormatted = FormatTokenUsageNumber(usage.Used, usage.Unit);
                var limitFormatted = FormatTokenUsageNumber(usage.Limit.Value, usage.Unit);
                var remainingFormatted = usage.Remaining.HasValue
                    ? FormatTokenUsageNumber(usage.Remaining.Value, usage.Unit)
                    : "?";
                var resetText = usage.ResetDate.HasValue
                    ? $"\nReset on {usage.ResetDate.Value:yyyy-MM-dd HH:mm}"
                    : string.Empty;

                inputToolTip =
                    $"Token Usage: Used: {usedFormatted} {usage.Unit} / {limitFormatted} {usage.Unit} ({percentage:F1}%) Remaining: {remainingFormatted} {usage.Unit}{resetText}";
            }

            bool isLimitReached = usage?.IsLimitReached == true;
            return new TokenUsageUiState(
                inputToolTip,
                ShouldShowLimitNotification: isLimitReached && !limitNotificationShown,
                ResetLimitNotification: usage != null && !usage.IsLimitReached,
                ShouldShowLimitBubble: isLimitReached && !limitBubbleShown,
                ResetLimitBubble: usage != null && !usage.IsLimitReached,
                NotificationTitle: isLimitReached ? "Token Limit Reached" : null,
                NotificationBody: isLimitReached
                    ? $"You've used all available tokens for {providerDisplayName}. Consider switching models or waiting until the limit resets."
                    : null);
        }

        private static string FormatNumber(long number)
        {
            if (number >= 1_000_000_000) return (number / 1_000_000_000.0).ToString("F1") + "B";
            if (number >= 1_000_000) return (number / 1_000_000.0).ToString("F1") + "M";
            if (number >= 1_000) return (number / 1_000.0).ToString("F1") + "K";
            return number.ToString();
        }

        private static string FormatTokenUsageNumber(long number, string unit)
        {
            if (unit == "USD")
            {
                double dollars = number / 100.0;
                if (dollars >= 1_000_000_000) return (dollars / 1_000_000_000.0).ToString("F1") + "B";
                if (dollars >= 1_000_000) return (dollars / 1_000_000.0).ToString("F1") + "M";
                if (dollars >= 1_000) return (dollars / 1_000.0).ToString("F1") + "K";
                return dollars.ToString("F2");
            }

            return FormatNumber(number);
        }
    }
}
