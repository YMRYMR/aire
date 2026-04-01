using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Aire.Data;
using Aire.Services;
using ChatMessage = Aire.UI.MainWindow.Models.ChatMessage;

namespace Aire
{
    public partial class MainWindow
    {
        private bool _sidebarOpen;
        private double _sidebarWidth = 220;
        private System.Windows.Threading.DispatcherTimer? _searchDebounce;

        private void InitSidebarState()
        {
            _sidebarOpen = AppState.GetSidebarOpen();
            ApplySidebarWidth(_sidebarOpen ? _sidebarWidth : 0);
            if (_sidebarOpen)
                _ = RefreshSidebarAsync();
        }

        private void SidebarToggleButton_Click(object sender, RoutedEventArgs e)
        {
            if (_sidebarOpen)
            {
                _sidebarWidth = SidebarColumn.Width.Value > 0
                    ? SidebarColumn.Width.Value
                    : _sidebarWidth;
            }

            _sidebarOpen = !_sidebarOpen;
            if (_sidebarOpen) AppState.OpenSidebar(); else AppState.CloseSidebar();
            ApplySidebarWidth(_sidebarOpen ? _sidebarWidth : 0);
            if (_sidebarOpen)
                _ = RefreshSidebarAsync();
        }

        private void ApplySidebarWidth(double width)
        {
            bool open = width > 0;
            SidebarColumn.Width = new GridLength(open ? width : 0);
            SplitterColumn.Width = new GridLength(open ? 4 : 0);
            SidebarSplitter.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
        }

        private async Task RefreshSidebarAsync(string? search = null)
        {
            var items = await _conversationApplicationService.ListConversationsAsync(search);
            ConversationSidebar.ItemsSource = items;
            if (_currentConversationId.HasValue)
            {
                foreach (ConversationSummary item in ConversationSidebar.ConversationListBox.Items)
                {
                    if (item.Id == _currentConversationId.Value)
                    {
                        ConversationSidebar.SelectedItem = item;
                        break;
                    }
                }
            }
        }

        private void ConversationSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _searchDebounce?.Stop();
            _searchDebounce = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(300)
            };
            _searchDebounce.Tick += async (s, _) =>
            {
                _searchDebounce.Stop();
                await RefreshSidebarAsync(ConversationSidebar.SearchText.Trim());
            };
            _searchDebounce.Start();
        }

        private async void ConversationListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ConversationSidebar.SelectedItem is not ConversationSummary summary) return;
            if (summary.Id == _currentConversationId) return;
            _currentConversationId = summary.Id;
            await LoadConversationMessages(summary.Id);
        }

        private async void NewChatButton_Click(object sender, RoutedEventArgs e)
        {
            var sel = ProviderComboBox.SelectedItem as Provider;
            if (sel == null) return;
            await ConversationFlow.CreateConversationAsync(sel, "New Chat", $"New conversation started with {sel.Name}.");
            await RefreshSidebarAsync();
        }

        private void ConversationListBox_RightClick(object sender, MouseButtonEventArgs e)
        {
            var element = e.OriginalSource as DependencyObject;
            while (element != null && element is not ListBoxItem)
                element = VisualTreeHelper.GetParent(element);

            if (element is ListBoxItem item)
                ConversationSidebar.SelectedItem = item.Content;

            if (ConversationSidebar.SelectedItem is not ConversationSummary)
                return;

            var menu = new System.Windows.Controls.ContextMenu();
            var rename = new System.Windows.Controls.MenuItem { Header = "Rename" };
            var delete = new System.Windows.Controls.MenuItem { Header = "Delete" };
            var sep = new Separator();
            var deleteAll = new System.Windows.Controls.MenuItem { Header = "Delete all conversations" };
            rename.Click += RenameConversation_Click;
            delete.Click += DeleteConversation_Click;
            deleteAll.Click += DeleteAllConversations_Click;
            menu.Items.Add(rename);
            menu.Items.Add(delete);
            menu.Items.Add(sep);
            menu.Items.Add(deleteAll);
            menu.PlacementTarget = ConversationSidebar.ConversationListBox;
            menu.IsOpen = true;
            e.Handled = true;
        }

        private void RenameConversation_Click(object sender, RoutedEventArgs e)
        {
            if (ConversationSidebar.SelectedItem is not ConversationSummary summary) return;
            summary.EditingTitle = summary.Title;
            summary.IsEditing = true;
        }

        private void ConversationTitle_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount < 2) return;
            if ((sender as FrameworkElement)?.DataContext is not ConversationSummary summary) return;
            e.Handled = true;
            summary.EditingTitle = summary.Title;
            summary.IsEditing = true;
        }

        private void TitleEditor_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is not System.Windows.Controls.TextBox tb || !(bool)e.NewValue) return;
            tb.Dispatcher.BeginInvoke(() =>
            {
                tb.Focus();
                tb.SelectAll();
            }, System.Windows.Threading.DispatcherPriority.Input);
        }

        private async void TitleEditor_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (sender is not System.Windows.Controls.TextBox tb) return;
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                await CommitRenameAsync(tb);
            }
            else if (e.Key == Key.Escape)
            {
                e.Handled = true;
                CancelRename(tb);
            }
        }

        private async void TitleEditor_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.TextBox tb)
                await CommitRenameAsync(tb);
        }

        private async Task CommitRenameAsync(System.Windows.Controls.TextBox tb)
        {
            if (tb.DataContext is not ConversationSummary summary) return;
            if (!summary.IsEditing) return;
            summary.IsEditing = false;
            var newTitle = summary.EditingTitle.Trim();
            if (!string.IsNullOrWhiteSpace(newTitle) && newTitle != summary.Title)
            {
                await _conversationApplicationService.RenameConversationAsync(summary.Id, newTitle);
                await RefreshSidebarAsync();
            }
        }

        private static void CancelRename(System.Windows.Controls.TextBox tb)
        {
            if (tb.DataContext is ConversationSummary summary)
                summary.IsEditing = false;
        }

        private async void DeleteConversation_Click(object sender, RoutedEventArgs e)
        {
            var summary = (sender as FrameworkElement)?.Tag as ConversationSummary
                       ?? ConversationSidebar.SelectedItem as ConversationSummary;
            if (summary == null) return;
            if (!UI.ConfirmationDialog.ShowCentered(this,
                    "Delete conversation",
                    $"Delete \"{summary.Title}\"? This cannot be undone.")) return;

            ConversationSidebar.SelectedItem = summary;

            await _conversationApplicationService.DeleteConversationAsync(summary.Id);

            if (_currentConversationId == summary.Id)
            {
                _currentConversationId = null;
                _conversationHistory.Clear();
                Messages.Clear();
            }

            await RefreshSidebarAsync();
        }

        private async void DeleteAllConversations_Click(object sender, RoutedEventArgs e)
        {
            if (!UI.ConfirmationDialog.ShowCentered(this,
                    "Delete all conversations",
                    "Permanently delete every conversation and all messages? This cannot be undone.")) return;

            await _conversationApplicationService.DeleteAllConversationsAsync();

            _currentConversationId = null;
            _conversationHistory.Clear();
            Messages.Clear();
            await RefreshSidebarAsync();
        }
    }
}
