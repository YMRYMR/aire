namespace Aire.Services
{
    public sealed class ApiTraceEntry
    {
        public long Id { get; set; }
        public DateTimeOffset Timestamp { get; set; }
        public string Kind { get; set; } = string.Empty;
        public string Method { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public bool? Success { get; set; }
        public object? Data { get; set; }
    }

    public sealed class ApiStateSnapshot
    {
        public int LocalApiPort { get; set; }
        public bool IsStartupReady { get; set; }
        public bool IsMainWindowVisible { get; set; }
        public bool IsSettingsOpen { get; set; }
        public bool IsBrowserOpen { get; set; }
        public bool ApiAccessEnabled { get; set; }
        public bool HasApiAccessToken { get; set; }
        public int? CurrentConversationId { get; set; }
        public int? CurrentProviderId { get; set; }
        public string? CurrentProviderName { get; set; }
        public string? CurrentProviderModel { get; set; }
        public int MessageCount { get; set; }
        public bool IsSidebarVisible { get; set; }
        public bool IsSearchOpen { get; set; }
        public bool IsAgentModeActive { get; set; }
        public bool IsOrchestratorModeActive { get; set; }
        public int OrchestratorHeartbeatCount { get; set; }
        public string? OrchestratorStopReason { get; set; }
        public List<string> OrchestratorGoals { get; set; } = new();
        public bool IsVoiceOutputEnabled { get; set; }
        public bool IsWindowPinned { get; set; }
        public string? InputText { get; set; }
        public bool HasAttachment { get; set; }
        public string AssistantMode { get; set; } = "general";
        public List<string> ActiveToolCategories { get; set; } = new();
        public string? SelectedWindowId { get; set; }
        public string? SelectedWindowTitle { get; set; }
        public string? SelectedWindowProcessName { get; set; }
        public int PendingApprovals { get; set; }
    }

    public sealed class ApiPendingApproval
    {
        public int Index { get; set; }
        public string Tool { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string RawJson { get; set; } = string.Empty;
        public string Timestamp { get; set; } = string.Empty;
    }

    public sealed class ApiToolExecutionResult
    {
        public string Status { get; set; } = "completed";
        public string TextResult { get; set; } = string.Empty;
        public int? PendingApprovalIndex { get; set; }
        public string? DirectoryPath { get; set; }
        public string? DirectorySummary { get; set; }
        public string? ScreenshotPath { get; set; }
    }

    public sealed class ApiProviderSnapshot
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string DisplayType { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public bool IsEnabled { get; set; }
        public string Color { get; set; } = "#007ACC";
    }
}
