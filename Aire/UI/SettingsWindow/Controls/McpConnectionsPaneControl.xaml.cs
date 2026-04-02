using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Data;
using RoutedEventArgs = System.Windows.RoutedEventArgs;
using RoutedEventHandler = System.Windows.RoutedEventHandler;
using SelectionChangedEventArgs = System.Windows.Controls.SelectionChangedEventArgs;
using SelectionChangedEventHandler = System.Windows.Controls.SelectionChangedEventHandler;
using TextChangedEventArgs = System.Windows.Controls.TextChangedEventArgs;
using Border = System.Windows.Controls.Border;
using Button = System.Windows.Controls.Button;
using CheckBox = System.Windows.Controls.CheckBox;
using ContextMenu = System.Windows.Controls.ContextMenu;
using ListView = System.Windows.Controls.ListView;
using TextBox = System.Windows.Controls.TextBox;
using TextBlock = System.Windows.Controls.TextBlock;
using UserControl = System.Windows.Controls.UserControl;
using Aire.UI.Settings.Models;

namespace Aire.UI.Settings.Controls
{
    public partial class McpConnectionsPaneControl : UserControl
    {
        public McpConnectionsPaneControl()
        {
            InitializeComponent();
        }

        // The ListView's internal ScrollViewer swallows MouseWheel even when its own
        // scrolling is disabled. Re-raise the event on this UserControl so it bubbles
        // up to the parent ScrollViewer in SettingsWindow.
        private void McpList_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Handled) return;
            e.Handled = true;
            RaiseEvent(new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
            {
                RoutedEvent = MouseWheelEvent
            });
        }

        public TextBlock McpServersTitle       => PART_McpServersTitle;
        public TextBlock McpServersDescription => PART_McpServersDescription;
        public Button    AddMcpBtn             => PART_AddMcpBtn;
        public Button    McpTemplatesBtn       => PART_McpTemplatesBtn;
        public ContextMenu McpTemplatesMenu    => PART_McpTemplatesMenu;
        public TextBlock McpCatalogTitle       => PART_McpCatalogTitle;
        public ListView  McpCatalogList        => PART_McpCatalogList;
        public ListView  McpServersList        => PART_McpServersList;
        public TextBlock McpTipText            => PART_McpTipText;
        public Border    McpEditPanel          => PART_McpEditPanel;
        public TextBlock McpEditTitle          => PART_McpEditTitle;
        public TextBox   McpNameBox            => PART_McpNameBox;
        public TextBox   McpCommandBox         => PART_McpCommandBox;
        public TextBox   McpArgsBox            => PART_McpArgsBox;
        public TextBox   McpWorkDirBox         => PART_McpWorkDirBox;
        public TextBox   McpEnvVarsBox         => PART_McpEnvVarsBox;
        public Button    SaveMcpBtn            => PART_SaveMcpBtn;
        public Button    CancelMcpBtn          => PART_CancelMcpBtn;
        public Button    TestMcpBtn            => PART_TestMcpBtn;
        public TextBlock McpTestResult         => PART_McpTestResult;

        public event RoutedEventHandler?          AddMcpClicked;
        public event RoutedEventHandler?          McpTemplatesClicked;
        public event RoutedEventHandler?          CatalogMcpActionClicked;
        public event SelectionChangedEventHandler? McpServersSelectionChanged;
        public event RoutedEventHandler?          McpEnabledToggleClicked;
        public event RoutedEventHandler?          EditMcpClicked;
        public event RoutedEventHandler?          DeleteMcpClicked;
        public event RoutedEventHandler?          SaveMcpClicked;
        public event RoutedEventHandler?          CancelMcpClicked;
        public event RoutedEventHandler?          TestMcpClicked;

        private void AddMcpBtn_Click(object sender, RoutedEventArgs e)           => AddMcpClicked?.Invoke(sender, e);
        private void McpTemplatesBtn_Click(object sender, RoutedEventArgs e)     => McpTemplatesClicked?.Invoke(sender, e);
        private void CatalogMcpActionBtn_Click(object sender, RoutedEventArgs e) => CatalogMcpActionClicked?.Invoke(sender, e);
        private void McpServersList_SelectionChanged(object sender, SelectionChangedEventArgs e) => McpServersSelectionChanged?.Invoke(sender, e);
        private void McpEnabledToggle_Click(object sender, RoutedEventArgs e)    => McpEnabledToggleClicked?.Invoke(sender, e);
        private void EditMcpBtn_Click(object sender, RoutedEventArgs e)          => EditMcpClicked?.Invoke(sender, e);
        private void DeleteMcpBtn_Click(object sender, RoutedEventArgs e)        => DeleteMcpClicked?.Invoke(sender, e);
        private void SaveMcpBtn_Click(object sender, RoutedEventArgs e)          => SaveMcpClicked?.Invoke(sender, e);
        private void CancelMcpBtn_Click(object sender, RoutedEventArgs e)        => CancelMcpClicked?.Invoke(sender, e);
        private void TestMcpBtn_Click(object sender, RoutedEventArgs e)          => TestMcpClicked?.Invoke(sender, e);

        private void McpSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var view = CollectionViewSource.GetDefaultView(PART_McpCatalogList.ItemsSource);
            if (view == null)
                return;

            var query = PART_McpSearchBox.Text.Trim();
            if (string.IsNullOrEmpty(query))
            {
                view.Filter = null;
            }
            else
            {
                view.Filter = o => o is McpCatalogEntryViewModel vm &&
                    (vm.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                     vm.Description.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                     vm.Category.Contains(query, StringComparison.OrdinalIgnoreCase));
            }
        }
    }
}
