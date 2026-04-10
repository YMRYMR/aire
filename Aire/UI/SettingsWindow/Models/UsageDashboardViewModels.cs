namespace Aire.UI.Settings.Models
{
    using System.Windows.Media;

    /// <summary>
    /// View model used by the usage dashboard provider list.
    /// </summary>
    internal sealed record UsageProviderRowViewModel(
        int ProviderId,
        string ProviderName,
        string ProviderDetail,
        string Color,
        string TokensText,
        string SpendText,
        string UsageDetailText);

    /// <summary>
    /// View model used by the usage dashboard conversation list.
    /// </summary>
    internal sealed record UsageConversationRowViewModel(
        int ConversationId,
        string Title,
        string ConversationDetail,
        string Color,
        string TokensText,
        string SpendText,
        string UsageDetailText);

    /// <summary>
    /// View model used by the usage trend legend.
    /// </summary>
    internal sealed record UsageTrendLegendItemViewModel(
        string Label,
        string TokensText,
        Brush ColorBrush);
}
