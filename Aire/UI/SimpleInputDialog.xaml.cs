using System.Windows;
using System.Windows.Input;

namespace Aire.UI
{
    public partial class SimpleInputDialog : Window
    {
        public string Value => InputBox.Text;

        public SimpleInputDialog(string prompt, string initialValue = "")
        {
            InitializeComponent();
            PromptText.Text  = prompt;
            InputBox.Text    = initialValue;
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

        private void InputBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter)  { DialogResult = true;  Close(); }
            if (e.Key == Key.Escape) { DialogResult = false; Close(); }
        }
    }
}
