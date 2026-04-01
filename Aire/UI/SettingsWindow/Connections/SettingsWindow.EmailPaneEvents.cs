namespace Aire.UI
{
    public partial class SettingsWindow
    {
        private void WireEmailPaneEvents()
        {
            EmailConnectionsPane.AddGmailClicked += AddGmailBtn_Click;
            EmailConnectionsPane.AddOutlookClicked += AddOutlookBtn_Click;
            EmailConnectionsPane.AddCustomEmailClicked += AddCustomEmailBtn_Click;
            EmailConnectionsPane.EmailAccountsSelectionChanged += EmailAccountsList_SelectionChanged;
            EmailConnectionsPane.TestEmailClicked += TestEmailBtn_Click;
            EmailConnectionsPane.DeleteEmailClicked += DeleteEmailBtn_Click;
            EmailConnectionsPane.EmailProviderSelectionChanged += EmailProviderCombo_SelectionChanged;
            EmailConnectionsPane.SignInWithGoogleClicked += SignInWithGoogleBtn_Click;
            EmailConnectionsPane.SaveEmailClicked += SaveEmailBtn_Click;
            EmailConnectionsPane.CancelEmailClicked += CancelEmailBtn_Click;
            EmailConnectionsPane.TestEmailConnectionClicked += TestEmailConnBtn_Click;
        }
    }
}
