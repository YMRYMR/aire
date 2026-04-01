using System.Windows;

namespace Aire.UI
{
    public partial class ConfirmationDialog : Window
    {
        public bool Result { get; private set; } = false;

        public ConfirmationDialog()
        {
            InitializeComponent();
            FontSize = Services.AppearanceService.FontSize;
            Services.AppearanceService.AppearanceChanged += OnThemeChanged;
            Closed += (_, _) => Services.AppearanceService.AppearanceChanged -= OnThemeChanged;
        }

        private void OnThemeChanged() => Dispatcher.Invoke(() => FontSize = Services.AppearanceService.FontSize);

        private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed) DragMove();
        }

        internal void YesButton_Click(object sender, RoutedEventArgs e)
        {
            Result = true;
            try { DialogResult = true; } catch { }
            Close();
        }

        internal void NoButton_Click(object sender, RoutedEventArgs e)
        {
            Result = false;
            try { DialogResult = false; } catch { }
            Close();
        }

        public static bool ShowDialog(Window owner, System.Windows.Point position,
            string? title = null, string? message = null)
        {
            var dialog = new ConfirmationDialog();
            dialog.Owner = owner;
            dialog.Topmost = owner?.Topmost ?? true;
            dialog.Left = position.X;
            dialog.Top = position.Y;

            if (title != null) dialog.TitleText.Text = title;
            if (message != null) dialog.MessageText.Text = message;

            return dialog.ShowDialog() == true;
        }

        public static bool ShowCentered(Window owner,
            string? title = null, string? message = null)
        {
            var dialog = new ConfirmationDialog();
            dialog.Owner = owner;
            dialog.Topmost = owner?.Topmost ?? true;
            dialog.WindowStartupLocation = System.Windows.WindowStartupLocation.CenterOwner;

            if (title != null) dialog.TitleText.Text = title;
            if (message != null) dialog.MessageText.Text = message;

            return dialog.ShowDialog() == true;
        }

        /// <summary>
        /// Shows an OK-only alert (no Yes/No choice). Does not return a value.
        /// </summary>
        public static void ShowAlert(Window owner, string? title = null, string? message = null)
        {
            var dialog = new ConfirmationDialog();
            dialog.Owner = owner;
            dialog.Topmost = owner?.Topmost ?? true;
            dialog.WindowStartupLocation = System.Windows.WindowStartupLocation.CenterOwner;

            if (title != null) dialog.TitleText.Text = title;
            if (message != null) dialog.MessageText.Text = message;

            dialog.NoButton.Visibility = Visibility.Collapsed;
            dialog.YesButton.Content   = "OK";

            dialog.ShowDialog();
        }
    }
}
