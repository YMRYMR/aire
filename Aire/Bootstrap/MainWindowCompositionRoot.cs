using System;
using System.Threading.Tasks;
using Aire.AppLayer.Api;
using Aire.AppLayer.Chat;
using Aire.AppLayer.Mcp;
using Aire.AppLayer.Providers;
using Aire.AppLayer.Settings;
using Aire.AppLayer.Tools;
using Aire.Data;
using Aire.Providers;
using Aire.Services;
using Aire.Services.Policies;
using Aire.Services.Workflows;
using Aire.Services.Tools;

namespace Aire.Bootstrap
{
    /// <summary>
    /// Centralized composition root for MainWindow dependencies.
    /// Builds the entire service graph in one place so that MainWindow
    /// does not act as its own composition root.
    /// </summary>
    public sealed class MainWindowCompositionRoot
    {
        public DatabaseService DatabaseService { get; }
        public ProviderFactory ProviderFactory { get; }
        public ChatService ChatService { get; }
        public LocalApiApplicationService LocalApiApplicationService { get; }
        public AssistantModeApplicationService AssistantModeApplicationService { get; }
        public GeneratedImageApplicationService GeneratedImageApplicationService { get; }
        public ChatSessionApplicationService ChatSessionApplicationService { get; }
        public ContextSettingsApplicationService ContextSettingsApplicationService { get; }
        public ConversationApplicationService ConversationApplicationService { get; }
        public ConversationAssetApplicationService ConversationAssetApplicationService { get; }
        public ConversationTranscriptApplicationService ConversationTranscriptApplicationService { get; }
        public ChatInteractionApplicationService ChatInteractionApplicationService { get; }
        public ChatTurnApplicationService ChatTurnApplicationService { get; }
        public ProviderActivationApplicationService ProviderActivationApplicationService { get; }
        public ProviderCatalogApplicationService ProviderCatalogApplicationService { get; }
        public ProviderUiStateApplicationService ProviderUiStateApplicationService { get; }
        public SwitchModelApplicationService SwitchModelApplicationService { get; }
        public ToolApprovalApplicationService ToolApprovalApplicationService { get; }
        public ToolControlSessionApplicationService ToolControlSessionApplicationService { get; }
        public ToolApprovalPromptApplicationService ToolApprovalPromptApplicationService { get; }
        public ToolApprovalExecutionApplicationService ToolApprovalExecutionApplicationService { get; }
        public ToolCategorySettingsApplicationService ToolCategorySettingsApplicationService { get; }
        public McpStartupApplicationService McpStartupApplicationService { get; }
        public FileSystemService FileSystemService { get; }
        public ToolExecutionService ToolExecutionService { get; }
        public EmailToolService EmailToolService { get; }
        public AgentModeService AgentModeService { get; }
        public SpeechRecognitionService SpeechService { get; }
        public SpeechSynthesisService TtsService { get; }
        public Action HideWindow { get; set; }
        public Func<Task> ShowWindow { get; set; }

        public MainWindowCompositionRoot(
            Action? hideWindow = null,
            Func<Task>? showWindow = null)
        {
            HideWindow = hideWindow ?? (() => { });
            ShowWindow = showWindow ?? (() => Task.CompletedTask);

            DatabaseService = new DatabaseService();
            LocalApiApplicationService = new LocalApiApplicationService();
            AssistantModeApplicationService = new AssistantModeApplicationService();
            ChatSessionApplicationService = new ChatSessionApplicationService(DatabaseService, DatabaseService);
            GeneratedImageApplicationService = new GeneratedImageApplicationService(ChatSessionApplicationService);
            ContextSettingsApplicationService = new ContextSettingsApplicationService(DatabaseService);
            ConversationApplicationService = new ConversationApplicationService(DatabaseService);
            ConversationAssetApplicationService = new ConversationAssetApplicationService(DatabaseService);
            ConversationTranscriptApplicationService = new ConversationTranscriptApplicationService();
            ChatInteractionApplicationService = new ChatInteractionApplicationService();
            ProviderFactory = new ProviderFactory(DatabaseService);
            ChatService = new ChatService(ProviderFactory);
            ProviderActivationApplicationService = new ProviderActivationApplicationService(
                ChatService,
                ProviderFactory,
                ChatSessionApplicationService);
            ProviderCatalogApplicationService = new ProviderCatalogApplicationService(DatabaseService);
            ProviderUiStateApplicationService = new ProviderUiStateApplicationService();
            SwitchModelApplicationService = new SwitchModelApplicationService(
                ProviderFactory,
                ChatService,
                ChatSessionApplicationService);
            FileSystemService = new FileSystemService();
            ToolApprovalApplicationService = new ToolApprovalApplicationService(
                new ToolAutoAcceptPolicyService(() => Task.FromResult(UI.SettingsWindow.AutoAcceptJsonCache)));
            ToolControlSessionApplicationService = new ToolControlSessionApplicationService(ToolApprovalApplicationService);
            ToolApprovalPromptApplicationService = new ToolApprovalPromptApplicationService();
            ToolCategorySettingsApplicationService = new ToolCategorySettingsApplicationService(DatabaseService);

            var commandService = new CommandExecutionService();
            EmailToolService = new EmailToolService(DatabaseService);
            ToolExecutionService = new ToolExecutionService(
                FileSystemService, commandService,
                hideWindowAsync: () => { HideWindow(); return Task.CompletedTask; },
                showWindowAsync: () => ShowWindow(),
                mcpManager: Services.Mcp.McpManager.Instance,
                emailTool: EmailToolService);
            var toolExecutionWorkflow = new ToolExecutionWorkflowService(ToolExecutionService, DatabaseService, DatabaseService);
            ChatTurnApplicationService = new ChatTurnApplicationService(
                ChatSessionApplicationService,
                toolExecutionWorkflow);
            ToolApprovalExecutionApplicationService = new ToolApprovalExecutionApplicationService(
                ToolApprovalPromptApplicationService,
                toolExecutionWorkflow);
            McpStartupApplicationService = new McpStartupApplicationService(DatabaseService);

            SpeechService = new SpeechRecognitionService();
            TtsService = new SpeechSynthesisService();
            AgentModeService = new AgentModeService();
        }
    }
}
