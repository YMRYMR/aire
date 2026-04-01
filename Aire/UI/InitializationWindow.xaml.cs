using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Aire.Services;

namespace Aire.UI
{
    /// <summary>
    /// Splash window shown at app launch while slow one-time data is loaded into
    /// <see cref="AppStartupCache"/>.  The window closes itself when loading finishes.
    /// </summary>
    public partial class InitializationWindow : Window
    {
        public InitializationWindow()
        {
            InitializeComponent();
        }

        // ── Public entry point ────────────────────────────────────────────────

        /// <summary>
        /// Shows the window, performs all startup loading tasks, populates
        /// <see cref="AppStartupCache"/>, then closes.
        /// Call with <c>await</c> from <see cref="App.OnStartup"/>.
        /// </summary>
        public async Task RunAndCloseAsync()
        {
            Show();

            try
            {
                await LoadAsync();
            }
            catch (Exception ex)
            {
                AppLogger.Warn("InitializationWindow", "Startup loading failed", ex);
            }
            finally
            {
                Close();
            }
        }

        // ── Private loading logic ─────────────────────────────────────────────

        private async Task LoadAsync()
        {
            // Step 1: hardware profile (synchronous but may spawn nvidia-smi / WMI)
            SetStatus("Reading system profile…");
            var profile = await Task.Run(() => OllamaService.GetLocalSystemProfile());

            // Step 2: is Ollama installed?
            SetStatus("Checking Ollama…");
            bool inPath = await Task.Run(() => OllamaService.IsOllamaInPath());

            if (!inPath)
            {
                // Fetch the available-model catalog even without Ollama so the "not installed"
                // panel can show model info if the user later installs it.
                SetStatus("Loading Ollama model catalog…");
                var available = await FetchAvailableModelsAsync();
                AppStartupCache.Set(
                    profile,
                    OllamaStartupStatus.NotInstalled,
                    Array.Empty<OllamaService.OllamaModel>(),
                    available);
                return;
            }

            // Step 3: is Ollama reachable (running)?
            SetStatus("Checking if Ollama is running…");
            using var svc = new OllamaService();
            bool running = await svc.IsOllamaReachableAsync(cancellationToken: CancellationToken.None);

            if (!running)
            {
                SetStatus("Loading Ollama model catalog…");
                var available = await FetchAvailableModelsAsync();
                AppStartupCache.Set(
                    profile,
                    OllamaStartupStatus.NotRunning,
                    Array.Empty<OllamaService.OllamaModel>(),
                    available);
                return;
            }

            // Step 4: load installed models and the full catalog in parallel
            SetStatus("Loading Ollama models…");
            var installedTask  = svc.GetInstalledModelsAsync(cancellationToken: CancellationToken.None);
            var availableTask  = FetchAvailableModelsAsync();
            await Task.WhenAll(installedTask, availableTask);

            List<OllamaService.OllamaModel> installed;
            try   { installed = await installedTask; }
            catch { installed = new List<OllamaService.OllamaModel>(); }

            var availableModels = await availableTask;

            AppStartupCache.Set(profile, OllamaStartupStatus.Ready, installed, availableModels);
        }

        private static async Task<IReadOnlyList<OllamaService.OllamaModel>> FetchAvailableModelsAsync()
        {
            try
            {
                using var svc = new OllamaService();
                return await svc.GetAvailableModelsAsync(CancellationToken.None);
            }
            catch
            {
                return Array.Empty<OllamaService.OllamaModel>();
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private void SetStatus(string text)
            => Dispatcher.Invoke(() => StatusText.Text = text);
    }
}
