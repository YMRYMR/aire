using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Aire.Data;
using Aire.Providers;
using ProviderChatMessage = Aire.Providers.ChatMessage;

namespace Aire.AppLayer.Chat
{
    /// <summary>
    /// Handles provider-generated image persistence and transcript shaping.
    /// </summary>
    public sealed class GeneratedImageApplicationService
    {
        public sealed record GeneratedImageTurnResult(
            string FinalText,
            string ImagePath,
            ProviderChatMessage AssistantHistoryMessage);

        private readonly ChatSessionApplicationService _chatSessionService;

        public GeneratedImageApplicationService(ChatSessionApplicationService chatSessionService)
        {
            _chatSessionService = chatSessionService;
        }

        public async Task<GeneratedImageTurnResult> GenerateAsync(
            IImageGenerationProvider provider,
            string prompt,
            int? conversationId,
            CancellationToken cancellationToken = default)
        {
            var result = await provider.GenerateImageAsync(prompt, cancellationToken).ConfigureAwait(false);
            if (!result.IsSuccess || result.ImageBytes == null || result.ImageBytes.Length == 0)
                throw new InvalidOperationException(result.ErrorMessage ?? "Image generation failed.");

            var imagePath = await SaveGeneratedImageAsync(result.ImageBytes, result.ImageMimeType, conversationId).ConfigureAwait(false);
            var finalText = string.IsNullOrWhiteSpace(result.RevisedPrompt)
                ? $"Generated image for: {prompt}"
                : $"Generated image. Revised prompt: {result.RevisedPrompt}";

            if (conversationId.HasValue)
                await _chatSessionService.PersistAssistantMessageAsync(
                    conversationId.Value,
                    finalText,
                    imagePath,
                    new[]
                    {
                        new MessageAttachment
                        {
                            FilePath = imagePath,
                            FileName = Path.GetFileName(imagePath),
                            MimeType = result.ImageMimeType,
                            SizeBytes = new FileInfo(imagePath).Length,
                            IsImage = true,
                            IsInlinePreview = true
                        }
                    }).ConfigureAwait(false);

            return new GeneratedImageTurnResult(
                finalText,
                imagePath,
                new ProviderChatMessage { Role = "assistant", Content = finalText });
        }

        private static async Task<string> SaveGeneratedImageAsync(byte[] imageBytes, string? imageMimeType, int? conversationId)
        {
            var root = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Aire",
                "GeneratedImages");
            var folder = conversationId.HasValue
                ? Path.Combine(root, conversationId.Value.ToString())
                : Path.Combine(root, "temp");
            Directory.CreateDirectory(folder);

            var extension = imageMimeType?.ToLowerInvariant() switch
            {
                "image/jpeg" => ".jpg",
                "image/webp" => ".webp",
                "image/gif" => ".gif",
                "image/bmp" => ".bmp",
                _ => ".png"
            };

            var path = Path.Combine(folder, $"generated_{DateTime.Now:yyyyMMdd_HHmmss_fff}{extension}");
            await File.WriteAllBytesAsync(path, imageBytes).ConfigureAwait(false);
            return path;
        }
    }
}
