using RoutedEventArgs = System.Windows.RoutedEventArgs;
using RoutedEventHandler = System.Windows.RoutedEventHandler;
using SelectionChangedEventArgs = System.Windows.Controls.SelectionChangedEventArgs;
using SelectionChangedEventHandler = System.Windows.Controls.SelectionChangedEventHandler;
using TextChangedEventArgs = System.Windows.Controls.TextChangedEventArgs;
using TextChangedEventHandler = System.Windows.Controls.TextChangedEventHandler;
using Button = System.Windows.Controls.Button;
using ListBox = System.Windows.Controls.ListBox;
using TextBlock = System.Windows.Controls.TextBlock;
using TextBox = System.Windows.Controls.TextBox;
using UserControl = System.Windows.Controls.UserControl;
using Aire.Services;

namespace Aire.UI.Settings.Controls
{
    public partial class TemplateListPaneControl : UserControl
    {
        private RotatingWatermarkHelper? _templateWatermark;

        public TemplateListPaneControl()
        {
            InitializeComponent();
            Loaded += (_, _) => ApplyLocalization();
            Loaded += (_, _) => Aire.UI.TextEntryLanguageHelper.Apply(PART_SampleTextBox);
            Services.LocalizationService.LanguageChanged += OnLanguageChanged;
            Unloaded += (_, _) => Services.LocalizationService.LanguageChanged -= OnLanguageChanged;
            Unloaded += (_, _) => _templateWatermark?.Dispose();
        }

        private void OnLanguageChanged() => Dispatcher.Invoke(ApplyLocalization);

        public Button AddTemplateButton => PART_AddTemplateButton;
        public ListBox TemplatesListView => PART_TemplatesListView;
        public TextBlock EditPanelTitle => PART_EditPanelTitle;
        public TextBox NameTextBox => PART_NameTextBox;
        public TextBox PrefixTextBox => PART_PrefixTextBox;
        public TextBox ShortcutTextBox => PART_ShortcutTextBox;
        public TextBox TemplateTextBox => PART_TemplateTextBox;
        public TextBlock TemplateWatermark => PART_TemplateWatermark;
        public TextBox SampleTextBox => PART_SampleTextBox;
        public TextBlock PreviewText => PART_PreviewText;
        public Button SaveButton => PART_SaveButton;

        public event RoutedEventHandler? AddTemplateClicked;
        public event SelectionChangedEventHandler? TemplateSelectionChanged;
        public event RoutedEventHandler? DeleteTemplateClicked;
        public event RoutedEventHandler? SaveTemplateClicked;
        public event TextChangedEventHandler? TemplateTextChanged;
        public event RoutedEventHandler? ExplainRecipeClicked;
        public event RoutedEventHandler? FixRecipeClicked;
        public event RoutedEventHandler? SummarizeRecipeClicked;
        public event RoutedEventHandler? ReviewRecipeClicked;

        public void ApplyLocalization()
        {
            var L = LocalizationService.S;

            PART_ListTitle.Text = L("settings.templates.listTitle", "Templates");
            PART_ListSubtitle.Text = L("settings.templates.listSubtitle", "Reusable prompts and slash shortcuts.");
            PART_AddTemplateButton.Content = L("settings.templates.addButton", "+ Add template");
            PART_EditPanelSubtitle.Text = L("settings.templates.panelSubtitle", "Write a prompt, test it with sample text, then save.");
            PART_EditPanelTitle.Text = L("settings.templates.createTitle", "Create a template");
            PART_NameLabel.Text = L("settings.templates.nameLabel", "Name");
            PART_ShortcutLabel.Text = L("settings.templates.shortcutLabel", "Shortcut");
            PART_PrefixLabel.Text = L("settings.templates.prefixLabel", "Extra note for Aire");
            PART_PrefixDescription.Text = L("settings.templates.prefixDescription", "Optional text Aire adds before the template.");
            PART_BodyStepTitle.Text = L("settings.templates.bodyStepTitle", "2. Write the template");
            PART_TestStepTitle.Text = L("settings.templates.testStepTitle", "3. Test it");
            PART_TestStepDescription.Text = L("settings.templates.testStepDescription", "Type a sample code snippet or a short piece of text. Aire will show you how the template changes it.");
            PART_PreviewHeading.Text = L("settings.templates.previewHeading", "What Aire will say");
            PART_SaveButton.Content = L("settings.templates.saveButton", "Save");
            Aire.UI.TextEntryLanguageHelper.Apply(PART_TemplateTextBox);
            Aire.UI.TextEntryLanguageHelper.Apply(PART_SampleTextBox);

            if (TemplatesListView.SelectedItem is not PromptTemplate)
            {
                PART_EditPanelTitle.Text = L("settings.templates.createTitle", "Create a template");
                PART_PreviewText.Text = L("settings.templates.previewEmpty", "Select a template to see a preview");
                PART_SampleTextBox.Text = L("settings.templates.samplePlaceholder", "Paste a short example here to test the template.");
            }

            RefreshWatermark();
        }

