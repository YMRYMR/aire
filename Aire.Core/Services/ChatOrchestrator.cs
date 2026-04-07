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
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                ErrorOccurred?.Invoke(this, "An unexpected error occurred.");
                return new AiResponse
                {
                    IsSuccess = false,
                    ErrorMessage = "An unexpected error occurred."
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
                var fullResponse = new System.Text.StringBuilder();
                // ConfigureAwait(false) keeps each iteration on the thread pool rather than
                // marshalling back to the WPF dispatcher after every chunk.  Without it the
                // loop body runs at Normal priority (9) on the UI thread, starving Render (7)
                // passes and causing the entire response to appear at once instead of streaming.
                await foreach (var chunk in provider.StreamChatAsync(messages, cancellationToken)
                    .WithCancellation(cancellationToken)
                    .ConfigureAwait(false))
                {
                    fullResponse.Append(chunk);
                    ResponseChunkReceived?.Invoke(this, chunk);
                }

                ResponseCompleted?.Invoke(this, new AiResponse
                {
                    Content = fullResponse.ToString(),
                    IsSuccess = true
                });
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                ErrorOccurred?.Invoke(this, "An unexpected error occurred.");
            }
        }

        public async Task<AiResponse> StreamMessageWithHistoryAsync(
            IEnumerable<ChatMessage> messages,
            CancellationToken cancellationToken = default)
        {
            var provider = _currentProvider ?? throw new InvalidOperationException("No AI provider selected.");

            try
            {
                var fullResponse = new System.Text.StringBuilder();
                // ConfigureAwait(false): same reason as StreamMessageAsync above.
                await foreach (var chunk in provider.StreamChatAsync(messages, cancellationToken)
                    .WithCancellation(cancellationToken)
                    .ConfigureAwait(false))
                {
                    fullResponse.Append(chunk);
                    ResponseChunkReceived?.Invoke(this, chunk);
                }

                var response = new AiResponse
                {
                    Content = fullResponse.ToString(),
                    IsSuccess = true
                };
                ResponseCompleted?.Invoke(this, response);
                return response;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                ErrorOccurred?.Invoke(this, "An unexpected error occurred.");
                return new AiResponse
                {
                    IsSuccess = false,
                    ErrorMessage = "An unexpected error occurred."
                };
            }
        }
    }
}
