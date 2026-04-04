using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Aire.Data;
using Aire.Services;
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
            Loaded += (_, _) => ApplyLocalization();
            Unloaded += (_, _) => LocalizationService.LanguageChanged -= OnLanguageChanged;
            LocalizationService.LanguageChanged += OnLanguageChanged;
        }

        private void OnLanguageChanged()
            => Dispatcher.Invoke(ApplyLocalization);

        private void ApplyLocalization()
        {
            var imageTooltip = LocalizationService.S("tooltip.openImageViewer", "Click to open in image viewer");
            AttachedImageElement.ToolTip = imageTooltip;
            ScreenshotImageElement.ToolTip = imageTooltip;
            ApproveToolButton.ToolTip = LocalizationService.S("tooltip.allow", "Allow");
            DenyToolButton.ToolTip = LocalizationService.S("tooltip.deny", "Deny");
        }

        private void AttachedImageElement_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) =>
            ChatImageMouseLeftButtonUp?.Invoke(sender, e);

        private void ScreenshotImageElement_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) =>
            ChatImageMouseLeftButtonUp?.Invoke(sender, e);

        private void InlineImage_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) =>
            ChatImageMouseLeftButtonUp?.Invoke(sender, e);

        private void FileAttachmentButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is MessageAttachment attachment)
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(attachment.FilePath) || !File.Exists(attachment.FilePath))
                    {
                        var owner = Window.GetWindow(this);
                        if (owner != null)
                            UI.ConfirmationDialog.ShowAlert(owner, "Attachment missing", $"The file '{attachment.DisplayName}' is no longer available on disk.");
                        return;
                    }

                    if (RequiresSafetyWarning(attachment.FilePath))
                    {
                        var owner = Window.GetWindow(this);
                        var result = owner == null
                            ? MessageBoxResult.Yes
                            : System.Windows.MessageBox.Show(owner,
                                $"The file '{attachment.DisplayName}' may be executable or script content. Open it anyway?",
                                "Open Attachment",
                                MessageBoxButton.YesNo,
                                MessageBoxImage.Warning);

                        if (result != MessageBoxResult.Yes)
                            return;
                    }

                    Process.Start(new ProcessStartInfo
                    {
                        FileName = attachment.FilePath,
                        UseShellExecute = true
                    });
                }
                catch
                {
                    AppLogger.Warn("MessageListItemControl.FileAttachmentButton_Click", $"Failed to open attachment '{attachment.FilePath}'");
                }
            }
        }

        private static bool RequiresSafetyWarning(string filePath)
            => Path.GetExtension(filePath).ToLowerInvariant() is ".exe" or ".bat" or ".cmd" or ".ps1" or ".psm1" or ".psd1" or ".vbs" or ".js" or ".jse" or ".wsf" or ".wsh" or ".msi" or ".reg" or ".scr" or ".com" or ".pif" or ".lnk" or ".hta" or ".cpl" or ".jar" or ".sh" or ".py" or ".rb";

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
            InlineImagesPanel.Visibility = msg.HasInlineImages ? Visibility.Visible : Visibility.Collapsed;
            AttachedImageElement.Visibility = msg.HasAttachedImage ? Visibility.Visible : Visibility.Collapsed;
            FileAttachmentsPanel.Visibility = msg.HasFileAttachments ? Visibility.Visible : Visibility.Collapsed;
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
