using System.Windows;
using System.Windows.Input;
using Aire.Services;
using WpfKeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace Aire.UI
{
    public partial class SimpleInputDialog : Window
    {
        public string Value => InputBox.Text;

        public SimpleInputDialog(string prompt, string initialValue = "")
        {
            InitializeComponent();
            FlowDirection = LocalizationService.IsRightToLeftLanguage(LocalizationService.CurrentCode)
                ? System.Windows.FlowDirection.RightToLeft
                : System.Windows.FlowDirection.LeftToRight;
            PromptText.Text  = prompt;
            InputBox.Text    = initialValue;
            CancelButton.Content = LocalizationService.S("input.cancel", "Cancel");
            OKButton.Content = LocalizationService.S("input.ok", "OK");
            InputBox.Focus();
            InputBox.SelectAll();
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void InputBox_KeyDown(object sender, WpfKeyEventArgs e)
        {
            if (e.Key == Key.Enter) { DialogResult = true; Close(); }
            if (e.Key == Key.Escape) { DialogResult = false; Close(); }
        }
    }
}