        private void AddTemplateButton_Click(object sender, RoutedEventArgs e)
            => AddTemplateClicked?.Invoke(this, e);

        private void TemplatesListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
            => TemplateSelectionChanged?.Invoke(this, e);

        private void DeleteTemplate_Click(object sender, RoutedEventArgs e)
            => DeleteTemplateClicked?.Invoke(this, e);

        private void SaveButton_Click(object sender, RoutedEventArgs e)
            => SaveTemplateClicked?.Invoke(this, e);

        private void TemplateField_TextChanged(object sender, TextChangedEventArgs e)
            => TemplateTextChanged?.Invoke(this, e);

        private void ApplyExplainRecipe_Click(object sender, RoutedEventArgs e)
            => ExplainRecipeClicked?.Invoke(this, e);

        private void ApplyFixRecipe_Click(object sender, RoutedEventArgs e)
            => FixRecipeClicked?.Invoke(this, e);

        private void ApplySummarizeRecipe_Click(object sender, RoutedEventArgs e)
            => SummarizeRecipeClicked?.Invoke(this, e);

        private void ApplyReviewRecipe_Click(object sender, RoutedEventArgs e)
            => ReviewRecipeClicked?.Invoke(this, e);

        private void FillCodeExample_Click(object sender, RoutedEventArgs e)
            => PART_SampleTextBox.Text = "public string Greeting(string name) => $\"Hello, {name}!\";";

        private void FillTextExample_Click(object sender, RoutedEventArgs e)
            => PART_SampleTextBox.Text = LocalizationService.S("settings.templates.sampleText", "This is a short note that needs to be explained or summarized.");

        private void InsertPlaceholder_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.Tag is string placeholder)
            {
                var caretIndex = PART_TemplateTextBox.CaretIndex;
                PART_TemplateTextBox.Text = PART_TemplateTextBox.Text.Insert(caretIndex, placeholder);
                PART_TemplateTextBox.CaretIndex = caretIndex + placeholder.Length;
                PART_TemplateTextBox.Focus();
            }
        }

        private void InitializeWatermark()
            => RefreshWatermark();

        private void RefreshWatermark()
        {
            _templateWatermark?.Dispose();
            _templateWatermark = new RotatingWatermarkHelper(
                PART_TemplateTextBox,
                PART_TemplateWatermark,
                LocalizationService.S(
                    "settings.templates.watermarkExamples",
                    "Explain this code in simple words:\n\n{{code}}\n" +
                    "Summarize this meeting note into five bullets:\n\n{{text}}\n" +
                    "Rewrite this paragraph in plain English:\n\n{{text}}\n" +
                    "Turn this support ticket into a short checklist:\n\n{{text}}\n" +
                    "Extract the action items from this email:\n\n{{text}}\n" +
                    "Review the selected text and point out unclear parts:\n\n{{selection}}\n" +
                    "Draft a friendly reply using the clipboard text as context:\n\n{{clipboard}}\n" +
                    "Convert this rough idea into a short project brief:\n\n{{text}}\n" +
                    "Create a concise changelog entry from these notes:\n\n{{text}}\n" +
                    "Find the bugs in this code and suggest fixes:\n\n{{code}}\n" +
                    "Turn this article into a beginner-friendly FAQ:\n\n{{text}}\n" +
                    "Write a short social post announcing this update:\n\n{{text}}\n" +
                    "Compare these two product descriptions and highlight the differences:\n\n{{selection}}\n\n{{clipboard}}\n" +
                    "Generate a study guide from these class notes:\n\n{{text}}\n" +
                    "Turn this clipboard text into a polished email:\n\n{{clipboard}}\n" +
                    "Create a project summary for a manager:\n\n{{text}}\n" +
                    "Rewrite this recipe in simple step-by-step language:\n\n{{text}}\n" +
                    "Turn this code comment into a better commit message:\n\n{{code}}\n" +
                    "Draft a one-paragraph description for this webpage:\n\n{{text}}\n" +
                    "Summarize this document for someone who is busy:\n\n{{text}}")
                    .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries));
        }
    }
}
