using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Aire.Services;
using System.Diagnostics;
using WpfRichTextBox = System.Windows.Controls.RichTextBox;
using WpfColor       = System.Windows.Media.Color;
using WpfFontFamily  = System.Windows.Media.FontFamily;
using WpfCursors     = System.Windows.Input.Cursors;
using WpfBrushes     = System.Windows.Media.Brushes;

namespace Aire.UI;

/// <summary>
/// Attached property that populates a TextBlock or RichTextBox from a Markdown string,
/// rendering mixed blocks such as bold, italic, inline code, fenced code blocks,
/// headings, lists, file listings, and links.
/// Usage in XAML:  ui:LinkedText.Text="{Binding Text}"
/// </summary>
public static partial class LinkedText
{
    // ── Regexes ──────────────────────────────────────────────────────────────

    // Inline patterns tried left-to-right in alternation order.
    // Bold must come before italic so **x** doesn't partially match as italic.
    private static readonly Regex InlineRegex = new(
        @"\*\*(?<b>[^*\n]+?)\*\*"              +   // **bold**
        @"|\*(?<i>[^*\n]+)\*"                  +   // *italic*
        @"|`(?<c>[^`\n]+)`"                    +   // `inline code`
        @"|(?<u>(?:https?|file):///[^\s<>""'\]\[)(]+|https?://[^\s<>""'\]\[)(]+)", // bare URL / file URL
        RegexOptions.Compiled);

