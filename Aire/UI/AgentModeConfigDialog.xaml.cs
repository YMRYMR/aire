using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Aire.Domain.Tools;
using Aire.Services;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace Aire.UI
{
    public partial class AgentModeConfigDialog : Window
    {
        private RotatingWatermarkHelper? _goalsWatermark;
        private bool _loadedPersistedConfig;

        public sealed record OrchestratorConfig(
            int TokenBudget,
            IReadOnlyCollection<string> Goals,
            HashSet<string> SelectedCategories);

        /// <summary>The user-selected token budget (0 = unlimited).</summary>
        public int TokenBudget { get; private set; }

        /// <summary>The goals entered by the user, one per line.</summary>
        public IReadOnlyCollection<string> Goals { get; private set; } = Array.Empty<string>();

        /// <summary>The user-selected tool categories (empty = all).</summary>
        public HashSet<string> SelectedCategories { get; private set; } = [];

        public bool UserConfirmed { get; private set; }

        public AgentModeConfigDialog()
        {
            InitializeComponent();
            InitializeThemeAndLanguage();
            Closed += (_, _) => PersistCurrentConfig();
            Closed += (_, _) => _goalsWatermark?.Dispose();
        }

        private void OnThemeChanged() => Dispatcher.Invoke(() => FontSize = Services.AppearanceService.FontSize);
        private void OnLanguageChanged() => Dispatcher.Invoke(ApplyLocalization);

        private void InitializeThemeAndLanguage()
        {
            FontSize = Services.AppearanceService.FontSize;
            Services.AppearanceService.AppearanceChanged += OnThemeChanged;
            Closed += (_, _) => Services.AppearanceService.AppearanceChanged -= OnThemeChanged;
            Services.LocalizationService.LanguageChanged += OnLanguageChanged;
            Closed += (_, _) => Services.LocalizationService.LanguageChanged -= OnLanguageChanged;
        }

        private void ApplyLocalization()
        {
            var L = Services.LocalizationService.S;
            Title = L("orchestrator.title", "Orchestrator Mode");
            TitleBarText.Text = L("orchestrator.dialogTitle", "Orchestrator Mode configuration");
            GoalsLabel.Text = L("orchestrator.goalsLabel", "Goals");
            GoalsDescriptionText.Text = L("orchestrator.goalsDescription", "Write one goal per line. The orchestrator will keep track of them while it runs.");
            BudgetLabel.Text = L("orchestrator.tokenBudgetLabel", "Token budget (0 = unlimited):");
            CategoriesLabel.Text = L("orchestrator.categoriesLabel", "Allowed tool categories");
            CategoriesDescriptionText.Text = L("orchestrator.categoriesDescription", "Leave everything checked if you want the orchestrator to handle the full goal flow.");
            CatFiles.Content = L("orchestrator.category.files", "Files");
            CatBrowser.Content = L("orchestrator.category.browser", "Browser");
            CatAgent.Content = L("orchestrator.category.agent", "Agent");
            CatMouse.Content = L("orchestrator.category.mouse", "Mouse");
            CatKeyboard.Content = L("orchestrator.category.keyboard", "Keyboard");
            CatSystem.Content = L("orchestrator.category.system", "System");
            CatEmail.Content = L("orchestrator.category.email", "Email");
            StartButton.Content = L("orchestrator.startButton", "Start");
            CancelButton.Content = L("orchestrator.cancelButton", "Cancel");
            FlowDirection = Services.LocalizationService.IsRightToLeftLanguage(Services.LocalizationService.CurrentCode)
                ? System.Windows.FlowDirection.RightToLeft
                : System.Windows.FlowDirection.LeftToRight;
            TextEntryLanguageHelper.Apply(GoalsTextBox);
            RefreshWatermark();
        }

        private void RefreshWatermark()
        {
            _goalsWatermark?.Dispose();
            _goalsWatermark = new RotatingWatermarkHelper(
                GoalsTextBox,
                GoalsWatermark,
                LocalizationService
                    .S("orchestrator.goalWatermarkExamples",
                        "Find the cheapest non-stop flight from Madrid to London next Friday.\n" +
                        "Write a 50,000-word mystery novel about a cat investigator and an ancient spaceship.\n" +
                        "Write a Rust app that shows me how to cook pasta and prints the steps in a terminal UI.\n" +
                        "Find all text documents on this machine that mention the singularity.\n" +
                        "Summarize the release notes on this webpage and tell me if they affect Linux users.\n" +
                        "Compare the product details on these two webpages and tell me which one is cheaper.\n" +
                        "Draft a polite email asking for an extension on a deadline.\n" +
                        "Find the best train option from Barcelona to Valencia this weekend and note the price.\n" +
                        "Read this GitHub issue and turn it into a short implementation checklist.\n" +
                        "Search my downloads folder for PDFs that mention rent or lease and list the files.\n" +
                        "Write a Python script that renames image files by date taken.\n" +
                        "Take the text from this page and turn it into a simple FAQ.\n" +
                        "Find three family-friendly restaurants near Plaza Mayor with vegetarian options.\n" +
                        "Compare the salary, commute, and remote policy in these two job offer emails.\n" +
                        "Look at this support ticket and propose the smallest safe fix.\n" +
                        "Summarize this meeting transcript in five bullets and list open questions.\n" +
                        "Find all Word documents on this machine that mention the word protocol.\n" +
                        "Help me plan a one-day itinerary in Lisbon with two museum stops and a lunch place.\n" +
                        "Draft a changelog entry for the last code change and keep it short.\n" +
                        "Turn this rough product idea into a clear step-by-step launch plan.")
                    .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries));
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            ApplyLocalization();
            LoadPersistedConfig();
            GoalsTextBox.Focus();
        }

        private void LoadPersistedConfig()
        {
            if (_loadedPersistedConfig)
                return;

            _loadedPersistedConfig = true;
            var persisted = AppState.GetOrchestratorConfig();
            if (persisted == null)
                return;

            BudgetTextBox.Text = persisted.TokenBudget.ToString();
            GoalsTextBox.Text = string.Join(Environment.NewLine, persisted.Goals ?? []);

            var selected = new HashSet<string>(persisted.SelectedCategories ?? [], StringComparer.OrdinalIgnoreCase);
            CatFiles.IsChecked = selected.Contains("filesystem");
            CatBrowser.IsChecked = selected.Contains("browser");
            CatAgent.IsChecked = selected.Contains("agent");
            CatMouse.IsChecked = selected.Contains("mouse");
            CatKeyboard.IsChecked = selected.Contains("keyboard");
            CatSystem.IsChecked = selected.Contains("system");
            CatEmail.IsChecked = selected.Contains("email");
        }

        private void PersistCurrentConfig()
        {
            if (!int.TryParse(BudgetTextBox.Text, out var budget) || budget < 0)
                budget = 0;

            var goals = GoalsTextBox.Text
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(goal => goal.Trim())
                .Where(goal => !string.IsNullOrWhiteSpace(goal))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var categories = new List<string>();
            if (CatFiles.IsChecked == true) categories.Add("filesystem");
            if (CatBrowser.IsChecked == true) categories.Add("browser");
            if (CatAgent.IsChecked == true) categories.Add("agent");
            if (CatMouse.IsChecked == true) categories.Add("mouse");
            if (CatKeyboard.IsChecked == true) categories.Add("keyboard");
            if (CatSystem.IsChecked == true) categories.Add("system");
            if (CatEmail.IsChecked == true) categories.Add("email");

            AppState.SetOrchestratorConfig(new OrchestratorConfigSnapshot(
                budget,
                goals,
                categories));
        }

        private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Escape:
                    e.Handled = true;
                    CancelButton_Click(sender, e);
                    break;

                case Key.Enter:
                    e.Handled = true;
                    if (Keyboard.FocusedElement is System.Windows.Controls.Button focusedBtn)
                        focusedBtn.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
                    else
                        StartButton_Click(sender, e);
                    break;
            }
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(BudgetTextBox.Text, out var budget) || budget < 0)
            {
                BudgetTextBox.Text = "0";
                BudgetTextBox.Focus();
                BudgetTextBox.SelectAll();
                return;
            }

            TokenBudget = budget;
            Goals = GoalsTextBox.Text
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(goal => goal.Trim())
                .Where(goal => !string.IsNullOrWhiteSpace(goal))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var categories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (CatFiles.IsChecked == true) categories.Add("filesystem");
            if (CatBrowser.IsChecked == true) categories.Add("browser");
            if (CatAgent.IsChecked == true) categories.Add("agent");
            if (CatMouse.IsChecked == true) categories.Add("mouse");
            if (CatKeyboard.IsChecked == true) categories.Add("keyboard");
            if (CatSystem.IsChecked == true) categories.Add("system");
            if (CatEmail.IsChecked == true) categories.Add("email");
            SelectedCategories = categories;
            PersistCurrentConfig();

            UserConfirmed = true;
            try { DialogResult = true; } catch { }
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            UserConfirmed = false;
            try { DialogResult = false; } catch { }
            Close();
        }

        /// <summary>
        /// Shows the configuration dialog centered on the owner.
        /// Returns null if cancelled, otherwise the config result.
        /// </summary>
        public static OrchestratorConfig? ShowConfigDialog(Window owner)
        {
            var dialog = new AgentModeConfigDialog
            {
                Owner = owner,
                Topmost = owner?.Topmost ?? true,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            var result = dialog.ShowDialog();
            if (result != true || !dialog.UserConfirmed)
                return null;

            return new OrchestratorConfig(dialog.TokenBudget, dialog.Goals, dialog.SelectedCategories);
        }
    }
}
