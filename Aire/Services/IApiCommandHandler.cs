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
        Task ShowSettingsWindowAsync();
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
    }
}
