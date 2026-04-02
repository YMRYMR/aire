namespace Aire.UI
{
    public partial class SettingsWindow
    {
        private void WireAutoAcceptPaneEvents()
        {
            AutoAcceptPane.AutoAcceptEnabledChanged += AutoAcceptEnabled_Changed;
            AutoAcceptPane.ApplyProfileClicked += ApplyAutoAcceptProfile_Click;
            AutoAcceptPane.SaveProfileClicked += SaveAutoAcceptProfile_Click;
            AutoAcceptPane.DeleteProfileClicked += DeleteAutoAcceptProfile_Click;
            AutoAcceptPane.ProfileSelectionChanged += AutoAcceptProfile_SelectionChanged;
        }
    }
}
