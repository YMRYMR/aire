using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Aire.Data;
using Aire.Domain.Providers;
using Aire.Providers;
using Aire.Services;

namespace Aire.AppLayer.Api
{
    /// <summary>
    /// Owns the non-UI request and response shaping used by the local API surface.
    /// This keeps local-API control rules out of MainWindow event and state code.
    /// </summary>
    public sealed class LocalApiApplicationService
    {
        /// <summary>
        /// Result of creating a conversation through the local API.
        /// </summary>
        /// <param name="Title">Normalized conversation title that should be persisted.</param>
        /// <param name="SystemMessage">Initial system message shown after the conversation is created.</param>
        public sealed record ConversationCreationPlan(
            string Title,
            string SystemMessage);

        /// <summary>
        /// Creates the provider snapshots returned by the local API.
        /// </summary>
        /// <param name="providers">Providers loaded from storage.</param>
        /// <returns>Serializable provider snapshots for API callers.</returns>
        public List<ApiProviderSnapshot> BuildProviderSnapshots(IEnumerable<Provider> providers)
            => providers.Select(p => new ApiProviderSnapshot
            {
                Id = p.Id,
                Name = p.Name,
                Type = p.Type,
                DisplayType = p.DisplayType,
                Model = p.Model,
                IsEnabled = p.IsEnabled,
                Color = p.Color
            }).ToList();

        /// <summary>
        /// Normalizes the title and initial system message for a newly created conversation.
        /// </summary>
        /// <param name="providerName">Display name of the provider that will own the conversation.</param>
        /// <param name="requestedTitle">Optional title supplied by the API caller.</param>
        /// <returns>The normalized creation plan.</returns>
        public ConversationCreationPlan BuildConversationCreationPlan(string providerName, string? requestedTitle)
        {
            var title = string.IsNullOrWhiteSpace(requestedTitle) ? "New Chat" : requestedTitle.Trim();
            return new ConversationCreationPlan(
                title,
                $"New conversation started with {providerName}.");
        }

        /// <summary>
        /// Resolves the provider that should become active when an API caller selects a conversation.
        /// </summary>
        /// <param name="conversation">Conversation being selected, if it exists.</param>
        /// <param name="providers">Providers currently available in the main picker.</param>
        /// <returns>The matching provider, or <see langword="null"/> when no matching provider is available.</returns>
        public Provider? ResolveConversationProvider(Aire.Data.Conversation? conversation, IEnumerable<Provider> providers)
            => conversation == null
                ? null
                : providers.FirstOrDefault(p => p.Id == conversation.ProviderId);

        /// <summary>
        /// Normalizes a model string supplied by the local API.
        /// </summary>
        /// <param name="model">Raw model text from the API request.</param>
        /// <returns>The trimmed model name, or <see langword="null"/> when it is blank.</returns>
        public string? NormalizeProviderModel(string? model)
        {
            var trimmed = model?.Trim() ?? string.Empty;
            return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
        }

        /// <summary>
        /// Normalizes a provider type string supplied by an API caller.
        /// </summary>
        /// <param name="type">Raw provider type text.</param>
        /// <returns>The canonical provider type identifier used by persistence and activation.</returns>
        public string NormalizeProviderType(string? type)
            => ProviderCatalog.NormalizeType(type);

        /// <summary>
        /// Builds a normalized tool-call request from the raw local API payload.
        /// </summary>
        /// <param name="tool">Requested tool name.</param>
        /// <param name="parameters">Tool parameters supplied by the API caller.</param>
        /// <returns>The normalized tool request used by the approval and execution workflows.</returns>
        public ToolCallRequest BuildToolRequest(string tool, JsonElement parameters)
        {
            var normalized = ToolExecutionService.NormalizeToolName(tool);
            var paramElement = parameters.ValueKind == JsonValueKind.Object
                ? parameters.Clone()
                : JsonDocument.Parse("{}").RootElement.Clone();

            return new ToolCallRequest
            {
                Tool = normalized,
                Parameters = paramElement,
                Description = normalized,
                RawJson = JsonSerializer.Serialize(new
                {
                    tool = normalized,
                    parameters = JsonSerializer.Deserialize<JsonElement>(paramElement.GetRawText())
                })
            };
        }

        /// <summary>
        /// Creates the standardized pending-approval result returned to local API callers.
        /// </summary>
        /// <param name="toolName">Normalized tool name awaiting approval.</param>
        /// <param name="approvalIndex">Index that callers can later approve or deny.</param>
        /// <returns>The normalized pending result.</returns>
        public ApiToolExecutionResult BuildPendingApprovalResult(string toolName, int? approvalIndex)
            => new()
            {
                Status = "pending_approval",
                TextResult = $"Approval required for tool '{toolName}'.",
                PendingApprovalIndex = approvalIndex
            };

        /// <summary>
        /// Converts a completed tool execution into the local API response shape.
        /// </summary>
        /// <param name="result">Completed tool execution result.</param>
        /// <returns>The normalized API result.</returns>
        public ApiToolExecutionResult BuildCompletedToolResult(ToolExecutionResult result)
            => new()
            {
                Status = "completed",
                TextResult = result.TextResult,
                DirectoryPath = result.DirectoryListing?.Path,
                DirectorySummary = result.DirectoryListing?.Summary,
                ScreenshotPath = result.ScreenshotPath
            };

        /// <summary>
        /// Builds the local API state snapshot from the current application and window state.
        /// </summary>
        /// <param name="localApiPort">Configured local API port.</param>
        /// <param name="isStartupReady">Whether startup has completed successfully.</param>
        /// <param name="isMainWindowVisible">Whether the main window is currently visible.</param>
        /// <param name="isSettingsOpen">Whether the settings window is currently open.</param>
        /// <param name="isBrowserOpen">Whether the internal browser window is currently open.</param>
        /// <param name="apiAccessEnabled">Whether local API access is enabled.</param>
        /// <param name="hasApiAccessToken">Whether a local API access token is configured.</param>
        /// <param name="currentConversationId">Currently active conversation id, if any.</param>
        /// <param name="provider">Currently selected provider, if any.</param>
        /// <param name="selectedWindow">Currently selected top-level window, if any.</param>
        /// <param name="pendingApprovals">Current number of pending approval prompts.</param>
        /// <returns>The normalized API state snapshot.</returns>
        public ApiStateSnapshot BuildStateSnapshot(
            int localApiPort,
            bool isStartupReady,
            bool isMainWindowVisible,
            bool isSettingsOpen,
            bool isBrowserOpen,
            bool apiAccessEnabled,
            bool hasApiAccessToken,
            int? currentConversationId,
            Provider? provider,
            TopLevelWindowInfo? selectedWindow,
            int pendingApprovals)
            => new()
            {
                LocalApiPort = localApiPort,
                IsStartupReady = isStartupReady,
                IsMainWindowVisible = isMainWindowVisible,
                IsSettingsOpen = isSettingsOpen,
                IsBrowserOpen = isBrowserOpen,
                ApiAccessEnabled = apiAccessEnabled,
                HasApiAccessToken = hasApiAccessToken,
                CurrentConversationId = currentConversationId,
                CurrentProviderId = provider?.Id,
                CurrentProviderName = provider?.Name,
                CurrentProviderModel = provider?.Model,
                SelectedWindowId = selectedWindow?.WindowId,
                SelectedWindowTitle = selectedWindow?.Title,
                SelectedWindowProcessName = selectedWindow?.ProcessName,
                PendingApprovals = pendingApprovals
            };

    }
}
