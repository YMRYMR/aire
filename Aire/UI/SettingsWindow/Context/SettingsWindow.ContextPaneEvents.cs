namespace Aire.UI
{
    public partial class SettingsWindow
    {
        private void WireContextPaneEvents()
        {
            ContextPane.ContextSettingChangedRequested += ContextSettingChanged;
            ContextPane.RestoreDefaultsRequested += RestoreContextDefaults;
        }
    }
}
