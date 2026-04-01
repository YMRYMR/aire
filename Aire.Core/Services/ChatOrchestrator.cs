using Aire.Providers;

namespace Aire.Services
{
    /// <summary>
    /// Platform-agnostic chat orchestration around a selected AI provider.
    /// </summary>
    public sealed class ChatOrchestrator
    {
        private IAiProvider? _currentProvider;

        public event EventHandler<string>? ResponseChunkReceived;
        public event EventHandler<AiResponse>? ResponseCompleted;
        public event EventHandler<string>? ErrorOccurred;

        public void SetProvider(IAiProvider? provider)
        {
            _currentProvider = provider;
        }

        public async Task<AiResponse> SendMessageAsync(string userMessage, string? imagePath = null, CancellationToken cancellationToken = default)
        {
            var messages = new List<ChatMessage>
            {
                new()
                {
                    Role = "user",
                    Content = userMessage,
                    ImagePath = imagePath
                }
            };

            return await SendMessageWithHistoryAsync(messages, cancellationToken).ConfigureAwait(false);
        }

        public async Task<AiResponse> SendMessageWithHistoryAsync(
            IEnumerable<ChatMessage> messages,
            CancellationToken cancellationToken = default)
        {
            var provider = _currentProvider ?? throw new InvalidOperationException("No AI provider selected.");

            try
            {
                var response = await provider.SendChatAsync(messages, cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccess)
                    ErrorOccurred?.Invoke(this, response.ErrorMessage ?? "Unknown error");
                else
                    ResponseCompleted?.Invoke(this, response);

                return response;
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, ex.Message);
                return new AiResponse
                {
                    IsSuccess = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        public async Task StreamMessageAsync(string userMessage, string? imagePath = null, CancellationToken cancellationToken = default)
        {
            var provider = _currentProvider ?? throw new InvalidOperationException("No AI provider selected.");

            var messages = new List<ChatMessage>
            {
                new()
                {
                    Role = "user",
                    Content = userMessage,
                    ImagePath = imagePath
                }
            };

            try
            {
                var fullResponse = string.Empty;
                await foreach (var chunk in provider.StreamChatAsync(messages, cancellationToken).WithCancellation(cancellationToken))
                {
                    fullResponse += chunk;
                    ResponseChunkReceived?.Invoke(this, chunk);
                }

                ResponseCompleted?.Invoke(this, new AiResponse
                {
                    Content = fullResponse,
                    IsSuccess = true
                });
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, ex.Message);
            }
        }
    }
}
