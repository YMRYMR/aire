using Aire.AppLayer.Chat;
using Xunit;

namespace Aire.Tests.Services;

public class StreamingResponsePresentationServiceTests
{
    private readonly StreamingResponsePresentationService _service = new();

    [Fact]
    public void GetVisibleText_RemovesCompleteToolCallBlocks()
    {
        var visible = _service.GetVisibleText("Hello<tool_call>{\"tool\":\"read_file\"}</tool_call> world");

        Assert.Equal("Hello world", visible);
    }

    [Fact]
    public void GetVisibleText_HidesIncompleteToolCallTail()
    {
        var visible = _service.GetVisibleText("Hello<tool_call>{\"tool\":\"read_file\"");

        Assert.Equal("Hello", visible);
    }

    [Fact]
    public void GetVisibleText_StripsStandaloneToolMarkers()
    {
        var visible = _service.GetVisibleText("<tool_call>\n<tool_calls>\n<tool_calls>\n");

        Assert.Equal(string.Empty, visible);
    }

    [Fact]
    public void GetVisibleText_RemovesCompleteStructuredBlocks()
    {
        var visible = _service.GetVisibleText(
            "I’m going to inspect the folder structure first.\n" +
            "<folder_structure>\n" +
            "  <action>list</action>\n" +
            "  <path>C:\\dev\\aire</path>\n" +
            "</folder_structure>\n" +
            "Then I’ll report back.");

        Assert.Equal("I’m going to inspect the folder structure first.\nThen I’ll report back.", visible);
    }

    [Fact]
    public void GetStreamingPreviewText_ShowsPartialWordImmediately()
    {
        // Partial words must be shown as they arrive so the UI feels responsive.
        var visible = _service.GetStreamingPreviewText("Hello there gen");

        Assert.Equal("Hello there gen", visible);
    }

    [Fact]
    public void GetStreamingPreviewText_ShowsCompletedSentenceWithPunctuation()
    {
        var visible = _service.GetStreamingPreviewText("Hello there.");

        Assert.Equal("Hello there.", visible);
    }
}
