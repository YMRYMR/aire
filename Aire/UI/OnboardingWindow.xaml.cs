using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using Aire.Data;
using Aire.Providers;
using Aire.Services;
using Aire.UI.Controls;

namespace Aire.UI
{
    public partial class OnboardingWindow : Window
    {
        public const int ProviderCardColumns = 4;

        public Action? OpenSettingsAction { get; set; }

        private int _step = 1;
        private CancellationTokenSource? _testCts;
        private CancellationTokenSource? _modelFetchCts;
        internal bool _claudeSessionActive;

        // Provider display names and docs are sourced from ProviderCatalog.
        internal static string ProviderDisplayName(string type) => ProviderCatalog.GetDisplayName(type);

        public OnboardingWindow()
        {
            InitializeComponent();
            ModelCatalog.EnsureDefaults();
            FontSize = AppearanceService.FontSize;
            AppearanceService.AppearanceChanged += ApplyThemeFontSize;

            ModelCombo.IsTextSearchEnabled = false;
            ModelCombo.AddHandler(
                System.Windows.Controls.Primitives.TextBoxBase.TextChangedEvent,
                new System.Windows.Controls.TextChangedEventHandler(OnStandardModelComboTextChanged));
            ModelCombo.DropDownOpened += (_, _) =>
            {
                EditableComboBoxFilterHelper.FocusEditableTextBox(ModelCombo);
            };
            ModelCombo.PreviewTextInput += (_, e) => EditableComboBoxFilterHelper.HandlePreviewTextInput(ModelCombo, e);
            ModelCombo.PreviewKeyDown += (_, e) => EditableComboBoxFilterHelper.HandlePreviewKeyDown(ModelCombo, e);
            ModelCombo.DropDownClosed += StandardModelCombo_DropDownClosed;

            Loaded += (_, _) =>
            {
                PruneHiddenProviderChoices();
                LocalizationService.LoadAll();
                BuildLanguageButtons();
                DetectAndSelectLanguage();
                BuildProviderCards();
                ShuffleProviderRows();
                ProviderTypeCombo.SelectedIndex = 0;
            };
        }
    }
}
