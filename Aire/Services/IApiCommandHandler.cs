using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Aire.Data;

namespace Aire.Services
{
    /// <summary>
    /// Defines the API command surface that the local TCP server dispatches to.
    /// Implementations are expected to handle thread marshaling internally
    /// (e.g. dispatching to the WPF UI thread) so callers can invoke methods
    /// from any thread without concern for affinity.
    /// </summary>
    internal interface IApiCommandHandler
    {
        Task<ApiStateSnapshot> ApiGetStateAsync();
        Task ShowMainWindowAsync();
        Task HideMainWindowAsync();
        Task ShowSettingsWindowAsync(string? tab = null);
        Task ShowBrowserWindowAsync();
        Task<List<ApiProviderSnapshot>> ApiListProvidersAsync();
        Task<ApiProviderSnapshot> ApiCreateProviderAsync(
            string? name,
            string type,
            string? apiKey,
            string? baseUrl,
            string model,
            bool isEnabled = true,
            string? color = null,
            bool selectAfterCreate = false,
            int? inheritCredentialsFromProviderId = null);
        Task<List<ConversationSummary>> ApiListConversationsAsync(string? search = null);
        Task<int> ApiCreateConversationAsync(string? title = null, int? providerId = null);
        Task<bool> ApiSelectConversationAsync(int conversationId);
        Task<bool> ApiDeleteConversationAsync(int conversationId);
        Task<bool> ApiRenameConversationAsync(int conversationId, string title);
        Task<List<Aire.Data.Message>> ApiGetMessagesAsync(int conversationId);
        Task<bool> ApiSetProviderAsync(int providerId);
        Task<bool> ApiSetProviderModelAsync(int providerId, string model);
        Task<bool> ApiSendMessageAsync(string text, int? conversationId = null);
        Task<bool> ApiSetAssistantModeAsync(string modeKey);
        Task<List<string>> ApiListAssistantModesAsync();
        Task<List<string>> ApiListToolCategoriesAsync();
        Task<bool> ApiSetToolCategoriesAsync(List<string>? categories);
        Task StopAiAsync();
        Task<ApiPendingApproval[]> ApiListPendingApprovalsAsync();
        Task<ApiPendingApproval?> ApiGetFirstPendingApprovalAsync();
        Task<bool> ApiSetPendingApprovalAsync(int index, bool approved);
        Task<ApiToolExecutionResult> ApiExecuteToolAsync(
            string tool,
            JsonElement parameters,
            bool waitForApproval,
            int approvalTimeoutSeconds);

        // ── UI state control ─────────────────────────────────────────────────
        /// <summary>Toggles or explicitly opens/closes the conversation sidebar.</summary>
        Task<bool> ApiToggleSidebarAsync(bool? open = null);
        /// <summary>Toggles or explicitly enables/disables voice output (TTS).</summary>
        Task<bool> ApiToggleVoiceOutputAsync(bool? enabled = null);
        /// <summary>Opens the in-chat search panel and optionally executes a query.</summary>
        Task<bool> ApiOpenSearchAsync(string? query = null);
        /// <summary>Navigates to the next or previous search result. direction: "next" | "prev"</summary>
        Task<bool> ApiNavigateSearchAsync(string direction);
        /// <summary>Closes the search panel and clears highlights.</summary>
        Task<bool> ApiCloseSearchAsync();
        /// <summary>Pins or unpins the window to the tray position.</summary>
        Task<bool> ApiPinWindowAsync(bool? pinned = null);

        // ── Orchestrator mode ────────────────────────────────────────────────
        /// <summary>Starts orchestrator mode with optional budget, goals, and tool categories, or stops it.</summary>
        Task<bool> ApiToggleOrchestratorModeAsync(
            bool? enabled = null,
            int? budget = null,
            List<string>? categories = null,
            List<string>? goals = null);
        Task<bool> ApiSetOrchestratorGoalsAsync(List<string> goals);
        /// <summary>Legacy alias kept for compatibility with older callers.</summary>
        Task<bool> ApiToggleAgentModeAsync(bool? enabled = null, int? budget = null, List<string>? categories = null);

        // ── Composer ─────────────────────────────────────────────────────────
        /// <summary>Sets the text in the message composer input box.</summary>
        Task<bool> ApiSetInputAsync(string text);
        /// <summary>Attaches a file or image from the given path to the next message.</summary>
        Task<bool> ApiAttachFileAsync(string filePath);
        /// <summary>Removes the currently attached file or image from the composer.</summary>
        Task<bool> ApiRemoveAttachmentAsync();

        // ── Conversation ─────────────────────────────────────────────────────
        /// <summary>Branches the current conversation from the specified message, discarding later messages.</summary>
        Task<bool> ApiBranchFromMessageAsync(int messageId);
        /// <summary>Branches the specified conversation at the requested message and selects the new branch.</summary>
        Task<int> ApiBranchConversationAsync(int conversationId, int upToMessageId);
    }
}
