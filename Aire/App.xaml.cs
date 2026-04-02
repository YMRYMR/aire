using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
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
        private SettingsWindow? _settingsWindow;
        private LocalApiService? _localApiService;

        // Track which satellite windows were visible before main window was hidden
        private bool _settingsWasVisible;
        private bool _browserWasVisible;

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
            _trayService.SettingsRequested += OnSettingsRequested;
            _trayService.ExitRequested     += OnExitRequested;

            SettingsWindow.OpenRequested += tab =>
            {
                OnSettingsRequested(this, EventArgs.Empty);
                if (tab != null)
                    Dispatcher.BeginInvoke(
                        () => _settingsWindow?.NavigateTo(tab),
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

            // Show onboarding wizard on first run OR when no providers are configured
            bool showWizard = !AppState.GetHasCompletedOnboarding();
            if (!showWizard)
            {
                try
                {
                    var db = new Aire.Data.DatabaseService();
                    db.InitializeAsync().GetAwaiter().GetResult();
                    var providers = db.GetProvidersAsync().GetAwaiter().GetResult();
                    showWizard = providers.Count == 0;
                    db.Dispose();
                }
                catch { /* non-fatal — proceed without wizard if DB check fails */ }
            }
            if (showWizard)
            {
                _mainWindow.Hide();
                var wizard = new OnboardingWindow();
                wizard.OpenSettingsAction = () => OnSettingsRequested(this, EventArgs.Empty);
                wizard.ShowDialog();
            }

            RestoreWindowsFromState();
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
            bool nowVisible = (bool)e.NewValue;
            if (nowVisible)
            {
                // Restore windows that were visible before main window was hidden
                if (_settingsWasVisible) _settingsWindow?.Show();
                if (_browserWasVisible)  WebViewWin.Current?.Show();
            }
            else
            {
                // Record which satellite windows are open, then hide them
                _settingsWasVisible = _settingsWindow?.IsVisible == true;
                _browserWasVisible  = WebViewWin.Current?.IsVisible == true;
                _settingsWindow?.Hide();
                WebViewWin.Current?.Hide();
            }
        }

        private void OnSettingsRequested(object? sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                // Only open one settings window at a time.
                if (_settingsWindow != null)
                {
                    _settingsWindow.Activate();
                    return;
                }

                _settingsWindow = new SettingsWindow { Owner = _mainWindow };
                _settingsWindow.ProvidersChanged += async () =>
                {
                    if (_mainWindow != null)
                        await _mainWindow.RefreshProvidersAsync();
                };
                _settingsWindow.Closed += (s, args) => _settingsWindow = null;
                _settingsWindow.Show();
            });
        }

        private void RestoreWindowsFromState()
        {
            try
            {
                if (AppState.GetSettingsOpen())
                    OnSettingsRequested(this, EventArgs.Empty);

                if (AppState.GetBrowserOpen())
                    new WebViewWin().Show();
            }
            catch { /* never crash startup */ }
        }

        private void OnExitRequested(object? sender, EventArgs e)
        {
            // Snapshot open windows BEFORE they close so the next launch can
            // restore them. IsShuttingDown stops the Closed handlers from
            // overwriting this with false.
            AppState.SetBrowserOpen(WebViewWin.Current  != null);
            AppState.SetSettingsOpen(SettingsWindow.Current != null);
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
