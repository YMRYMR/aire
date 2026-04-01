namespace Aire
{
    public partial class MainWindow
    {
        private System.Windows.Controls.Border SearchPanel => SearchPanelControl.SearchPanel;
        private System.Windows.Controls.TextBox SearchTextBox => SearchPanelControl.SearchTextBox;
        private System.Windows.Controls.TextBlock SearchCountText => SearchPanelControl.SearchCountText;
        private System.Windows.Controls.Button SearchPrevButton => SearchPanelControl.SearchPrevButton;
        private System.Windows.Controls.Button SearchNextButton => SearchPanelControl.SearchNextButton;
        private System.Windows.Controls.Button CloseSearchButton => SearchPanelControl.CloseSearchButton;
    }
}
