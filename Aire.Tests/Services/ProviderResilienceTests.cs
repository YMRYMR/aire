using System;
using System.Net;
using System.Net.Http;
using Aire.Services;
using Xunit;

namespace Aire.Tests.Services;

public sealed class ProviderResilienceTests
{
    [Theory]
    [InlineData(HttpStatusCode.PaymentRequired, "payment required", CooldownReason.BillingError)]
    [InlineData(HttpStatusCode.ServiceUnavailable, "service unavailable", CooldownReason.ServiceUnavailable)]
    [InlineData(HttpStatusCode.BadGateway, "bad gateway", CooldownReason.ServiceUnavailable)]
    [InlineData(HttpStatusCode.GatewayTimeout, "gateway timeout", CooldownReason.ServiceUnavailable)]
    public void ProviderErrorClassifier_ClassifiesHttpStatusBranches(HttpStatusCode statusCode, string messageText, CooldownReason expected)
    {
        var ex = new HttpRequestException(messageText, null, statusCode);

        var reason = ProviderErrorClassifier.Classify(ex, out var message);

        Assert.Equal(expected, reason);
        Assert.False(string.IsNullOrWhiteSpace(message));
    }

    [Theory]
    [InlineData("quota exceeded for this month", CooldownReason.QuotaExhausted)]
    [InlineData("payment_required", CooldownReason.BillingError)]
    [InlineData("service unavailable 503", CooldownReason.ServiceUnavailable)]
    [InlineData("normal validation error", CooldownReason.None)]
    public void ProviderErrorClassifier_ClassifiesMessageOnlyBranches(string rawMessage, CooldownReason expected)
    {
        var reason = ProviderErrorClassifier.Classify(new InvalidOperationException(rawMessage), out var message);

        Assert.Equal(expected, reason);
        if (expected == CooldownReason.None)
            Assert.Equal(string.Empty, message);
        else
            Assert.False(string.IsNullOrWhiteSpace(message));
    }

    [Fact]
    public void ProviderErrorClassifier_ExtractReadableMessage_ParsesAndTrimsLongJsonErrors()
    {
        var raw = "prefix " +
                  """
                  {
                    "error": {
                      "message": "A very long provider error message that explains the issue in detail. For more information see docs.",
                      "details": [{ "retryDelay": "30s" }]
                    }
                  }
                  """;

        var message = ProviderErrorClassifier.ExtractReadableMessage(raw);

        Assert.NotNull(message);
        Assert.Contains("A very long provider error message", message, StringComparison.Ordinal);
        Assert.DoesNotContain("For more information", message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Retry in 30s", message, StringComparison.Ordinal);
    }

    [Fact]
    public void ProviderErrorClassifier_ExtractReadableMessage_ReturnsNullForInvalidPayloads()
    {
        Assert.Null(ProviderErrorClassifier.ExtractReadableMessage(string.Empty));
        Assert.Null(ProviderErrorClassifier.ExtractReadableMessage("plain text only"));
        Assert.Null(ProviderErrorClassifier.ExtractReadableMessage("{not json"));
    }

    [Fact]
    public void ProviderAvailabilityTracker_RaisesEventsAndClearsExpiredEntries()
    {
        var tracker = ProviderAvailabilityTracker.Instance;
        const int providerId = 812345;
        var changedCount = 0;
        tracker.AvailabilityChanged += OnChanged;

        try
        {
            tracker.ClearCooldown(providerId);

            tracker.SetCooldown(providerId, CooldownReason.ServiceUnavailable, "temporary");
            Assert.True(tracker.IsOnCooldown(providerId));
            Assert.Equal(CooldownReason.ServiceUnavailable, tracker.GetCooldown(providerId)?.Reason);

            tracker.ClearCooldown(providerId);
            Assert.False(tracker.IsOnCooldown(providerId));
            Assert.Null(tracker.GetCooldown(providerId));
            Assert.True(changedCount >= 2);
        }
        finally
        {
            tracker.AvailabilityChanged -= OnChanged;
            tracker.ClearCooldown(providerId);
        }

        void OnChanged(int id)
        {
            if (id == providerId)
                changedCount++;
        }
    }
}
