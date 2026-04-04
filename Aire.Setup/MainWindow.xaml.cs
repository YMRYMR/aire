using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Aire.AppLayer.Chat;
using Aire.Bootstrap;
using Aire.Services;

namespace Aire.Setup;

public partial class MainWindow : Window
{
    private readonly SpeechSynthesisService _ttsService;
    private readonly SpeechRecognitionService _speechService;
    private readonly AssistantModeApplicationService _assistantModeService;
    private List<AssistantModeOption> _assistantModes = [];
    private bool _suppressEvents;
    private bool _voiceSessionActive;

    public MainWindow()
    {
        InitializeComponent();
        FontSize = AppearanceService.FontSize;

        _assistantModeService = new AssistantModeApplicationService();

        _ttsService = new SpeechSynthesisService();
        _ttsService.SetUseLocalOnly(false, notify: false);
        _ttsService.SetVoiceEnabled(true, notify: false);

        _speechService = new SpeechRecognitionService();
        _speechService.PhraseRecognized += OnVoicePhraseRecognized;
        _speechService.CommandRecognized += OnVoicePhraseRecognized;
        _speechService.Stopped += OnVoiceStopped;
        _speechService.DownloadProgress += OnVoiceDownloadProgress;

        LocalizationService.LanguageChanged += OnLanguageChanged;
        AppearanceService.AppearanceChanged += ApplyThemeFontSize;

        BuildLanguageChoices();
        BuildAssistantModeChoices();
        LoadPreferences();
        ApplyLocalization();
        UpdateSummary();
        UpdateVoiceStatus();
    }

    private void ApplyThemeFontSize()
        => Dispatcher.Invoke(() => FontSize = AppearanceService.FontSize);

    private void OnLanguageChanged()
    {
        Dispatcher.Invoke(() =>
        {
            ApplyLocalization();
            BuildLanguageChoices();
            BuildAssistantModeChoices();
            UpdateModeDescription();
            UpdateSummary();
            UpdateVoiceStatus();
        });
    }

    private void BuildLanguageChoices()
    {
        string? selected = GetSelectedTag(LanguageComboBox);
        LanguageComboBox.Items.Clear();

        IReadOnlyList<LanguageInfo> languages = LocalizationService.AvailableLanguages.Count > 0
            ? LocalizationService.AvailableLanguages
            : [new LanguageInfo("en", "English", ""), new LanguageInfo("es", "Español", "")];

        foreach (LanguageInfo language in languages)
        {
            LanguageComboBox.Items.Add(new ComboBoxItem
            {
                Content = language.NativeName,
                Tag = language.Code,
            });
        }

        SelectComboItemByTag(LanguageComboBox, selected ?? LocalizationService.CurrentCode);
    }

    private void BuildAssistantModeChoices()
    {
        string? selected = GetSelectedTag(AssistantModeComboBox);
        _assistantModes = _assistantModeService.GetModes()
            .Select(mode => new AssistantModeOption(mode.Key, mode.DisplayName, mode.Description))
            .ToList();

        AssistantModeComboBox.Items.Clear();
        foreach (AssistantModeOption option in _assistantModes)
        {
            AssistantModeComboBox.Items.Add(new ComboBoxItem
            {
                Content = option.DisplayName,
                Tag = option.Key,
            });
        }

        SelectComboItemByTag(AssistantModeComboBox, selected ?? SetupPreferencesStore.Load().DefaultAssistantMode);
    }

