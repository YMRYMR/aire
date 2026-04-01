namespace Aire.AppLayer.Abstractions
{
    /// <summary>
    /// Enriched validation result returned by a provider adapter.
    /// Extends the basic valid/invalid model with classification and
    /// remediation guidance so that the UI can present actionable feedback.
    /// </summary>
    /// <remarks>
    /// This type lives in the Application layer because its exact shape will
    /// be refined as adapters are implemented. The lightweight
    /// <c>ProviderValidationResult</c> in Domain remains the stable core contract.
    /// </remarks>
    public sealed class ProviderValidationOutcome
    {
        /// <summary>Whether the provider configuration is usable.</summary>
        public bool IsValid { get; init; }

        /// <summary>
        /// Short human-readable description of why validation failed.
        /// <see langword="null"/> when <see cref="IsValid"/> is <see langword="true"/>.
        /// </summary>
        public string? ErrorMessage { get; init; }

        /// <summary>
        /// Classification of the failure for cooldown and retry policy.
        /// </summary>
        public ProviderValidationFailureKind FailureKind { get; init; }

        /// <summary>
        /// Optional remediation hint shown to the user (e.g. "Check your API key at openai.com").
        /// </summary>
        public string? RemediationHint { get; init; }

        /// <summary>Convenience factory for a passing validation.</summary>
        public static ProviderValidationOutcome Valid() => new()
        {
            IsValid = true,
            FailureKind = ProviderValidationFailureKind.None
        };

        /// <summary>Convenience factory for a generic invalid result.</summary>
        public static ProviderValidationOutcome Invalid(
            string errorMessage,
            ProviderValidationFailureKind failureKind = ProviderValidationFailureKind.Unknown,
            string? remediationHint = null) => new()
        {
            IsValid = false,
            ErrorMessage = errorMessage,
            FailureKind = failureKind,
            RemediationHint = remediationHint
        };
    }

    /// <summary>
    /// Classification of a provider validation failure for policy decisions.
    /// </summary>
    public enum ProviderValidationFailureKind
    {
        /// <summary>No failure (validation passed).</summary>
        None,
        /// <summary>Missing or invalid API key.</summary>
        InvalidCredentials,
        /// <summary>Network connectivity or DNS failure.</summary>
        NetworkError,
        /// <summary>Rate limit or quota exceeded.</summary>
        RateLimit,
        /// <summary>Billing issue (insufficient credits, expired plan).</summary>
        BillingError,
        /// <summary>Provider service is temporarily unavailable.</summary>
        ServiceUnavailable,
        /// <summary>Unrecognized failure.</summary>
        Unknown
    }
}
