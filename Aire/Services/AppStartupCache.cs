using System.Collections.Generic;

namespace Aire.Services
{
    /// <summary>Ollama availability as determined at startup.</summary>
    public enum OllamaStartupStatus
    {
        Unknown,
        NotInstalled,
        NotRunning,
        Ready,
    }

    /// <summary>
    /// In-memory cache populated by <see cref="Aire.UI.InitializationWindow"/> before any
    /// other window is shown.  Values stay valid for the lifetime of the process but can be
    /// invalidated (e.g. after the user installs/uninstalls Ollama at runtime) so that the
    /// next consumer triggers a fresh check.
    /// </summary>
    public static class AppStartupCache
    {
        private static volatile bool _isReady;

        /// <summary>
        /// <c>true</c> once <see cref="Aire.UI.InitializationWindow"/> has finished loading.
        /// </summary>
        public static bool IsReady => _isReady;

        /// <summary>Hardware snapshot (RAM, VRAM, disk).  Never null when <see cref="IsReady"/> is true.</summary>
        public static OllamaService.OllamaSystemProfile? SystemProfile { get; private set; }

        /// <summary>Ollama availability determined at launch.</summary>
        public static OllamaStartupStatus OllamaStatus { get; private set; } = OllamaStartupStatus.Unknown;

        /// <summary>Models already installed in Ollama at launch (empty when Ollama is not running).</summary>
        public static IReadOnlyList<OllamaService.OllamaModel> InstalledModels { get; private set; }
            = System.Array.Empty<OllamaService.OllamaModel>();

        /// <summary>
        /// Full Ollama model catalog fetched at launch (curated list + known metadata).
        /// Empty when the network request failed or was skipped.
        /// </summary>
        public static IReadOnlyList<OllamaService.OllamaModel> AvailableModels { get; private set; }
            = System.Array.Empty<OllamaService.OllamaModel>();

        // ── Internal API ──────────────────────────────────────────────────────

        /// <summary>
        /// Called by <see cref="Aire.UI.InitializationWindow"/> once all data has been gathered.
        /// </summary>
        internal static void Set(
            OllamaService.OllamaSystemProfile        profile,
            OllamaStartupStatus                      ollamaStatus,
            IReadOnlyList<OllamaService.OllamaModel> installedModels,
            IReadOnlyList<OllamaService.OllamaModel> availableModels)
        {
            SystemProfile   = profile;
            OllamaStatus    = ollamaStatus;
            InstalledModels = installedModels;
            AvailableModels = availableModels;
            _isReady        = true;
        }

        /// <summary>
        /// Clears the cache so the next consumer re-runs the checks from scratch.
        /// Call this after the user installs, uninstalls, or changes Ollama settings.
        /// </summary>
        public static void Invalidate()
        {
            _isReady        = false;
            SystemProfile   = null;
            OllamaStatus    = OllamaStartupStatus.Unknown;
            InstalledModels = System.Array.Empty<OllamaService.OllamaModel>();
            AvailableModels = System.Array.Empty<OllamaService.OllamaModel>();
        }
    }
}
