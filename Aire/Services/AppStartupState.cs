using System;
using System.Threading;
using System.Threading.Tasks;

namespace Aire.Services
{
    /// <summary>
    /// Process-wide startup gate used by background services that must wait until the main app has finished initializing.
    /// </summary>
    internal static class AppStartupState
    {
        private static readonly TaskCompletionSource<bool> _readyTcs =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private static Exception? _startupError;

        /// <summary>
        /// Returns whether startup finished successfully.
        /// </summary>
        public static bool IsReady => _readyTcs.Task.IsCompletedSuccessfully;

        /// <summary>
        /// Captures the startup exception when initialization failed.
        /// </summary>
        public static Exception? StartupError => _startupError;

        /// <summary>
        /// Marks the app as fully initialized and releases any waiters.
        /// </summary>
        public static void MarkReady()
        {
            _readyTcs.TrySetResult(true);
        }

        /// <summary>
        /// Marks startup as failed and propagates the exception to any waiters.
        /// </summary>
        /// <param name="exception">Startup exception that caused initialization to fail.</param>
        public static void MarkFailed(Exception exception)
        {
            _startupError = exception;
            _readyTcs.TrySetException(exception);
        }

        /// <summary>
        /// Waits until startup completes successfully or faults with the captured startup exception.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token used by background services waiting on startup readiness.</param>
        public static async Task WaitUntilReadyAsync(CancellationToken cancellationToken = default)
        {
            var task = _readyTcs.Task;
            if (task.IsCompleted)
            {
                await task.ConfigureAwait(false);
                return;
            }

            if (cancellationToken.CanBeCanceled)
                await task.WaitAsync(cancellationToken).ConfigureAwait(false);
            else
                await task.ConfigureAwait(false);
        }
    }
}
