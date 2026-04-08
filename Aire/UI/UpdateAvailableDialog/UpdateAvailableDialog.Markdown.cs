using System;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using Aire.Services;

namespace Aire.UI;

public partial class UpdateAvailableDialog
{
    // ── Markdown renderer ────────────────────────────────────────────────────
    // Supports the subset of Markdown that GitHub release notes typically use:
    //   ## / ###  headings   |   **bold**   |   `code`   |   bare URLs
    //   * / -     bullets    |   blank lines as paragraph breaks

    private static readonly Regex InlinePattern = new(
        @"(\*\*(?<bold>.+?)\*\*|`(?<code>[^`]+)`|(?<url>https?://\S+))",
        RegexOptions.Compiled);

    private static void RenderNotes(System.Windows.Controls.TextBlock tb, string markdown)
    {
        tb.Inlines.Clear();

        var lines = markdown.Split('\n');
        bool firstLine = true;

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd('\r');

            if (!firstLine)
                tb.Inlines.Add(new LineBreak());
            firstLine = false;

            // Heading (## or ###)
            if (line.StartsWith("### ", StringComparison.Ordinal))
            {
                tb.Inlines.Add(new Run(line[4..].Trim()) { FontWeight = System.Windows.FontWeights.SemiBold });
                continue;
            }
            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                tb.Inlines.Add(new Run(line[3..].Trim()) { FontWeight = System.Windows.FontWeights.Bold });
                continue;
            }

            // Blank line -> extra visual gap
            if (string.IsNullOrWhiteSpace(line))
            {
                tb.Inlines.Add(new Run(" "));
                continue;
            }

            // Bullet point
            string bulletPrefix = string.Empty;
            string content = line;
            if (line.StartsWith("* ", StringComparison.Ordinal) || line.StartsWith("- ", StringComparison.Ordinal))
            {
                bulletPrefix = "• ";
                content = line[2..];
            }
            else if (line.StartsWith("  * ", StringComparison.Ordinal) || line.StartsWith("  - ", StringComparison.Ordinal))
            {
                bulletPrefix = "    • ";
                content = line[4..];
            }

            if (bulletPrefix.Length > 0)
                tb.Inlines.Add(new Run(bulletPrefix));

            AddInlineMarkdown(tb.Inlines, content);
        }
    }

    private static void AddInlineMarkdown(InlineCollection inlines, string text)
    {
        int lastIndex = 0;
        foreach (Match m in InlinePattern.Matches(text))
        {
            // Plain text before this match
            if (m.Index > lastIndex)
                inlines.Add(new Run(text[lastIndex..m.Index]));

            if (m.Groups["bold"].Success)
            {
                inlines.Add(new Run(m.Groups["bold"].Value) { FontWeight = System.Windows.FontWeights.SemiBold });
            }
            else if (m.Groups["code"].Success)
            {
                inlines.Add(new Run(m.Groups["code"].Value)
                {
                    FontFamily = new System.Windows.Media.FontFamily("Consolas, Courier New"),
                    Foreground = (System.Windows.Media.Brush)System.Windows.Application.Current.Resources["CodeForegroundBrush"],
                });
            }
            else if (m.Groups["url"].Success)
            {
                var uriStr = m.Groups["url"].Value.TrimEnd('.', ',', ')');
                if (Uri.TryCreate(uriStr, UriKind.Absolute, out var uri))
                {
                    var link = new Hyperlink(new Run(uriStr)) { NavigateUri = uri };
                    link.RequestNavigate += (_, e) =>
                    {
                        try { Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true }); }
                        catch (Exception ex) { AppLogger.Warn("App.Update", "Failed to open link", ex); }
                    };
                    inlines.Add(link);
                }
                else
                {
                    inlines.Add(new Run(m.Value));
                }
            }

            lastIndex = m.Index + m.Length;
        }

        // Remaining plain text
        if (lastIndex < text.Length)
            inlines.Add(new Run(text[lastIndex..]));
    }
}
