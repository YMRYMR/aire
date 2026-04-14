using System.Windows;

namespace Aire.UI.MainWindow.Controls
{
    public partial class MainComposerControl : System.Windows.Controls.UserControl
    {
        public MainComposerControl()
        {
            InitializeComponent();
        }

        public System.Windows.Controls.Border MouseSessionBanner => PART_MouseSessionBanner;
        public System.Windows.Controls.TextBlock MouseSessionLabel => PART_MouseSessionLabel;
        public System.Windows.Controls.Button EndSessionButton => PART_EndSessionButton;
        public System.Windows.Controls.StackPanel ImagePreviewPanel => PART_ImagePreviewPanel;
        public System.Windows.Controls.Border ImageThumbnailBorder => PART_ImageThumbnailBorder;
        public System.Windows.Controls.Image AttachedImagePreview => PART_AttachedImagePreview;
        public System.Windows.Controls.Border FileChipBorder => PART_FileChipBorder;
        public System.Windows.Controls.TextBlock AttachedFileNameText => PART_AttachedFileNameText;
        public System.Windows.Controls.TextBlock AttachedFileSizeText => PART_AttachedFileSizeText;
        public System.Windows.Controls.Border LargeFileWarning => PART_LargeFileWarning;
        public System.Windows.Controls.Button RemoveImageButton => PART_RemoveImageButton;
        public System.Windows.Controls.Button StopAiButton => PART_StopAiButton;
        public System.Windows.Controls.TextBox InputTextBox => PART_InputTextBox;
        public System.Windows.Controls.Button MicButton => PART_MicButton;
        public System.Windows.Controls.Button ToolsButton => PART_ToolsButton;
        public System.Windows.Controls.Primitives.ToggleButton AgentModeButton => PART_AgentModeButton;
        public System.Windows.Controls.Border AgentModeBanner => PART_AgentModeBanner;
        public System.Windows.Controls.TextBlock AgentModeStatusText => PART_AgentModeStatusText;
        public System.Windows.Controls.Button StopAgentButton => PART_StopAgentButton;
        public System.Windows.Controls.Border ProgressOverlay => PART_ProgressOverlay;
        public System.Windows.Controls.TextBlock ThinkingText => PART_ThinkingText;

        public event RoutedEventHandler? EndSessionClicked;
        public event System.Windows.Input.MouseButtonEventHandler? FileChipClicked;
        public event RoutedEventHandler? RemoveImageClicked;
        public event RoutedEventHandler? StopAiClicked;
        public event System.Windows.Input.KeyEventHandler? InputPreviewKeyDown;
        public event System.Windows.DragEventHandler? InputPreviewDrop;
        public event System.Windows.DragEventHandler? InputPreviewDragOver;
        public event RoutedEventHandler? MicClicked;
        public event RoutedEventHandler? ToolsClicked;
        public event RoutedEventHandler? AgentModeClicked;
        public event RoutedEventHandler? StopAgentClicked;

        private void EndSessionButton_Click(object sender, RoutedEventArgs e)
            => EndSessionClicked?.Invoke(sender, e);

        private void FileChipBorder_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
            => FileChipClicked?.Invoke(sender, e);

        private void RemoveImageButton_Click(object sender, RoutedEventArgs e)
            => RemoveImageClicked?.Invoke(sender, e);

        private void StopAiButton_Click(object sender, RoutedEventArgs e)
            => StopAiClicked?.Invoke(sender, e);

        private void InputTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
            => InputPreviewKeyDown?.Invoke(sender, e);

        private void InputTextBox_Drop(object sender, System.Windows.DragEventArgs e)
            => InputPreviewDrop?.Invoke(sender, e);

        private void InputTextBox_DragOver(object sender, System.Windows.DragEventArgs e)
            => InputPreviewDragOver?.Invoke(sender, e);

        private void MicButton_Click(object sender, RoutedEventArgs e)
            => MicClicked?.Invoke(sender, e);

        private void ToolsButton_Click(object sender, RoutedEventArgs e)
            => ToolsClicked?.Invoke(sender, e);

        private void AgentModeButton_Click(object sender, RoutedEventArgs e)
            => AgentModeClicked?.Invoke(sender, e);

        private void StopAgentButton_Click(object sender, RoutedEventArgs e)
            => StopAgentClicked?.Invoke(sender, e);
    }
}
