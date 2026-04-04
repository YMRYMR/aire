using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Aire.Bootstrap;
using Aire.AppLayer.Startup;
using Aire.Providers;
using Aire.Services;
using Aire.UI;
using MessageBox = System.Windows.MessageBox;
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
                catch { /* existing instance may not have created the event yet */ }
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
                ShowInitialMainWindow();
                await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            });
            ShutdownMode = ShutdownMode.OnLastWindowClose;

            if (AppStartupState.StartupError != null)
            {
                MessageBox.Show($"Failed to initialize Aire: {AppStartupState.StartupError.Message}", "Aire", MessageBoxButton.OK, MessageBoxImage.Error);
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
            catch
            {
                // Non-fatal: if the startup inspection fails, continue without onboarding.
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

            SetupPreferences setupPreferences = SetupPreferencesStore.Load();
            if (setupPreferences.VoiceInputEnabled && _mainWindow?.IsVisible == true)
            {
                _ = Dispatcher.BeginInvoke(async () => await _mainWindow.TryEnableStartupVoiceInputAsync());
            }
        }

        private void ShowInitialMainWindow()
        {
            if (_mainWindow == null)
                return;

            _mainWindow.WindowState = WindowState.Normal;

            if (!_mainWindow.IsVisible)
                _mainWindow.Show();

            if (_trayService?.IsAttachedToTray == true)
            {
                _trayService.ShowMainWindow();
            }
            else
            {
                _mainWindow.Activate();
                _mainWindow.Focus();
            }

            Dispatcher.BeginInvoke(() =>
            {
                if (_mainWindow == null)
                    return;

                _mainWindow.WindowState = WindowState.Normal;
                if (_trayService?.IsAttachedToTray == true)
                {
                    _trayService.ShowMainWindow();
                }
                else
                {
                    _mainWindow.Activate();
                    _mainWindow.Topmost = true;
                    _mainWindow.Topmost = false;
                    _mainWindow.Focus();
                }
            }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
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
    }
}
