using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Aire.AppLayer.Providers;
using Aire.Data;
using Aire.Providers;
using Aire.Services;
using Aire.Services.Providers;

namespace Aire.UI.Controls
{
    internal record OllamaPickerEntry(
        string   ModelName,
        bool     IsInstalled,
        string   SizeStr,
        string   ParamSize,
        string[] Tags,
        bool     IsRecommended)
    {
        public string Prefix   => IsInstalled ? "✓" : IsRecommended ? "★" : "";
        public string TagsText => Tags.Length > 0 ? "  ·  " + string.Join(" · ", Tags) : "";
        public override string ToString() => ModelName;
    }

    public partial class OllamaModelPickerControl : System.Windows.Controls.UserControl
    {
        // ── Dependency properties ─────────────────────────────────────────────

        public static readonly DependencyProperty BaseUrlProperty =
            DependencyProperty.Register(nameof(BaseUrl), typeof(string), typeof(OllamaModelPickerControl));

        public string? BaseUrl
        {
            get => (string?)GetValue(BaseUrlProperty);
            set => SetValue(BaseUrlProperty, value);
        }

        public static readonly DependencyProperty ShowImportButtonProperty =
            DependencyProperty.Register(nameof(ShowImportButton), typeof(bool), typeof(OllamaModelPickerControl),
                new PropertyMetadata(false, OnShowImportButtonChanged));

        public bool ShowImportButton
        {
            get => (bool)GetValue(ShowImportButtonProperty);
            set => SetValue(ShowImportButtonProperty, value);
        }

