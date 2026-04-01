using Aire.UI.Settings.Controls;
using Button = System.Windows.Controls.Button;
using ListBox = System.Windows.Controls.ListBox;

namespace Aire.UI
{
    public partial class SettingsWindow
    {
        private ProviderListPaneControl ProviderListPane => ProviderListPaneControl;
        private Button AddProviderButton => ProviderListPane.AddProviderButton;
        private Button SetupWizardButton => ProviderListPane.SetupWizardButton;
        private ListBox ProvidersListView => ProviderListPane.ProvidersListView;
    }
}
