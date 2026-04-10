using System;
using System.Threading.Tasks;
using System.Threading;
using System.Windows;
using Aire.Bootstrap;
using Aire.AppLayer.Startup;
using Aire.Providers;
using Aire.Services;
using Aire.UI;
using Application = System.Windows.Application;
using WebViewWin  = Aire.UI.WebViewWindow;

namespace Aire
{
    public partial class App : System.Windows.Application
    {
        // ── Single-instance enforcement ───────────────────────────────────────
        private static Mutex? _instanceMutex;
        private EventWaitHandle? _activateEvent;

        // ── App state ─────────────────────────────────────────────────────────
        private TrayIconService? _trayService;
        private MainWindow? _mainWindow;
        private LocalApiService? _localApiService;
        private ProviderModelRefreshService? _providerModelRefreshService;
        private GitHubReleaseUpdateService? _updateService;
        private readonly StartupDecisionApplicationService _startupDecisionService = new();
        private readonly StartupWindowCoordinator _startupWindowCoordinator = new();
        private readonly WindowVisibilityCoordinator _windowVisibilityCoordinator = new();

        protected override async void OnStartup(StartupEventArgs e)
        {
            GpuPreferenceService.ApplyHighPerformancePreference();

            // Try to claim the single-instance mutex.
            _instanceMutex = new Mutex(true, "Aire_SingleInstance_v1", out bool isFirstInstance);

            if (!isFirstInstance)
            {
                // Signal the already-running instance to show itself, then quit.
                try
                {
                    using var ev = EventWaitHandle.OpenExisting("Aire_Activate_v1");
                    ev.Set();
                }
                catch (Exception ex)
                {
                    // The existing instance may still be starting up, but log other failures.
                    AppLogger.Warn("App.SingleInstance", "Failed to signal the existing instance", ex);
                }
                Shutdown(0);
                return;
            }

            // Create the inter-process activation event and listen on a background thread.
            _activateEvent = new EventWaitHandle(false, EventResetMode.AutoReset, "Aire_Activate_v1");
            var listener = new Thread(() =>
            {
                while (_activateEvent.WaitOne())
                    Dispatcher.Invoke(() => _trayService?.ShowMainWindow());
            })
            { IsBackground = true, Name = "SingleInstanceListener" };
            listener.Start();

            base.OnStartup(e);

            // Apply saved appearance settings before any window is shown so the
            // splash screen (and all subsequent windows) use the correct theme.
            AppearanceService.ApplySaved();
            _ = CurrencyExchangeService.RefreshAsync();

            // Show a splash window and use it to host startup work until the main
            // window is ready to appear immediately after the splash closes.
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            _mainWindow = new MainWindow();
            MainWindow = _mainWindow;
            var initWindow = new InitializationWindow();
            await initWindow.RunAndCloseAsync(async progress =>
            {
                progress.Report("Creating main window…");
                await _mainWindow.InitializeStartupAsync(progress);
            },
            async () =>
            {
                _startupWindowCoordinator.ShowInitialMainWindow(_mainWindow, _trayService, Dispatcher);
                await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            });
            ShutdownMode = ShutdownMode.OnLastWindowClose;

            if (AppStartupState.StartupError != null)
            {
                ConfirmationDialog.ShowAlert(_mainWindow, "Aire startup failed", $"Failed to initialize Aire: {AppStartupState.StartupError.Message}");
            }

            _trayService = new TrayIconService(_mainWindow);
            _trayService.OpenChatRequested += (s, args) => _trayService.ShowMainWindow();
            _trayService.SettingsRequested += (_, _) => _startupWindowCoordinator.ShowSettingsWindow(_mainWindow, _mainWindow);
            _trayService.ExitRequested     += OnExitRequested;

            SettingsWindow.OpenRequested += tab =>
            {
                _startupWindowCoordinator.ShowSettingsWindow(_mainWindow, _mainWindow);
                if (tab != null)
                    Dispatcher.BeginInvoke(
                        () => _startupWindowCoordinator.CurrentSettingsWindow?.NavigateTo(tab),
                        System.Windows.Threading.DispatcherPriority.Loaded);
            };
            _mainWindow.TrayService = _trayService;
            _mainWindow.IsVisibleChanged += OnMainWindowVisibilityChanged;

            _localApiService = new LocalApiService(_mainWindow);
            AppState.ApiAccessChanged += OnApiAccessChanged;
            UpdateLocalApiServiceState();

            // Auto-prompt Claude.ai login when the Anthropic provider needs it
            ClaudeAiSession.PromptLogin = () =>
            {
                var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                Dispatcher.Invoke(() =>
                {
                    var win = new ClaudeAiLoginWindow { Owner = _mainWindow };
                    win.ShowDialog();
                    tcs.SetResult();
                });
                return tcs.Task;
            };

            // Show onboarding wizard on first run OR when no providers are configured.
            bool showWizard;
            try
            {
                using var db = new Aire.Data.DatabaseService();
                db.InitializeAsync().GetAwaiter().GetResult();
                showWizard = await _startupDecisionService.ShouldShowOnboardingAsync(db, AppState.GetHasCompletedOnboarding());
            }
            catch (Exception ex)
            {
                // Non-fatal: if the startup inspection fails, continue without onboarding.
                AppLogger.Warn("App.Startup", "Failed to inspect onboarding state", ex);
                showWizard = false;
            }
            if (showWizard)
            {
                _mainWindow.Hide();
                var wizard = new OnboardingWindow();
                wizard.OpenSettingsAction = () => _startupWindowCoordinator.ShowSettingsWindow(_mainWindow, _mainWindow);
                wizard.ShowDialog();
            }

            _startupWindowCoordinator.RestoreWindowsFromState(_mainWindow, _mainWindow);

            _providerModelRefreshService = new ProviderModelRefreshService(
                notificationSink: (title, body) =>
                    Dispatcher.BeginInvoke(() => _trayService?.ShowNotification(title, body)));
            _providerModelRefreshService.Start();

            _updateService = new GitHubReleaseUpdateService("YMRYMR", "aire");
            _ = CheckForUpdatesAsync();

            SetupPreferences setupPreferences = SetupPreferencesStore.Load();
            if (setupPreferences.VoiceInputEnabled && _mainWindow?.IsVisible == true)
            {
                _ = Dispatcher.BeginInvoke(async () => await _mainWindow.TryEnableStartupVoiceInputAsync());
            }
        }

        private void OnMainWindowVisibilityChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
        {
            _windowVisibilityCoordinator.HandleMainWindowVisibilityChanged((bool)e.NewValue, _startupWindowCoordinator.CurrentSettingsWindow, WebViewWin.Current);
        }

        private void OnExitRequested(object? sender, EventArgs e)
        {
            // Snapshot open windows BEFORE they close so the next launch can
            // restore them. IsShuttingDown stops the Closed handlers from
            // overwriting this with false.
            _startupWindowCoordinator.SnapshotOpenWindows();
            AppState.IsShuttingDown = true;

            _trayService?.Dispose();
            Shutdown();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _ = Aire.Services.Mcp.McpManager.Instance.StopAllAsync();
            _mainWindow?.Cleanup();
            _providerModelRefreshService?.Dispose();
            _trayService?.Dispose();
            _localApiService?.Dispose();
            AppState.ApiAccessChanged -= OnApiAccessChanged;
            _activateEvent?.Dispose();
            _instanceMutex?.ReleaseMutex();
            _instanceMutex?.Dispose();
            base.OnExit(e);
        }

        private void OnApiAccessChanged()
        {
            Dispatcher.Invoke(UpdateLocalApiServiceState);
        }

        private void UpdateLocalApiServiceState()
        {
            if (_localApiService == null) return;

            if (AppState.GetApiAccessEnabled())
                _localApiService.Start();
            else
                _ = _localApiService.StopAsync();
        }

        private async Task CheckForUpdatesAsync()
        {
            var updateService = _updateService;
            if (updateService == null)
                return;

            try
            {
                var update = await updateService.CheckLatestReleaseAsync().ConfigureAwait(false);
                if (update == null)
                    return;

                var shouldInstall = false;
                await Dispatcher.InvokeAsync(() =>
                {
                    shouldInstall = UpdateAvailableDialog.ShowDialog(_mainWindow, update) == true;
                });

                if (!shouldInstall)
                    return;

                var installerPath = await updateService.DownloadInstallerAsync(update).ConfigureAwait(false);
                await Dispatcher.InvokeAsync(() =>
                {
                    updateService.LaunchInstaller(installerPath);
                    Shutdown();
                });
            }
            catch (Exception ex)
            {
                AppLogger.Warn("App.Update", "Update check or installation failed", ex);
            }
        }
    }
}
