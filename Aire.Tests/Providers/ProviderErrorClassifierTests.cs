using System;
using System.Net;
using System.Net.Http;
using Aire.Services;
using Xunit;

namespace Aire.Tests.Providers;

public class ProviderErrorClassifierTests
{
    [Fact]
    public void Classify_BillingErrorText_ReturnsBillingReason()
    {
        Exception ex = new Exception("payment required: credits exhausted");
        string message;
        CooldownReason actual = ProviderErrorClassifier.Classify(ex, out message);
        Assert.Equal(CooldownReason.BillingError, actual);
        Assert.Contains("credits exhausted", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Classify_QuotaExceededText_ReturnsQuotaReason()
    {
        Exception ex = new Exception("quota exceeded for this month");
        string message;
        CooldownReason actual = ProviderErrorClassifier.Classify(ex, out message);
        Assert.Equal(CooldownReason.QuotaExhausted, actual);
        Assert.Contains("6 hours", message);
    }

    [Fact]
    public void Classify_ServiceUnavailableHttpErrors_ReturnsServiceUnavailable()
    {
        HttpRequestException ex = new HttpRequestException("bad gateway", null, HttpStatusCode.BadGateway);
        string message;
        CooldownReason actual = ProviderErrorClassifier.Classify(ex, out message);
        Assert.Equal(CooldownReason.ServiceUnavailable, actual);
        Assert.Contains("30 minutes", message);
    }

    [Fact]
    public void ExtractReadableMessage_ReturnsTrimmedMessageAndRetryDelay()
    {
        string raw = "prefix text {\r\n  \"error\": {\r\n    \"message\": \"Rate limited. For more information, see docs.\",\r\n    \"details\": [\r\n      { \"retryDelay\": \"32s\" }\r\n    ]\r\n  }\r\n}";
        string actual = ProviderErrorClassifier.ExtractReadableMessage(raw);
        Assert.Equal("Rate limited\n(Retry in 32s)", actual);
    }

    [Fact]
    public void ExtractReadableMessage_TruncatesLongMessages()
    {
        string text = new string('a', 350);
        string raw = "{\"message\":\"" + text + "\"}";
        string text2 = ProviderErrorClassifier.ExtractReadableMessage(raw);
        Assert.NotNull(text2);
        Assert.True(text2.Length <= 301);
        Assert.EndsWith("…", text2);
    }

    [Fact]
    public void ExtractReadableMessage_InvalidPayload_ReturnsNull()
    {
        Assert.Null(ProviderErrorClassifier.ExtractReadableMessage("not-json"));
    }
}
