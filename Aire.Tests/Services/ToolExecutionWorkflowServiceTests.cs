using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Aire.AppLayer.Abstractions;
using Aire.Services;
using Aire.Services.Workflows;
using Xunit;

namespace Aire.Tests.Services;

public sealed class ToolExecutionWorkflowServiceTests
{
    [Fact]
    public async Task ExecuteAsync_ApprovedWithoutConversation_PersistsAuditButNotTranscript()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"aire-tool-workflow-{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(tempFile, "hello from tool workflow");
        try
        {
            var conversations = new RecordingConversationRepository();
            var settings = new RecordingSettingsRepository();
            var toolService = new ToolExecutionService(new FileSystemService(), new CommandExecutionService());
            var workflow = new ToolExecutionWorkflowService(toolService, conversations, settings);

            var outcome = await workflow.ExecuteAsync(
                CreateRequest("read_file", $$"""{"path":"{{tempFile.Replace("\\", "\\\\")}}"}""", $"Read file: {tempFile}"),
                approved: true,
                conversationId: null);

            Assert.True(outcome.Approved);
            Assert.NotNull(outcome.ExecutionResult);
            Assert.Contains("hello from tool workflow", outcome.ToolResult, StringComparison.Ordinal);
            Assert.StartsWith("✓ Read file:", outcome.ToolCallStatus, StringComparison.Ordinal);
            Assert.Equal(tempFile, outcome.ToolPath);
            Assert.Empty(conversations.SavedMessages);
            Assert.Single(settings.FileAccessLogs);
            Assert.Equal(("read_file", tempFile, true), settings.FileAccessLogs[0]);
        }
        finally
        {
            try { File.Delete(tempFile); } catch { }
        }
    }

    [Fact]
    public async Task ExecuteAsync_DeniedWithConversation_PersistsDeniedTranscriptAndAudit()
    {
        var conversations = new RecordingConversationRepository();
        var settings = new RecordingSettingsRepository();
        var toolService = new ToolExecutionService(new FileSystemService(), new CommandExecutionService());
        var workflow = new ToolExecutionWorkflowService(toolService, conversations, settings);

        var outcome = await workflow.ExecuteAsync(
            CreateRequest("move_file", """{"from":"C:\\repo\\old.txt","to":"C:\\repo\\new.txt"}""", "Move file"),
            approved: false,
            conversationId: 42);

        Assert.False(outcome.Approved);
        Assert.Equal("[Operation denied by user]", outcome.ToolResult);
        Assert.Equal("✗ Denied", outcome.ToolCallStatus);
        Assert.Null(outcome.ExecutionResult);
        Assert.Equal("C:\\repo\\old.txt", outcome.ToolPath);
        Assert.Contains("Operation denied by user", outcome.HistoryContent, StringComparison.Ordinal);
        Assert.Single(conversations.SavedMessages);
        Assert.Equal((42, "tool", "✗ Denied"), conversations.SavedMessages[0]);
        Assert.Single(settings.FileAccessLogs);
        Assert.Equal(("move_file", "C:\\repo\\old.txt", false), settings.FileAccessLogs[0]);
    }

    [Fact]
    public async Task ExecuteAsync_Denied_UsesDirectoryFallback_ForAuditPath()
    {
        var conversations = new RecordingConversationRepository();
        var settings = new RecordingSettingsRepository();
        var toolService = new ToolExecutionService(new FileSystemService(), new CommandExecutionService());
        var workflow = new ToolExecutionWorkflowService(toolService, conversations, settings);

        var outcome = await workflow.ExecuteAsync(
            CreateRequest("search_files", """{"directory":"C:\\repo","pattern":"*.cs"}""", "Search files"),
            approved: false,
            conversationId: null);

        Assert.Equal("C:\\repo", outcome.ToolPath);
        Assert.Single(settings.FileAccessLogs);
        Assert.Equal(("search_files", "C:\\repo", false), settings.FileAccessLogs[0]);
    }

    [Fact]
    public async Task ExecuteAsync_Approved_PropagatesConversationPersistenceFailures()
    {
        var conversations = new ThrowingConversationRepository();
        var settings = new RecordingSettingsRepository();
        var toolService = new ToolExecutionService(new FileSystemService(), new CommandExecutionService());
        var workflow = new ToolExecutionWorkflowService(toolService, conversations, settings);

        await Assert.ThrowsAsync<InvalidOperationException>(() => workflow.ExecuteAsync(
            CreateRequest("read_file", """{"path":"C:\\repo\\file.txt"}""", "Read file"),
            approved: true,
            conversationId: 42));
    }

    private static ToolCallRequest CreateRequest(string tool, string json, string description)
    {
        using var doc = JsonDocument.Parse(json);
        return new ToolCallRequest
        {
            Tool = tool,
            Parameters = doc.RootElement.Clone(),
            Description = description,
            RawJson = json
        };
    }

    private sealed class RecordingConversationRepository : IConversationRepository
    {
        public List<(int ConversationId, string Role, string Content)> SavedMessages { get; } = [];

        public Task<int> CreateConversationAsync(int providerId, string title) => throw new NotSupportedException();
        public Task<Aire.Data.Conversation?> GetLatestConversationAsync(int providerId) => throw new NotSupportedException();
        public Task<Aire.Data.Conversation?> GetConversationAsync(int conversationId) => throw new NotSupportedException();
        public Task<List<Aire.Data.ConversationSummary>> ListConversationsAsync(string? search = null) => throw new NotSupportedException();
        public Task UpdateConversationTitleAsync(int conversationId, string title) => throw new NotSupportedException();
        public Task UpdateConversationProviderAsync(int conversationId, int providerId) => throw new NotSupportedException();
        public Task UpdateConversationAssistantModeAsync(int conversationId, string assistantModeKey) => throw new NotSupportedException();
        public Task<List<Aire.Data.Message>> GetMessagesAsync(int conversationId) => throw new NotSupportedException();
        public Task DeleteMessagesByConversationIdAsync(int conversationId) => throw new NotSupportedException();
        public Task DeleteConversationAsync(int conversationId) => throw new NotSupportedException();
        public Task DeleteAllConversationsAsync() => throw new NotSupportedException();

        public Task SaveMessageAsync(
            int conversationId,
            string role,
            string content,
            string? imagePath = null,
            IEnumerable<Aire.Data.MessageAttachment>? attachments = null)
        {
            SavedMessages.Add((conversationId, role, content));
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingConversationRepository : IConversationRepository
    {
        public Task<int> CreateConversationAsync(int providerId, string title) => throw new NotSupportedException();
        public Task<Aire.Data.Conversation?> GetLatestConversationAsync(int providerId) => throw new NotSupportedException();
        public Task<Aire.Data.Conversation?> GetConversationAsync(int conversationId) => throw new NotSupportedException();
        public Task<List<Aire.Data.ConversationSummary>> ListConversationsAsync(string? search = null) => throw new NotSupportedException();
        public Task UpdateConversationTitleAsync(int conversationId, string title) => throw new NotSupportedException();
        public Task UpdateConversationProviderAsync(int conversationId, int providerId) => throw new NotSupportedException();
        public Task UpdateConversationAssistantModeAsync(int conversationId, string assistantModeKey) => throw new NotSupportedException();
        public Task<List<Aire.Data.Message>> GetMessagesAsync(int conversationId) => throw new NotSupportedException();
        public Task DeleteMessagesByConversationIdAsync(int conversationId) => throw new NotSupportedException();
        public Task DeleteConversationAsync(int conversationId) => throw new NotSupportedException();
        public Task DeleteAllConversationsAsync() => throw new NotSupportedException();

        public Task SaveMessageAsync(
            int conversationId,
            string role,
            string content,
            string? imagePath = null,
            IEnumerable<Aire.Data.MessageAttachment>? attachments = null)
            => throw new InvalidOperationException("conversation persistence failed");
    }

    private sealed class RecordingSettingsRepository : ISettingsRepository
    {
        public List<(string Operation, string Path, bool Allowed)> FileAccessLogs { get; } = [];

        public Task<string?> GetSettingAsync(string key) => throw new NotSupportedException();
        public Task SetSettingAsync(string key, string value) => throw new NotSupportedException();

        public Task LogFileAccessAsync(string operation, string path, bool allowed)
        {
            FileAccessLogs.Add((operation, path, allowed));
            return Task.CompletedTask;
        }
    }
}
