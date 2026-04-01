using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows;
using WpfFontFamily = System.Windows.Media.FontFamily;
using WpfCursors = System.Windows.Input.Cursors;
using WpfBrushes = System.Windows.Media.Brushes;

namespace Aire.UI;

public static partial class LinkedText
{
    private static void BuildInlines(InlineCollection inlines, string text)
    {
        var parts = text.Split('\n');
        for (int i = 0; i < parts.Length; i++)
        {
            if (i > 0) inlines.Add(new LineBreak());
            if (!string.IsNullOrEmpty(parts[i]))
                AddInlineSegment(inlines, parts[i]);
        }
    }

    private static void AddInlineSegment(InlineCollection inlines, string text)
    {
        int pos = 0;

        foreach (System.Text.RegularExpressions.Match m in InlineRegex.Matches(text))
        {
            if (m.Index > pos)
                inlines.Add(new Run(text.Substring(pos, m.Index - pos)));

            if (m.Groups["b"].Success)
            {
                inlines.Add(new Bold(new Run(m.Groups["b"].Value)));
            }
            else if (m.Groups["i"].Success)
            {
                inlines.Add(new Italic(new Run(m.Groups["i"].Value)));
            }
            else if (m.Groups["c"].Success)
            {
                var run = new Run(m.Groups["c"].Value)
                {
                    FontFamily = new WpfFontFamily("Consolas, Courier New"),
                };
                run.SetResourceReference(TextElement.ForegroundProperty, "CodeForegroundBrush");
                run.SetResourceReference(TextElement.BackgroundProperty, "CodeBackgroundBrush");
                inlines.Add(run);
            }
            else if (m.Groups["u"].Success)
            {
                var url = m.Value.TrimEnd(TrailingJunk);
                var linkTb = new TextBlock
                {
                    Text = url,
                    TextWrapping = TextWrapping.Wrap,
                    Cursor = WpfCursors.Hand,
                    Background = WpfBrushes.Transparent,
                    Tag = LinkTag + url,
                };
                linkTb.SetResourceReference(TextBlock.ForegroundProperty, "LinkBrush");
                linkTb.MouseEnter += (_, _) => Mouse.OverrideCursor = WpfCursors.Hand;
                linkTb.MouseLeave += (_, _) => Mouse.OverrideCursor = null;

                inlines.Add(new InlineUIContainer(linkTb) { BaselineAlignment = BaselineAlignment.TextBottom });

                if (url.Length < m.Value.Length)
                    inlines.Add(new Run(m.Value.Substring(url.Length)));
            }

            pos = m.Index + m.Length;
        }

        if (pos < text.Length)
            inlines.Add(new Run(text.Substring(pos)));
    }
}