        private static void OnShowImportButtonChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is OllamaModelPickerControl ctrl)
                ctrl.ImportBtn.Visibility = (bool)e.NewValue ? Visibility.Visible : Visibility.Collapsed;
        }

        // ── State ─────────────────────────────────────────────────────────────

        public string? SelectedModelName { get; private set; }
        public bool    SelectedModelIsInstalled { get; private set; }

        /// <summary>Raised when the user selects or downloads a model. Args: (modelName, isInstalled).</summary>
        public event Action<string?, bool>? ModelSelectionChanged;

        // ── Private fields ────────────────────────────────────────────────────

        private static readonly OnboardingOllamaApplicationService _onboardingService = new();
        private static readonly OllamaActionApplicationService     _actionService     = new(new OllamaManagementClient());

        private CancellationTokenSource? _cts;
        private CancellationTokenSource? _downloadCts;
        private List<OllamaPickerEntry>  _allEntries       = [];
        private CollectionViewSource     _viewSource       = new();
        private bool                     _suppressFilter;
        private OllamaPickerEntry?       _preFilterEntry;
        private string?                  _pendingModel;
        /// <summary>The formatted display text that was in the box when the dropdown opened.</summary>
        private string?                  _preOpenText;

        public OllamaModelPickerControl()
        {
            InitializeComponent();

            ModelCombo.IsTextSearchEnabled = false;
            ModelCombo.AddHandler(
                System.Windows.Controls.Primitives.TextBoxBase.TextChangedEvent,
                new TextChangedEventHandler(OnModelComboTextChanged));
            ModelCombo.DropDownOpened  += ModelCombo_DropDownOpened;
            ModelCombo.PreviewTextInput += (_, e) => EditableComboBoxFilterHelper.HandlePreviewTextInput(ModelCombo, e);
            ModelCombo.PreviewKeyDown   += (_, e) => EditableComboBoxFilterHelper.HandlePreviewKeyDown(ModelCombo, e);
            ModelCombo.DropDownClosed   += ModelCombo_DropDownClosed;
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>Starts the Ollama status check (checking → not-installed / not-running / ready).</summary>
        public Task CheckAsync(string? baseUrl = null, string? preferredModel = null)
        {
            if (baseUrl != null) BaseUrl = baseUrl;
            _pendingModel = preferredModel;
            return RunCheckAsync();
        }

        /// <summary>Cancels any in-progress check or model load.</summary>
        public void CancelCheck()
        {
            _cts?.Cancel();
            _cts = null;
        }

        /// <summary>Refreshes the model list while staying in the ready state.</summary>
        public Task RefreshModelsAsync() => LoadModelsAsync(null, CancellationToken.None);

        /// <summary>Pre-selects a model by name if it's already in the loaded list.</summary>
        public void PreSelectModel(string? modelName)
        {
            if (string.IsNullOrEmpty(modelName) || _allEntries.Count == 0)
                return;

            var match = _allEntries.FirstOrDefault(e =>
                string.Equals(e.ModelName, modelName, StringComparison.OrdinalIgnoreCase));

            if (match == null) return;

            _suppressFilter = true;
            ModelCombo.SelectedItem = match;
            ModelCombo.Text = string.IsNullOrEmpty(match.SizeStr)
                ? match.ModelName
                : $"{match.ModelName}  ({match.SizeStr})";
            _suppressFilter = false;
        }

        // ── State machine ─────────────────────────────────────────────────────

        private async Task RunCheckAsync()
        {
            CancelCheck();
            _cts = new CancellationTokenSource();
            var ct = _cts.Token;

            // Fast path: the startup cache already knows whether Ollama is available.
            // Skip the checking animation and go straight to the appropriate state.
            if (AppStartupCache.IsReady)
            {
                try
                {
                    switch (AppStartupCache.OllamaStatus)
                    {
                        case OllamaStartupStatus.NotInstalled:
                            ShowState("not-installed");
                            ShowRamInfo();
                            return;

                        case OllamaStartupStatus.NotRunning:
                            ShowState("not-running");
                            // Still try to auto-start Ollama — it may have been started
                            // manually after our check, or will respond after a retry.
                            TryStartOllama();
                            using (var svcFast = new OllamaService())
                            {
                                for (int i = 0; i < 8 && !ct.IsCancellationRequested; i++)
                                {
                                    await Task.Delay(1000, ct);
                                    bool nowRunning = await svcFast.IsOllamaReachableAsync(BaseUrl, cancellationToken: ct);
                                    if (nowRunning)
                                    {
                                        ShowState("ready");
                                        await LoadModelsAsync(svcFast, ct);
                                        return;
                                    }
                                }
                            }
                            return;

                        case OllamaStartupStatus.Ready:
                            ShowState("ready");
                            await LoadModelsAsync(null, ct);
                            return;
                    }
                }
                catch (OperationCanceledException) { }
                catch
                {
                    if (!ct.IsCancellationRequested)
                        Dispatcher.Invoke(() => InstallStatusText.Text = LocalizationService.S("ollama.errorStatus", "Error checking Ollama status."));
                }
                return;
            }

            // Slow path: no cache — run the full check sequence.
            ShowState("checking");

            try
            {
                using var svc    = new OllamaService();
                bool inPath  = OllamaService.IsOllamaInPath();
                bool running = false;

                if (inPath)
                {
                    running = await svc.IsOllamaReachableAsync(BaseUrl, cancellationToken: ct);
                    if (!running)
                    {
                        ShowState("not-running");
                        TryStartOllama();
                        for (int i = 0; i < 8 && !ct.IsCancellationRequested; i++)
                        {
                            await Task.Delay(1000, ct);
                            running = await svc.IsOllamaReachableAsync(BaseUrl, cancellationToken: ct);
                            if (running) break;
                        }
                    }
                }

                if (ct.IsCancellationRequested) return;

                if (!inPath && !running)
                {
                    ShowState("not-installed");
                    ShowRamInfo();
                    return;
                }

                if (!running)
                {
                    ShowState("not-running");
                    return;
                }

                ShowState("ready");
                await LoadModelsAsync(svc, ct);
            }
            catch (OperationCanceledException) { }
                catch
                {
                    if (!ct.IsCancellationRequested)
                        Dispatcher.Invoke(() => InstallStatusText.Text = LocalizationService.S("ollama.errorStatus", "Error checking Ollama status."));
                }
        }

        private void ShowState(string state)
        {
            Dispatcher.Invoke(() =>
            {
                CheckingPanel.Visibility    = state == "checking"      ? Visibility.Visible : Visibility.Collapsed;
                NotInstalledPanel.Visibility = state == "not-installed" ? Visibility.Visible : Visibility.Collapsed;
                NotRunningPanel.Visibility  = state == "not-running"   ? Visibility.Visible : Visibility.Collapsed;
                ReadyPanel.Visibility       = state == "ready"         ? Visibility.Visible : Visibility.Collapsed;
            });
        }

        private void ShowRamInfo()
        {
            var profile  = AppStartupCache.SystemProfile ?? OllamaService.GetLocalSystemProfile();
            var guidance = _onboardingService.BuildHardwareGuidance(profile);

            Dispatcher.Invoke(() =>
            {
                SpecsText.Text = guidance.SummaryLine;
                if (guidance.WarningLine != null)
                {
                    WarningText.Text = guidance.WarningLine;
                    WarningBorder.Visibility = Visibility.Visible;
                }
                else
                {
                    WarningBorder.Visibility = Visibility.Collapsed;
                }
            });
        }

        private static void TryStartOllama()
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName       = "ollama",
                    Arguments      = "serve",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                });
            }
            catch { }
        }

        private async Task LoadModelsAsync(OllamaService? svc, CancellationToken ct)
        {
            bool ownsSvc = svc == null;
            svc ??= new OllamaService();

            try
            {
                Dispatcher.Invoke(() =>
                {
                    ReadyText.Text = LocalizationService.S("ollama.running", "Ollama is running. Loading models\u2026");
                    LabelModel.Text = LocalizationService.S("ollama.chooseModel", "Choose a model:");
                    ModelCombo.ItemsSource = null;
                    ModelCombo.Text = string.Empty;
                    ModelHint.Text = LocalizationService.S("ollama.loadingModels", "Loading models\u2026");
                    DownloadBtn.Visibility = Visibility.Collapsed;
                    DownloadProgressBorder.Visibility = Visibility.Collapsed;
                });

                // Always fetch a fresh installed list (user may have downloaded/removed models).
                // For the available-model catalog reuse the startup cache when present to avoid
                // a second network round-trip to ollama.com on every panel open.
                var installedTask = svc.GetInstalledModelsAsync(BaseUrl, cancellationToken: ct);
                var availableTask = AppStartupCache.AvailableModels.Count > 0
                    ? Task.FromResult<System.Collections.Generic.IReadOnlyList<OllamaService.OllamaModel>>(AppStartupCache.AvailableModels)
                    : svc.GetAvailableModelsAsync(ct).ContinueWith(
                        t => (System.Collections.Generic.IReadOnlyList<OllamaService.OllamaModel>)(t.IsCompletedSuccessfully ? t.Result : Array.Empty<OllamaService.OllamaModel>()),
                        ct, System.Threading.Tasks.TaskContinuationOptions.None, System.Threading.Tasks.TaskScheduler.Default);
                await Task.WhenAll(installedTask, availableTask);

                if (ct.IsCancellationRequested) return;

                var installed = await installedTask;
                var available = await availableTask;
                var profile   = AppStartupCache.SystemProfile ?? OllamaService.GetLocalSystemProfile();
                var catalog   = _onboardingService.BuildCatalog(installed, available, profile);

                var items = catalog.Entries
                    .Select(e => new OllamaPickerEntry(
                        e.ModelName, e.IsInstalled, e.SizeText,
                        e.ParameterSize, e.Tags, e.IsRecommended))
                    .ToList();

                Dispatcher.Invoke(() =>
                {
                    _allEntries = items;
                    _viewSource.Source = _allEntries;
                    ModelCombo.ItemsSource = _viewSource.View;

                    ReadyText.Text  = catalog.ReadyText;
                    ModelHint.Text  = catalog.HintText;
                    UninstallBtn.Visibility = Visibility.Collapsed;
                    LegendText.Visibility   = items.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

                    if (items.Count > 0)
                    {
                        var preferred = !string.IsNullOrEmpty(_pendingModel)
                            ? items.FirstOrDefault(e =>
                                string.Equals(e.ModelName, _pendingModel, StringComparison.OrdinalIgnoreCase))
                            : null;

                        _pendingModel = null;
                        ModelCombo.SelectedIndex = preferred != null
                            ? items.IndexOf(preferred)
                            : 0;
                    }
                });
            }
            catch (OperationCanceledException) { }
            catch
            {
                Dispatcher.Invoke(() => ModelHint.Text = LocalizationService.S("ollama.couldNotLoad", "Could not load models."));
            }
            finally
            {
                if (ownsSvc) svc.Dispose();
            }
        }

        // ── Model combo ───────────────────────────────────────────────────────

        private void ModelCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ModelCombo.SelectedItem is not OllamaPickerEntry entry) return;

            SelectedModelName        = entry.ModelName;
            SelectedModelIsInstalled = entry.IsInstalled;

            _suppressFilter = true;
            ModelCombo.Text = string.IsNullOrEmpty(entry.SizeStr)
                ? entry.ModelName
                : $"{entry.ModelName}  ({entry.SizeStr})";
            _suppressFilter = false;

            if (ModelCombo.ItemsSource != null)
            {
                var view = CollectionViewSource.GetDefaultView(ModelCombo.ItemsSource);
                if (view.Filter != null)
                {
                    _suppressFilter = true;
                    view.Filter = null;
                    _suppressFilter = false;
                }
            }

            if (entry.IsInstalled)
            {
                ModelHint.Text = entry.IsRecommended
                    ? $"✓ {entry.ModelName} is installed and ready to use. This is one of the best fits for this PC."
                    : $"✓ {entry.ModelName} is installed and ready to use.";
                DownloadBtn.Visibility   = Visibility.Collapsed;
                UninstallBtn.ToolTip     = $"Uninstall {entry.ModelName}";
                UninstallBtn.Visibility  = Visibility.Visible;
                UninstallBtn.IsEnabled   = true;
            }
            else
            {
                var paramHint = string.IsNullOrEmpty(entry.ParamSize) ? string.Empty : $" ({entry.ParamSize})";
                var sizeHint  = string.IsNullOrEmpty(entry.SizeStr)   ? string.Empty : $"  Download size: {entry.SizeStr}.";
                var recHint   = entry.IsRecommended
                    ? " Aire thinks this is one of the best models to start with on this PC."
                    : entry.Tags.Any(t => string.Equals(t, "too large",  StringComparison.OrdinalIgnoreCase))
                        ? " This one may be too heavy for this PC."
                        : entry.Tags.Any(t => string.Equals(t, "may be slow", StringComparison.OrdinalIgnoreCase))
                            ? " This one may still work, but it could feel slower."
                            : string.Empty;

                ModelHint.Text = $"{entry.ModelName}{paramHint} is not yet downloaded.{sizeHint}{recHint}";
                DownloadBtn.ToolTip     = string.IsNullOrEmpty(entry.SizeStr)
                    ? $"Download {entry.ModelName}"
                    : $"Download {entry.ModelName}  ({entry.SizeStr})";
                DownloadBtn.Visibility  = Visibility.Visible;
                DownloadBtn.IsEnabled   = true;
                UninstallBtn.Visibility = Visibility.Collapsed;
            }

            ModelSelectionChanged?.Invoke(SelectedModelName, SelectedModelIsInstalled);
        }

        private void ModelCombo_DropDownOpened(object? sender, EventArgs e)
        {
            // Remember the formatted text that was showing (e.g. "phi4  (2.0 GB)") and
            // immediately clear the box so the user can start typing a fresh filter without
            // first having to erase the size/label suffix.
            _preOpenText = ModelCombo.Text;
            _suppressFilter = true;
            ModelCombo.Text = string.Empty;
            _suppressFilter = false;

            EditableComboBoxFilterHelper.FocusEditableTextBox(ModelCombo, selectAll: false);
        }

        private void OnModelComboTextChanged(object sender, TextChangedEventArgs e)
        {
            if (_suppressFilter || ModelCombo.ItemsSource == null || !ModelCombo.IsDropDownOpen) return;

            var typed = ModelCombo.Text;
            var view  = CollectionViewSource.GetDefaultView(ModelCombo.ItemsSource);

            if (string.IsNullOrWhiteSpace(typed))
            {
                view.Filter = null;
            }
            else
            {
                _preFilterEntry ??= ModelCombo.SelectedItem as OllamaPickerEntry;
                view.Filter = obj => obj is OllamaPickerEntry entry &&
                                     entry.ModelName.Contains(typed, StringComparison.OrdinalIgnoreCase);
            }

            ModelCombo.IsDropDownOpen = true;
        }

        private void ModelCombo_DropDownClosed(object? sender, EventArgs e)
        {
            if (ModelCombo.ItemsSource == null) return;

            // Remove any active filter.
            var view = CollectionViewSource.GetDefaultView(ModelCombo.ItemsSource);
            _suppressFilter = true;
            view.Filter = null;
            _suppressFilter = false;

            // Restore the display text: prefer the newly selected item, otherwise the
            // text that was showing when the dropdown opened.
            if (ModelCombo.SelectedItem is OllamaPickerEntry selected)
            {
                _suppressFilter = true;
                ModelCombo.Text = string.IsNullOrEmpty(selected.SizeStr)
                    ? selected.ModelName
                    : $"{selected.ModelName}  ({selected.SizeStr})";
                _suppressFilter = false;
            }
            else if (_preFilterEntry != null)
            {
                _suppressFilter = true;
                ModelCombo.SelectedItem = _preFilterEntry;
                ModelCombo.Text = string.IsNullOrEmpty(_preFilterEntry.SizeStr)
                    ? _preFilterEntry.ModelName
                    : $"{_preFilterEntry.ModelName}  ({_preFilterEntry.SizeStr})";
                _suppressFilter = false;
            }
            else if (_preOpenText != null)
            {
                _suppressFilter = true;
                ModelCombo.Text = _preOpenText;
                _suppressFilter = false;
            }

            _preFilterEntry = null;
            _preOpenText    = null;
        }

        // ── Button handlers ───────────────────────────────────────────────────

        private async void InstallBtn_Click(object sender, RoutedEventArgs e)
        {
            InstallBtn.IsEnabled    = false;
            InstallBar.Visibility   = Visibility.Visible;
            InstallStatusText.Text  = LocalizationService.S("ollama.downloading", "Downloading Ollama installer\u2026");

            try
            {
                var progress = new Progress<int>(pct =>
                    Dispatcher.Invoke(() => InstallStatusText.Text = string.Format(LocalizationService.S("ollama.installing", "Installing\u2026 {0}%"), pct)));

                var result = await _actionService.InstallAsync(progress);
                InstallBar.Visibility = Visibility.Collapsed;

                if (!result.Succeeded)
                {
                    InstallStatusText.Text = result.UserMessage;
                    InstallBtn.IsEnabled   = true;
                    return;
                }

                InstallStatusText.Text = LocalizationService.S("ollama.installComplete", "Installation complete. Starting Ollama\u2026");
                await Task.Delay(3000);
                await RunCheckAsync();
            }
            catch
            {
                InstallBar.Visibility  = Visibility.Collapsed;
                InstallStatusText.Text = LocalizationService.S("ollama.installFailed", "Installation failed.");
                InstallBtn.IsEnabled   = true;
            }
        }

        private async void RetryBtn_Click(object sender, RoutedEventArgs e)
            => await RunCheckAsync();

        private async void RefreshBtn_Click(object sender, RoutedEventArgs e)
            => await LoadModelsAsync(null, _cts?.Token ?? CancellationToken.None);

        private async void DownloadBtn_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedModelName == null) return;
            var modelName = SelectedModelName;

            _downloadCts?.Cancel();
            _downloadCts = new CancellationTokenSource();
            var ct = _downloadCts.Token;

            DownloadBtn.IsEnabled               = false;
            CancelDownloadBtn.IsEnabled         = true;
            DownloadProgressBar.IsIndeterminate = true;
            DownloadProgressBar.Value           = 0;
            DownloadProgressText.Text           = LocalizationService.S("ollama.connecting", "Connecting\u2026");
            DownloadProgressBorder.Visibility   = Visibility.Visible;

            try
            {
                // Progress<T> marshals callbacks to the UI thread automatically.
                var progress = new Progress<OllamaService.OllamaPullProgress>(p =>
                {
                    if (p.Total > 0)
                    {
                        DownloadProgressBar.IsIndeterminate = false;
                        var pct = (double)p.Completed / p.Total * 100.0;
                        DownloadProgressBar.Value = pct;
                        DownloadProgressText.Text =
                            $"{p.Status}  {FormatSize(p.Completed)} / {FormatSize(p.Total)}  ({pct:0}%)";
                    }
                    else
                    {
                        DownloadProgressBar.IsIndeterminate = true;
                        DownloadProgressText.Text = p.Status;
                    }
                });

                var result = await _actionService.DownloadModelAsync(modelName, BaseUrl, progress, ct);
                DownloadProgressBorder.Visibility = Visibility.Collapsed;

                if (ct.IsCancellationRequested)
                {
                    ModelHint.Text        = LocalizationService.S("ollama.downloadCancel", "Download cancelled.");
                    DownloadBtn.IsEnabled = true;
                    return;
                }

                if (!result.Succeeded)
                {
                    ModelHint.Text        = result.UserMessage;
                    DownloadBtn.IsEnabled = true;
                    return;
                }

                SelectedModelIsInstalled = true;
                ModelSelectionChanged?.Invoke(SelectedModelName, true);
                await LoadModelsAsync(null, _cts?.Token ?? CancellationToken.None);

                var match = _allEntries.FirstOrDefault(e2 =>
                    string.Equals(e2.ModelName, modelName, StringComparison.OrdinalIgnoreCase));
                if (match != null)
                {
                    _suppressFilter = true;
                    ModelCombo.SelectedItem = match;
                    _suppressFilter = false;
                }
            }
            catch (OperationCanceledException)
            {
                DownloadProgressBorder.Visibility = Visibility.Collapsed;
                ModelHint.Text        = "Download cancelled.";
                DownloadBtn.IsEnabled = true;
            }
            catch
            {
                DownloadProgressBorder.Visibility = Visibility.Collapsed;
                ModelHint.Text        = LocalizationService.S("ollama.downloadFailed", "Download failed.");
                DownloadBtn.IsEnabled = true;
            }
            finally
            {
                CancelDownloadBtn.IsEnabled = false;
            }
        }

        private void CancelDownloadBtn_Click(object sender, RoutedEventArgs e)
        {
            _downloadCts?.Cancel();
            CancelDownloadBtn.IsEnabled  = false;
            DownloadProgressText.Text    = LocalizationService.S("ollama.cancelling", "Cancelling\u2026");
        }

        private async void UninstallBtn_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedModelName == null || !SelectedModelIsInstalled) return;
            var modelName = SelectedModelName;

            if (!ConfirmationDialog.ShowCentered(
                    Window.GetWindow(this),
                    title:   LocalizationService.S("ollama.uninstallModel", "Uninstall Ollama Model"),
                    message: string.Format(LocalizationService.S("ollama.uninstallModelMsg", "Are you sure you want to uninstall '{0}'? This will delete the model files from your system."), modelName)))
            {
                return;
            }

            UninstallBtn.IsEnabled = false;
            try
            {
                var result = await _actionService.UninstallModelAsync(modelName, BaseUrl);
                if (!result.Succeeded)
                {
                    ModelHint.Text         = result.UserMessage;
                    UninstallBtn.IsEnabled = true;
                    return;
                }

                ModelHint.Text = result.UserMessage;
                await LoadModelsAsync(null, _cts?.Token ?? CancellationToken.None);
            }
            catch
            {
                ModelHint.Text         = LocalizationService.S("ollama.uninstallFailed", "Uninstall failed.");
                UninstallBtn.IsEnabled = true;
            }
        }

        private async void ImportBtn_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title     = "Import Model Definitions",
                Filter    = "JSON files (*.json)|*.json",
                Multiselect = false,
            };

            if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;

            try
            {
                ModelCatalog.ImportFile(dialog.FileName);
                await LoadModelsAsync(null, _cts?.Token ?? CancellationToken.None);
            }
            catch
            {
                ModelHint.Text = LocalizationService.S("ollama.importFailed", "Import failed.");
            }
        }

        private static string FormatSize(long bytes)
            => OllamaModelCatalogApplicationService.FormatModelSize(bytes);
    }
}
