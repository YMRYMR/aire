namespace Aire.Screenshots;

internal sealed class ScreenshotPlan
{
    public List<UiAutomationAction> SetupActions { get; init; } = [];
    public List<ScreenshotRequest> Screenshots { get; init; } = [];
}
