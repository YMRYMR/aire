using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Aire.AppLayer.Abstractions;
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
        /// Progress payload for one completed capability test.
        /// </summary>
        public sealed record ProgressUpdate(CapabilityTestResult Result, int CompletedCount);

        /// <summary>
        /// Result of one capability-test run.
        /// </summary>
        public sealed record RunResult(IReadOnlyList<CapabilityTestResult> Results, DateTime TestedAt);

        private readonly ProviderCapabilityTestSessionService _sessionService = new();

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
    }
}
