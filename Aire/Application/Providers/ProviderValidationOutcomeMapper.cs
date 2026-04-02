using System;
using Aire.AppLayer.Abstractions;
using Aire.Domain.Providers;

namespace Aire.AppLayer.Providers
{
    /// <summary>
    /// Maps the legacy provider validation contract into the richer application-side
    /// validation outcome used by the adapter architecture.
    /// </summary>
    public static class ProviderValidationOutcomeMapper
    {
        /// <summary>
        /// Converts a basic provider validation result into an enriched validation outcome.
        /// </summary>
        /// <param name="result">Legacy validation result returned by the current provider contract.</param>
        /// <returns>Enriched validation outcome with failure classification and optional guidance.</returns>
        public static ProviderValidationOutcome FromLegacyResult(ProviderValidationResult result)
        {
            if (result.IsValid)
                return ProviderValidationOutcome.Valid();

            string errorMessage = result.Error ?? string.Empty;
            var failureKind = Classify(errorMessage);
            return ProviderValidationOutcome.Invalid(errorMessage, failureKind, BuildRemediationHint(failureKind));
        }

        internal static ProviderValidationFailureKind Classify(string errorMessage)
        {
            if (string.IsNullOrWhiteSpace(errorMessage))
                return ProviderValidationFailureKind.Unknown;

            string normalized = errorMessage.ToLowerInvariant();

            if (normalized.Contains("api key") || normalized.Contains("invalid api key") || normalized.Contains("unauthorized") || normalized.Contains("forbidden"))
                return ProviderValidationFailureKind.InvalidCredentials;

            if (normalized.Contains("dns") || normalized.Contains("name or service not known") || normalized.Contains("connection refused") ||
                normalized.Contains("timeout") || normalized.Contains("timed out") || normalized.Contains("network"))
                return ProviderValidationFailureKind.NetworkError;

            if (normalized.Contains("rate limit") || normalized.Contains("too many requests") || normalized.Contains("quota"))
                return ProviderValidationFailureKind.RateLimit;

            if (normalized.Contains("insufficient balance") || normalized.Contains("billing") || normalized.Contains("recharge") ||
                normalized.Contains("credits") || normalized.Contains("resource package"))
                return ProviderValidationFailureKind.BillingError;

            if (normalized.Contains("service unavailable") || normalized.Contains("server error") || normalized.Contains("bad gateway"))
                return ProviderValidationFailureKind.ServiceUnavailable;

            return ProviderValidationFailureKind.Unknown;
        }

        internal static string? BuildRemediationHint(ProviderValidationFailureKind failureKind)
        {
            return failureKind switch
            {
                ProviderValidationFailureKind.InvalidCredentials => "Check the API key or provider login/session.",
                ProviderValidationFailureKind.NetworkError => "Check your internet connection, DNS, proxy, or provider base URL.",
                ProviderValidationFailureKind.RateLimit => "Wait and retry, or switch to another model/provider temporarily.",
                ProviderValidationFailureKind.BillingError => "Check your provider credits, billing status, or active resource package.",
                ProviderValidationFailureKind.ServiceUnavailable => "Retry later or use another provider while the service recovers.",
                _ => null
            };
        }
    }
}
