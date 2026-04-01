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
