using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Aire.Services;
using WpfColor = System.Windows.Media.Color;
using WpfFontFamily = System.Windows.Media.FontFamily;

namespace Aire.UI;

public static partial class LinkedText
{
    private static IEnumerable<Block> ParseTextBlocks(string text, bool isFirstInDoc)
    {
        var blocks = new List<Block>();
        var paraLines = new List<string>();
        List<ListItem>? listItems = null;
        bool listOrdered = false;
        var fileLines = new List<string>();
        bool inFileListing = false;

        void FlushParagraph()
        {
            if (paraLines.Count == 0) return;
            var content = string.Join("\n", paraLines).Trim('\n', '\r');
            paraLines.Clear();
            if (string.IsNullOrWhiteSpace(content)) return;

            double topMargin = (blocks.Count == 0 && isFirstInDoc) ? 0 : 5;
            var para = new Paragraph { Margin = new Thickness(0, topMargin, 0, 0) };
            BuildInlines(para.Inlines, content);
            blocks.Add(para);
        }

        void FlushList()
        {
            if (listItems == null) return;
            double topMargin = (blocks.Count == 0 && isFirstInDoc) ? 0 : 4;
            var lst = new System.Windows.Documents.List
            {
                MarkerStyle = listOrdered ? TextMarkerStyle.Decimal : TextMarkerStyle.Disc,
                Margin = new Thickness(18, topMargin, 0, 0),
                Padding = new Thickness(4, 0, 0, 0),
            };
            foreach (var item in listItems)
                lst.ListItems.Add(item);
            blocks.Add(lst);
            listItems = null;
        }

        void FlushFileListing()
        {
            if (!inFileListing || fileLines.Count == 0)
                return;

            var listing = ParseListingFromLines(fileLines);
            if (listing != null)
                blocks.Add(FileListingBlock(listing, blocks.Count == 0 && isFirstInDoc));

            fileLines.Clear();
            inFileListing = false;
        }

        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.TrimEnd();
            var trimmed = line.TrimStart();
            var isFileLineStart = IsFileSectionHeader(trimmed) ||
                                  IsFileListItem(trimmed) ||
                                  trimmed.StartsWith("|", StringComparison.Ordinal);
            var isFileContinuation = IsListingSubHeader(trimmed);
            var isFileLine = isFileLineStart || (inFileListing && isFileContinuation);

            if (inFileListing && !string.IsNullOrWhiteSpace(line) && !isFileLine)
            {
                FlushFileListing();
            }

            var hm = HeadingRegex.Match(line);
            if (hm.Success)
            {
                FlushList();
                FlushParagraph();
                FlushFileListing();
                int level = hm.Groups[1].Length;
                double topMargin = (blocks.Count == 0 && isFirstInDoc) ? 0 : 7;
                var hPara = new Paragraph
                {
                    FontWeight = FontWeights.SemiBold,
                    FontSize = level == 1 ? 15 : level == 2 ? 13.5 : 12.5,
                    Margin = new Thickness(0, topMargin, 0, 2),
                };
                BuildInlines(hPara.Inlines, hm.Groups[2].Value);
                blocks.Add(hPara);
                continue;
            }

            var bm = BulletRegex.Match(line);
            if (bm.Success)
            {
                FlushParagraph();
                FlushFileListing();
                if (listItems == null) { listOrdered = false; listItems = new(); }
                var itemPara = new Paragraph { Margin = new Thickness(0) };
                BuildInlines(itemPara.Inlines, bm.Groups[1].Value);
                listItems.Add(new ListItem(itemPara));
                continue;
            }

            var om = OrderedRegex.Match(line);
            if (om.Success)
            {
                FlushParagraph();
                FlushFileListing();
                if (listItems == null) { listOrdered = true; listItems = new(); }
                var itemPara = new Paragraph { Margin = new Thickness(0) };
                BuildInlines(itemPara.Inlines, om.Groups[1].Value);
                listItems.Add(new ListItem(itemPara));
                continue;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                FlushList();
                FlushParagraph();
                if (inFileListing)
                    fileLines.Add(line);
                continue;
            }

            if (isFileLineStart)
            {
                FlushList();
                FlushParagraph();
                fileLines.Add(line);
                inFileListing = true;
                continue;
            }

            if (inFileListing && isFileContinuation)
            {
                fileLines.Add(line);
                continue;
            }

            if (listItems != null)
                FlushList();

            paraLines.Add(line);
        }

