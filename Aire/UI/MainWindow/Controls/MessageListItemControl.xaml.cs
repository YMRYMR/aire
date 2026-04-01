using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ChatMessage = Aire.UI.MainWindow.Models.ChatMessage;

namespace Aire.UI.MainWindow.Controls
{
    public partial class MessageListItemControl : System.Windows.Controls.UserControl
    {
        private ChatMessage? _message;

        public event MouseButtonEventHandler? ChatImageMouseLeftButtonUp;
        public event RoutedEventHandler? AnswerButtonClick;
        public event RoutedEventHandler? ApproveToolCallClick;
        public event RoutedEventHandler? DenyToolCallClick;

        public MessageListItemControl()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        private void AttachedImageElement_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) =>
            ChatImageMouseLeftButtonUp?.Invoke(sender, e);

        private void ScreenshotImageElement_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) =>
            ChatImageMouseLeftButtonUp?.Invoke(sender, e);

        private void AnswerButton_Click(object sender, RoutedEventArgs e) =>
            AnswerButtonClick?.Invoke(sender, e);

        private void ApproveToolButton_Click(object sender, RoutedEventArgs e) =>
            ApproveToolCallClick?.Invoke(sender, e);

        private void DenyToolButton_Click(object sender, RoutedEventArgs e) =>
            DenyToolCallClick?.Invoke(sender, e);

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (_message != null)
            {
                _message.PropertyChanged -= OnMessagePropertyChanged;
            }

            _message = e.NewValue as ChatMessage;
            if (_message != null)
            {
                _message.PropertyChanged += OnMessagePropertyChanged;
            }

            UpdateVisualState();
        }

        private void OnMessagePropertyChanged(object? sender, PropertyChangedEventArgs e) => UpdateVisualState();

        private void UpdateVisualState()
        {
            var msg = _message;
            if (msg == null)
            {
                return;
            }

            DateSeparator.Visibility = msg.Sender == "Date" ? Visibility.Visible : Visibility.Collapsed;
            var isSystem = msg.Sender == "System";
            var isDate = msg.Sender == "Date";

            MessageRow.Visibility = (isSystem || isDate) ? Visibility.Collapsed : Visibility.Visible;
            SystemText.Visibility = isSystem ? Visibility.Visible : Visibility.Collapsed;
            SystemText.Foreground = TryFindResource("StatusTextBrush") as System.Windows.Media.Brush ?? msg.SenderForeground;

            MessageRow.HorizontalAlignment = msg.Sender == "You"
                ? System.Windows.HorizontalAlignment.Right
                : System.Windows.HorizontalAlignment.Left;
            MessageBorder.Background = msg.Sender == "You"
                ? (TryFindResource("UserMessageBrush") as System.Windows.Media.Brush ?? msg.BackgroundBrush)
                : (TryFindResource("AssistantMessageBrush") as System.Windows.Media.Brush ?? msg.BackgroundBrush);
            MessageBorder.CornerRadius = msg.Sender == "You"
                ? new CornerRadius(14, 14, 4, 14)
                : new CornerRadius(14, 14, 14, 4);
            MessageText.Foreground = msg.Sender == "You"
                ? (TryFindResource("UserMessageTextBrush") as System.Windows.Media.Brush ?? MessageText.Foreground)
                : (TryFindResource("AssistantMessageTextBrush") as System.Windows.Media.Brush ?? MessageText.Foreground);

            MessageText.Visibility = string.IsNullOrEmpty(msg.Text) ? Visibility.Collapsed : Visibility.Visible;
            ToolApprovalPanel.Visibility = msg.IsApprovalPending ? Visibility.Visible : Visibility.Collapsed;
            ToolCallDescriptionText.Visibility = msg.IsApprovalPending ? Visibility.Visible : Visibility.Collapsed;
            ToolCallStatusText.Visibility = msg.HasToolCallStatus ? Visibility.Visible : Visibility.Collapsed;
            AttachedImageElement.Visibility = msg.HasAttachedImage ? Visibility.Visible : Visibility.Collapsed;
            ScreenshotImageElement.Visibility = msg.HasScreenshot ? Visibility.Visible : Visibility.Collapsed;
            TodoListPanel.Visibility = msg.HasTodoItems ? Visibility.Visible : Visibility.Collapsed;
            FollowUpPanel.Visibility = msg.HasFollowUpQuestion ? Visibility.Visible : Visibility.Collapsed;
            AnswerButtonsPanel.Visibility = msg.ShowAnswerButtons ? Visibility.Visible : Visibility.Collapsed;

            MessageBorder.BorderThickness = msg.IsSearchMatch ? new Thickness(1) : new Thickness(0);
            MessageBorder.BorderBrush = msg.IsSearchMatch
                ? new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#5A6070"))
                : null;
        }
    }
}
