using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using Aire.AppLayer.Chat;
using Aire.AppLayer.Connections;
using Aire.AppLayer.Mcp;
using Aire.AppLayer.Settings;
using Aire.AppLayer.Tools;
using Aire.Data;
using Aire.Providers;
using Aire.Services;
using Aire.UI.Settings.Models;

namespace Aire.UI
{
    public partial class SettingsWindow : Window
    {
        private const int MinTimeoutMinutes = 1;
        private const int MaxTimeoutMinutes = 43200;

        internal DatabaseService _databaseService;
        internal McpCatalogApplicationService _mcpCatalogApplicationService;
        internal McpConfigApplicationService _mcpConfigApplicationService;
        internal EmailAccountApplicationService _emailAccountApplicationService;
        internal AppSettingsApplicationService _appSettingsApplicationService;
        internal ContextSettingsApplicationService _contextSettingsApplicationService;
        internal AutoAcceptProfilesApplicationService _autoAcceptProfilesApplicationService;
        internal ProviderFactory _providerFactory;
        private readonly SpeechSynthesisService? _ttsService;
        private List<Provider> _providers = new();
        private Provider? _selectedProvider;
        private bool _suppressAutoSave;
        private bool _suppressAppearance = true; // prevents feedback loops while initialising controls
        private bool _suppressApiAccess; // prevents feedback loops while initialising API access controls
        private bool _suppressAutoAccept; // prevents feedback loops while initialising auto-accept controls
        private bool _suppressContextSettings; // prevents feedback loops while initialising context controls
        private bool _suppressAutoAcceptProfileSelection;
        private bool _isRefreshing; // set during RefreshProvidersList to suppress model-reload side effects
        internal bool _suppressModelFilter; // prevents filter from running during programmatic Text changes
        private OllamaModelItem? _preFilterSelection; // tracks selection before filtering starts
        private System.Threading.CancellationTokenSource? _toastCts;
        private System.Threading.CancellationTokenSource? _testCts;
        private System.Windows.Threading.DispatcherTimer? _timeoutSaveTimer;
        private readonly CapabilityTestRunner _testRunner = new();

        // Drag-and-drop state for provider list reordering
        private System.Windows.Point _dragStartPoint;
        private Provider? _draggedProvider;

        private double? _savedEditPanelWidth;

        public event Action? ProvidersChanged;
        public event Action? AppearanceChanged;

        public Task RefreshProvidersForExternalChangeAsync(int? reSelectId = null)
            => RefreshProvidersList(reSelectId);

        /// <summary>
        /// Maps action IDs declared by <see cref="IProviderMetadata"/> to their XAML buttons.
        /// </summary>
        private Dictionary<string, System.Windows.Controls.Button>? _actionButtonMap;

        private Dictionary<string, System.Windows.Controls.Button> ActionButtonMap =>
            _actionButtonMap ??= new()
            {
                ["claude-login"] = ClaudeAiLoginButton,
                ["refresh-models"] = RefreshModelsButton,
                ["codex-install"] = InstallOllamaButton,
            };

        private void PruneHiddenProviderChoices()
            => ProviderChoiceVisibility.PruneHiddenChoices(TypeComboBox);

        // Connections tab collections
        private System.Collections.ObjectModel.ObservableCollection<EmailAccountViewModel> _emailVms = new();
        private System.Collections.ObjectModel.ObservableCollection<McpServerViewModel> _mcpVms = new();
        private System.Collections.ObjectModel.ObservableCollection<McpCatalogEntryViewModel> _mcpCatalogVms = new();
        private System.Collections.ObjectModel.ObservableCollection<UsageProviderRowViewModel> _usageProviderVms = new();
        private System.Collections.ObjectModel.ObservableCollection<UsageConversationRowViewModel> _usageConversationVms = new();
        private System.Collections.ObjectModel.ObservableCollection<UsageTrendLegendItemViewModel> _usageTrendLegendVms = new();
        private IReadOnlyList<UsageTrendSeries> _usageTrendSeries = Array.Empty<UsageTrendSeries>();
        private EmailAccountViewModel? _editingEmailVm;
        private string _editingOAuthRefreshToken = string.Empty; // plaintext, in-memory only
        private McpServerViewModel? _editingMcpVm;
    }
}
