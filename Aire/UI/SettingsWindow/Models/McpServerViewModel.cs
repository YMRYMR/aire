using System.ComponentModel;
using System.Runtime.CompilerServices;
using Aire.Services.Mcp;

namespace Aire.UI.Settings.Models
{
    internal sealed class McpServerViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private void Notify([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public McpServerConfig Model { get; set; }

        public McpServerViewModel(McpServerConfig model)
        {
            Model = model;
            RefreshStatus();
        }

        public string Name => Model.Name;
        public string CommandPreview => $"{Model.Command} {Model.Arguments}".Trim();

        public bool IsEnabled
        {
            get => Model.IsEnabled;
            set
            {
                Model.IsEnabled = value;
                Notify();
            }
        }

        private string _statusColor = "#888888";
        public string StatusColor
        {
            get => _statusColor;
            set
            {
                _statusColor = value;
                Notify();
            }
        }

        public void RefreshStatus()
        {
            StatusColor = McpManager.Instance.IsServerRunning(Model.Name)
                ? "#3CB371"
                : "#888888";
        }
    }
}
