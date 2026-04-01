using Aire.UI.Settings.Controls;
using Button = System.Windows.Controls.Button;
using CheckBox = System.Windows.Controls.CheckBox;
using TextBox = System.Windows.Controls.TextBox;
using TextBlock = System.Windows.Controls.TextBlock;

namespace Aire.UI
{
    public partial class SettingsWindow
    {
        private LocalApiAccessPaneControl LocalApiAccessPane => LocalApiAccessPaneControl;
        private TextBlock ApiAccessTitle => LocalApiAccessPane.ApiAccessTitle;
        private TextBlock ApiAccessDescription => LocalApiAccessPane.ApiAccessDescription;
        private CheckBox ApiAccessEnabledCheckBox => LocalApiAccessPane.ApiAccessEnabledCheckBox;
        private TextBlock ApiAccessTokenTitle => LocalApiAccessPane.ApiAccessTokenTitle;
        private TextBlock ApiAccessTokenDescription => LocalApiAccessPane.ApiAccessTokenDescription;
        private TextBox ApiAccessTokenBox => LocalApiAccessPane.ApiAccessTokenBox;
        private Button CopyApiAccessTokenButton => LocalApiAccessPane.CopyApiAccessTokenButton;
        private Button RegenerateApiAccessTokenButton => LocalApiAccessPane.RegenerateApiAccessTokenButton;
    }
}
