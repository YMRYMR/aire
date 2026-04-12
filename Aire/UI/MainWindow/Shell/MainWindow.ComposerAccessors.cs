namespace Aire
{
    public partial class MainWindow
    {
        private System.Windows.Controls.Border MouseSessionBanner => ComposerControl.MouseSessionBanner;
        private System.Windows.Controls.TextBlock MouseSessionLabel => ComposerControl.MouseSessionLabel;
        private System.Windows.Controls.Button EndSessionButton => ComposerControl.EndSessionButton;
        private System.Windows.Controls.StackPanel ImagePreviewPanel => ComposerControl.ImagePreviewPanel;
        private System.Windows.Controls.Border ImageThumbnailBorder => ComposerControl.ImageThumbnailBorder;
        private System.Windows.Controls.Image AttachedImagePreview => ComposerControl.AttachedImagePreview;
        private System.Windows.Controls.Border FileChipBorder => ComposerControl.FileChipBorder;
        private System.Windows.Controls.TextBlock AttachedFileNameText => ComposerControl.AttachedFileNameText;
        private System.Windows.Controls.TextBlock AttachedFileSizeText => ComposerControl.AttachedFileSizeText;
        private System.Windows.Controls.Border LargeFileWarning => ComposerControl.LargeFileWarning;
        private System.Windows.Controls.Button RemoveImageButton => ComposerControl.RemoveImageButton;
        private System.Windows.Controls.Button StopAiButton => ComposerControl.StopAiButton;
        private System.Windows.Controls.TextBox InputTextBox => ComposerControl.InputTextBox;
        private System.Windows.Controls.Button MicButton => ComposerControl.MicButton;
        private System.Windows.Controls.Button ToolsButton => ComposerControl.ToolsButton;
        private System.Windows.Controls.Primitives.ToggleButton AgentModeButton => ComposerControl.AgentModeButton;
        private System.Windows.Controls.Border ProgressOverlay => ComposerControl.ProgressOverlay;
        private System.Windows.Controls.TextBlock ThinkingText => ComposerControl.ThinkingText;
    }
}
