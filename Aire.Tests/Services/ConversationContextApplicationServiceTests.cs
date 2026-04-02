using System;
using System.Linq;
using Aire.AppLayer.Chat;
using Aire.Providers;
using Xunit;

namespace Aire.Tests.Services;

public class ConversationContextApplicationServiceTests
{
    [Fact]
    public void BuildContextWindow_PreservesAnchorsAndRecentMessages()
    {
        var service = new ConversationContextApplicationService();
        var history = Enumerable.Range(1, 20)
            .Select(i => new ChatMessage
            {
                Role = i % 2 == 0 ? "assistant" : "user",
                Content = $"m{i}"
            })
            .ToList();

        var result = service.BuildContextWindow(
            history,
            maxMessages: 8,
            anchorMessages: 2,
            uncachedRecentMessages: 3,
            enablePromptCaching: true,
            enableConversationSummaries: false);

        Assert.Equal(8, result.Count);
        Assert.Equal("m1", result[0].Content);
        Assert.Equal("m2", result[1].Content);
        Assert.Equal("m18", result[^3].Content);
        Assert.Equal("m19", result[^2].Content);
        Assert.Equal("m20", result[^1].Content);
    }

    [Fact]
    public void BuildContextWindow_MarksStablePrefixAsCachePreferred()
    {
        var service = new ConversationContextApplicationService();
        var history = Enumerable.Range(1, 10)
            .Select(i => new ChatMessage
            {
                Role = i % 2 == 0 ? "assistant" : "user",
                Content = $"m{i}"
            })
            .ToList();

        var result = service.BuildContextWindow(history, maxMessages: 10, anchorMessages: 2, uncachedRecentMessages: 4);

        Assert.True(result[0].PreferPromptCache);
        Assert.True(result[1].PreferPromptCache);
        Assert.True(result[5].PreferPromptCache);
        Assert.False(result[^1].PreferPromptCache);
        Assert.False(result[^2].PreferPromptCache);
        Assert.False(result[^3].PreferPromptCache);
        Assert.False(result[^4].PreferPromptCache);
    }

    [Fact]
    public void BuildContextWindow_DoesNotCacheImageMessages_OrSystemMessages()
    {
        var service = new ConversationContextApplicationService();
        var history =
            new[]
            {
                new ChatMessage { Role = "system", Content = "sys" },
                new ChatMessage { Role = "user", Content = "anchor" },
                new ChatMessage { Role = "assistant", Content = "image reply", ImagePath = "C:\\img.png" },
                new ChatMessage { Role = "user", Content = "latest" },
            };

        var result = service.BuildContextWindow(history, maxMessages: 4, anchorMessages: 2, uncachedRecentMessages: 1);

        Assert.Equal("system", result[0].Role);
        Assert.False(result[0].PreferPromptCache);
        Assert.True(result[1].PreferPromptCache);
        Assert.False(result[2].PreferPromptCache);
    }

    [Fact]
    public void BuildContextWindow_InsertsSummaryForOmittedMessages_WhenEnabled()
    {
        var service = new ConversationContextApplicationService();
        var history = Enumerable.Range(1, 14)
            .Select(i => new ChatMessage
            {
                Role = i % 2 == 0 ? "assistant" : "user",
                Content = $"message {i}"
            })
            .ToList();

        var result = service.BuildContextWindow(
            history,
            maxMessages: 6,
            anchorMessages: 2,
            uncachedRecentMessages: 2,
            enablePromptCaching: true,
            enableConversationSummaries: true,
            summaryMaxCharacters: 300);

        Assert.Contains(result, m => m.Role == "system" && m.Content.StartsWith("Conversation summary of earlier omitted context:", StringComparison.Ordinal));
    }

    [Fact]
    public void BuildContextWindow_DoesNotInsertSummary_WhenDisabled()
    {
        var service = new ConversationContextApplicationService();
        var history = Enumerable.Range(1, 14)
            .Select(i => new ChatMessage
            {
                Role = i % 2 == 0 ? "assistant" : "user",
                Content = $"message {i}"
            })
            .ToList();

        var result = service.BuildContextWindow(
            history,
            maxMessages: 6,
            anchorMessages: 2,
            uncachedRecentMessages: 2,
            enablePromptCaching: true,
            enableConversationSummaries: false,
            summaryMaxCharacters: 300);

        Assert.DoesNotContain(result, m => m.Role == "system" && m.Content.StartsWith("Conversation summary of earlier omitted context:", StringComparison.Ordinal));
    }
}
