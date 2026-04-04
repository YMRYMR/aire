using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Aire.Services;

namespace Aire.UI
{
    /// <summary>
    /// Splash window shown at app launch while slow one-time data is loaded into
    /// <see cref="AppStartupCache"/>.  The window closes itself when loading finishes.
    /// </summary>
    public partial class InitializationWindow : Window
    {
        private readonly ObservableCollection<string> _startupActions = new();

        public InitializationWindow()
        {
            InitializeComponent();
            ActionItemsControl.ItemsSource = _startupActions;
        }

        // ── Public entry point ────────────────────────────────────────────────

        /// <summary>
        /// Shows the window, performs all startup loading tasks, populates
        /// <see cref="AppStartupCache"/>, then closes.
        /// Call with <c>await</c> from <see cref="App.OnStartup"/>.
        /// </summary>
        public async Task RunAndCloseAsync(
            Func<IProgress<string>, Task>? startupWork = null,
            Func<Task>? beforeClose = null)
        {
            Show();
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);

            try
            {
                var progress = new Progress<string>(AppendStatus);
                await LoadAsync(progress);
                if (startupWork != null)
                    await startupWork(progress);
                if (beforeClose != null)
                    await beforeClose();
            }
            catch (Exception ex)
            {
                AppLogger.Warn("InitializationWindow", "Startup loading failed", ex);
                AppendStatus("Startup warning.");
            }
            finally
            {
                Close();
            }
        }

        // ── Private loading logic ─────────────────────────────────────────────

        private async Task LoadAsync(IProgress<string> progress)
        {
            // Step 1: hardware profile (synchronous but may spawn nvidia-smi / WMI)
            progress.Report("Reading system profile…");
            var profile = await Task.Run(() => OllamaService.GetLocalSystemProfile());

            // Step 2: is Ollama installed?
            progress.Report("Checking Ollama…");
            bool inPath = await Task.Run(() => OllamaService.IsOllamaInPath());

            if (!inPath)
            {
                // Fetch the available-model catalog even without Ollama so the "not installed"
                // panel can show model info if the user later installs it.
                progress.Report("Loading Ollama model catalog…");
                var available = await FetchAvailableModelsAsync();
                AppStartupCache.Set(
                    profile,
                    OllamaStartupStatus.NotInstalled,
                    Array.Empty<OllamaService.OllamaModel>(),
                    available);
                return;
            }

            // Step 3: is Ollama reachable (running)?
            progress.Report("Checking if Ollama is running…");
            using var svc = new OllamaService();
            bool running = await svc.IsOllamaReachableAsync(cancellationToken: CancellationToken.None);

            if (!running)
            {
                progress.Report("Loading Ollama model catalog…");
                var available = await FetchAvailableModelsAsync();
                AppStartupCache.Set(
                    profile,
                    OllamaStartupStatus.NotRunning,
                    Array.Empty<OllamaService.OllamaModel>(),
                    available);
                return;
            }

            // Step 4: load installed models and the full catalog in parallel
            progress.Report("Loading Ollama models…");
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

        private void AppendStatus(string text)
        {
            if (_startupActions.Count == 0 || !string.Equals(_startupActions[^1], text, StringComparison.Ordinal))
            {
                _startupActions.Add(text);
                while (_startupActions.Count > 6)
                    _startupActions.RemoveAt(0);
            }

            StatusText.Text = text;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                ActionItemsControl.UpdateLayout();
            }), DispatcherPriority.Background);
        }
    }
}