        FlushList();
        FlushParagraph();
        FlushFileListing();

        return blocks;
    }

    private static Block CodeFenceBlock(string code, string lang)
    {
        var stack = new StackPanel();

        if (!string.IsNullOrWhiteSpace(lang))
        {
            stack.Children.Add(new TextBlock
            {
                Text = lang,
                Foreground = new SolidColorBrush(WpfColor.FromRgb(0x8A, 0x8F, 0xA2)),
                FontSize = 10,
                Margin = new Thickness(0, 0, 0, 4),
            });
        }

        var tb = new TextBlock
        {
            Text = code.Trim('\n', '\r'),
            FontFamily = new WpfFontFamily("Consolas, Courier New, monospace"),
            FontSize = 12,
            Foreground = new SolidColorBrush(WpfColor.FromRgb(0xC8, 0xCA, 0xD2)),
            TextWrapping = TextWrapping.Wrap,
        };
        stack.Children.Add(tb);

        var border = new Border
        {
            Background = new SolidColorBrush(WpfColor.FromRgb(0x0D, 0x0E, 0x14)),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10, 8, 10, 8),
            Margin = new Thickness(0, 4, 0, 4),
            Child = stack,
        };
        return new BlockUIContainer(border);
    }

    private static Block FileListingBlock(DirectoryListing listing, bool isFirstInDoc)
    {
        var outer = new StackPanel();

        if (!string.IsNullOrWhiteSpace(listing.Path))
        {
            outer.Children.Add(new TextBlock
            {
                Text = listing.Path,
                Foreground = new SolidColorBrush(WpfColor.FromRgb(0x4B, 0x55, 0x63)),
                FontSize = 10,
                FontFamily = new WpfFontFamily("Consolas, Courier New, monospace"),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 5),
            });
        }

        var rows = new StackPanel();
        foreach (var entry in listing.Entries)
        {
            var row = new Grid { Margin = new Thickness(0, 1, 0, 1) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var name = new TextBlock
            {
                Text = entry.DisplayName,
                ToolTip = entry.Name,
                FontFamily = new WpfFontFamily("Consolas, Courier New, monospace"),
                FontSize = 12,
                Foreground = entry.IsDirectory
                    ? new SolidColorBrush(WpfColor.FromRgb(0x9C, 0xA3, 0xAF))
                    : new SolidColorBrush(WpfColor.FromRgb(0xC8, 0xCA, 0xD2)),
                VerticalAlignment = VerticalAlignment.Top,
                TextWrapping = TextWrapping.Wrap,
            };
            Grid.SetColumn(name, 0);

            var meta = new TextBlock
            {
                Text = entry.Meta,
                FontFamily = new WpfFontFamily("Consolas, Courier New, monospace"),
                FontSize = 10,
                Foreground = new SolidColorBrush(WpfColor.FromRgb(0x4B, 0x55, 0x63)),
                Margin = new Thickness(10, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Top,
            };
            Grid.SetColumn(meta, 1);

            row.Children.Add(name);
            row.Children.Add(meta);
            rows.Children.Add(row);
        }

        var scroll = new ScrollViewer
        {
            MaxHeight = 220,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = rows,
        };
        outer.Children.Add(scroll);

        outer.Children.Add(new TextBlock
        {
            Text = listing.Summary,
            Foreground = new SolidColorBrush(WpfColor.FromRgb(0x4B, 0x55, 0x63)),
            FontSize = 10,
            Margin = new Thickness(0, 5, 0, 0),
        });

        var border = new Border
        {
            Background = new SolidColorBrush(WpfColor.FromRgb(0x0D, 0x0E, 0x14)),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(8, 6, 8, 6),
            Margin = new Thickness(0, isFirstInDoc ? 0 : 4, 0, 4),
            Child = outer,
        };
        return new BlockUIContainer(border);
    }

    internal static DirectoryListing? ParseListingFromLines(IEnumerable<string> lines)
    {
        var listing = new DirectoryListing();

        foreach (var raw in lines)
        {
            var line = raw.TrimStart();
            if (string.IsNullOrWhiteSpace(line))
                continue;

            if (!line.StartsWith("[DIR]", StringComparison.OrdinalIgnoreCase) &&
                !line.StartsWith("[FILE]", StringComparison.OrdinalIgnoreCase) &&
                IsFileSectionHeader(line))
            {
                var path = ExtractPathFromHeader(line);
                if (!string.IsNullOrWhiteSpace(path))
                    listing.Path = path;
                continue;
            }

            var outputMatch = FileOutputEntryRegex.Match(line);
            if (outputMatch.Success)
            {
                var isDir = outputMatch.Groups[1].Value.Equals("DIR", StringComparison.OrdinalIgnoreCase);
                var name = outputMatch.Groups["name"].Value.Trim().TrimEnd('/', '\\');
                var meta = outputMatch.Groups["meta"].Value.Trim();
                if (!isDir && !string.IsNullOrWhiteSpace(meta))
                {
                    var comma = meta.IndexOf(',');
                    if (comma >= 0)
                        meta = meta.Substring(0, comma).Trim();
                }

                if (!string.IsNullOrWhiteSpace(name))
                {
                    listing.Entries.Add(new DirectoryEntry
                    {
                        IsDirectory = isDir,
                        Name = name,
                        Size = isDir ? string.Empty : meta,
                        Modified = string.Empty
                    });
                }
                continue;
            }

            if (!IsFileListItem(line) && !line.StartsWith("|", StringComparison.Ordinal))
                continue;

            if (line.StartsWith("|", StringComparison.Ordinal))
            {
                var cols = line.Split('|');
                if (cols.Length < 3)
                    continue;

                var nameCol = cols[1].Trim().Trim('`', ' ');
                if (string.IsNullOrWhiteSpace(nameCol) || nameCol.Replace("-", "").Trim().Length == 0)
                    continue;

                AddListingEntry(listing, nameCol, cols.Length > 2 ? cols[2].Trim() : string.Empty);
                continue;
            }

            var body = FileListItemRegex.Replace(line, string.Empty).TrimStart();
            var bm = FileBacktickNameRegex.Match(body);
            if (bm.Success)
            {
                AddListingEntry(listing, bm.Groups[1].Value, bm.Groups[2].Value);
                continue;
            }

            var pm = FilePlainNameRegex.Match(body);
            if (pm.Success)
                AddListingEntry(listing, pm.Groups[1].Value.TrimEnd(), pm.Groups[2].Value);
        }

        return listing.Entries.Count > 0 ? listing : null;
    }

    internal static string? ExtractPathFromHeader(string line)
    {
        var idx = line.IndexOf(':');
        if (idx >= 0 && idx + 1 < line.Length)
            return line.Substring(idx + 1).Trim().Trim('`', '*');

        var match = Regex.Match(line, @"`([^`]+)`");
        return match.Success ? match.Groups[1].Value : null;
    }

    private static bool IsFileSectionHeader(string t) =>
        t.StartsWith("[DIR]") || t.StartsWith("[FILE]") || t.StartsWith("Contents of:") ||
        FileSectionHeaderRegex.IsMatch(t);

    private static bool IsFileListItem(string t) =>
        FileListItemRegex.IsMatch(t) || t.StartsWith("|", StringComparison.Ordinal);

    private static bool IsListingSubHeader(string t) => FileSubHeaderRegex.IsMatch(t);

    internal static void AddListingEntry(DirectoryListing listing, string name, string size)
    {
        if (string.IsNullOrWhiteSpace(name))
            return;

        var isDir = name.EndsWith("/", StringComparison.Ordinal) || name.EndsWith("\\", StringComparison.Ordinal);
        if (isDir)
            name = name.TrimEnd('/', '\\');

        listing.Entries.Add(new DirectoryEntry
        {
            IsDirectory = isDir,
            Name = name,
            Size = size,
            Modified = string.Empty
        });
    }
}
