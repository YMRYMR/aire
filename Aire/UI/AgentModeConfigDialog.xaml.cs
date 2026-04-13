using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace Aire.UI
{
    public partial class AgentModeConfigDialog : Window
    {
        /// <summary>The user-selected token budget (0 = unlimited).</summary>
        public int TokenBudget { get; private set; }

        /// <summary>The user-selected tool categories (empty = all).</summary>
        public HashSet<string> SelectedCategories { get; private set; } = [];

        public bool UserConfirmed { get; private set; }

        public AgentModeConfigDialog()
        {
            InitializeComponent();
            InitializeThemeAndLanguage();
        }

        private void OnThemeChanged() => Dispatcher.Invoke(() => FontSize = Services.AppearanceService.FontSize);

        private void InitializeThemeAndLanguage()
        {
            FontSize = Services.AppearanceService.FontSize;
            FlowDirection = Services.LocalizationService.IsRightToLeftLanguage(Services.LocalizationService.CurrentCode)
                ? System.Windows.FlowDirection.RightToLeft
                : System.Windows.FlowDirection.LeftToRight;
            Services.AppearanceService.AppearanceChanged += OnThemeChanged;
            Closed += (_, _) => Services.AppearanceService.AppearanceChanged -= OnThemeChanged;
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed) DragMove();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            BudgetTextBox.Focus();
            BudgetTextBox.SelectAll();
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
            var categories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (CatFile.IsChecked == true) categories.Add("file");
            if (CatSystem.IsChecked == true) categories.Add("system");
            if (CatWeb.IsChecked == true) categories.Add("web");
            if (CatScreenshot.IsChecked == true) categories.Add("screenshot");
            SelectedCategories = categories;

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
        /// Shows the agent mode config dialog centered on the owner.
        /// Returns null if cancelled, otherwise the config result.
        /// </summary>
        public static (int budget, HashSet<string> categories)? ShowConfigDialog(Window owner)
        {
            var dialog = new AgentModeConfigDialog();
            dialog.Owner = owner;
            dialog.Topmost = owner?.Topmost ?? true;
            dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;

            var result = dialog.ShowDialog();
            if (result != true || !dialog.UserConfirmed)
                return null;

            return (dialog.TokenBudget, dialog.SelectedCategories);
        }
    }
}
