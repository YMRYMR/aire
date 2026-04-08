using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using Aire.AppLayer.Connections;
using Aire.AppLayer.Chat;
using Aire.AppLayer.Mcp;
using Aire.AppLayer.Settings;
using Aire.AppLayer.Tools;
using Aire.Data;
using Aire.Providers;
using Aire.Services;
using Aire.UI.Controls;

namespace Aire.UI
{
    public partial class SettingsWindow
    {
        public SettingsWindow(SpeechSynthesisService? ttsService = null)
        {
            InitializeComponent();
            PruneHiddenProviderChoices();
            _databaseService = new DatabaseService();
            _mcpCatalogApplicationService = new McpCatalogApplicationService();
            _mcpConfigApplicationService = new McpConfigApplicationService(_databaseService);
            _emailAccountApplicationService = new EmailAccountApplicationService(_databaseService);
            _appSettingsApplicationService = new AppSettingsApplicationService(_databaseService);
            _contextSettingsApplicationService = new ContextSettingsApplicationService(_databaseService);
            _autoAcceptProfilesApplicationService = new AutoAcceptProfilesApplicationService(_databaseService);
            _ttsService = ttsService ?? SpeechSynthesisService.Current;
            WireProviderListPaneEvents();
            WireAppearancePaneEvents();
            WireLocalApiPaneEvents();
            WireEmailPaneEvents();
            WireMcpPaneEvents();
            WireVoicePaneEvents();
            WireContextPaneEvents();
            WireAutoAcceptPaneEvents();
            OllamaModelPicker.ModelSelectionChanged += (_, _) => _ = PerformAutoSave();

            Current = this;
            Aire.Services.AppState.SetSettingsOpen(true);
            LoadWindowState();
            SizeChanged += (_, _) => SaveWindowState();
            LocationChanged += (_, _) => SaveWindowState();

            Loaded += OnWindowLoaded;
        }

        private async void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            if (_savedEditPanelWidth is { } editPanelWidth)
                EditPanelColumn.Width = new GridLength(editPanelWidth, GridUnitType.Pixel);

            FontSize = AppearanceService.FontSize;
            AppearanceService.AppearanceChanged += OnThemeChanged;
            LocalizationService.LanguageChanged += OnLanguageChanged;
            Closed += (_, _) =>
            {
                Current = null;
                if (!Aire.Services.AppState.IsShuttingDown)
                    Aire.Services.AppState.SetSettingsOpen(false);
                AppearanceService.AppearanceChanged -= OnThemeChanged;
                LocalizationService.LanguageChanged -= OnLanguageChanged;
            };

            _suppressAppearance = true;
            BrightnessSlider.Value = AppearanceService.Brightness;
            TintSlider.Value = AppearanceService.TintPosition;
            AccentBrightnessSlider.Value = AppearanceService.AccentBrightness;
            AccentTintSlider.Value = AppearanceService.AccentTintPosition;
            FontSizeSlider.Value = AppearanceService.FontSize;
            FontSizeDisplay.Text = $"{AppearanceService.FontSize:0}";
            BrightnessValueLabel.Text = $"{AppearanceService.Brightness:0.00}";
            TintValueLabel.Text = $"{AppearanceService.TintPosition:0.000}";
            _suppressAppearance = false;

            _suppressApiAccess = true;
            ApiAccessEnabledCheckBox.IsChecked = AppState.GetApiAccessEnabled();
            ApiAccessTokenBox.Text = AppState.EnsureApiAccessToken();
            _suppressApiAccess = false;

            PopulateLanguageComboBox();
            PopulateVoiceSection();

            var hwnd = new WindowInteropHelper(this).Handle;
            int dark = 1;
            DwmSetWindowAttribute(hwnd, 20, ref dark, sizeof(int));
            HwndSource.FromHwnd(hwnd).AddHook(WndProc);

            ApplyLocalization();

            ModelComboBox.IsTextSearchEnabled = false;
            ModelComboBox.AddHandler(System.Windows.Controls.Primitives.TextBoxBase.TextChangedEvent, new TextChangedEventHandler(OnModelComboBoxTextChanged));
            ModelComboBox.DropDownOpened += async (_, _) =>
            {
                EditableComboBoxFilterHelper.FocusEditableTextBox(ModelComboBox);

                if (_selectedProvider != null && _selectedProvider.Type != "Ollama" && !string.IsNullOrWhiteSpace(ApiKeyPasswordBox.Password))
                {
                    try
                    {
                        var meta = ProviderFactory.GetMetadata(_selectedProvider.Type);
                        await PopulateModelsFromMetadataAsync(meta);
                        EditableComboBoxFilterHelper.FocusEditableTextBox(ModelComboBox, selectAll: false);
                    }
                    catch
                    {
                        Debug.WriteLine("Live model fetch on dropdown open failed.");
                    }
                }
            };
            ModelComboBox.PreviewTextInput += (_, e) => EditableComboBoxFilterHelper.HandlePreviewTextInput(ModelComboBox, e);
            ModelComboBox.PreviewKeyDown += (_, e) => EditableComboBoxFilterHelper.HandlePreviewKeyDown(ModelComboBox, e);
            ModelComboBox.DropDownClosed += ModelComboBox_DropDownClosed;

            try
            {
                await _databaseService.InitializeAsync();
                await RefreshProvidersList();
                await LoadContextSettings();
                await LoadAutoAcceptProfilesAsync();
                await LoadAutoAcceptSettings();
                HookAutoAcceptEvents();
            }
            catch
            {
                ShowToast("Database error.", isError: true);
            }
        }
    }
}
