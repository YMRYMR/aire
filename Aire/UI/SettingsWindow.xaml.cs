using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using Aire.AppLayer.Connections;
using Aire.AppLayer.Chat;
using Aire.AppLayer.Mcp;
using Aire.AppLayer.Settings;
using Aire.AppLayer.Tools;
using Aire.Data;
using Aire.Providers;
using Aire.Services;
using Aire.UI.Settings.Models;
using Microsoft.VisualBasic;
using MessageBox = System.Windows.MessageBox;

namespace Aire.UI
{
    public partial class SettingsWindow : Window
    {
        private const int MinTimeoutMinutes = 1;
        private const int MaxTimeoutMinutes = 43200;
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left, Top, Right, Bottom; }

        internal DatabaseService _databaseService;
        internal McpCatalogApplicationService _mcpCatalogApplicationService;
        internal McpConfigApplicationService _mcpConfigApplicationService;
        internal EmailAccountApplicationService _emailAccountApplicationService;
        internal AppSettingsApplicationService _appSettingsApplicationService;
        internal ContextSettingsApplicationService _contextSettingsApplicationService;
        internal AutoAcceptProfilesApplicationService _autoAcceptProfilesApplicationService;
        private readonly SpeechSynthesisService? _ttsService;
        private List<Provider> _providers = new();
        private Provider? _selectedProvider;
        private bool _suppressAutoSave;
        private bool _suppressAppearance = true; // prevents feedback loops while initialising controls
        private bool _suppressApiAccess; // prevents feedback loops while initialising API access controls
        private bool _suppressAutoAccept; // prevents feedback loops while initialising auto‑accept controls
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
        private Provider?            _draggedProvider;

        // ── Window state persistence ──────────────────────────────────────────
        public static SettingsWindow? Current { get; private set; }

        /// <summary>
        /// Fired when any code (e.g. HelpWindow) wants to open Settings, optionally navigating
        /// to a specific tab. App.xaml.cs subscribes to this and handles it.
        /// </summary>
        public static event Action<string?>? OpenRequested;

        /// <summary>Request the Settings window to open, optionally jumping to a tab.</summary>
        /// <param name="tab">One of: providers, appearance, voice, connections — or null for default.</param>
        public static void RequestOpen(string? tab = null) => OpenRequested?.Invoke(tab);

        /// <summary>
        /// In-memory snapshot of the last-saved auto-accept JSON.
        /// Updated every time the user changes a setting. MainWindow reads this
        /// to avoid a cross-connection database round-trip on every tool call.
        /// </summary>
        public static string? AutoAcceptJsonCache { get; private set; }

        /// <summary>Called by MainWindow at startup to warm the cache from the DB.</summary>
        public static void SetAutoAcceptCache(string json) => AutoAcceptJsonCache = json;

        internal static readonly string StatePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Aire", "settingsstate.json");

        private double? _savedEditPanelWidth;

        // ─────────────────────────────────────────────────────────────────────

        public event Action? ProvidersChanged;
        public event Action? AppearanceChanged;

        public Task RefreshProvidersForExternalChangeAsync(int? reSelectId = null)
            => RefreshProvidersList(reSelectId);

        // ── Auto‑accept ───────────────────────────────────────────────────────

        // ── Providers ─────────────────────────────────────────────────────────

        /// <summary>
         /// Maps action IDs declared by <see cref="IProviderMetadata"/> to their XAML buttons.
        /// </summary>
        private Dictionary<string, System.Windows.Controls.Button>? _actionButtonMap;

        private Dictionary<string, System.Windows.Controls.Button> ActionButtonMap =>
            _actionButtonMap ??= new()
            {
                ["claude-login"]   = ClaudeAiLoginButton,
                ["refresh-models"] = RefreshModelsButton,
                ["codex-install"]  = InstallOllamaButton,
            };

        private void PruneClaudeWebChoices()
        {
            if (ProviderVisibility.ShowClaudeWebProvider)
                return;

            RemoveProviderChoice(TypeComboBox, "ClaudeWeb");
        }

        private static void RemoveProviderChoice(System.Windows.Controls.ComboBox comboBox, string tag)
        {
            for (int index = comboBox.Items.Count - 1; index >= 0; index--)
            {
                if (comboBox.Items[index] is ComboBoxItem item &&
                    item.Tag is string itemTag &&
                    string.Equals(itemTag, tag, StringComparison.OrdinalIgnoreCase))
                {
                    comboBox.Items.RemoveAt(index);
                }
            }
        }

        // ── Capability Tests ─────────────────────────────────────────────────

        /// <summary>
        /// Builds a temporary <see cref="IAiProvider"/> from the current form values
        /// (without requiring the user to have saved first).

        // ── Connections tab — view models ─────────────────────────────────────

        // ── Collections ───────────────────────────────────────────────────────
        private System.Collections.ObjectModel.ObservableCollection<EmailAccountViewModel> _emailVms = new();
        private System.Collections.ObjectModel.ObservableCollection<McpServerViewModel>   _mcpVms   = new();
        private System.Collections.ObjectModel.ObservableCollection<McpCatalogEntryViewModel> _mcpCatalogVms = new();
        private EmailAccountViewModel?   _editingEmailVm;
        private string            _editingOAuthRefreshToken = string.Empty;  // plaintext, in-memory only
        private McpServerViewModel?      _editingMcpVm;

    }
}
