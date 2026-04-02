using Aire.AppLayer.Chat;
using Aire.Providers;
using Xunit;

namespace Aire.Tests.Services;

public class ConversationSummaryApplicationServiceTests
{
    [Fact]
    public void BuildSummaryMessage_ReturnsSystemSummary_ForOmittedTurns()
    {
        var service = new ConversationSummaryApplicationService();

        var summary = service.BuildSummaryMessage(
        [
            new ChatMessage { Role = "user", Content = "Please investigate the outage and check the API logs." },
            new ChatMessage { Role = "assistant", Content = "I found repeated 500 errors on the billing endpoint." },
            new ChatMessage { Role = "user", Content = "Also confirm whether the retry job is stuck." }
        ], 220);

        Assert.NotNull(summary);
        Assert.Equal("system", summary!.Role);
        Assert.Contains("User:", summary.Content);
        Assert.Contains("Assistant:", summary.Content);
    }

    [Fact]
    public void BuildSummaryMessage_TruncatesToBudget()
    {
        var service = new ConversationSummaryApplicationService();

        var summary = service.BuildSummaryMessage(
        [
            new ChatMessage { Role = "user", Content = new string('x', 400) },
            new ChatMessage { Role = "assistant", Content = new string('y', 400) }
        ], 180);

        Assert.NotNull(summary);
        Assert.True(summary!.Content.Length <= 180);
        Assert.EndsWith("…", summary.Content);
    }

    [Fact]
    public void BuildSummaryMessage_PreservesToolOutcomes_AndStructuredFacts()
    {
        var service = new ConversationSummaryApplicationService();

        var summary = service.BuildSummaryMessage(
        [
            new ChatMessage
            {
                Role = "tool",
                Content = """
                          Result: Downloaded file successfully
                          Path: C:\docs\book.txt
                          Bytes: 2048000
                          """
            },
            new ChatMessage
            {
                Role = "assistant",
                Content = """
                          Findings:
                          - The document is plain text.
                          - The introduction starts at chapter 1.
                          """
            }
        ], 320);

        Assert.NotNull(summary);
        Assert.Contains("Tool:", summary!.Content);
        Assert.Contains("Result: Downloaded file successfully", summary.Content);
        Assert.Contains("Path: C:\\docs\\book.txt", summary.Content);
        Assert.Contains("Assistant:", summary.Content);
        Assert.Contains("- The document is plain text.", summary.Content);
    }

    [Fact]
    public void BuildSummaryMessage_DeduplicatesRepeatedSnippets()
    {
        var service = new ConversationSummaryApplicationService();

        var summary = service.BuildSummaryMessage(
        [
            new ChatMessage { Role = "assistant", Content = "Status: queued" },
            new ChatMessage { Role = "assistant", Content = "Status: queued" },
            new ChatMessage { Role = "assistant", Content = "Status: queued" }
        ], 220);

        Assert.NotNull(summary);
        Assert.Equal(1, summary!.Content.Split("Status: queued").Length - 1);
    }

    [Fact]
    public void BuildSummaryMessage_LabelsTaskFlowSnippets_Semantically()
    {
        var service = new ConversationSummaryApplicationService();

        var summary = service.BuildSummaryMessage(
        [
            new ChatMessage { Role = "assistant", Content = "Todo list updated: 3 task(s), 1 completed." },
            new ChatMessage { Role = "assistant", Content = "Ask: Which repository should I deploy?" },
            new ChatMessage { Role = "assistant", Content = "Complete task: Deployment checklist drafted." }
        ], 320);

        Assert.NotNull(summary);
        Assert.Contains("Todo:", summary!.Content);
        Assert.Contains("Question:", summary.Content);
        Assert.Contains("Completion:", summary.Content);
    }
}