    private void ApplyLocalization()
    {
        var L = LocalizationService.S;

        Title = L("setup.title", "Aire - Setup");
        WindowTitleTextBlock.Text = Title;
        CloseButton.ToolTip = LocalizationService.S("tooltip.close", "Close");

        HeroTitleTextBlock.Text = L("setup.heroTitle", "First-Run Setup");
        HeroDescriptionTextBlock.Text = L("setup.heroDescription", "Choose the initial accessibility and assistant defaults before Aire opens its normal onboarding wizard.");

        LanguageSectionTitleTextBlock.Text = L("setup.languageSectionTitle", "1. Language");
        LanguageSectionDescriptionTextBlock.Text = L("setup.languageSectionDescription", "Pick the language Aire should use on first launch.");

        AssistantModeSectionTitleTextBlock.Text = L("setup.assistantModeSectionTitle", "2. Default Assistant Mode");
        AssistantModeSectionDescriptionTextBlock.Text = L("setup.assistantModeSectionDescription", "This will be the starting mode for new conversations until you change it.");

        AccessibilitySectionTitleTextBlock.Text = L("setup.accessibilitySectionTitle", "3. Accessibility Defaults");
        VoiceOutputCheckBox.Content = L("setup.voiceOutputLabel", "Enable voice output in Aire from the start");
        VoiceOutputDescriptionTextBlock.Text = L("setup.voiceOutputDescription", "Aire can read responses aloud after the main app opens.");
        VoiceInputCheckBox.Content = L("setup.voiceInputLabel", "Enable voice input from the start");
        VoiceInputDescriptionTextBlock.Text = L("setup.voiceInputDescription", "If the local speech model is available, Aire will start listening automatically on launch.");
        UseLocalVoicesOnlyCheckBox.Content = L("setup.localVoicesLabel", "Use only local Windows voices");
        UseLocalVoicesOnlyDescriptionTextBlock.Text = L("setup.localVoicesDescription", "Useful when you want setup and voice output to work offline.");

        VoiceGuidedSetupSectionTitleTextBlock.Text = L("setup.voiceGuidedSectionTitle", "4. Voice-Guided Setup");
        VoiceGuidanceCheckBox.Content = L("setup.voiceGuidanceLabel", "Read setup instructions aloud");
        VoiceCommandsCheckBox.Content = L("setup.voiceCommandsLabel", "Allow spoken setup commands");
        VoiceCommandsDescriptionTextBlock.Text = L("setup.voiceCommandsDescription", "Recognized commands include: English, Spanish, General, Developer, Architect, Teacher, Enable voice output, Disable voice output, Enable voice input, Disable voice input, Read summary, Save, Open Aire.");
        StartVoiceButton.Content = L("setup.startVoice", "Start Voice Setup");
        StopVoiceButton.Content = L("setup.stopVoice", "Stop Voice Setup");

        SummarySectionTitleTextBlock.Text = L("setup.summaryTitle", "Summary");
        SaveButton.Content = L("setup.savePreferences", "Save Preferences");
        SaveAndOpenButton.Content = L("setup.saveAndOpen", "Save And Open Aire");
    }

    private void LoadPreferences()
    {
        _suppressEvents = true;
        try
        {
            SetupPreferences preferences = SetupPreferencesStore.Load();
            if (!string.IsNullOrWhiteSpace(preferences.LanguageCode))
            {
                LocalizationService.SetLanguage(preferences.LanguageCode);
            }

            BuildLanguageChoices();
            BuildAssistantModeChoices();
            SelectComboItemByTag(LanguageComboBox, preferences.LanguageCode);
            SelectComboItemByTag(AssistantModeComboBox, preferences.DefaultAssistantMode);
            VoiceInputCheckBox.IsChecked = preferences.VoiceInputEnabled;
            VoiceOutputCheckBox.IsChecked = preferences.VoiceOutputEnabled;
            VoiceGuidanceCheckBox.IsChecked = preferences.VoiceGuidanceEnabled;
            VoiceCommandsCheckBox.IsChecked = preferences.VoiceGuidanceEnabled || preferences.VoiceInputEnabled;
            UseLocalVoicesOnlyCheckBox.IsChecked = preferences.UseLocalVoicesOnly;
            _ttsService.SetUseLocalOnly(preferences.UseLocalVoicesOnly, notify: false);
            _ttsService.SetVoiceEnabled(true, notify: false);
            if (!string.IsNullOrWhiteSpace(preferences.SelectedVoice))
            {
                _ttsService.SetVoice(preferences.SelectedVoice, notify: false);
            }

            _ttsService.SetRate(preferences.VoiceRate, notify: false);
            UpdateModeDescription();
        }
        finally
        {
            _suppressEvents = false;
        }
    }

