using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aire.AppLayer.Chat;
using Aire.Data;
using Aire.Domain.Providers;
using Aire.Providers;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Aire.Tests.Services;

public sealed class GeneratedImageApplicationServiceTests : IDisposable
{
    private readonly string _dbPath;
    private readonly string _localAppDataPath;
    private readonly string? _oldLocalAppData;

    public GeneratedImageApplicationServiceTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"aire_generated_image_tests_{Guid.NewGuid():N}.db");
        _localAppDataPath = Path.Combine(Path.GetTempPath(), $"aire_generated_image_localappdata_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_localAppDataPath);
        _oldLocalAppData = Environment.GetEnvironmentVariable("LOCALAPPDATA");
        Environment.SetEnvironmentVariable("LOCALAPPDATA", _localAppDataPath);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("LOCALAPPDATA", _oldLocalAppData);
        SqliteConnection.ClearAllPools();

        TryDelete(_dbPath);
        TryDelete(_localAppDataPath);
    }

    [Fact]
    public async Task GenerateAsync_PersistsAssistantMessage_WithAttachmentMetadata()
    {
        using var db = new DatabaseService(_dbPath);
        await db.InitializeAsync();

        int providerId = await db.InsertProviderAsync(new Provider
        {
            Name = "Image Provider",
            Type = "OpenAI",
            ApiKey = "sk-test",
            Model = "gpt-4o",
            IsEnabled = true
        });

        int conversationId = await db.CreateConversationAsync(providerId, "Generated Images");
        var service = new GeneratedImageApplicationService(new ChatSessionApplicationService(db, db));
        var provider = new StubImageGenerationProvider(
            success: true,
            imageBytes: [1, 2, 3, 4],
            imageMimeType: "image/jpeg",
            revisedPrompt: "a photo-realistic robot portrait");

        var result = await service.GenerateAsync(provider, "draw a robot", conversationId, CancellationToken.None);

        Assert.Equal("Generated image. Revised prompt: a photo-realistic robot portrait", result.FinalText);
        Assert.EndsWith(".jpg", result.ImagePath, StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(result.ImagePath));
        Assert.Equal("assistant", result.AssistantHistoryMessage.Role);
        Assert.Equal(result.FinalText, result.AssistantHistoryMessage.Content);

        List<Message> messages = await db.GetMessagesAsync(conversationId);
        Message message = Assert.Single(messages);
        Assert.Equal("assistant", message.Role);
        Assert.Equal(result.FinalText, message.Content);
        Assert.Equal(result.ImagePath, message.ImagePath);
        Assert.NotNull(message.AttachmentsJson);
        Assert.Single(message.Attachments);

        MessageAttachment attachment = message.Attachments[0];
        Assert.Equal(result.ImagePath, attachment.FilePath);
        Assert.Equal(Path.GetFileName(result.ImagePath), attachment.FileName);
        Assert.Equal("image/jpeg", attachment.MimeType);
        Assert.Equal(4, attachment.SizeBytes);
        Assert.True(attachment.IsImage);
        Assert.True(attachment.IsInlinePreview);
    }

    [Fact]
    public async Task GenerateAsync_WhenConversationIdIsNull_DoesNotPersistAConversationMessage()
    {
        using var db = new DatabaseService(_dbPath);
        await db.InitializeAsync();

        int providerId = await db.InsertProviderAsync(new Provider
        {
            Name = "Image Provider",
            Type = "OpenAI",
            ApiKey = "sk-test",
            Model = "gpt-4o",
            IsEnabled = true
        });

        int conversationId = await db.CreateConversationAsync(providerId, "Unused conversation");
        var service = new GeneratedImageApplicationService(new ChatSessionApplicationService(db, db));
        var provider = new StubImageGenerationProvider(
            success: true,
            imageBytes: [9, 8, 7],
            imageMimeType: "image/png");

        var result = await service.GenerateAsync(provider, "draw a skyline", conversationId: null, CancellationToken.None);

        Assert.Contains(Path.Combine("GeneratedImages", "temp"), result.ImagePath, StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(result.ImagePath));
        Assert.Empty(await db.GetMessagesAsync(conversationId));
    }

    [Theory]
    [MemberData(nameof(InvalidGenerationResults))]
    public async Task GenerateAsync_ThrowsInvalidOperationException_WhenProviderReturnsInvalidResult(
        bool success,
        byte[]? imageBytes,
        string? errorMessage,
        string expectedMessage)
    {
        using var db = new DatabaseService(_dbPath);
        await db.InitializeAsync();

        var service = new GeneratedImageApplicationService(new ChatSessionApplicationService(db, db));
        var provider = new StubImageGenerationProvider(success, imageBytes, errorMessage: errorMessage);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GenerateAsync(provider, "draw a bridge", conversationId: null, CancellationToken.None));

        Assert.Equal(expectedMessage, exception.Message);
    }

    public static IEnumerable<object?[]> InvalidGenerationResults()
    {
        yield return new object?[] { false, new byte[] { 1 }, "Provider failed", "Provider failed" };
        yield return new object?[] { true, Array.Empty<byte>(), null, "Image generation failed." };
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
                return;
            }

            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
        }
    }

    private sealed class StubImageGenerationProvider : IAiProvider, IImageGenerationProvider
    {
        private readonly bool _success;
        private readonly byte[]? _imageBytes;
        private readonly string? _errorMessage;
        private readonly string? _revisedPrompt;

        public StubImageGenerationProvider(
            bool success,
            byte[]? imageBytes,
            string? errorMessage = null,
            string imageMimeType = "image/png",
            string? revisedPrompt = null)
        {
            _success = success;
            _imageBytes = imageBytes;
            _errorMessage = errorMessage;
            _revisedPrompt = revisedPrompt;
            ImageMimeType = imageMimeType;
        }

        public string ProviderType => "Image";
        public string DisplayName => "Image";
        public ProviderCapabilities Capabilities => ProviderCapabilities.SystemPrompt;
        public ToolCallMode ToolCallMode => ToolCallMode.TextBased;
        public ToolOutputFormat ToolOutputFormat => ToolOutputFormat.AireText;
        public bool SupportsImageGeneration => true;
        public string ImageMimeType { get; }

        public bool Has(ProviderCapabilities cap) => (Capabilities & cap) == cap;
        public void Initialize(ProviderConfig config) { }
        public void PrepareForCapabilityTesting() { }
        public void SetToolsEnabled(bool enabled) { }
        public void SetEnabledToolCategories(IEnumerable<string>? categories) { }

        public Task<AiResponse> SendChatAsync(IEnumerable<ChatMessage> messages, CancellationToken cancellationToken = default)
            => Task.FromResult(new AiResponse { IsSuccess = true, Content = string.Empty });

        public async IAsyncEnumerable<string> StreamChatAsync(IEnumerable<ChatMessage> messages, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            yield break;
        }

        public Task<ProviderValidationResult> ValidateConfigurationAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(ProviderValidationResult.Ok());

        public Task<TokenUsage?> GetTokenUsageAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<TokenUsage?>(null);

        public Task<ImageGenerationResult> GenerateImageAsync(string prompt, CancellationToken cancellationToken = default)
            => Task.FromResult(new ImageGenerationResult
            {
                IsSuccess = _success,
                ImageBytes = _imageBytes,
                ImageMimeType = ImageMimeType,
                RevisedPrompt = _revisedPrompt,
                ErrorMessage = _errorMessage
            });
    }
}
