using System.ComponentModel;
using System.Runtime.CompilerServices;
using Aire.AppLayer.Mcp;
using Aire.Services.Mcp;

namespace Aire.UI.Settings.Models
{
    internal sealed class McpCatalogEntryViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        public McpCatalogEntryViewModel(McpCatalogApplicationService.McpCatalogEntry entry)
        {
            Entry = entry;
        }

        public McpCatalogApplicationService.McpCatalogEntry Entry { get; }

        public string Key => Entry.Key;
        public string Name => Entry.Name;
        public string Description => Entry.Description;
        public string Category => Entry.Category;

        private int? _installedConfigId;
        public int? InstalledConfigId
        {
            get => _installedConfigId;
            set
            {
                if (_installedConfigId == value)
                    return;

                _installedConfigId = value;
                Notify();
                Notify(nameof(IsInstalled));
                Notify(nameof(ActionLabel));
                Notify(nameof(StatusText));
            }
        }

        public bool IsInstalled => InstalledConfigId.HasValue;
        public string ActionLabel => IsInstalled ? "Remove" : "Install";
        public string StatusText => IsInstalled ? "Installed" : "Available";

        public void RefreshInstalled(McpCatalogApplicationService catalogService, IReadOnlyList<McpServerConfig> configs)
            => InstalledConfigId = catalogService.FindInstalledConfig(Key, configs)?.Id;

        private void Notify([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
