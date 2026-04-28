using System;
using Aire.AppLayer.Chat;
using Xunit;

namespace Aire.Tests.Services;

public class AssistantImageResponseApplicationServiceTests
{
    [Fact]
    public void Parse_ExtractsMarkdownImage_AndLeavesVisibleText()
    {
        var service = new AssistantImageResponseApplicationService();

        var parsed = service.Parse("Here is the diagram.\n\n![architecture](https://example.com/diagram.png)");

        Assert.Single(parsed.ImageReferences);
        Assert.Equal("https://example.com/diagram.png", parsed.ImageReferences[0]);
        Assert.Contains("Here is the diagram.", parsed.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_ExtractsStandaloneImageUrl()
    {
        var service = new AssistantImageResponseApplicationService();

        var parsed = service.Parse("https://example.com/render.webp");

        Assert.Single(parsed.ImageReferences);
        Assert.Equal("https://example.com/render.webp", parsed.ImageReferences[0]);
        Assert.Equal(string.Empty, parsed.Text);
    }

    [Fact]
    public void Parse_ExtractsMultipleMarkdownImages_AndBuildPersistedContentRoundTrips()
    {
        var service = new AssistantImageResponseApplicationService();
        var persisted = service.BuildPersistedContent(
            "Two renders",
            new[]
            {
                "https://example.com/a.png",
                "https://example.com/b.png"
            });

        var parsed = service.Parse(persisted);

        Assert.Equal("Two renders", parsed.Text);
        Assert.Equal(2, parsed.ImageReferences.Count);
        Assert.Equal("https://example.com/a.png", parsed.ImageReferences[0]);
        Assert.Equal("https://example.com/b.png", parsed.ImageReferences[1]);
    }

    [Fact]
    public void Parse_StripsStandaloneToolMarkersFromVisibleText()
    {
        var service = new AssistantImageResponseApplicationService();

        var parsed = service.Parse("Now I need to continue.\n<tool_call>\n<tool_calls>\n<tool_calls>\n");

        Assert.Equal("Now I need to continue.", parsed.Text);
        Assert.Empty(parsed.ImageReferences);
    }
}
