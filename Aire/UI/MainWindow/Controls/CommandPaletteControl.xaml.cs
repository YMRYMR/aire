using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Aire.UI.MainWindow.Controls
{
    public sealed class CommandItem
    {
        public string Name { get; init; } = "";
        public string Shortcut { get; init; } = "";
        public Action? Execute { get; init; }
    }

    public partial class CommandPaletteControl : System.Windows.Controls.UserControl
    {
        private readonly ObservableCollection<CommandItem> _allCommands = new();
        private readonly ObservableCollection<CommandItem> _filteredCommands = new();

        public CommandPaletteControl()
        {
            InitializeComponent();
            PART_CommandList.ItemsSource = _filteredCommands;
        }

        public void SetCommands(System.Collections.Generic.IEnumerable<CommandItem> commands)
        {
            _allCommands.Clear();
            foreach (var cmd in commands)
                _allCommands.Add(cmd);
            ApplyFilter("");
        }

        public void Open()
        {
            Visibility = Visibility.Visible;
            PART_SearchBox.Text = "";
            ApplyFilter("");
            if (_filteredCommands.Count > 0)
                PART_CommandList.SelectedIndex = 0;
            PART_SearchBox.Focus();
        }

        public void Close()
        {
            Visibility = Visibility.Collapsed;
        }

        public bool IsOpen => Visibility == Visibility.Visible;

        public event Action? ClosedExternally;

        protected override void OnKeyDown(System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close();
                ClosedExternally?.Invoke();
                e.Handled = true;
                return;
            }

            base.OnKeyDown(e);
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilter(PART_SearchBox.Text);
        }

        private void ApplyFilter(string? text)
        {
            var query = (text ?? "").Trim();
            _filteredCommands.Clear();
            var matches = string.IsNullOrEmpty(query)
                ? _allCommands
                : _allCommands.Where(c => c.Name.Contains(query, StringComparison.OrdinalIgnoreCase));

            foreach (var cmd in matches)
                _filteredCommands.Add(cmd);

            if (_filteredCommands.Count > 0)
                PART_CommandList.SelectedIndex = 0;
        }

        private void CommandList_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ExecuteSelected();
                e.Handled = true;
            }
        }

        private void CommandList_Click(object sender, MouseButtonEventArgs e)
        {
            ExecuteSelected();
        }

        private void ExecuteSelected()
        {
            if (PART_CommandList.SelectedItem is CommandItem cmd)
            {
                Close();
                ClosedExternally?.Invoke();
                cmd.Execute?.Invoke();
            }
        }
    }
}
