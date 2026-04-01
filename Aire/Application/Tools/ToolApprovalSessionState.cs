namespace Aire.AppLayer.Tools
{
    /// <summary>
    /// Tracks the current short-lived approval bypass sessions for mouse and keyboard tools.
    /// </summary>
    public sealed record ToolApprovalSessionState(
        bool MouseSessionActive,
        System.DateTime MouseSessionExpiry,
        bool KeyboardSessionActive,
        System.DateTime KeyboardSessionExpiry);
}
