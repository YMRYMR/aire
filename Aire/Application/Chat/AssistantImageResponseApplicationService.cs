using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text.Json;

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

        private const string ImageMetadataPrefix = "<!--aire-images:";
        private const string ImageMetadataSuffix = "-->";

        public sealed record ParsedAssistantContent(string Text, IReadOnlyList<string> ImageReferences);

        public ParsedAssistantContent Parse(string rawContent)
        {
            if (string.IsNullOrWhiteSpace(rawContent))
                return new ParsedAssistantContent(string.Empty, Array.Empty<string>());

            var imageReferences = new List<string>();
            var text = StripPersistedImageMetadata(rawContent, imageReferences);

            foreach (Match markdownMatch in MarkdownImageRegex.Matches(text))
            {
                var candidate = markdownMatch.Groups["url"].Value.Trim();
                if (!string.IsNullOrWhiteSpace(candidate))
                    imageReferences.Add(candidate);
            }

            if (imageReferences.Count > 0)
            {
                text = MarkdownImageRegex.Replace(text, string.Empty);
            }
            else
            {
                foreach (Match urlMatch in StandaloneImageUrlRegex.Matches(text.Trim()))
                {
                    var candidate = urlMatch.Groups["url"].Value.Trim();
                    if (!string.IsNullOrWhiteSpace(candidate))
                        imageReferences.Add(candidate);
                }

                if (imageReferences.Count > 0)
                    text = StandaloneImageUrlRegex.Replace(text, string.Empty);
            }

            text = text.Replace("\r\n\r\n\r\n", "\r\n\r\n", StringComparison.Ordinal)
                       .Trim();

            return new ParsedAssistantContent(
                text,
                imageReferences
                    .Where(reference => !string.IsNullOrWhiteSpace(reference))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray());
        }

        public string BuildPersistedContent(string text, IReadOnlyList<string> imageReferences)
        {
            var normalizedText = string.IsNullOrWhiteSpace(text) ? string.Empty : text.Trim();
            if (imageReferences == null || imageReferences.Count == 0)
                return normalizedText;

            var encodedReferences = JsonSerializer.Serialize(
                imageReferences.Where(reference => !string.IsNullOrWhiteSpace(reference)).Distinct(StringComparer.OrdinalIgnoreCase));
            if (string.IsNullOrWhiteSpace(encodedReferences) || encodedReferences == "[]")
                return normalizedText;

            return string.IsNullOrWhiteSpace(normalizedText)
                ? $"{ImageMetadataPrefix}{encodedReferences}{ImageMetadataSuffix}"
                : $"{normalizedText}\r\n\r\n{ImageMetadataPrefix}{encodedReferences}{ImageMetadataSuffix}";
        }

        private static string StripPersistedImageMetadata(string rawContent, List<string> imageReferences)
        {
            var text = rawContent;
            var start = text.IndexOf(ImageMetadataPrefix, StringComparison.Ordinal);
            if (start < 0)
                return text;

            var end = text.IndexOf(ImageMetadataSuffix, start, StringComparison.Ordinal);
            if (end < 0)
                return text;

            var jsonStart = start + ImageMetadataPrefix.Length;
            var json = text[jsonStart..end];
            try
            {
                var parsed = JsonSerializer.Deserialize<string[]>(json);
                if (parsed != null)
                    imageReferences.AddRange(parsed.Where(reference => !string.IsNullOrWhiteSpace(reference)));
            }
            catch
            {
                // Ignore malformed metadata and leave the text visible rather than failing transcript reconstruction.
                return text;
            }

            return text.Remove(start, (end + ImageMetadataSuffix.Length) - start).Trim();
        }
    }
}
