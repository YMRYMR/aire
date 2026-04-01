using System.Windows;

namespace Aire.UI.MainWindow.Controls
{
    public partial class SearchPanelControl : System.Windows.Controls.UserControl
    {
        public SearchPanelControl()
        {
            InitializeComponent();
        }

        public System.Windows.Controls.Border SearchPanel => PART_SearchPanel;
        public System.Windows.Controls.TextBox SearchTextBox => PART_SearchTextBox;
        public System.Windows.Controls.TextBlock SearchCountText => PART_SearchCountText;
        public System.Windows.Controls.Button SearchPrevButton => PART_SearchPrevButton;
        public System.Windows.Controls.Button SearchNextButton => PART_SearchNextButton;
        public System.Windows.Controls.Button CloseSearchButton => PART_CloseSearchButton;

        public event System.Windows.Controls.TextChangedEventHandler? SearchTextChanged;
        public event System.Windows.Input.KeyEventHandler? SearchKeyDown;
        public event RoutedEventHandler? SearchPrevClicked;
        public event RoutedEventHandler? SearchNextClicked;
        public event RoutedEventHandler? CloseSearchClicked;

        public void FocusSearchBox()
        {
            PART_SearchTextBox.Focus();
        }

        public void SelectSearchText()
        {
            PART_SearchTextBox.SelectAll();
        }

        private void SearchTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
            => SearchTextChanged?.Invoke(sender, e);

        private void SearchTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
            => SearchKeyDown?.Invoke(sender, e);

        private void SearchPrevButton_Click(object sender, RoutedEventArgs e)
            => SearchPrevClicked?.Invoke(sender, e);

        private void SearchNextButton_Click(object sender, RoutedEventArgs e)
            => SearchNextClicked?.Invoke(sender, e);

        private void CloseSearchButton_Click(object sender, RoutedEventArgs e)
            => CloseSearchClicked?.Invoke(sender, e);
    }
}
