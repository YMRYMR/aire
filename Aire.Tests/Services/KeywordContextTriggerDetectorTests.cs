using System.Collections.Generic;
using Aire.Data;
using Aire.Services;
using Xunit;

namespace Aire.Tests.Services;

public sealed class KeywordContextTriggerDetectorTests
{
    private readonly KeywordContextTriggerDetector _detector = new();

    [Fact]
    public void DetectTriggers_NoMessages_ReturnsNone()
    {
        var result = _detector.DetectTriggers(new List<Message>());
        Assert.False(result.IsToolFocus);
        Assert.False(result.IsRetryFollowUp);
    }

    [Fact]
    public void DetectTriggers_NullMessages_ReturnsNone()
    {
        var result = _detector.DetectTriggers(null);
        Assert.False(result.IsToolFocus);
        Assert.False(result.IsRetryFollowUp);
    }

    [Fact]
    public void DetectTriggers_ToolKeywordInLatest_DetectsToolFocus()
    {
        var messages = new[]
        {
            new Message { Content = "Hello" },
            new Message { Content = "Please run command ls -la" }
        };
        var result = _detector.DetectTriggers(messages);
        Assert.True(result.IsToolFocus);
        Assert.False(result.IsRetryFollowUp);
    }

    [Fact]
    public void DetectTriggers_ToolKeywordInPrevious_DetectsToolFocus()
    {
        var messages = new[]
        {
            new Message { Content = "Please using a tool to list files" },
            new Message { Content = "Okay" }
        };
        var result = _detector.DetectTriggers(messages);
        Assert.True(result.IsToolFocus);
    }

    [Fact]
    public void DetectTriggers_RetryPhraseInLatest_DetectsRetry()
    {
        var messages = new[]
        {
            new Message { Content = "Something failed" },
            new Message { Content = "Try again" }
        };
        var result = _detector.DetectTriggers(messages);
        Assert.True(result.IsRetryFollowUp);
    }

    [Fact]
    public void DetectTriggers_BothKeywords_DetectsBoth()
    {
        var messages = new[]
        {
            new Message { Content = "Please using a tool and try again" }
        };
        var result = _detector.DetectTriggers(messages);
        Assert.True(result.IsToolFocus);
        Assert.True(result.IsRetryFollowUp);
    }

    [Fact]
    public void DetectTriggers_NoKeywords_ReturnsNone()
    {
        var messages = new[]
        {
            new Message { Content = "Good morning" }
        };
        var result = _detector.DetectTriggers(messages);
        Assert.False(result.IsToolFocus);
        Assert.False(result.IsRetryFollowUp);
    }

    [Fact]
    public void DetectTriggers_KeywordCaseInsensitive()
    {
        var messages = new[]
        {
            new Message { Content = "USING A TOOL" }
        };
        var result = _detector.DetectTriggers(messages);
        Assert.True(result.IsToolFocus);
    }

    [Fact]
    public void DetectTriggers_RetryKeywordVariants()
    {
        var messages = new[]
        {
            new Message { Content = "Could you please fix it?" }
        };
        var result = _detector.DetectTriggers(messages);
        Assert.True(result.IsRetryFollowUp);
    }
}