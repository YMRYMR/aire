using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Aire.Data;
using Aire.Providers;
using Aire.Services;
using Microsoft.Win32;
using MessageBox = System.Windows.MessageBox;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using Color = System.Windows.Media.Color;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using ProviderChatMessage = Aire.Providers.ChatMessage;
using AireMessage = Aire.Data.Message;
using Aire.AppLayer.Api;
using Aire.AppLayer.Chat;
using Aire.AppLayer.Mcp;
using Aire.AppLayer.Providers;
using Aire.AppLayer.Settings;
using Aire.AppLayer.Tools;
using ChatMessage = Aire.UI.MainWindow.Models.ChatMessage;
using TodoItem = Aire.UI.MainWindow.Models.TodoItem;

namespace Aire
{
    public partial class MainWindow : Window
    {
        internal DatabaseService _databaseService;
        internal ProviderFactory _providerFactory;
        internal readonly ChatService _chatService;
        private readonly LocalApiApplicationService _localApiApplicationService;
        private readonly AssistantModeApplicationService _assistantModeApplicationService;
        private readonly GeneratedImageApplicationService _generatedImageApplicationService;
        internal ChatSessionApplicationService _chatSessionApplicationService;
        private readonly ContextSettingsApplicationService _contextSettingsApplicationService;
        internal ConversationApplicationService _conversationApplicationService;
        private readonly ConversationAssetApplicationService _conversationAssetApplicationService;
        private readonly ConversationTranscriptApplicationService _conversationTranscriptApplicationService;
        private readonly ChatInteractionApplicationService _chatInteractionApplicationService;
        private readonly ChatTurnApplicationService _chatTurnApplicationService;
        internal readonly ProviderActivationApplicationService _providerActivationApplicationService;
        private readonly ProviderCatalogApplicationService _providerCatalogApplicationService;
        private readonly ProviderUiStateApplicationService _providerUiStateApplicationService;
        private readonly SwitchModelApplicationService _switchModelApplicationService;
        private readonly ToolApprovalApplicationService _toolApprovalApplicationService;
        private readonly ToolControlSessionApplicationService _toolControlSessionApplicationService;
        private readonly ToolApprovalPromptApplicationService _toolApprovalPromptApplicationService;
        private readonly ToolApprovalExecutionApplicationService _toolApprovalExecutionApplicationService;
        private readonly ToolCategorySettingsApplicationService _toolCategorySettingsApplicationService;
        private readonly McpStartupApplicationService _mcpStartupApplicationService;
        private readonly FileSystemService _fileSystemService;
        internal ToolExecutionService _toolExecutionService;
        private readonly Aire.Services.Tools.EmailToolService _emailToolService;
        internal SpeechRecognitionService _speechService;
        private List<Aire.Data.Provider> _providers = new();
        internal int? _currentConversationId;
        private string? _attachedImagePath;   // set when an image is attached
        private string? _attachedFilePath;    // set when a non-image file is attached
        private string? _attachedFileName;    // display name for the file chip
        private bool _isProcessing;
        private bool _isSwitchingChat = false;
        private Aire.UI.SessionPanicButton? _panicButton;
        internal UI.SettingsWindow?          _settingsWindow;
        private UI.HelpWindow?              _helpWindow;
        private readonly SpeechSynthesisService _ttsService;
        private CancellationTokenSource? _aiCancellationTokenSource; // for stopping AI operations
        private CancellationTokenSource? _selectionCancellationTokenSource;
        // Current provider instance — updated when the ComboBox selection changes.
        private IAiProvider? _currentProvider;
        internal int?         _currentProviderId;          // tracks last-active provider ID
        internal bool         _suppressProviderChange;     // true during programmatic ComboBox changes

        // Provider availability tracking
        internal ProviderAvailabilityTracker _availabilityTracker = ProviderAvailabilityTracker.Instance;
        private Task? _startupInitializationTask;
        private HashSet<string> _enabledToolCategories = new(StringComparer.OrdinalIgnoreCase);
        private ContextWindowSettings _contextWindowSettings = ContextWindowSettings.Default;
        private string _assistantModeKey = "general";
        private string _assistantModeDisplayName = "General";

        // Token usage tracking
        private TokenUsage? _cachedTokenUsage;
        private System.Windows.Threading.DispatcherTimer? _tokenUsageRefreshTimer;
        private bool _limitReachedNotificationShown = false;
        private bool _limitBubbleShown = false;

        internal List<ProviderChatMessage> _conversationHistory = new();

        // ── Input history (Up/Down navigation) ────────────────────────────────
        internal readonly List<string> _inputHistory = new();
        private int _historyIndex = -1;       // -1 = not navigating
        private string _inputDraft = string.Empty; // saved draft before navigating

        // Search state
        private readonly List<int> _searchMatchIndices = new();
        private int _searchCurrentIndex = -1;

        // Chat-message brushes — live in AppearanceService so they update when the theme changes.
        private static SolidColorBrush UserBgBrush   => Aire.Services.AppearanceService.UserBgBrush;
        private static SolidColorBrush UserFgBrush   => Aire.Services.AppearanceService.UserFgBrush;
        private static SolidColorBrush AiBgBrush     => Aire.Services.AppearanceService.AiBgBrush;
        private static SolidColorBrush AiFgBrush     => Aire.Services.AppearanceService.AiFgBrush;
        private static SolidColorBrush SystemBgBrush => Aire.Services.AppearanceService.SystemBgBrush;
        private static SolidColorBrush SystemFgBrush => Aire.Services.AppearanceService.SystemFgBrush;
        private static SolidColorBrush ErrorBgBrush  => Aire.Services.AppearanceService.ErrorBgBrush;
        private static SolidColorBrush ErrorFgBrush  => Aire.Services.AppearanceService.ErrorFgBrush;

        private static readonly string _windowStatePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Aire", "windowstate.json");

        // ── Task / follow-up state ─────────────────────────────────────────────
        /// <summary>The last todo-list ChatMessage created by update_todo_list (updated in place).</summary>
        private ChatMessage? _todoListMessage;

    }

}

