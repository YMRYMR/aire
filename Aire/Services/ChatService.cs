using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Aire.Providers;
using ProviderChatMessage = Aire.Providers.ChatMessage;

namespace Aire.Services
{
    /// <summary>
    /// Orchestrates chat interactions with AI providers.
    /// </summary>
    public class ChatService
    {
        private readonly ProviderFactory _providerFactory;
        private readonly ChatOrchestrator _orchestrator = new();

        /// <summary>
        /// Raised when a provider emits another streaming response chunk.
        /// </summary>
        public event EventHandler<string>? ResponseChunkReceived
        {
            add => _orchestrator.ResponseChunkReceived += value;
            remove => _orchestrator.ResponseChunkReceived -= value;
        }

        /// <summary>
        /// Raised when a full non-streaming or streaming response completes successfully.
        /// </summary>
        public event EventHandler<AiResponse>? ResponseCompleted
        {
            add => _orchestrator.ResponseCompleted += value;
            remove => _orchestrator.ResponseCompleted -= value;
        }

        /// <summary>
        /// Raised when provider execution fails.
        /// </summary>
        public event EventHandler<string>? ErrorOccurred
        {
            add => _orchestrator.ErrorOccurred += value;
            remove => _orchestrator.ErrorOccurred -= value;
        }

        /// <summary>
        /// Creates the chat service wrapper used by older app code and tests.
        /// </summary>
        /// <param name="databaseService">Database service retained for compatibility while persistence stays app-side.</param>
        /// <param name="providerFactory">Factory used to resolve active provider instances.</param>
        public ChatService(ProviderFactory providerFactory)
        {
            _providerFactory = providerFactory ?? throw new ArgumentNullException(nameof(providerFactory));
        }

        /// <summary>
        /// Sets the current provider by its ID.
        /// </summary>
        /// <param name="providerId">Persisted provider id that should become active.</param>
        public async Task SetProviderAsync(int providerId)
        {
            var provider = await _providerFactory.GetCurrentProviderAsync(providerId).ConfigureAwait(false);
            _orchestrator.SetProvider(provider);
        }

        /// <summary>
        /// Sends a chat message and returns the full response.
        /// </summary>
        /// <param name="userMessage">User text to send.</param>
        /// <param name="imagePath">Optional image path to send alongside the message.</param>
        /// <returns>The completed AI response.</returns>
        public async Task<AiResponse> SendMessageAsync(string userMessage, string? imagePath = null)
        {
            return await _orchestrator.SendMessageAsync(userMessage, imagePath).ConfigureAwait(false);
        }

        /// <summary>
        /// Sends a full conversation history and returns the AI's next response.
        /// Use this when you need multi-turn context (e.g., with tool call results injected).
        /// </summary>
        /// <param name="messages">Full provider-facing conversation history.</param>
        /// <param name="cancellationToken">Cancellation token for the provider request.</param>
        /// <returns>The completed AI response for the supplied history.</returns>
        public async Task<AiResponse> SendMessageWithHistoryAsync(
            IEnumerable<ProviderChatMessage> messages,
            CancellationToken cancellationToken = default)
        {
            return await _orchestrator.SendMessageWithHistoryAsync(messages, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Sends a chat message and streams the response incrementally.
        /// </summary>
        /// <param name="userMessage">User text to send.</param>
        /// <param name="imagePath">Optional image path to send alongside the message.</param>
        /// <param name="cancellationToken">Cancellation token for the streaming request.</param>
        public async Task StreamMessageAsync(string userMessage, string? imagePath = null, CancellationToken cancellationToken = default)
        {
            await _orchestrator.StreamMessageAsync(userMessage, imagePath, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Legacy placeholder retained for compatibility while conversation storage remains app-side.
        /// </summary>
        /// <param name="providerId">Unused legacy provider filter.</param>
        /// <returns>An empty list retained for compatibility with older call sites.</returns>
        public async Task<List<Conversation>> GetConversationsAsync(int? providerId = null)
        {
            await Task.CompletedTask;
            return new List<Conversation>();
        }

        /// <summary>
        /// Legacy placeholder retained for compatibility while message persistence remains app-side.
        /// </summary>
        /// <param name="conversationId">Unused legacy conversation id.</param>
        /// <param name="role">Unused legacy message role.</param>
        /// <param name="content">Unused legacy message text.</param>
        /// <param name="imagePath">Unused legacy image path.</param>
        public async Task SaveMessageAsync(int conversationId, string role, string content, string? imagePath = null)
        {
            await Task.CompletedTask;
        }
    }

    /// <summary>
    /// Legacy conversation DTO retained for compatibility with older callers.
    /// </summary>
    public class Conversation
    {
        public int Id { get; set; }
        public int ProviderId { get; set; }
        public string Title { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
