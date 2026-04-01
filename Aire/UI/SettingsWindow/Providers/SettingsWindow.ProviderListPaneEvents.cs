namespace Aire.UI
{
    public partial class SettingsWindow
    {
        private void WireProviderListPaneEvents()
        {
            ProviderListPane.AddProviderClicked += AddProviderButton_Click;
            ProviderListPane.SetupWizardClicked += SetupWizardButton_Click;
            ProviderListPane.ProvidersSelectionChanged += ProvidersListView_SelectionChanged;
            ProviderListPane.ProvidersPreviewMouseMoveForwarded += ProvidersListView_PreviewMouseMove;
            ProviderListPane.ProvidersDragOverForwarded += ProvidersListView_DragOver;
            ProviderListPane.ProvidersDropForwarded += ProvidersListView_Drop;
            ProviderListPane.DragHandleMouseDownForwarded += DragHandle_MouseDown;
            ProviderListPane.DeleteProviderClicked += DeleteListItem_Click;
            ProviderListPane.EnabledDotClicked += EnabledDot_Click;
        }
    }
}