    private static readonly Regex HeadingRegex  = new(@"^(#{1,6})\s+(.+)$");
    private static readonly Regex BulletRegex   = new(@"^\s*[-*+]\s+(.+)$");
    private static readonly Regex OrderedRegex  = new(@"^\s*\d+[.)]\s+(.+)$");
    private static readonly Regex FileSectionHeaderRegex = new(
        @"^\*{0,2}(Folders?|Files?|Directories?)\s*[\(\*\:]",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex FileListItemRegex = new(
        @"^(\-|\*|\d+\.)\s",
        RegexOptions.Compiled);
    private static readonly Regex FileBacktickNameRegex = new(
        @"`([^`]+)`(?:\s*\(([^)]*)\))?",
        RegexOptions.Compiled);
    private static readonly Regex FilePlainNameRegex = new(
        @"^([\w.\-][\w.\-\s]*(?:/|\\|\.\w+))(?:\s*[-–(]\s*([\d.,]+\s*[KMGT]?B)\)?)?",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex FileOutputEntryRegex = new(
        @"^\[(DIR|FILE)\]\s+(?<name>.+?)(?:\s+\((?<meta>.*)\))?$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex FileSubHeaderRegex = new(
        @"^[*#].+:[\*]*$",
        RegexOptions.Compiled);
    private static readonly char[] TrailingJunk = ['.', ',', ';', ':', '!', '?', ')', ']'];

    // Tag prefix used to mark link TextBlocks so RtbLinkClick can identify them.
    private const string LinkTag = "link:";

    // Tracks which RichTextBoxes already have the click hook installed.
    private static readonly ConditionalWeakTable<WpfRichTextBox, object> _hookInstalled = new();

    // ── Attached property ─────────────────────────────────────────────────────

    public static readonly DependencyProperty TextProperty =
        DependencyProperty.RegisterAttached(
            "Text",
            typeof(string),
            typeof(LinkedText),
            new PropertyMetadata(null, OnTextChanged));

    public static string? GetText(DependencyObject d) => (string?)d.GetValue(TextProperty);
    public static void SetText(DependencyObject d, string? value) => d.SetValue(TextProperty, value);

    private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var text = (string?)e.NewValue ?? string.Empty;

        if (d is TextBlock tb)
        {
            tb.Inlines.Clear();
            if (!string.IsNullOrEmpty(text))
                BuildInlines(tb.Inlines, text);
            return;
        }

        if (d is WpfRichTextBox rtb)
        {
            // Install a single click hook per RichTextBox.
            // handledEventsToo:true is essential — the RichTextBox class handler fires
            // during the PreviewMouseLeftButtonDown tunnel and marks the event handled
            // before it can reach child elements.  Our handler then checks OriginalSource
            // (which always points to the innermost visual element, regardless of Handled).
            if (!_hookInstalled.TryGetValue(rtb, out _))
            {
                _hookInstalled.Add(rtb, new object());
                rtb.AddHandler(
                    UIElement.PreviewMouseLeftButtonDownEvent,
                    new MouseButtonEventHandler(RtbLinkClick),
                    handledEventsToo: true);
            }
            rtb.Document = BuildDocument(text);
        }
    }

    private static void RtbLinkClick(object sender, MouseButtonEventArgs e)
    {
        // Walk up from the deepest visual element under the pointer.
        // VisualTreeHelper.GetParent only accepts Visual/Visual3D — ContentElements
        // (Run, Paragraph, …) are FrameworkContentElements and will throw if passed in.
        // Guard every step so text-selection clicks never crash.
        var el = e.OriginalSource as DependencyObject;
        while (el is not null and not WpfRichTextBox)
        {
            if (el is FrameworkElement { Tag: string tag } && tag.StartsWith(LinkTag))
            {
                OpenUrl(tag.Substring(LinkTag.Length));
                e.Handled = true;
                return;
            }
            // Only Visual elements can be walked via VisualTreeHelper.
            if (el is not Visual and not System.Windows.Media.Media3D.Visual3D)
                break;
            el = VisualTreeHelper.GetParent(el);
        }
    }

    // ── FlowDocument builder ──────────────────────────────────────────────────

    private static FlowDocument BuildDocument(string text)
    {
        var doc = new FlowDocument
        {
            PagePadding = new Thickness(0),
            LineHeight  = double.NaN,
        };

        if (string.IsNullOrEmpty(text))
        {
            doc.Blocks.Add(new Paragraph { Margin = new Thickness(0) });
            return doc;
        }

        foreach (var block in BuildBlocks(text))
            doc.Blocks.Add(block);

        if (doc.Blocks.Count == 0)
            doc.Blocks.Add(new Paragraph { Margin = new Thickness(0) });

        return doc;
    }

    // ── Block-level pipeline ──────────────────────────────────────────────────

    private static IEnumerable<Block> BuildBlocks(string text)
    {
        bool firstBlock = true;
        foreach (var (content, isCode, lang) in SplitCodeFences(text))
        {
            if (isCode)
            {
                yield return CodeFenceBlock(content, lang);
                firstBlock = false;
                continue;
            }

            foreach (var block in ParseTextBlocks(content, firstBlock))
            {
                yield return block;
                firstBlock = false;
            }
        }
    }

    // Split text into fenced-code segments and plain-text segments.
    private static List<(string content, bool isCode, string lang)> SplitCodeFences(string text)
    {
        var result = new List<(string, bool, string)>();
        var lines  = text.Split('\n');
        var buf    = new List<string>();
        bool inCode   = false;
        string codeLang = "";

        foreach (var line in lines)
        {
            var trimmed = line.TrimStart();
            if (!inCode && trimmed.StartsWith("```"))
            {
                if (buf.Count > 0)
                {
                    result.Add((string.Join("\n", buf), false, ""));
                    buf.Clear();
                }
                codeLang = trimmed.Substring(3).Trim();
                inCode   = true;
            }
            else if (inCode && trimmed == "```")
            {
                result.Add((string.Join("\n", buf), true, codeLang));
                buf.Clear();
                inCode   = false;
                codeLang = "";
            }
            else
            {
                buf.Add(line);
            }
        }

        if (buf.Count > 0)
            result.Add((string.Join("\n", buf), inCode, codeLang));

        return result;
    }

    private static void OpenUrl(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
            uri.IsFile &&
            !string.IsNullOrWhiteSpace(uri.LocalPath))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = uri.LocalPath,
                UseShellExecute = true
            });
            return;
        }

        WebViewWindow.OpenInNewTab(url);
    }
}
