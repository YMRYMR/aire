using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using Aire.Services;
using Aire.UI;
using Xunit;

namespace Aire.Tests.UI;

public sealed class LinkedTextAdvancedTests : TestBase
{
    [Fact]
    public void LinkedText_SetText_OnTextBlock_RendersInlineMarkdown()
    {
        RunOnStaThread(() =>
        {
            EnsureApplication();

            var textBlock = new TextBlock();
            LinkedText.SetText(textBlock, "Hello **bold** *italic* `code` https://example.com");

            Assert.Contains(textBlock.Inlines.OfType<Bold>(), bold => new TextRange(bold.ContentStart, bold.ContentEnd).Text.Contains("bold", StringComparison.Ordinal));
            Assert.Contains(textBlock.Inlines.OfType<Italic>(), italic => new TextRange(italic.ContentStart, italic.ContentEnd).Text.Contains("italic", StringComparison.Ordinal));
            Assert.Contains(textBlock.Inlines.OfType<Run>(), run => run.Text.Contains("code", StringComparison.Ordinal));
            Assert.Contains(textBlock.Inlines.OfType<InlineUIContainer>(), container =>
                container.Child is TextBlock linkText &&
                linkText.Tag is string tag &&
                tag.Contains("https://example.com", StringComparison.Ordinal));
        });
    }

    [Fact]
    public void LinkedText_SetText_OnRichTextBox_RendersBlocksForHeadingsListsCodeAndFiles()
    {
        RunOnStaThread(() =>
        {
            EnsureApplication();

            var richTextBox = new RichTextBox();
            LinkedText.SetText(richTextBox, """
            # Heading

            Paragraph text.

            - Bullet one

            1. Number two

            ```csharp
            Console.WriteLine("hi");
            ```

            Files: `C:/repo`
            - `src/app.cs` (12 KB)
            [FILE] readme.md (2 KB, today)
            """);

            var blocks = richTextBox.Document.Blocks.ToList();

            Assert.Contains(blocks, block => block is Paragraph paragraph && new TextRange(paragraph.ContentStart, paragraph.ContentEnd).Text.Contains("Heading", StringComparison.Ordinal));
            Assert.Contains(blocks, block => block is System.Windows.Documents.List list && list.MarkerStyle == TextMarkerStyle.Disc);
            Assert.Contains(blocks, block => block is System.Windows.Documents.List list && list.MarkerStyle == TextMarkerStyle.Decimal);
            Assert.Equal(2, blocks.OfType<BlockUIContainer>().Count());
            Assert.Contains(blocks.OfType<BlockUIContainer>(), container => GetBlockText(container).Contains("Console.WriteLine", StringComparison.Ordinal) && GetBlockText(container).Contains("csharp", StringComparison.OrdinalIgnoreCase));
        });
    }

    [Fact]
    public void LinkedText_ParseListingFromLines_ParsesFileSectionHeaderAndEntries()
    {
        var method = typeof(LinkedText).GetMethod("ParseListingFromLines", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(method);

        var listing = (DirectoryListing?)method!.Invoke(null, new object[]
        {
            new[]
            {
                "Files: `C:/repo`",
                "- `src/app.cs` (12 KB)",
                "[FILE] readme.md (2 KB, today)",
            }
        });

        Assert.NotNull(listing);
        Assert.Equal("C:/repo", listing!.Path);
        Assert.Equal("2 files", listing.Summary);
        Assert.Collection(
            listing.Entries,
            entry =>
            {
                Assert.False(entry.IsDirectory);
                Assert.Equal("src/app.cs", entry.Name);
                Assert.Equal("12 KB", entry.Size);
            },
            entry =>
            {
                Assert.False(entry.IsDirectory);
                Assert.Equal("readme.md", entry.Name);
                Assert.Equal("2 KB", entry.Size);
            });
    }

    private static string GetBlockText(DependencyObject? node)
    {
        return node switch
        {
            null => string.Empty,
            TextBlock textBlock => textBlock.Text,
            Border border => GetBlockText(border.Child),
            StackPanel stackPanel => string.Concat(stackPanel.Children.OfType<DependencyObject>().Select(GetBlockText)),
            Grid grid => string.Concat(grid.Children.OfType<DependencyObject>().Select(GetBlockText)),
            ScrollViewer scrollViewer => GetBlockText(scrollViewer.Content as DependencyObject),
            BlockUIContainer blockUIContainer => GetBlockText(blockUIContainer.Child),
            _ => string.Empty
        };
    }
}
