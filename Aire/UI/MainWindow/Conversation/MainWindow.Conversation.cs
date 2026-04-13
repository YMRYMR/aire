using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Aire.Providers;
using Aire.Services;
using ChatMessage = Aire.UI.MainWindow.Models.ChatMessage;

namespace Aire
{
    public partial class MainWindow
    {
        private void AnswerButton_Click(object sender, RoutedEventArgs e)
        {
            var btn = e.OriginalSource as System.Windows.Controls.Button
                ?? sender as System.Windows.Controls.Button;
            if (btn == null) return;
            var answer = btn.Content?.ToString() ?? string.Empty;

            DependencyObject el = btn;
            while (el != null)
            {
                if (el is FrameworkElement { DataContext: ChatMessage msg } && msg.AnswerTcs != null)
                {
                    msg.AnswerSubmitted = true;
                    msg.AnswerTcs.TrySetResult(answer);
                    return;
                }
                el = VisualTreeHelper.GetParent(el);
            }
        }

        private async Task LoadConversationMessages(int conversationId, bool syncProviderSelection = true)
        {
            if (syncProviderSelection)
                await ConversationFlow.SyncConversationSelectionStateAsync(conversationId);

            await ConversationFlow.LoadConversationMessagesAsync(conversationId);
        }

        private async void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            var clearButton = (System.Windows.Controls.Button)sender;
            var buttonPosition = clearButton.PointToScreen(new System.Windows.Point(0, clearButton.ActualHeight));
            if (Aire.UI.ConfirmationDialog.ShowDialog(this, buttonPosition))
                await DoClearConversationAsync();
        }

        private async void ClearChatMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (Aire.UI.ConfirmationDialog.ShowCentered(this))
                await DoClearConversationAsync();
        }

        private async void BranchFromMessage_Click(object sender, RoutedEventArgs e)
        {
            // Walk up the visual tree to find the MessageListItemControl and its ChatMessage DataContext.
            if (sender is not FrameworkElement fe) return;
            DependencyObject el = fe;
            ChatMessage? msg = null;
            while (el != null)
            {
                if (el is FrameworkElement { DataContext: ChatMessage m })
                {
                    msg = m;
                    break;
                }
                el = VisualTreeHelper.GetParent(el);
            }

            if (msg == null || msg.DbMessageId <= 0) return;

            try
            {
                await ConversationFlow.BranchFromMessageAsync(msg.DbMessageId);
            }
            catch (Exception ex)
            {
                AppLogger.Warn("BranchFromMessage", "Failed to branch conversation", ex);
                await AddErrorMessageAsync(
                    LocalizationService.S("branch.error", "Failed to branch conversation. Please try again."));
            }
        }

        private async Task DoClearConversationAsync()
            => await ConversationFlow.ClearConversationAsync();

        private void InputTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.V && Keyboard.Modifiers == ModifierKeys.Control)
            {
                if (System.Windows.Clipboard.ContainsImage() &&
                    _currentProvider?.Has(ProviderCapabilities.ImageInput) == true)
                {
                    var bitmapSource = System.Windows.Clipboard.GetImage();
                    if (bitmapSource != null)
                    {
                        AttachImageFromClipboard(bitmapSource);
                        e.Handled = true;
                        return;
                    }
                }
            }

            if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None)
            {
                e.Handled = true;
                if (string.IsNullOrEmpty(InputTextBox.Text))
                {
                    var pending = GetPendingApproval();
                    if (pending != null)
                    {
                        pending.ApprovalTcs!.TrySetResult(true);
                        return;
                    }
                }
                QueueSendMessage();
            }
        }

        private void MainWindow_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            // Ctrl+K → command palette
            if (e.Key == Key.K && e.KeyboardDevice.Modifiers == ModifierKeys.Control)
            {
                ToggleCommandPalette();
                e.Handled = true;
                return;
            }

            // Close command palette on Escape
            if (e.Key == Key.Escape && CommandPalettePopup.IsOpen)
            {
                CommandPalettePopup.IsOpen = false;
                e.Handled = true;
                return;
            }

            // Global tool approval shortcuts: Enter approves, Escape denies.
            if (!InputTextBox.IsKeyboardFocused || string.IsNullOrEmpty(InputTextBox.Text))
            {
                if (e.Key == Key.Enter)
                {
                    var pending = GetPendingApproval();
                    if (pending != null)
                    {
                        pending.ApprovalTcs?.TrySetResult(true);
                        e.Handled = true;
                        return;
                    }
                }

                if (e.Key == Key.Escape)
                {
                    var pending = GetPendingApproval();
                    if (pending != null)
                    {
                        pending.ApprovalTcs?.TrySetResult(false);
                        e.Handled = true;
                        return;
                    }
                }
            }

            if (InputTextBox.IsKeyboardFocused)
            {
                if (e.Key == Key.Up && _inputHistory.Count > 0)
                {
                    var caretLine = InputTextBox.GetLineIndexFromCharacterIndex(InputTextBox.CaretIndex);
                    if (caretLine != 0) return;

                    if (_historyIndex == -1)
                    {
                        _inputDraft = InputTextBox.Text;
                        _historyIndex = _inputHistory.Count - 1;
                    }
                    else if (_historyIndex > 0)
                    {
                        _historyIndex--;
                    }
                    InputTextBox.Text = _inputHistory[_historyIndex];
                    InputTextBox.CaretIndex = InputTextBox.Text.Length;
                    e.Handled = true;
                    return;
                }

                if (e.Key == Key.Down && _historyIndex != -1)
                {
                    var caretLine = InputTextBox.GetLineIndexFromCharacterIndex(InputTextBox.CaretIndex);
                    if (caretLine != InputTextBox.LineCount - 1) return;

                    if (_historyIndex < _inputHistory.Count - 1)
                    {
                        _historyIndex++;
                        InputTextBox.Text = _inputHistory[_historyIndex];
                    }
                    else
                    {
                        _historyIndex = -1;
                        InputTextBox.Text = _inputDraft;
                    }
                    InputTextBox.CaretIndex = InputTextBox.Text.Length;
                    e.Handled = true;
                    return;
                }
            }

            if (e.Key == Key.Escape)
            {
                if (string.IsNullOrEmpty(InputTextBox.Text))
                {
                    var pending = GetPendingApproval();
                    if (pending != null)
                    {
                        pending.ApprovalTcs!.TrySetResult(false);
                        e.Handled = true;
                        return;
                    }
                }
                if (SearchPanel.Visibility == Visibility.Visible)
                {
                    CloseSearch();
                    e.Handled = true;
                }
            }
            else if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
            {
                ToggleSearchPanel();
                e.Handled = true;
            }
        }

        private void InputTextBox_DragOver(object sender, System.Windows.DragEventArgs e)
        {
            if (_currentProvider?.Has(ProviderCapabilities.ImageInput) == true &&
                e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(System.Windows.DataFormats.FileDrop);
                if (files != null && files.Length > 0)
                {
                    var ext = Path.GetExtension(files[0]).ToLowerInvariant();
                    if (ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".bmp" || ext == ".gif")
                    {
                        e.Effects = System.Windows.DragDropEffects.Copy;
                        e.Handled = true;
                        return;
                    }
                }
            }
            e.Effects = System.Windows.DragDropEffects.None;
            e.Handled = true;
        }
    }
}
