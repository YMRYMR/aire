using System;
using System.Threading.Tasks;
using Aire.AppLayer.Api;
using Aire.AppLayer.Chat;
using Aire.AppLayer.Mcp;
using Aire.AppLayer.Providers;
using Aire.AppLayer.Tools;
using Aire.Data;
using Aire.Providers;
using Aire.Services;
using Aire.Services.Policies;
using Aire.Services.Workflows;

namespace Aire
{
    public partial class MainWindow
    {
        private ToolAutoAcceptPolicyService? _toolAutoAcceptPolicy;

        internal Task<bool> IsToolAutoAcceptedAsync(string toolName)
            => (_toolAutoAcceptPolicy ??= new ToolAutoAcceptPolicyService(
                    () => _chatSessionApplicationService.GetSettingAsync("auto_accept_settings")))
                .IsAutoAcceptedAsync(toolName, UI.SettingsWindow.AutoAcceptJsonCache);

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;

            _databaseService = new DatabaseService();
            _localApiApplicationService = new LocalApiApplicationService();
            _chatSessionApplicationService = new ChatSessionApplicationService(_databaseService, _databaseService);
            _conversationApplicationService = new ConversationApplicationService(_databaseService);
            _conversationAssetApplicationService = new ConversationAssetApplicationService(_databaseService);
            _conversationTranscriptApplicationService = new ConversationTranscriptApplicationService();
            _chatInteractionApplicationService = new ChatInteractionApplicationService();
            _providerFactory = new ProviderFactory(_databaseService);
            _chatService = new ChatService(_providerFactory);
            _providerActivationApplicationService = new ProviderActivationApplicationService(
                _chatService,
                _providerFactory,
                _chatSessionApplicationService);
            _providerCatalogApplicationService = new ProviderCatalogApplicationService(_databaseService);
            _providerUiStateApplicationService = new ProviderUiStateApplicationService();
            _switchModelApplicationService = new SwitchModelApplicationService(
                _providerFactory,
                _chatService,
                _chatSessionApplicationService);
            _fileSystemService = new FileSystemService();
            _toolApprovalApplicationService = new ToolApprovalApplicationService(
                new ToolAutoAcceptPolicyService(() => Task.FromResult(UI.SettingsWindow.AutoAcceptJsonCache)));
            _toolControlSessionApplicationService = new ToolControlSessionApplicationService(_toolApprovalApplicationService);
            _toolApprovalPromptApplicationService = new ToolApprovalPromptApplicationService();
            _toolCategorySettingsApplicationService = new ToolCategorySettingsApplicationService(_databaseService);

            var commandService = new CommandExecutionService();
            _emailToolService = new Aire.Services.Tools.EmailToolService(_databaseService);
            _toolExecutionService = new ToolExecutionService(
                _fileSystemService, commandService,
                hideWindowAsync: () => { Dispatcher.Invoke(Hide); return Task.CompletedTask; },
                showWindowAsync: () => { Dispatcher.Invoke(() => TrayService?.ShowMainWindow()); return Task.CompletedTask; },
                mcpManager: Aire.Services.Mcp.McpManager.Instance,
                emailTool: _emailToolService);
            var toolExecutionWorkflow = new ToolExecutionWorkflowService(_toolExecutionService, _databaseService, _databaseService);
            _chatTurnApplicationService = new ChatTurnApplicationService(
                _chatSessionApplicationService,
                toolExecutionWorkflow);
            _toolApprovalExecutionApplicationService = new ToolApprovalExecutionApplicationService(
                _toolApprovalPromptApplicationService,
                toolExecutionWorkflow);
            _mcpStartupApplicationService = new McpStartupApplicationService(_databaseService);

            _speechService = new SpeechRecognitionService();
            _speechService.PhraseRecognized += OnPhraseRecognized;
            _speechService.CommandRecognized += OnCommandRecognized;
            _speechService.CountdownTick += OnCountdownTick;
            _speechService.SilenceTimeout += OnSilenceTimeout;
            _speechService.Stopped += OnRecognitionStopped;

            _ttsService = new SpeechSynthesisService();
            _ttsService.SpeakingCompleted += OnTtsSpeakingCompleted;

            AppearanceService.AppearanceChanged += () => Dispatcher.Invoke(() => FontSize = AppearanceService.FontSize);
            LocalizationService.LoadAll();
            LocalizationService.LanguageChanged += () =>
            {
                Dispatcher.Invoke(() =>
                {
                    ApplyLocalization();
                    SaveWindowSize();
                });
                _speechService.Language = LocalizationService.CurrentCode;
            };

            LoadWindowSize();
            FontSize = AppearanceService.FontSize;
            _speechService.Language = LocalizationService.CurrentCode;
            UpdateVoiceOutputButton();
            if (LocalizationService.HelpSections.Count == 0)
                LocalizationService.SetLanguage("en");
            SizeChanged += (s, e) => SaveWindowSize();
            LocationChanged += (s, e) => SaveWindowSize();
            _ttsService.SettingsChanged += SaveWindowSize;

            Loaded += OnWindowLoaded;
            IsVisibleChanged += (_, e) => { if ((bool)e.NewValue) InputTextBox.Focus(); };
            IsVisibleChanged += (s, e) => { if ((bool)e.NewValue) ScrollToBottom(); };
            _availabilityTracker.AvailabilityChanged += _ => Dispatcher.Invoke(RefreshProviderAvailabilityUI);
        }

        private bool _isAttached = true;

        public Task InitializeStartupAsync()
            => _startupInitializationTask ??= InitializeStartupCoreAsync();

        private async Task InitializeStartupCoreAsync()
        {
            try
            {
                await _databaseService.InitializeAsync();

                var autoAcceptJson = await _chatSessionApplicationService.GetSettingAsync("auto_accept_settings");
                if (!string.IsNullOrEmpty(autoAcceptJson))
                    UI.SettingsWindow.SetAutoAcceptCache(autoAcceptJson);

                var toolCategorySelection = await _toolCategorySettingsApplicationService.LoadAsync();
                _enabledToolCategories = new HashSet<string>(toolCategorySelection.EnabledCategories, StringComparer.OrdinalIgnoreCase);
                UpdateToolsButtonState();

                int? savedProviderId = await _chatSessionApplicationService.GetSelectedProviderIdAsync();

                await LoadProviders(savedProviderId: savedProviderId);

                InitSidebarState();

                await _mcpStartupApplicationService.StartAllAsync();
                await _emailToolService.RefreshIsConfiguredAsync();

                AppStartupState.MarkReady();
            }
            catch (Exception ex)
            {
                AppStartupState.MarkFailed(ex);
                throw;
            }
        }
    }
}
