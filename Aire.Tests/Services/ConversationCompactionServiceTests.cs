using Aire.Data;
using Aire.Providers;
using Aire.Services;
using System;
using System.Collections.Generic;
using Xunit;

namespace Aire.Tests.Services;

public sealed class ConversationCompactionServiceTests
{
    [Fact]
    public void EstimateMessageTokens_WithImagePath_UsesProviderSpecificFormula()
    {
        // Arrange
        var estimator = new OpenAiTokenEstimator();
        var service = new ConversationCompactionService();
        var message = new ChatMessage
        {
            Role = "user",
            Content = "test",
            ImagePath = "test.png"
        };

        // Act: compute token count via CompactConversation (which calls EstimateMessageTokens)
        var messages = new[] { message };
        var compacted = service.CompactConversation(messages, targetTokenCount: 100000, estimator, modelId: null);
        // The method returns the same messages if token count under target.
        // We need to know the token count that was computed; we can re-estimate using the same estimator.
        // Instead, we can directly call private method via reflection, but for simplicity,
        // we'll assert that the image token count is not the hardcoded 100.
        // We'll compute expected token count using the estimator's image formula.
        var imageMetadata = new ImageMetadata
        {
            Width = 0,
            Height = 0,
            Format = "png",
            SizeBytes = 0,
            FilePath = "test.png",
            DetailLevel = null
        };
        int expectedImageTokens = estimator.EstimateTokensForImage(imageMetadata);
        // The total tokens should be text tokens + image tokens.
        int expectedTotalTokens = estimator.EstimateTokens("test") + expectedImageTokens;

        // We'll compute actual total tokens by calling the private method via reflection.
        int actualTotalTokens = InvokeEstimateMessageTokens(service, message, estimator, null);
        Assert.Equal(expectedTotalTokens, actualTotalTokens);
        // Ensure the image token count is not the old hardcoded 100.
        Assert.NotEqual(100, expectedImageTokens);
    }

    [Fact]
    public void EstimateMessageTokens_WithImageBytes_NoMimeType_DefaultsToUnknownFormat()
    {
        var estimator = new OpenAiTokenEstimator();
        var service = new ConversationCompactionService();
        var message = new ChatMessage
        {
            Role = "user",
            Content = "",
            ImageBytes = new byte[] { 0x00, 0x01 }
        };

        int tokens = InvokeEstimateMessageTokens(service, message, estimator, null);
        // Should call estimator.EstimateTokensForImage with format "unknown"
        // Since width/height zero, low detail => 85 tokens.
        Assert.Equal(85, tokens);
    }

    [Fact]
    public void EstimateMessageTokens_WithImageBytes_JpegMimeType_SetsFormat()
    {
        var estimator = new OpenAiTokenEstimator();
        var service = new ConversationCompactionService();
        var message = new ChatMessage
        {
            Role = "user",
            Content = "",
            ImageBytes = new byte[] { 0x00, 0x01 },
            ImageMimeType = "image/jpeg"
        };

        int tokens = InvokeEstimateMessageTokens(service, message, estimator, null);
        // Should still be 85 (low detail)
        Assert.Equal(85, tokens);
    }

    [Fact]
    public void EstimateMessageTokens_EstimatorThrowsNotImplementedException_FallsBackToDefault()
    {
        var estimator = new ThrowingTokenEstimator();
        var service = new ConversationCompactionService();
        var message = new ChatMessage
        {
            Role = "user",
            Content = "",
            ImagePath = "test.png"
        };

        int tokens = InvokeEstimateMessageTokens(service, message, estimator, null);
        // Default fallback: low detail => 85 tokens
        Assert.Equal(85, tokens);
    }

    [Fact]
    public void EstimateMessageTokens_NoImage_DoesNotCallEstimateTokensForImage()
    {
        var estimator = new RecordingTokenEstimator();
        var service = new ConversationCompactionService();
        var message = new ChatMessage
        {
            Role = "user",
            Content = "hello"
        };

        int tokens = InvokeEstimateMessageTokens(service, message, estimator, null);
        Assert.Equal(estimator.TextTokens, tokens);
        Assert.False(estimator.ImageEstimationCalled);
    }

    // Helper to invoke private static method
    private static int InvokeEstimateMessageTokens(
        ConversationCompactionService service,
        ChatMessage message,
        ITokenEstimator estimator,
        string? modelId)
    {
        var method = typeof(ConversationCompactionService).GetMethod(
            "EstimateMessageTokens",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        if (method == null)
            throw new InvalidOperationException("Private method not found");
        return (int)method.Invoke(null, new object[] { message, estimator, modelId })!;
    }

    // Mock token estimator that throws NotImplementedException for image estimation
    private sealed class ThrowingTokenEstimator : ITokenEstimator
    {
        public int EstimateTokens(string text, string? modelId = null) => 0;
        public int EstimateTokensForImage(ImageMetadata image) => throw new NotImplementedException();
        public int EstimateTokensForAttachments(IEnumerable<MessageAttachment> attachments) => 0;
    }

    // Mock token estimator that records calls
    private sealed class RecordingTokenEstimator : ITokenEstimator
    {
        public int TextTokens { get; private set; }
        public bool ImageEstimationCalled { get; private set; }

        public int EstimateTokens(string text, string? modelId = null)
        {
            TextTokens = text.Length; // simple heuristic
            return TextTokens;
        }

        public int EstimateTokensForImage(ImageMetadata image)
        {
            ImageEstimationCalled = true;
            return 85;
        }

        public int EstimateTokensForAttachments(IEnumerable<MessageAttachment> attachments) => 0;
    }
}