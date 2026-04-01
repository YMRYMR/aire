namespace Aire.Domain.Providers
{
    /// <summary>
    /// Normalized input for creating a runtime provider instance from UI or application state.
    /// </summary>
    public sealed record ProviderRuntimeRequest(
        string Type,
        string? ApiKey,
        string? BaseUrl,
        string Model,
        bool ClaudeWebSessionReady);

    /// <summary>
    /// Persistable provider data collected from onboarding or settings.
    /// </summary>
    public sealed record ProviderDraft(
        string Name,
        string Type,
        string? ApiKey,
        string? BaseUrl,
        string Model,
        bool IsEnabled = true,
        string Color = "#007ACC");

    /// <summary>
    /// Result of trying to persist a provider, including duplicate detection.
    /// </summary>
    public sealed record ProviderPersistResult(
        bool Saved,
        bool IsDuplicate,
        int? ProviderId = null);

    /// <summary>
    /// Result of a lightweight provider connection test.
    /// </summary>
    public sealed record ProviderSmokeTestResult(
        bool Success,
        string? ErrorMessage = null);

    /// <summary>
    /// Result of a provider configuration validation, including a human-readable reason when invalid.
    /// </summary>
    public sealed record ProviderValidationResult
    {
        public bool IsValid { get; init; }
        public string? Error { get; init; }

        /// <summary>Creates a passing validation result.</summary>
        public static ProviderValidationResult Ok() => new() { IsValid = true };

        /// <summary>Creates a failing validation result with a human-readable reason.</summary>
        public static ProviderValidationResult Fail(string error) => new() { IsValid = false, Error = error };
    }
}
