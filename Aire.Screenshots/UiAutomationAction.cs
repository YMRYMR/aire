namespace Aire.Screenshots;

internal sealed class UiAutomationAction
{
    public string Kind { get; init; } = string.Empty;
    public string? ExactTitle { get; init; }
    public string? TitleContains { get; init; }
    public string? ProcessName { get; init; }
    public string? AutomationId { get; init; }
    public string? Name { get; init; }
    public string? ControlType { get; init; }
    public string? ExecutablePath { get; init; }
    public string? Arguments { get; init; }
    public int DelayMs { get; init; }
}
