using System.Collections;
using System.Windows;

namespace Aire.UI.MainWindow.Controls
{
    public partial class ConversationSidebarControl : System.Windows.Controls.UserControl
    {
        public ConversationSidebarControl()
        {
            InitializeComponent();
        }

        public IEnumerable? ItemsSource
        {
            get => PART_ConversationList.ItemsSource;
            set => PART_ConversationList.ItemsSource = value;
        }

        public object? SelectedItem
        {
            get => PART_ConversationList.SelectedItem;
            set => PART_ConversationList.SelectedItem = value;
        }

        public string SearchText
        {
            get => PART_SearchTextBox.Text;
            set => PART_SearchTextBox.Text = value;
        }

        public object? NewConversationButtonToolTip
        {
            get => PART_NewConversationButton.ToolTip;
            set => PART_NewConversationButton.ToolTip = value;
        }

        public System.Windows.Controls.TextBox SearchTextBox => PART_SearchTextBox;

        public System.Windows.Controls.ListBox ConversationListBox => PART_ConversationList;

        public event System.Windows.Controls.TextChangedEventHandler? SearchBoxTextChanged;
        public event RoutedEventHandler? NewConversationClicked;
        public event System.Windows.Controls.SelectionChangedEventHandler? ConversationSelectionChanged;
        public event System.Windows.Input.MouseButtonEventHandler? ConversationListRightClick;
        public event System.Windows.Input.MouseButtonEventHandler? ConversationTitleMouseDown;
        public event DependencyPropertyChangedEventHandler? TitleEditorVisibleChanged;
        public event System.Windows.Input.KeyEventHandler? TitleEditorKeyDown;
        public event RoutedEventHandler? TitleEditorLostFocus;
        public event RoutedEventHandler? DeleteConversationClicked;

        public void FocusSearchBox() => PART_SearchTextBox.Focus();

        public void SelectSearchText() => PART_SearchTextBox.SelectAll();

        private void SearchTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
            => SearchBoxTextChanged?.Invoke(sender, e);

        private void NewConversationButton_Click(object sender, RoutedEventArgs e)
            => NewConversationClicked?.Invoke(sender, e);

        private void ConversationList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
            => ConversationSelectionChanged?.Invoke(sender, e);

        private void ConversationList_MouseRightButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
            => ConversationListRightClick?.Invoke(sender, e);

        private void ConversationTitle_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
            => ConversationTitleMouseDown?.Invoke(sender, e);

        private void TitleEditor_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
            => TitleEditorVisibleChanged?.Invoke(sender, e);

        private void TitleEditor_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
            => TitleEditorKeyDown?.Invoke(sender, e);

        private void TitleEditor_LostFocus(object sender, RoutedEventArgs e)
            => TitleEditorLostFocus?.Invoke(sender, e);

        private void DeleteConversationButton_Click(object sender, RoutedEventArgs e)
            => DeleteConversationClicked?.Invoke(sender, e);
    }
}
