using System.Collections.Generic;
using System.Text.Json;
using Aire.Services;
using Aire.Services.Tools;
using Xunit;

namespace Aire.Tests.Core;

public class ToolHelperAndModelTests
{
    [Fact]
    public void ToolHelpers_ReadsStringsIntsAndIndexFallbacks()
    {
        using JsonDocument jsonDocument = JsonDocument.Parse("{\"name\":\"abc\",\"count\":\"5\",\"tab_index\":7}");
        ToolCallRequest request = new ToolCallRequest
        {
            Parameters = jsonDocument.RootElement.Clone()
        };
        Assert.Equal("abc", ToolHelpers.GetString(request, "name"));
        Assert.Equal(5, ToolHelpers.GetInt(request, "count"));
        Assert.Equal(7, ToolHelpers.GetIndexParam(request));
    }

    [Fact]
    public void DirectoryEntry_AndListing_ExposeFriendlyDisplayValues()
    {
        DirectoryEntry directoryEntry = new DirectoryEntry
        {
            IsDirectory = true,
            Name = "docs"
        };
        DirectoryEntry directoryEntry2 = new DirectoryEntry
        {
            IsDirectory = false,
            Name = "a.txt",
            Size = "12 B"
        };
        DirectoryListing directoryListing = new DirectoryListing
        {
            Entries = new List<DirectoryEntry> { directoryEntry, directoryEntry2 }
        };
        Assert.Equal("docs/", directoryEntry.DisplayName);
        Assert.Equal("12 B", directoryEntry2.Meta);
        Assert.Equal("1 folder, 1 file", directoryListing.Summary);
    }
}