    private static void SelectComboItemByTag(ComboBox comboBox, string? tag)
    {
        ComboBoxItem? item = comboBox.Items
            .OfType<ComboBoxItem>()
            .FirstOrDefault(candidate => string.Equals(candidate.Tag as string, tag, StringComparison.OrdinalIgnoreCase));
        comboBox.SelectedItem = item ?? comboBox.Items.OfType<ComboBoxItem>().FirstOrDefault();
    }

    private SetupPreferences BuildPreferences()
    {
        return new SetupPreferences
        {
            LanguageCode = GetSelectedTag(LanguageComboBox) ?? "en",
            DefaultAssistantMode = GetSelectedTag(AssistantModeComboBox) ?? "general",
            VoiceInputEnabled = VoiceInputCheckBox.IsChecked == true,
            VoiceOutputEnabled = VoiceOutputCheckBox.IsChecked == true,
            VoiceGuidanceEnabled = VoiceGuidanceCheckBox.IsChecked == true,
            UseLocalVoicesOnly = UseLocalVoicesOnlyCheckBox.IsChecked == true,
            SelectedVoice = _ttsService.SelectedVoice,
            VoiceRate = _ttsService.Rate,
        };
    }

    private static string? GetSelectedTag(ComboBox comboBox)
        => (comboBox.SelectedItem as ComboBoxItem)?.Tag as string;

