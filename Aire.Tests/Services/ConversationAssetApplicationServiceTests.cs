using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Aire.AppLayer.Chat;
using Aire.AppLayer.Abstractions;
using Aire.Data;
using Xunit;

namespace Aire.Tests.Services;

public sealed class ConversationAssetApplicationServiceTests
{
    [Fact]
    public async Task PersistScreenshotAsync_ReturnsOriginalPath_WhenConversationIsMissing()
    {
        var repository = new FakeConversationRepository();
        var service = new ConversationAssetApplicationService(repository);
        string root = Path.Combine(Path.GetTempPath(), $"aire-asset-tests-{Guid.NewGuid():N}");
        string source = Path.Combine(root, "source.png");
        Directory.CreateDirectory(root);
        await File.WriteAllBytesAsync(source, [1, 2, 3, 4]);

        try
        {
            string result = await service.PersistScreenshotAsync(null, source, root, new DateTime(2026, 4, 9, 12, 0, 0, DateTimeKind.Local));

            Assert.Equal(source, result);
            Assert.Empty(repository.SavedMessages);
            Assert.True(File.Exists(source));
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }

    [Fact]
    public async Task PersistScreenshotAsync_CopiesScreenshot_AndPersistsInlineAttachmentMetadata()
    {
        var repository = new FakeConversationRepository();
        var service = new ConversationAssetApplicationService(repository);
        string root = Path.Combine(Path.GetTempPath(), $"aire-asset-tests-{Guid.NewGuid():N}");
        string source = Path.Combine(root, "source.png");
        Directory.CreateDirectory(root);
        await File.WriteAllBytesAsync(source, [1, 2, 3, 4, 5]);

        try
        {
            string persisted = await service.PersistScreenshotAsync(42, source, root, new DateTime(2026, 4, 9, 12, 34, 56, 789, DateTimeKind.Local));

            Assert.StartsWith(Path.Combine(root, "42"), persisted, StringComparison.OrdinalIgnoreCase);
            Assert.EndsWith(".png", persisted, StringComparison.OrdinalIgnoreCase);
            Assert.True(File.Exists(persisted));
            Assert.Single(repository.SavedMessages);

            var message = repository.SavedMessages[0];
            var attachments = new List<MessageAttachment>(message.Attachments!);
            Assert.Equal(42, message.ConversationId);
            Assert.Equal("assistant", message.Role);
            Assert.Equal(string.Empty, message.Content);
            Assert.Equal(persisted, message.ImagePath);
            Assert.Single(attachments);
            Assert.True(attachments[0].IsImage);
            Assert.True(attachments[0].IsInlinePreview);
            Assert.Equal(persisted, attachments[0].FilePath);
            Assert.Equal("image/png", attachments[0].MimeType);
            Assert.Equal("20260409_123456_789.png", attachments[0].FileName);
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }

    private sealed class FakeConversationRepository : IConversationRepository
    {
        public List<(int ConversationId, string Role, string Content, string? ImagePath, IEnumerable<MessageAttachment>? Attachments)> SavedMessages { get; } = [];

        public Task<int> CreateConversationAsync(int providerId, string title) => throw new NotSupportedException();
        public Task<Conversation?> GetLatestConversationAsync(int providerId) => throw new NotSupportedException();
        public Task<Conversation?> GetConversationAsync(int conversationId) => throw new NotSupportedException();
        public Task<List<ConversationSummary>> ListConversationsAsync(string? search = null) => throw new NotSupportedException();
        public Task UpdateConversationTitleAsync(int conversationId, string title) => throw new NotSupportedException();
        public Task UpdateConversationProviderAsync(int conversationId, int providerId) => throw new NotSupportedException();
        public Task UpdateConversationAssistantModeAsync(int conversationId, string assistantModeKey) => throw new NotSupportedException();

        public Task SaveMessageAsync(
            int conversationId,
            string role,
            string content,
            string? imagePath = null,
            IEnumerable<MessageAttachment>? attachments = null)
        {
            SavedMessages.Add((conversationId, role, content, imagePath, attachments));
            return Task.CompletedTask;
        }

        public Task<List<Message>> GetMessagesAsync(int conversationId) => throw new NotSupportedException();
        public Task DeleteMessagesByConversationIdAsync(int conversationId) => throw new NotSupportedException();
        public Task DeleteConversationAsync(int conversationId) => throw new NotSupportedException();
        public Task DeleteAllConversationsAsync() => throw new NotSupportedException();
    }
}
