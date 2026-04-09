using Aire.AppLayer.Providers;
using Aire.Providers;
using Aire.Domain.Providers;
using Aire.Services;
using Xunit;

namespace Aire.Tests.Services;

public sealed class ProviderUiStateApplicationServiceTests
{
    [Fact]
    public void BuildCapabilityUiState_EnablesImages_WhenProviderSupportsImageInput()
    {
        var service = new ProviderUiStateApplicationService();
        var provider = new FakeProvider(ProviderCapabilities.TextChat | ProviderCapabilities.ImageInput | ProviderCapabilities.ToolCalling);

        var state = service.BuildCapabilityUiState(provider, speechHasMic: true, speechModelExists: false, speechUnavailableReason: null);

        Assert.True(state.CanImages);
        Assert.True(state.MicEnabled);
        Assert.Equal("Click to download the Whisper speech model (~150 MB)", state.MicToolTip);
        Assert.Contains("image", state.ProviderToolTip, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildAvailabilityUiState_UsesCooldownMessageAndClearsExpiredEntries()
    {
        var service = new ProviderUiStateApplicationService();
        var tracker = ProviderAvailabilityTracker.Instance;
        const int providerId = 931;

        try
        {
            tracker.SetCooldown(providerId, CooldownReason.RateLimit, "Try again later.");

            var state = service.BuildAvailabilityUiState(providerId, tracker);

            Assert.True(state.IsOnCooldown);
            Assert.Contains("Try again later.", state.CooldownMessage, StringComparison.Ordinal);
            Assert.Contains("↻", state.CheckAgainToolTip, StringComparison.Ordinal);
        }
        finally
        {
            tracker.ClearCooldown(providerId);
        }
    }

    [Fact]
    public void BuildTokenUsageUiState_ShowsLimitNotifications_AndResetsWhenBelowLimit()
    {
        var service = new ProviderUiStateApplicationService();

        var overLimit = service.BuildTokenUsageUiState(
            new TokenUsage { Used = 100, Limit = 100, Unit = "tokens" },
            "StubProvider",
            limitNotificationShown: false,
            limitBubbleShown: false);

        var belowLimit = service.BuildTokenUsageUiState(
            new TokenUsage { Used = 50, Limit = 100, Unit = "tokens" },
            "StubProvider",
            limitNotificationShown: true,
            limitBubbleShown: true);

        Assert.True(overLimit.ShouldShowLimitNotification);
        Assert.True(overLimit.ShouldShowLimitBubble);
        Assert.Equal("Token Limit Reached", overLimit.NotificationTitle);
        Assert.Contains("StubProvider", overLimit.NotificationBody, StringComparison.Ordinal);

        Assert.False(belowLimit.ShouldShowLimitNotification);
        Assert.False(belowLimit.ShouldShowLimitBubble);
        Assert.True(belowLimit.ResetLimitNotification);
        Assert.True(belowLimit.ResetLimitBubble);
        Assert.Null(belowLimit.NotificationTitle);
    }

    private sealed class FakeProvider : IAiProvider
    {
        public FakeProvider(ProviderCapabilities capabilities)
        {
            CapabilitiesValue = capabilities;
        }

        public string ProviderType => "Fake";
        public string DisplayName => "Fake";
        public ProviderCapabilities CapabilitiesValue { get; }
        public ToolCallMode ToolCallMode => ToolCallMode.TextBased;
        public ToolOutputFormat ToolOutputFormat => ToolOutputFormat.AireText;

        public ProviderCapabilities Capabilities => CapabilitiesValue;

        public bool Has(ProviderCapabilities cap) => (Capabilities & cap) == cap;
        public void Initialize(ProviderConfig config) { }
        public Task<AiResponse> SendChatAsync(IEnumerable<ChatMessage> messages, CancellationToken cancellationToken = default)
            => Task.FromResult(new AiResponse());
        public void PrepareForCapabilityTesting() { }
        public void SetToolsEnabled(bool enabled) { }
        public void SetEnabledToolCategories(IEnumerable<string>? categories) { }
        public async IAsyncEnumerable<string> StreamChatAsync(IEnumerable<ChatMessage> messages, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }
        public Task<ProviderValidationResult> ValidateConfigurationAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(ProviderValidationResult.Ok());
        public Task<TokenUsage?> GetTokenUsageAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<TokenUsage?>(null);
    }
}
