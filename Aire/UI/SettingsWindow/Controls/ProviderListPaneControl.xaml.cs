using System.Windows;
using RoutedEventHandler = System.Windows.RoutedEventHandler;
using SelectionChangedEventHandler = System.Windows.Controls.SelectionChangedEventHandler;
using SelectionChangedEventArgs = System.Windows.Controls.SelectionChangedEventArgs;
using Button = System.Windows.Controls.Button;
using ListBox = System.Windows.Controls.ListBox;
using UserControl = System.Windows.Controls.UserControl;
using MouseEventHandler = System.Windows.Input.MouseEventHandler;
using MouseButtonEventHandler = System.Windows.Input.MouseButtonEventHandler;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
using DragEventHandler = System.Windows.DragEventHandler;
using DragEventArgs = System.Windows.DragEventArgs;

namespace Aire.UI.Settings.Controls
{
    public partial class ProviderListPaneControl : UserControl
    {
        public ProviderListPaneControl()
        {
            InitializeComponent();
        }

        public Button AddProviderButton => PART_AddProviderButton;
        public Button SetupWizardButton => PART_SetupWizardButton;
        public ListBox ProvidersListView => PART_ProvidersListView;

        public event RoutedEventHandler? AddProviderClicked;
        public event RoutedEventHandler? SetupWizardClicked;
        public event SelectionChangedEventHandler? ProvidersSelectionChanged;
        public event MouseEventHandler? ProvidersPreviewMouseMoveForwarded;
        public event DragEventHandler? ProvidersDragOverForwarded;
        public event DragEventHandler? ProvidersDropForwarded;
        public event RoutedEventHandler? DeleteProviderClicked;
        public event RoutedEventHandler? EnabledDotClicked;

        private void AddProviderButton_Click(object sender, RoutedEventArgs e) => AddProviderClicked?.Invoke(sender, e);
        private void SetupWizardButton_Click(object sender, RoutedEventArgs e) => SetupWizardClicked?.Invoke(sender, e);
        public event MouseButtonEventHandler? DragHandleMouseDownForwarded;
        private void ProvidersListView_SelectionChanged(object sender, SelectionChangedEventArgs e) => ProvidersSelectionChanged?.Invoke(sender, e);
        private void ProvidersListView_PreviewMouseMove(object sender, MouseEventArgs e) => ProvidersPreviewMouseMoveForwarded?.Invoke(sender, e);
        private void ProvidersListView_DragOver(object sender, DragEventArgs e) => ProvidersDragOverForwarded?.Invoke(sender, e);
        private void ProvidersListView_Drop(object sender, DragEventArgs e) => ProvidersDropForwarded?.Invoke(sender, e);
        private void DragHandle_MouseDown(object sender, MouseButtonEventArgs e) => DragHandleMouseDownForwarded?.Invoke(sender, e);
        private void DeleteListItem_Click(object sender, RoutedEventArgs e) => DeleteProviderClicked?.Invoke(sender, e);
        private void EnabledDot_Click(object sender, RoutedEventArgs e) => EnabledDotClicked?.Invoke(sender, e);
    }
}
