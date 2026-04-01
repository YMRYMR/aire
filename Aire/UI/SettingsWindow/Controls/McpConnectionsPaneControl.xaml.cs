using RoutedEventArgs = System.Windows.RoutedEventArgs;
using RoutedEventHandler = System.Windows.RoutedEventHandler;
using SelectionChangedEventArgs = System.Windows.Controls.SelectionChangedEventArgs;
using SelectionChangedEventHandler = System.Windows.Controls.SelectionChangedEventHandler;
using Border = System.Windows.Controls.Border;
using Button = System.Windows.Controls.Button;
using CheckBox = System.Windows.Controls.CheckBox;
using ContextMenu = System.Windows.Controls.ContextMenu;
using ListView = System.Windows.Controls.ListView;
using TextBox = System.Windows.Controls.TextBox;
using TextBlock = System.Windows.Controls.TextBlock;
using UserControl = System.Windows.Controls.UserControl;

namespace Aire.UI.Settings.Controls
{
    public partial class McpConnectionsPaneControl : UserControl
    {
        public McpConnectionsPaneControl()
        {
            InitializeComponent();
        }

        public TextBlock McpServersTitle => PART_McpServersTitle;
        public TextBlock McpServersDescription => PART_McpServersDescription;
        public Button AddMcpBtn => PART_AddMcpBtn;
        public Button McpTemplatesBtn => PART_McpTemplatesBtn;
        public ContextMenu McpTemplatesMenu => PART_McpTemplatesMenu;
        public ListView McpServersList => PART_McpServersList;
        public TextBlock McpTipText => PART_McpTipText;
        public Border McpEditPanel => PART_McpEditPanel;
        public TextBlock McpEditTitle => PART_McpEditTitle;
        public TextBox McpNameBox => PART_McpNameBox;
        public TextBox McpCommandBox => PART_McpCommandBox;
        public TextBox McpArgsBox => PART_McpArgsBox;
        public TextBox McpWorkDirBox => PART_McpWorkDirBox;
        public TextBox McpEnvVarsBox => PART_McpEnvVarsBox;
        public Button SaveMcpBtn => PART_SaveMcpBtn;
        public Button CancelMcpBtn => PART_CancelMcpBtn;
        public Button TestMcpBtn => PART_TestMcpBtn;
        public TextBlock McpTestResult => PART_McpTestResult;

        public event RoutedEventHandler? AddMcpClicked;
        public event RoutedEventHandler? McpTemplatesClicked;
        public event SelectionChangedEventHandler? McpServersSelectionChanged;
        public event RoutedEventHandler? McpEnabledToggleClicked;
        public event RoutedEventHandler? EditMcpClicked;
        public event RoutedEventHandler? DeleteMcpClicked;
        public event RoutedEventHandler? SaveMcpClicked;
        public event RoutedEventHandler? CancelMcpClicked;
        public event RoutedEventHandler? TestMcpClicked;

        private void AddMcpBtn_Click(object sender, RoutedEventArgs e) => AddMcpClicked?.Invoke(sender, e);
        private void McpTemplatesBtn_Click(object sender, RoutedEventArgs e) => McpTemplatesClicked?.Invoke(sender, e);
        private void McpServersList_SelectionChanged(object sender, SelectionChangedEventArgs e) => McpServersSelectionChanged?.Invoke(sender, e);
        private void McpEnabledToggle_Click(object sender, RoutedEventArgs e) => McpEnabledToggleClicked?.Invoke(sender, e);
        private void EditMcpBtn_Click(object sender, RoutedEventArgs e) => EditMcpClicked?.Invoke(sender, e);
        private void DeleteMcpBtn_Click(object sender, RoutedEventArgs e) => DeleteMcpClicked?.Invoke(sender, e);
        private void SaveMcpBtn_Click(object sender, RoutedEventArgs e) => SaveMcpClicked?.Invoke(sender, e);
        private void CancelMcpBtn_Click(object sender, RoutedEventArgs e) => CancelMcpClicked?.Invoke(sender, e);
        private void TestMcpBtn_Click(object sender, RoutedEventArgs e) => TestMcpClicked?.Invoke(sender, e);
    }
}
