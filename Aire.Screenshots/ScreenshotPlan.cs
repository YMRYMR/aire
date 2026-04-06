namespace Aire.Screenshots;

internal sealed class LanguageBatch
{
    /// <summary>
    /// Root folder where language sub-folders will be created.
    /// e.g. "Aire/Assets/Help" → screenshots saved to "Aire/Assets/Help/en/", "Aire/Assets/Help/es/", …
    /// </summary>
    public string OutputFolder { get; init; } = string.Empty;

    /// <summary>Language codes to iterate, in order.</summary>
    public List<string> Languages { get; init; } = [];

    /// <summary>Milliseconds to wait after switching language before taking the first screenshot.</summary>
    public int SwitchDelayMs { get; init; } = 1000;

    /// <summary>
    /// Actions run before switching to each language (including the first).
    /// Use try-invoke actions to close any windows left open by the previous batch.
    /// </summary>
    public List<UiAutomationAction> PreBatchActions { get; init; } = [];
}

internal sealed class ScreenshotPlan
{
    public List<UiAutomationAction> SetupActions { get; init; } = [];
    public LanguageBatch? LanguageBatch { get; init; }
    public List<ScreenshotRequest> Screenshots { get; init; } = [];
}
