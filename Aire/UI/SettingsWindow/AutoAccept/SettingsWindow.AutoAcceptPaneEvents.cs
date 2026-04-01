namespace Aire.UI
{
    public partial class SettingsWindow
    {
        private void WireAutoAcceptPaneEvents()
        {
            AutoAcceptPane.AutoAcceptEnabledChanged += AutoAcceptEnabled_Changed;
        }
    }
}
