using System;
using System.Linq;
using System.Text.Json;
using Aire.AppLayer.Chat;
using Aire.Data;
using Xunit;

namespace Aire.Tests.Services;

public class ConversationTranscriptApplicationServiceTests
{
    [Fact]
    public void BuildTranscript_ParsesPersistedAssistantImageMetadata_WithoutLeakingMarkerText()
    {
        var service = new ConversationTranscriptApplicationService();
        var imageParser = new AssistantImageResponseApplicationService();
        var messages = new[]
        {
            new Message
            {
                Role = "assistant",
                Content = imageParser.BuildPersistedContent(
                    "Rendered concept",
                    new[]
                    {
                        "https://example.com/one.png",
                        "https://example.com/two.png"
                    }),
                CreatedAt = new DateTime(2026, 4, 2, 10, 30, 0, DateTimeKind.Utc)
            }
        };

        var transcript = service.BuildTranscript(messages);

        Assert.Single(transcript.Entries);
        var entry = transcript.Entries[0];
        Assert.Equal("Rendered concept", entry.Text);
        Assert.Equal(2, entry.ImageReferences.Count);
        Assert.DoesNotContain("aire-images", entry.Text, StringComparison.Ordinal);
        Assert.Single(transcript.ConversationHistory);
        Assert.Equal("Rendered concept", transcript.ConversationHistory[0].Content);
    }

    [Fact]
    public void BuildTranscript_PreservesLegacySingleImagePath()
    {
        var service = new ConversationTranscriptApplicationService();
        var messages = new[]
        {
            new Message
            {
                Role = "assistant",
                Content = "Rendered concept",
                ImagePath = @"C:\temp\generated.png",
                CreatedAt = DateTime.UtcNow
            }
        };

        var transcript = service.BuildTranscript(messages);

        Assert.Single(transcript.Entries[0].ImageReferences);
        Assert.Equal(@"C:\temp\generated.png", transcript.Entries[0].ImageReferences.Single());
    }

    [Fact]
    public void BuildTranscript_PreservesLegacyJpegImagePath()
    {
        var service = new ConversationTranscriptApplicationService();
        var messages = new[]
        {
            new Message
            {
                Role = "assistant",
                Content = "Rendered concept",
                ImagePath = @"C:\temp\generated.jpg",
                CreatedAt = DateTime.UtcNow
            }
        };

        var transcript = service.BuildTranscript(messages);

        Assert.Single(transcript.Entries[0].ImageReferences);
        Assert.Equal(@"C:\temp\generated.jpg", transcript.Entries[0].ImageReferences.Single());
    }

    [Fact]
    public void BuildTranscript_PreservesNonImageAttachments()
    {
        var service = new ConversationTranscriptApplicationService();
        string attachmentsJson = JsonSerializer.Serialize(new[]
        {
            new MessageAttachment
            {
                FileName = "notes.txt",
                FilePath = @"C:\temp\notes.txt",
                MimeType = "text/plain",
                IsImage = false
            }
        });
        var messages = new[]
        {
            new Message
            {
                Role = "user",
                Content = "Please review the file.",
                AttachmentsJson = attachmentsJson,
                CreatedAt = DateTime.UtcNow
            }
        };

        var transcript = service.BuildTranscript(messages);

        Assert.Single(transcript.Entries);
        var entry = transcript.Entries[0];
        Assert.Single(entry.FileAttachments);
        Assert.Empty(entry.ImageReferences);
    }

    [Fact]
    public void BuildTranscript_IgnoresMalformedAttachmentJson()
    {
        var service = new ConversationTranscriptApplicationService();
        var messages = new[]
        {
            new Message
            {
                Role = "user",
                Content = "Review this.",
                AttachmentsJson = "{ this is not valid json",
                CreatedAt = DateTime.UtcNow
            }
        };

        var transcript = service.BuildTranscript(messages);

        Assert.Single(transcript.Entries);
        Assert.Empty(transcript.Entries[0].FileAttachments);
        Assert.Empty(transcript.Entries[0].ImageReferences);
    }
}
