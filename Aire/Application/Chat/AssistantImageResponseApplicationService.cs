using System;
using System.Text.RegularExpressions;

namespace Aire.AppLayer.Chat
{
    /// <summary>
    /// Extracts assistant-returned image references from provider text responses and normalizes the visible text.
    /// </summary>
    public sealed class AssistantImageResponseApplicationService
    {
        private static readonly Regex MarkdownImageRegex = new(
            @"!\[[^\]]*\]\((?<url>[^)\s]+)\)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex StandaloneImageUrlRegex = new(
            @"^(?<url>https?://\S+\.(png|jpg|jpeg|gif|bmp|webp)(\?\S*)?)$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline);

        public sealed record ParsedAssistantContent(string Text, string? ImageReference);

        public ParsedAssistantContent Parse(string rawContent)
        {
            if (string.IsNullOrWhiteSpace(rawContent))
                return new ParsedAssistantContent(string.Empty, null);

            string? imageReference = null;
            string text = rawContent;

            var markdownMatch = MarkdownImageRegex.Match(text);
            if (markdownMatch.Success)
            {
                imageReference = markdownMatch.Groups["url"].Value.Trim();
                text = MarkdownImageRegex.Replace(text, string.Empty, 1);
            }
            else
            {
                var urlMatch = StandaloneImageUrlRegex.Match(text.Trim());
                if (urlMatch.Success)
                {
                    imageReference = urlMatch.Groups["url"].Value.Trim();
                    text = string.Empty;
                }
            }

            text = text.Replace("\r\n\r\n\r\n", "\r\n\r\n", StringComparison.Ordinal)
                       .Trim();

            return new ParsedAssistantContent(text, string.IsNullOrWhiteSpace(imageReference) ? null : imageReference);
        }
    }
}
