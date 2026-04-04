using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Aire.AppLayer.Abstractions;
using Aire.Domain.Providers;
using Aire.Providers;
using Aire.Services;

namespace Aire.AppLayer.Providers
{
    /// <summary>
    /// Application-layer orchestration for running provider capability tests and persisting the resulting session.
    /// </summary>
    public sealed class ProviderCapabilityTestApplicationService
    {
        /// <summary>
        /// Shared execution result for one validated capability-test run.
        /// </summary>
        public sealed record ExecutionResult(
            bool Started,
            IReadOnlyList<CapabilityTestResult> Results,
            DateTime? TestedAt,
            string? BlockingMessage,
            string? WarningMessage)
        {
            public static ExecutionResult Blocked(string message)
                => new(false, Array.Empty<CapabilityTestResult>(), null, message, null);
        }

        /// <summary>
        /// Progress payload for one completed capability test.
        /// </summary>
        public sealed record ProgressUpdate(CapabilityTestResult Result, int CompletedCount);

        /// <summary>
        /// Result of one capability-test run.
        /// </summary>
        public sealed record RunResult(IReadOnlyList<CapabilityTestResult> Results, DateTime TestedAt);

        private readonly ProviderCapabilityTestSessionService _sessionService = new();
        private readonly ProviderSetupApplicationService _providerSetupService;

        /// <summary>
        /// Creates the capability-test orchestration over the shared provider setup/runtime workflow.
        /// </summary>
        public ProviderCapabilityTestApplicationService()
            : this(new ProviderSetupApplicationService())
        {
        }

        /// <summary>
        /// Creates the capability-test orchestration over an injected provider setup workflow.
        /// </summary>
        /// <param name="providerSetupService">Shared provider setup workflow used for validation and smoke tests.</param>
        public ProviderCapabilityTestApplicationService(ProviderSetupApplicationService providerSetupService)
        {
            _providerSetupService = providerSetupService;
        }

        /// <summary>
        /// Runs capability tests through the supplied runner delegate and persists the results when a provider id is available.
        /// </summary>
        /// <param name="provider">Provider instance to exercise.</param>
        /// <param name="providerId">Provider id used for persistence, or <see langword="null"/> to skip saving.</param>
        /// <param name="model">Model identifier associated with the test run.</param>
        /// <param name="runAllAsync">Delegate that runs the capability test suite.</param>
        /// <param name="settingsRepository">Settings repository used to persist test sessions.</param>
        /// <param name="progress">Optional progress callback for each completed test.</param>
        /// <param name="cancellationToken">Cancellation token for the test run.</param>
        /// <returns>The completed test results and their timestamp.</returns>
        public async Task<RunResult> RunAndPersistAsync(
            IAiProvider provider,
            int? providerId,
            string model,
            Func<IAiProvider, CancellationToken, IAsyncEnumerable<CapabilityTestResult>> runAllAsync,
            ISettingsRepository settingsRepository,
            IProgress<ProgressUpdate>? progress,
            CancellationToken cancellationToken)
        {
            var results = new List<CapabilityTestResult>();

            await foreach (var result in runAllAsync(provider, cancellationToken))
            {
                results.Add(result);
                progress?.Report(new ProgressUpdate(result, results.Count));
            }

            var testedAt = DateTime.Now;
            if (providerId.HasValue)
            {
                await _sessionService.SaveAsync(
                    providerId.Value,
                    model,
                    results,
                    testedAt,
                    settingsRepository);
            }

            return new RunResult(results, testedAt);
        }

        /// <summary>
        /// Validates one configured provider, applies the shared smoke-test policy, then runs and persists
        /// the capability suite. This keeps capability-test orchestration out of WPF event handlers.
        /// </summary>
        /// <param name="provider">Provider instance to validate and exercise.</param>
        /// <param name="providerId">Provider id used for persistence, or <see langword="null"/> to skip saving.</param>
        /// <param name="model">Model identifier associated with the test run.</param>
        /// <param name="runAllAsync">Delegate that runs the capability test suite.</param>
        /// <param name="settingsRepository">Settings repository used to persist test sessions.</param>
        /// <param name="progress">Optional progress callback for each completed test.</param>
        /// <param name="cancellationToken">Cancellation token for the overall workflow.</param>
        /// <returns>
        /// A blocked result when validation or auth-level smoke checks fail, otherwise the completed run
        /// plus an optional non-blocking warning from the smoke test.
        /// </returns>
        public async Task<ExecutionResult> ValidateRunAndPersistAsync(
            IAiProvider provider,
            int? providerId,
            string model,
            Func<IAiProvider, CancellationToken, IAsyncEnumerable<CapabilityTestResult>> runAllAsync,
            ISettingsRepository settingsRepository,
            IProgress<ProgressUpdate>? progress,
            CancellationToken cancellationToken)
        {
            var validation = await _providerSetupService.ValidateDetailedAsync(provider, cancellationToken).ConfigureAwait(false);
            if (!validation.IsValid)
            {
                var reason = validation.ErrorMessage ?? "Provider configuration is invalid.";
                if (!string.IsNullOrWhiteSpace(validation.RemediationHint))
                    reason = $"{reason} {validation.RemediationHint}";

                return ExecutionResult.Blocked($"Validation failed: {reason}");
            }

            var smokeTest = await _providerSetupService.RunSmokeTestAsync(provider, cancellationToken).ConfigureAwait(false);
            var warning = BuildSmokeTestWarning(validation, smokeTest);
            if (warning.BlockingMessage != null)
                return ExecutionResult.Blocked(warning.BlockingMessage);

            var runResult = await RunAndPersistAsync(
                provider,
                providerId,
                model,
                runAllAsync,
                settingsRepository,
                progress,
                cancellationToken).ConfigureAwait(false);

            return new ExecutionResult(true, runResult.Results, runResult.TestedAt, null, warning.WarningMessage);
        }

        private static (string? BlockingMessage, string? WarningMessage) BuildSmokeTestWarning(
            ProviderValidationOutcome validation,
            ProviderSmokeTestResult smokeTest)
        {
            if (smokeTest.Success)
                return (null, null);

            var error = smokeTest.ErrorMessage ?? "Unknown provider smoke-test failure.";
            var isAuthError = validation.FailureKind == ProviderValidationFailureKind.InvalidCredentials
                || error.Contains("auth", StringComparison.OrdinalIgnoreCase)
                || error.Contains("api key", StringComparison.OrdinalIgnoreCase)
                || error.Contains("unauthorized", StringComparison.OrdinalIgnoreCase)
                || error.Contains("forbidden", StringComparison.OrdinalIgnoreCase)
                || error.Contains("invalid key", StringComparison.OrdinalIgnoreCase);

            return isAuthError
                ? ($"Test run failed: {error}", null)
                : (null, $"Smoke test warning: {error}");
        }
    }
}
