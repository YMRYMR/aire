using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Aire.AppLayer.Abstractions;
using Aire.AppLayer.Tools;
using Aire.Services;
using Aire.Services.Workflows;
using Xunit;

namespace Aire.Tests.Services;

public sealed class ToolApprovalExecutionApplicationServiceTests
{
    private readonly ToolApprovalPromptApplicationService _promptService = new();

    [Fact]
    public async Task CompleteAsync_Approved_StatusIsCompletedAndResultContainsToolOutput()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"aire-approval-test-{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(tempFile, "tool output content");
        try
        {
            var service = CreateService();
            var request = CreateRequest("read_file", $$"""{"path":"{{tempFile.Replace("\\", "\\\\")}}"}""", $"Read {tempFile}");

            var result = await service.CompleteAsync(request, approved: true, conversationId: null);

            Assert.Equal("completed", result.Status);
            Assert.Contains("tool output content", result.TextResult, StringComparison.Ordinal);
            Assert.StartsWith("\u2713 Read ", result.ToolCallStatus, StringComparison.Ordinal);
            Assert.NotNull(result.ExecutionOutcome);
            Assert.True(result.ExecutionOutcome.Approved);
        }
        finally
        {
            try { File.Delete(tempFile); } catch { }
        }
    }

    [Fact]
    public async Task CompleteAsync_Denied_StatusIsDeniedAndTextResultIsDeniedMessage()
    {
        var service = CreateService();
        var request = CreateRequest("move_file", """{"from":"C:\\a.txt","to":"C:\\b.txt"}""", "Move file");

        var result = await service.CompleteAsync(request, approved: false, conversationId: null);

        Assert.Equal("denied", result.Status);
        Assert.Equal("Tool execution was denied.", result.TextResult);
        Assert.Equal("\u2717 Denied", result.ToolCallStatus);
        Assert.NotNull(result.ExecutionOutcome);
        Assert.False(result.ExecutionOutcome.Approved);
    }

    [Fact]
    public async Task CompleteAsync_ConversationIdPassedThrough_PersistsTranscript()
    {
        var conversations = new RecordingConversationRepository();
        var service = CreateService(conversations);
        var request = CreateRequest("read_file", """{"path":"C:\\repo\\file.txt"}""", "Read file");

        await service.CompleteAsync(request, approved: false, conversationId: 42);

        Assert.Single(conversations.SavedMessages);
        Assert.Equal(42, conversations.SavedMessages[0].ConversationId);
    }

    [Fact]
    public async Task CompleteAsync_NullConversationId_DoesNotPersistTranscript()
    {
        var conversations = new RecordingConversationRepository();
        var service = CreateService(conversations);
        var request = CreateRequest("read_file", """{"path":"C:\\repo\\file.txt"}""", "Read file");

        await service.CompleteAsync(request, approved: false, conversationId: null);

        Assert.Empty(conversations.SavedMessages);
    }

    [Fact]
    public async Task CompleteAsync_Approved_ReturnsExecutionOutcomeInResult()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"aire-approval-outcome-{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(tempFile, "content");
        try
        {
            var service = CreateService();
            var request = CreateRequest("read_file", $$"""{"path":"{{tempFile.Replace("\\", "\\\\")}}"}""", "Read file");

            var result = await service.CompleteAsync(request, approved: true, conversationId: 7);

            Assert.NotNull(result.ExecutionOutcome);
            Assert.True(result.ExecutionOutcome.Approved);
            Assert.Equal(tempFile, result.ExecutionOutcome.ToolPath);
        }
        finally
        {
            try { File.Delete(tempFile); } catch { }
        }
    }

    private ToolApprovalExecutionApplicationService CreateService(
        RecordingConversationRepository? conversations = null)
    {
        conversations ??= new RecordingConversationRepository();
        var settings = new RecordingSettingsRepository();
        var toolService = new ToolExecutionService(new FileSystemService(), new CommandExecutionService());
        var workflow = new ToolExecutionWorkflowService(toolService, conversations, settings);
        return new ToolApprovalExecutionApplicationService(_promptService, workflow);
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
            IEnumerable<Aire.Data.MessageAttachment>? attachments = null,
            int? tokens = null)
        {
            SavedMessages.Add((conversationId, role, content));
            return Task.CompletedTask;
        }
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
