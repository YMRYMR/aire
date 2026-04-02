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

        Assert.Equal("https://example.com/diagram.png", parsed.ImageReference);
        Assert.Contains("Here is the diagram.", parsed.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_ExtractsStandaloneImageUrl()
    {
        var service = new AssistantImageResponseApplicationService();

        var parsed = service.Parse("https://example.com/render.webp");

        Assert.Equal("https://example.com/render.webp", parsed.ImageReference);
        Assert.Equal(string.Empty, parsed.Text);
    }
}
