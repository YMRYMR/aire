using System.Text.RegularExpressions;

namespace Aire.AppLayer.Chat;

/// <summary>
/// Converts a raw streaming provider transcript into the text that should be shown
/// live in the chat UI, hiding Aire tool-call blocks until the turn completes.
/// </summary>
public sealed class StreamingResponsePresentationService
{
    private static readonly Regex CompleteToolCallRegex = new(
        "<tool_call>.*?</tool_call>",
        RegexOptions.Singleline | RegexOptions.Compiled);

    public string GetVisibleText(string rawContent)
    {
        if (string.IsNullOrEmpty(rawContent))
            return string.Empty;

        var visible = CompleteToolCallRegex.Replace(rawContent, string.Empty);
        var openIndex = visible.LastIndexOf("<tool_call", System.StringComparison.Ordinal);
        if (openIndex >= 0)
            visible = visible[..openIndex];

        return visible.TrimEnd();
    }

    /// <summary>
    /// Returns the visible streaming text as it arrives.
    /// Partial words are intentionally included — they produce the natural
    /// character-by-character typing effect. Only incomplete tool-call blocks
    /// are withheld (handled by <see cref="GetVisibleText"/>).
    /// </summary>
    public string GetStreamingPreviewText(string rawContent)
        => GetVisibleText(rawContent);
}
