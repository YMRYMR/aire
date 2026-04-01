using System.Windows.Controls;
using Aire.Services;
using Aire.UI;
using Xunit;

namespace Aire.Tests.UI;

public class LinkedTextAndFlagPainterTests : TestBase
{
    [Fact]
    public void LinkedText_ParsesFileListingsAndHeaders()
    {
        var lines = new[]
        {
            "Files: `C:/repo`",
            "- `src/app.cs` (12 KB)",
            "[DIR] docs",
            "[FILE] readme.md (2 KB, today)"
        };

        var listing = LinkedText.ParseListingFromLines(lines);

        Assert.NotNull(listing);
        Assert.Equal("C:/repo", listing!.Path);
        Assert.Equal(3, listing.Entries.Count);
        Assert.Equal("src/app.cs", listing.Entries[0].Name);
        Assert.True(listing.Entries[1].IsDirectory);
        Assert.Equal("2 KB", listing.Entries[2].Size);

        Assert.Equal("C:/repo", LinkedText.ExtractPathFromHeader("Files: `C:/repo`"));

        var emptyListing = new DirectoryListing();
        LinkedText.AddListingEntry(emptyListing, "folder/", "1 KB");
        Assert.Single(emptyListing.Entries);
        Assert.True(emptyListing.Entries[0].IsDirectory);
    }

    [Fact]
    public void FlagPainter_Create_ReturnsBorderForKnownAndUnknownLanguages()
    {
        RunOnStaThread(() =>
        {
            var known   = FlagPainter.Create("en", w: 30, h: 20);
            var unknown = FlagPainter.Create("xx", w: 24, h: 16);

            var knownBorder   = Assert.IsType<Border>(known);
            var unknownBorder = Assert.IsType<Border>(unknown);
            Assert.Equal(30.0, knownBorder.Width);
            Assert.Equal(20.0, knownBorder.Height);
            Assert.NotNull(knownBorder.Child);
            Assert.NotNull(unknownBorder.Child);
        });
    }
}
