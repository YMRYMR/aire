using System.Net;
using System.Text.Json;

namespace Aire.Services
{
    /// <summary>
    /// Classifies provider exceptions into cooldown reasons.
    /// </summary>
    public static class ProviderErrorClassifier
    {
        public static CooldownReason Classify(Exception ex, out string message)
        {
            var text = ex.Message ?? string.Empty;

            if (ex is HttpRequestException httpEx && httpEx.StatusCode.HasValue)
            {
                switch (httpEx.StatusCode.Value)
                {
                    case HttpStatusCode.TooManyRequests:
                        message = "Rate limit reached. Cooling down for 5 minutes.";
                        return CooldownReason.RateLimit;

                    case HttpStatusCode.PaymentRequired:
                        message = "Payment required or credits exhausted.";
                        return CooldownReason.BillingError;

                    case HttpStatusCode.Forbidden when ContainsAny(text, "billing", "payment", "credit", "quota"):
                        message = "Billing or quota error. Please check your account.";
                        return CooldownReason.BillingError;

                    case HttpStatusCode.ServiceUnavailable:
                    case HttpStatusCode.BadGateway:
                    case HttpStatusCode.GatewayTimeout:
                        message = "Service temporarily unavailable. Retrying in 30 minutes.";
                        return CooldownReason.ServiceUnavailable;
                }
            }

            if (ContainsAny(text, "rate limit", "ratelimit", "rate_limit", "too many requests", "429"))
            {
                message = "Rate limit reached. Cooling down for 5 minutes.";
                return CooldownReason.RateLimit;
            }

            if (ContainsAny(text, "quota exceeded", "quota_exceeded", "quota_limit", "monthly limit", "daily limit", "usage limit"))
            {
                message = "Usage quota exhausted. Cooling down for 6 hours.";
                return CooldownReason.QuotaExhausted;
            }

            if (ContainsAny(text, "billing", "payment required", "payment_required", "credits exhausted", "insufficient credits", "402"))
            {
                message = "Billing error or credits exhausted. Please top up your account.";
                return CooldownReason.BillingError;
            }

            if (ContainsAny(text, "overloaded", "service unavailable", "temporarily unavailable", "503", "502", "504"))
            {
                message = "Service temporarily unavailable. Retrying in 30 minutes.";
                return CooldownReason.ServiceUnavailable;
            }

            message = string.Empty;
            return CooldownReason.None;
        }

        public static string? ExtractReadableMessage(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return null;

            var jsonStart = raw.IndexOf('{');
            if (jsonStart < 0)
                return null;

            try
            {
                using var doc = JsonDocument.Parse(raw[jsonStart..]);
                var root = doc.RootElement;

                string? message = null;
                if (root.TryGetProperty("error", out var errorElement))
                {
                    if (errorElement.TryGetProperty("message", out var messageElement))
                        message = messageElement.GetString()?.Trim();
                }
                else if (root.TryGetProperty("message", out var topLevelMessage))
                {
                    message = topLevelMessage.GetString()?.Trim();
                }

                if (string.IsNullOrWhiteSpace(message))
                    return null;

                var markerIndex = message.IndexOf(". For more information", StringComparison.OrdinalIgnoreCase);
                if (markerIndex > 0)
                    message = message[..markerIndex].TrimEnd('.');

                if (message.Length > 300)
                    message = message[..300] + "…";

                string? retryDelay = null;
                if (root.TryGetProperty("error", out var errorWithDetails) &&
                    errorWithDetails.TryGetProperty("details", out var details) &&
                    details.ValueKind == JsonValueKind.Array)
                {
                    foreach (var detail in details.EnumerateArray())
                    {
                        if (!detail.TryGetProperty("retryDelay", out var retryDelayElement))
                            continue;

                        retryDelay = retryDelayElement.GetString();
                        break;
                    }
                }

                return retryDelay != null
                    ? $"{message}\n(Retry in {retryDelay})"
                    : message;
            }
            catch
            {
                return null;
            }
        }

        private static bool ContainsAny(string haystack, params string[] needles)
        {
            foreach (var needle in needles)
            {
                if (haystack.Contains(needle, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
    }
}
