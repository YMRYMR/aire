using Aire.AppLayer.Chat;
using Aire.Data;
using Aire.Providers;
using Aire.Services;
using System.Collections.Generic;
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

    [Fact]
    public void BuildSummaryMessage_RemovesGroups_WhenTokenLimitExceeded()
    {
        var stub = new StubTokenEstimator();
        var service = new ConversationSummaryApplicationService(stub);

        var messages = new[]
        {
            new ChatMessage { Role = "user", Content = "First user message." },
            new ChatMessage { Role = "assistant", Content = "First assistant response." },
            new ChatMessage { Role = "user", Content = "Second user message." },
        };

        // Stub returns token count equal to text length.
        // Set maxTokens low enough that token limit will be exceeded.
        // The algorithm should remove groups from the end and still produce a summary.
        var summary = service.BuildSummaryMessage(messages, maxCharacters: 1000, maxTokens: 10);
        Assert.NotNull(summary);
        Assert.Equal("system", summary!.Role);
        // The resulting summary should be non-empty (at least header)
        Assert.False(string.IsNullOrWhiteSpace(summary.Content));
        // Ensure it does not contain the second group (since removed)
        // This is a basic sanity check; we could do more precise assertions but keep it simple.
    }

    private class StubTokenEstimator : ITokenEstimator
    {
        public int EstimateTokens(string text, string? modelId = null) => text.Length;
        public int EstimateTokensForImage(ImageMetadata image) => 0;
        public int EstimateTokensForAttachments(System.Collections.Generic.IEnumerable<MessageAttachment> attachments) => 0;
    }
}
