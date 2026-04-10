using System;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Input;
using Clipboard = System.Windows.Clipboard;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace Aire
{
    public partial class MainWindow
    {
        private void ToggleSearchPanel()
        {
            if (SearchPanel.Visibility == Visibility.Visible)
                CloseSearch();
            else
                OpenSearch();
        }

        internal void OpenSearch()
        {
            SearchPanel.Visibility = Visibility.Visible;
            SearchTextBox.Dispatcher.BeginInvoke(new Action(() =>
            {
                SearchPanelControl.FocusSearchBox();
                SearchPanelControl.SelectSearchText();
            }), System.Windows.Threading.DispatcherPriority.Input);
        }

        internal void CloseSearch()
        {
            if (SearchPanelControl is null)
                return;

            SearchPanel.Visibility = Visibility.Collapsed;
            SearchTextBox.Text = string.Empty;
            ClearSearchHighlights();
        }

        private void ClearSearchHighlights()
        {
            foreach (var msg in Messages)
                msg.IsSearchMatch = false;
            _searchMatchIndices.Clear();
            _searchCurrentIndex = -1;
            SearchCountText.Text = string.Empty;
        }

        internal void PerformSearch(string query)
        {
            ClearSearchHighlights();
            if (string.IsNullOrWhiteSpace(query)) return;

            for (int i = 0; i < Messages.Count; i++)
            {
                var msg = Messages[i];
                if (msg.Sender is "Date" or "System") continue;
                if (msg.Text.Contains(query, StringComparison.OrdinalIgnoreCase))
                {
                    msg.IsSearchMatch = true;
                    _searchMatchIndices.Add(i);
                }
            }

            if (_searchMatchIndices.Count > 0)
            {
                _searchCurrentIndex = 0;
                ScrollToMessageIndex(_searchMatchIndices[0]);
            }

            UpdateSearchCount();
        }

        private void ScrollToMessageIndex(int index)
        {
            // ItemsControl doesn't virtualize by default, so all containers exist.
            var container = MessagesItemsControl.ItemContainerGenerator
                .ContainerFromIndex(index) as FrameworkElement;
            container?.BringIntoView();
        }

        private void UpdateSearchCount()
        {
            if (_searchMatchIndices.Count == 0)
                SearchCountText.Text = string.IsNullOrWhiteSpace(SearchTextBox.Text) ? string.Empty : "0";
            else
                SearchCountText.Text = $"{_searchCurrentIndex + 1}/{_searchMatchIndices.Count}";
        }

        private void SearchTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            PerformSearch(SearchTextBox.Text);
        }

        private void SearchTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (Keyboard.Modifiers == ModifierKeys.Shift) NavigateSearchPrev();
                else NavigateSearchNext();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                CloseSearch();
                e.Handled = true;
            }
        }

        private void SearchNext_Click(object sender, RoutedEventArgs e) => NavigateSearchNext();
        private void SearchPrev_Click(object sender, RoutedEventArgs e) => NavigateSearchPrev();
        private void CloseSearch_Click(object sender, RoutedEventArgs e) => CloseSearch();
        private void SearchButton_Click(object sender, RoutedEventArgs e) => OpenSearch();
        private void FindMenuItem_Click(object sender, RoutedEventArgs e) => OpenSearch();
        private void FindCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            OpenSearch();
            e.Handled = true;
        }

        internal void NavigateSearchNext()
        {
            if (_searchMatchIndices.Count == 0) return;
            _searchCurrentIndex = (_searchCurrentIndex + 1) % _searchMatchIndices.Count;
            ScrollToMessageIndex(_searchMatchIndices[_searchCurrentIndex]);
            UpdateSearchCount();
        }

        internal void NavigateSearchPrev()
        {
            if (_searchMatchIndices.Count == 0) return;
            _searchCurrentIndex = (_searchCurrentIndex - 1 + _searchMatchIndices.Count) % _searchMatchIndices.Count;
            ScrollToMessageIndex(_searchMatchIndices[_searchCurrentIndex]);
            UpdateSearchCount();
        }

        private void SaveChatText_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog
            {
                Filter = "Text file (*.txt)|*.txt",
                FileName = $"chat_{DateTime.Now:yyyy-MM-dd}",
                Title = "Save chat as text"
            };
            if (dlg.ShowDialog() != true) return;

            try
            {
                File.WriteAllText(dlg.FileName, BuildChatText());
            }
            catch
            {
                UI.ConfirmationDialog.ShowAlert(this, "Error", "Failed to save.");
            }
        }

        private void CopyChatText_Click(object sender, RoutedEventArgs e)
        {
            try { Clipboard.SetText(BuildChatText()); }
            catch { /* clipboard operations fail silently if the window loses focus during the call */ }
        }

        internal string BuildChatText()
        {
            var sb = new StringBuilder();
            foreach (var msg in Messages)
            {
                if (msg.Sender == "Date")
                {
                    sb.AppendLine();
                    sb.AppendLine($"── {msg.Text} ──");
                    continue;
                }

                if (msg.Sender == "System") continue;
                sb.AppendLine($"[{msg.Timestamp}] {msg.Sender}: {msg.Text}");
            }

            return sb.ToString().Trim();
        }
    }
}
