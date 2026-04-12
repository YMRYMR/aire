namespace Aire.UI
{
    public partial class SettingsWindow
    {
        private void WireLocalApiPaneEvents()
        {
            LocalApiAccessPane.ApiAccessEnabledChanged += ApiAccessEnabled_Changed;
            LocalApiAccessPane.ApiPortChanged += ApiPort_Changed;
            LocalApiAccessPane.CopyApiAccessTokenClicked += CopyApiAccessTokenButton_Click;
            LocalApiAccessPane.RegenerateApiAccessTokenClicked += RegenerateApiAccessTokenButton_Click;
        }
    }
}
