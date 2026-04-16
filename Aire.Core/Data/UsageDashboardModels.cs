namespace Aire.Data
{
    /// <summary>
    /// Snapshot of the token-usage dashboard.
    /// </summary>
    public sealed record UsageDashboardSnapshot(
        long TotalTokens,
        int ProviderCount,
        int ConversationCount,
        int AssistantMessageCount,
        IReadOnlyList<ProviderUsageSummary> Providers,
        IReadOnlyList<ConversationUsageSummary> Conversations,
        IReadOnlyList<UsageTrendSeries> TrendSeries);

    /// <summary>
    /// Aggregated token usage for one configured provider.
    /// </summary>
    public sealed record ProviderUsageSummary(
        int ProviderId,
        string ProviderName,
        string ProviderType,
        string Model,
        string Color,
        bool IsEnabled,
        long TokensUsed,
        int ConversationCount,
        int AssistantMessageCount,
        DateTime? LastUsedAt);

    /// <summary>
    /// Aggregated token usage for one conversation.
    /// </summary>
    public sealed record ConversationUsageSummary(
        int ProviderId,
        int ConversationId,
        string Title,
        string ProviderName,
        string ProviderType,
        string Model,
        string Color,
        DateTime UpdatedAt,
        long TokensUsed,
        int AssistantMessageCount);

    /// <summary>
    /// Historical token usage for one provider/model combination.
    /// </summary>
    public sealed record UsageTrendSeries(
        int ProviderId,
        string ProviderName,
        string ProviderType,
        string Model,
        string Color,
        long TotalTokens,
        IReadOnlyList<UsageTrendPoint> Points)
    {
        public string Label => string.IsNullOrWhiteSpace(Model) ? ProviderName : $"{ProviderName} · {Model}";
    }

    /// <summary>
    /// One bucket in a provider/model usage trend.
    /// </summary>
    public sealed record UsageTrendPoint(DateOnly Bucket, long TokensUsed);
}