    private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressEvents)
        {
            return;
        }

        string? code = GetSelectedTag(LanguageComboBox);
        if (!string.IsNullOrWhiteSpace(code) && !string.Equals(code, LocalizationService.CurrentCode, StringComparison.OrdinalIgnoreCase))
        {
            LocalizationService.SetLanguage(code);
            return;
        }

        UpdateSummary();
    }

    private void AssistantModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressEvents)
        {
            return;
        }

        UpdateModeDescription();
        UpdateSummary();
    }

    private void PreferenceControl_Changed(object sender, RoutedEventArgs e)
    {
        if (_suppressEvents)
        {
            return;
        }

        _ttsService.SetUseLocalOnly(UseLocalVoicesOnlyCheckBox.IsChecked == true, notify: false);
        UpdateSummary();
        UpdateVoiceStatus();

        if (VoiceGuidanceCheckBox.IsChecked == true)
        {
            SpeakSummary();
        }
    }

    private void UpdateModeDescription()
    {
        string? key = GetSelectedTag(AssistantModeComboBox);
        AssistantModeOption option = _assistantModes.FirstOrDefault(mode => mode.Key == key)
            ?? _assistantModes.First(mode => mode.Key == "general");
        AssistantModeDescriptionTextBlock.Text = option.Description;
    }

    private void UpdateSummary()
    {
        SetupPreferences preferences = BuildPreferences();
        string language = LocalizationService.AvailableLanguages
            .FirstOrDefault(lang => string.Equals(lang.Code, preferences.LanguageCode, StringComparison.OrdinalIgnoreCase))
            ?.NativeName ?? preferences.LanguageCode;
        string assistantMode = _assistantModes.FirstOrDefault(mode => mode.Key == preferences.DefaultAssistantMode)?.DisplayName
            ?? LocalizationService.S("assistantMode.general.name", "General");
        string enabled = LocalizationService.S("setup.enabled", "Enabled");
        string disabled = LocalizationService.S("setup.disabled", "Disabled");
        string yes = LocalizationService.S("setup.yes", "Yes");
        string no = LocalizationService.S("setup.no", "No");

        SummaryTextBlock.Text =
            $"{LocalizationService.S("setup.summaryLanguage", "Language")}: {language}\n\n" +
            $"{LocalizationService.S("setup.summaryAssistantMode", "Default assistant mode")}: {assistantMode}\n\n" +
            $"{LocalizationService.S("setup.summaryVoiceOutput", "Voice output in Aire")}: {(preferences.VoiceOutputEnabled ? enabled : disabled)}\n" +
            $"{LocalizationService.S("setup.summaryVoiceInput", "Voice input in Aire")}: {(preferences.VoiceInputEnabled ? enabled : disabled)}\n" +
            $"{LocalizationService.S("setup.summaryLocalVoices", "Use local voices only")}: {(preferences.UseLocalVoicesOnly ? yes : no)}\n\n" +
            $"{LocalizationService.S("setup.summaryReadAloud", "Read setup aloud")}: {(preferences.VoiceGuidanceEnabled ? enabled : disabled)}\n" +
            $"{LocalizationService.S("setup.summaryVoiceCommands", "Spoken setup commands")}: {(VoiceCommandsCheckBox.IsChecked == true ? enabled : disabled)}\n\n" +
            LocalizationService.S("setup.summaryNextStep", "When you open Aire, it will continue into the normal onboarding wizard so you can configure a provider.");
    }

    private void UpdateVoiceStatus(string? overrideText = null)
    {
        if (!string.IsNullOrWhiteSpace(overrideText))
        {
            VoiceStatusTextBlock.Text = overrideText;
            return;
        }

        if (_voiceSessionActive)
        {
            VoiceStatusTextBlock.Text = LocalizationService.S("setup.voiceStatusListening", "Voice setup is listening. Speak a command, then pause.");
            return;
        }

        if (!VoiceCommandsCheckBox.IsChecked.GetValueOrDefault())
        {
            VoiceStatusTextBlock.Text = LocalizationService.S("setup.voiceStatusCommandsOff", "Spoken setup commands are off.");
            return;
        }

        if (!_speechService.ModelExists)
        {
            VoiceStatusTextBlock.Text = LocalizationService.S("setup.voiceStatusModelNeeded", "Voice commands need the local speech model. Start Voice Setup to download it.");
            return;
        }

        VoiceStatusTextBlock.Text = LocalizationService.S("setup.voiceStatusReady", "Voice commands are ready. Start Voice Setup to begin listening.");
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        SavePreferences();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        DragMove();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void SaveAndOpenButton_Click(object sender, RoutedEventArgs e)
    {
        SavePreferences();
        if (!TryLaunchAire())
        {
            MessageBox.Show(
                LocalizationService.S("setup.launchNotFoundMessage", "Preferences were saved, but Aire.exe was not found next to the setup app. Launch Aire manually to continue with onboarding."),
                LocalizationService.S("setup.title", "Aire - Setup"),
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }

    private void SavePreferences()
    {
        SetupPreferencesStore.Save(BuildPreferences());
        UpdateVoiceStatus(LocalizationService.S("setup.preferencesSaved", "Preferences saved."));
        if (VoiceGuidanceCheckBox.IsChecked == true)
        {
            _ttsService.SetVoiceEnabled(true, notify: false);
            _ttsService.Speak(LocalizationService.S("setup.preferencesSavedSpoken", "Preferences saved. Aire is ready to open its onboarding wizard."));
        }
    }

    private async void StartVoiceButton_Click(object sender, RoutedEventArgs e)
    {
        VoiceCommandsCheckBox.IsChecked = true;
        if (!_speechService.ModelExists)
        {
            UpdateVoiceStatus(LocalizationService.S("setup.voiceStatusDownloading", "Downloading the local speech model for voice setup..."));
            bool downloaded = await _speechService.DownloadModelAsync();
            if (!downloaded)
            {
                UpdateVoiceStatus(LocalizationService.S("setup.voiceStatusDownloadFailed", "Voice model download failed. Check the internet connection and try again."));
                return;
            }
        }

        string? error = _speechService.StartListening();
        if (error != null)
        {
            UpdateVoiceStatus(string.Format(LocalizationService.S("setup.voiceStatusStartFailed", "Voice setup could not start: {0}"), error));
            return;
        }

        _voiceSessionActive = true;
        UpdateVoiceStatus();
        if (VoiceGuidanceCheckBox.IsChecked == true)
        {
            SpeakSummary();
        }
    }

    private void StopVoiceButton_Click(object sender, RoutedEventArgs e)
    {
        _speechService.StopListening();
        _voiceSessionActive = false;
        UpdateVoiceStatus();
    }

    private void OnVoiceStopped()
    {
        Dispatcher.Invoke(() =>
        {
            _voiceSessionActive = false;
            UpdateVoiceStatus();
        });
    }

    private void OnVoiceDownloadProgress(double progress)
    {
        Dispatcher.Invoke(() =>
        {
            UpdateVoiceStatus(string.Format(LocalizationService.S("setup.voiceStatusDownloadProgress", "Downloading voice model: {0}%"), (int)(progress * 100)));
        });
    }

    private void OnVoicePhraseRecognized(string phrase)
    {
        Dispatcher.Invoke(() => ApplyVoiceCommand(phrase));
    }

    private void ApplyVoiceCommand(string phrase)
    {
        string normalized = NormalizeVoiceCommand(phrase);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return;
        }

        if (normalized.Contains("english"))
        {
            SelectComboItemByTag(LanguageComboBox, "en");
        }
        else if (normalized.Contains("spanish") || normalized.Contains("espanol"))
        {
            SelectComboItemByTag(LanguageComboBox, "es");
        }
        else if (normalized.Contains("general"))
        {
            SelectComboItemByTag(AssistantModeComboBox, "general");
        }
        else if (normalized.Contains("developer"))
        {
            SelectComboItemByTag(AssistantModeComboBox, "developer");
        }
        else if (normalized.Contains("architect"))
        {
            SelectComboItemByTag(AssistantModeComboBox, "architect");
        }
        else if (normalized.Contains("teacher"))
        {
            SelectComboItemByTag(AssistantModeComboBox, "teacher");
        }
        else if (normalized.Contains("scientist"))
        {
            SelectComboItemByTag(AssistantModeComboBox, "scientist");
        }
        else if (normalized.Contains("security"))
        {
            SelectComboItemByTag(AssistantModeComboBox, "security");
        }
        else if (normalized.Contains("painter"))
        {
            SelectComboItemByTag(AssistantModeComboBox, "painter");
        }
        else if (normalized.Contains("enable voice output"))
        {
            VoiceOutputCheckBox.IsChecked = true;
        }
        else if (normalized.Contains("disable voice output"))
        {
            VoiceOutputCheckBox.IsChecked = false;
        }
        else if (normalized.Contains("enable voice input"))
        {
            VoiceInputCheckBox.IsChecked = true;
        }
        else if (normalized.Contains("disable voice input"))
        {
            VoiceInputCheckBox.IsChecked = false;
        }
        else if (normalized.Contains("enable local voices"))
        {
            UseLocalVoicesOnlyCheckBox.IsChecked = true;
        }
        else if (normalized.Contains("disable local voices"))
        {
            UseLocalVoicesOnlyCheckBox.IsChecked = false;
        }
        else if (normalized.Contains("read summary"))
        {
            SpeakSummary();
            return;
        }
        else if (normalized.Contains("save and open") || normalized.Contains("open aire") || normalized.Contains("launch aire"))
        {
            SavePreferences();
            TryLaunchAire();
            return;
        }
        else if (normalized.Contains("save"))
        {
            SavePreferences();
            return;
        }
        else
        {
            UpdateVoiceStatus(string.Format(LocalizationService.S("setup.voiceStatusCommandNotRecognized", "Voice command not recognized: {0}"), phrase));
            return;
        }

        string? selectedLanguage = GetSelectedTag(LanguageComboBox);
        if (!string.IsNullOrWhiteSpace(selectedLanguage) && !string.Equals(selectedLanguage, LocalizationService.CurrentCode, StringComparison.OrdinalIgnoreCase))
        {
            LocalizationService.SetLanguage(selectedLanguage);
        }

        UpdateModeDescription();
        UpdateSummary();
        UpdateVoiceStatus(string.Format(LocalizationService.S("setup.voiceStatusCommandApplied", "Applied voice command: {0}"), phrase));
        if (VoiceGuidanceCheckBox.IsChecked == true)
        {
            SpeakSummary();
        }
    }

    private void SpeakSummary()
    {
        _ttsService.SetVoiceEnabled(true, notify: false);
        _ttsService.SetUseLocalOnly(UseLocalVoicesOnlyCheckBox.IsChecked == true, notify: false);
        _ttsService.Speak(
            string.Format(
                LocalizationService.S("setup.summarySpoken", "Language {0}. Assistant mode {1}. Voice output {2}. Voice input {3}. Say save, or say open Aire."),
                LocalizationService.AvailableLanguages.FirstOrDefault(lang => lang.Code == GetSelectedTag(LanguageComboBox))?.NativeName ?? "English",
                ((AssistantModeComboBox.SelectedItem as ComboBoxItem)?.Content as string) ?? LocalizationService.S("assistantMode.general.name", "General"),
                VoiceOutputCheckBox.IsChecked == true ? LocalizationService.S("setup.enabledSpoken", "enabled") : LocalizationService.S("setup.disabledSpoken", "disabled"),
                VoiceInputCheckBox.IsChecked == true ? LocalizationService.S("setup.enabledSpoken", "enabled") : LocalizationService.S("setup.disabledSpoken", "disabled")));
    }

    private static string NormalizeVoiceCommand(string phrase)
        => phrase.Trim().ToLowerInvariant();

    private bool TryLaunchAire()
    {
        string? airePath = ResolveAireExecutablePath();
        if (airePath == null)
        {
            return false;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = airePath,
            UseShellExecute = true,
        });
        return true;
    }

    private static string? ResolveAireExecutablePath()
    {
        string baseDirectory = AppContext.BaseDirectory;
        string[] candidates =
        [
            Path.Combine(baseDirectory, "Aire.exe"),
            Path.Combine(baseDirectory, "..", "Aire.exe"),
            Path.GetFullPath(Path.Combine(baseDirectory, "..", "..", "..", "Aire", "bin", "Debug", "net10.0-windows10.0.17763.0", "Aire.exe")),
            Path.GetFullPath(Path.Combine(baseDirectory, "..", "..", "..", "Aire", "bin", "Release", "net10.0-windows10.0.17763.0", "Aire.exe")),
            Path.GetFullPath(Path.Combine(baseDirectory, "..", "..", "..", "..", "Aire", "bin", "Debug", "net10.0-windows10.0.17763.0", "Aire.exe")),
            Path.GetFullPath(Path.Combine(baseDirectory, "..", "..", "..", "..", "Aire", "bin", "Release", "net10.0-windows10.0.17763.0", "Aire.exe")),
        ];

        return candidates.FirstOrDefault(File.Exists);
    }

    protected override void OnClosed(EventArgs e)
    {
        AppearanceService.AppearanceChanged -= ApplyThemeFontSize;
        LocalizationService.LanguageChanged -= OnLanguageChanged;
        _speechService.Dispose();
        _ttsService.Dispose();
        base.OnClosed(e);
    }

    private sealed record AssistantModeOption(string Key, string DisplayName, string Description);
}
