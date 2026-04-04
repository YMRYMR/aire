using System;
using System.Collections.Generic;
using System.IO;
using Aire.Services.Workflows;
using Xunit;

namespace Aire.Tests.Services;

public sealed class ChatSubmissionWorkflowServiceTests
{
    [Fact]
    public void PrepareSubmission_TruncatesLargeInlineTextAttachment_AndBuildsShortTitle()
    {
        var service = new ChatSubmissionWorkflowService();
        var tempFile = Path.Combine(Path.GetTempPath(), $"aire-submission-large-{Guid.NewGuid():N}.txt");
        var longPrompt = new string('x', 90);
        var largeBody = new string('a', 100_050);
        File.WriteAllText(tempFile, largeBody);

        try
        {
            var submission = service.PrepareSubmission(
                longPrompt,
                attachedImagePath: null,
                attachedFilePath: tempFile,
                textExtensions: new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".txt" },
                conversationHistoryCount: 0);

            Assert.Contains("Attached:", submission.DisplayContent, StringComparison.Ordinal);
            Assert.Contains("[truncated at 100 KB]", submission.DisplayContent, StringComparison.Ordinal);
            Assert.NotNull(submission.SuggestedConversationTitle);
            Assert.EndsWith("…", submission.SuggestedConversationTitle, StringComparison.Ordinal);
            Assert.True(submission.SuggestedConversationTitle!.Length <= 61);
            Assert.Null(submission.HistoryImagePath);
        }
        finally
        {
            try { File.Delete(tempFile); } catch { }
        }
    }

    [Fact]
    public void PrepareSubmission_OnUnreadableTextAttachment_FallsBackToUserText()
    {
        var service = new ChatSubmissionWorkflowService();
        var missingFile = Path.Combine(Path.GetTempPath(), $"aire-missing-{Guid.NewGuid():N}.txt");

        var submission = service.PrepareSubmission(
            "Review this",
            attachedImagePath: null,
            attachedFilePath: missingFile,
            textExtensions: new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".txt" },
            conversationHistoryCount: 2);

        Assert.Equal("Review this", submission.PersistedContent);
        Assert.Equal("Review this", submission.DisplayContent);
        Assert.Null(submission.HistoryImagePath);
        Assert.Null(submission.SuggestedConversationTitle);
    }

    [Fact]
    public void PrepareSubmission_PrefersExplicitImagePath_OverBinaryAttachmentHistoryPath()
    {
        var service = new ChatSubmissionWorkflowService();

        var submission = service.PrepareSubmission(
            "See image",
            attachedImagePath: "C:\\images\\explicit.png",
            attachedFilePath: "C:\\files\\archive.zip",
            textExtensions: new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".txt" },
            conversationHistoryCount: 3);

        Assert.Equal("C:\\images\\explicit.png", submission.HistoryImagePath);
    }

    [Fact]
    public void UpdateInputHistory_SuppressesConsecutiveDuplicates()
    {
        var service = new ChatSubmissionWorkflowService();
        var history = new List<string> { "first", "second" };

        var result = service.UpdateInputHistory(history, "second");

        Assert.Equal((-1, string.Empty), result);
        Assert.Equal(2, history.Count);
        Assert.Equal("second", history[^1]);
    }

    [Fact]
    public void BuildProviderHistoryMessage_PreservesContentAndImagePath()
    {
        var service = new ChatSubmissionWorkflowService();

        var message = service.BuildProviderHistoryMessage("hello", "C:\\images\\capture.png", null);

        Assert.Equal("user", message.Role);
        Assert.Equal("hello", message.Content);
        Assert.Equal("C:\\images\\capture.png", message.ImagePath);
    }
}
