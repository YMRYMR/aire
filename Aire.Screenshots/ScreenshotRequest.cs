namespace Aire.Screenshots;

internal sealed record ScreenshotRequest(
    string OutputPath,
    string? ExactTitle,
    string? TitleContains,
    string? ProcessName,
    int DelayMs,
    int Padding,
    bool ActivateWindow,
    bool UseActiveWindow,
    IReadOnlyList<UiAutomationAction>? Actions);
