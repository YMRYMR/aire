using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Aire.AppLayer.Api;
using Aire.AppLayer.Chat;
using Aire.AppLayer.Mcp;
using Aire.AppLayer.Providers;
using Aire.AppLayer.Settings;
using Aire.AppLayer.Tools;
using Aire.Data;
using Aire.Providers;
using Aire.Services;
using ChatMessage = Aire.UI.MainWindow.Models.ChatMessage;
using Color = System.Windows.Media.Color;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MessageBox = System.Windows.MessageBox;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using ProviderChatMessage = Aire.Providers.ChatMessage;
using TodoItem = Aire.UI.MainWindow.Models.TodoItem;
using Microsoft.Win32;

namespace Aire
{
    public partial class MainWindow : IApiCommandHandler
    {
        internal void ApplyContextWindowSettings(ContextWindowSettings settings)
        {
            _contextWindowSettings = settings;
        }

        internal void ApplyCustomInstructions(string instructions)
        {
            _customInstructions = instructions;
        }

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
        internal AgentModeService? _agentModeService;
        internal SpeechRecognitionService _speechService;
        private List<Aire.Data.Provider> _providers = new();
        internal int? _currentConversationId;
        private string? _attachedImagePath;
        private string? _attachedFilePath;
        private string? _attachedFileName;
        private bool _isProcessing;
        private Aire.UI.SessionPanicButton? _panicButton;
        internal UI.SettingsWindow? _settingsWindow;
        private UI.HelpWindow? _helpWindow;
        private readonly SpeechSynthesisService _ttsService;
        private CancellationTokenSource? _aiCancellationTokenSource;
        private IAiProvider? _currentProvider;
        internal int? _currentProviderId;
        internal bool _suppressProviderChange;
        internal ProviderAvailabilityTracker _availabilityTracker = ProviderAvailabilityTracker.Instance;
        private Task? _startupInitializationTask;
        private HashSet<string> _enabledToolCategories = new(StringComparer.OrdinalIgnoreCase);
        internal bool _toolsSupportedByProvider = true;
        private ContextWindowSettings _contextWindowSettings = ContextWindowSettings.Default;
        private string _customInstructions = string.Empty;
        private string _assistantModeKey = "general";
        private string _assistantModeDisplayName = "General";
        private TokenUsage? _cachedTokenUsage;
        private System.Windows.Threading.DispatcherTimer? _tokenUsageRefreshTimer;
        private bool _limitReachedNotificationShown = false;
        private bool _limitBubbleShown = false;
        internal List<ProviderChatMessage> _conversationHistory = new();
        internal readonly List<string> _inputHistory = new();
        private int _historyIndex = -1;
        private string _inputDraft = string.Empty;
        private string _orchestratorInputDraft = string.Empty;
        private bool _followMessagesScroll = true;
        private readonly List<int> _searchMatchIndices = new();
        private int _searchCurrentIndex = -1;
        private static SolidColorBrush UserBgBrush => Aire.Services.AppearanceService.UserBgBrush;
        private static SolidColorBrush UserFgBrush => Aire.Services.AppearanceService.UserFgBrush;
        private static SolidColorBrush AiBgBrush => Aire.Services.AppearanceService.AiBgBrush;
        private static SolidColorBrush AiFgBrush => Aire.Services.AppearanceService.AiFgBrush;
        private static SolidColorBrush SystemBgBrush => Aire.Services.AppearanceService.SystemBgBrush;
        private static SolidColorBrush SystemFgBrush => Aire.Services.AppearanceService.SystemFgBrush;
        private static SolidColorBrush ErrorBgBrush => Aire.Services.AppearanceService.ErrorBgBrush;
        private static SolidColorBrush ErrorFgBrush => Aire.Services.AppearanceService.ErrorFgBrush;
        private static readonly string _windowStatePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Aire", "windowstate.json");
        private ChatMessage? _todoListMessage;
    }
}
