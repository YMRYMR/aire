using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Aire.Providers;
using Aire.Services;
using Aire.Domain.Providers;
using Xunit;

namespace Aire.Tests.Capabilities
{
    public class CapabilityTestRunnerTests : TestBase
    {
        private sealed class StubProvider(Func<IReadOnlyList<Aire.Providers.ChatMessage>, AiResponse> responder) : IAiProvider
        {
            public string ProviderType => "Stub";
            public string DisplayName => "Stub";
            public ProviderCapabilities Capabilities => ProviderCapabilities.ToolCalling | ProviderCapabilities.SystemPrompt;
            public ToolCallMode ToolCallMode => ToolCallMode.TextBased;
            public ToolOutputFormat ToolOutputFormat => ToolOutputFormat.AireText;

            public bool Has(ProviderCapabilities cap) => (Capabilities & cap) == cap;

            public void Initialize(ProviderConfig config) { }
            public void PrepareForCapabilityTesting() { }
            public void SetToolsEnabled(bool enabled) { }
            public void SetEnabledToolCategories(IEnumerable<string>? categories) { }

            public Task<AiResponse> SendChatAsync(IEnumerable<Aire.Providers.ChatMessage> messages, CancellationToken cancellationToken = default)
                => Task.FromResult(responder(messages.ToList()));

            public async IAsyncEnumerable<string> StreamChatAsync(IEnumerable<Aire.Providers.ChatMessage> messages, [EnumeratorCancellation] CancellationToken cancellationToken = default)
            {
                yield break;
            }

            public Task<ProviderValidationResult> ValidateConfigurationAsync(CancellationToken cancellationToken = default) => Task.FromResult(ProviderValidationResult.Ok());

            public Task<TokenUsage?> GetTokenUsageAsync(CancellationToken cancellationToken = default) => Task.FromResult<TokenUsage?>(null);
        }

        private sealed class ThrowingProvider(string message) : IAiProvider
        {
            public string ProviderType => "Throwing";
            public string DisplayName => "Throwing";
            public ProviderCapabilities Capabilities => ProviderCapabilities.ToolCalling;
            public ToolCallMode ToolCallMode => ToolCallMode.TextBased;
            public ToolOutputFormat ToolOutputFormat => ToolOutputFormat.AireText;

            public bool Has(ProviderCapabilities cap) => (Capabilities & cap) == cap;

            public void Initialize(ProviderConfig config) { }
            public void PrepareForCapabilityTesting() { }
            public void SetToolsEnabled(bool enabled) { }
            public void SetEnabledToolCategories(IEnumerable<string>? categories) { }

            public Task<AiResponse> SendChatAsync(IEnumerable<Aire.Providers.ChatMessage> messages, CancellationToken cancellationToken = default)
                => throw new InvalidOperationException(message);

            public async IAsyncEnumerable<string> StreamChatAsync(IEnumerable<Aire.Providers.ChatMessage> messages, [EnumeratorCancellation] CancellationToken cancellationToken = default)
            {
                yield break;
            }

            public Task<ProviderValidationResult> ValidateConfigurationAsync(CancellationToken cancellationToken = default) => Task.FromResult(ProviderValidationResult.Fail("Invalid"));

            public Task<TokenUsage?> GetTokenUsageAsync(CancellationToken cancellationToken = default) => Task.FromResult<TokenUsage?>(null);
        }

        private sealed class PromptCapturingProvider(ToolOutputFormat toolOutputFormat, string responseContent) : IAiProvider
        {
            public string ProviderType => "PromptCapture";
            public string DisplayName => "PromptCapture";
            public ProviderCapabilities Capabilities => ProviderCapabilities.ToolCalling | ProviderCapabilities.SystemPrompt;
            public ToolCallMode ToolCallMode => ToolCallMode.TextBased;
            public ToolOutputFormat ToolOutputFormat => toolOutputFormat;
            public int PrepareCalls { get; private set; }
            public IReadOnlyList<Aire.Providers.ChatMessage> LastMessages { get; private set; } = [];

            public bool Has(ProviderCapabilities cap) => (Capabilities & cap) == cap;
            public void Initialize(ProviderConfig config) { }
            public void PrepareForCapabilityTesting() => PrepareCalls++;
            public void SetToolsEnabled(bool enabled) { }
            public void SetEnabledToolCategories(IEnumerable<string>? categories) { }

            public Task<AiResponse> SendChatAsync(IEnumerable<Aire.Providers.ChatMessage> messages, CancellationToken cancellationToken = default)
            {
                LastMessages = messages.ToList();
                return Task.FromResult(new AiResponse { IsSuccess = true, Content = responseContent });
            }

            public async IAsyncEnumerable<string> StreamChatAsync(IEnumerable<Aire.Providers.ChatMessage> messages, [EnumeratorCancellation] CancellationToken cancellationToken = default)
            {
                yield break;
            }

            public Task<ProviderValidationResult> ValidateConfigurationAsync(CancellationToken cancellationToken = default) => Task.FromResult(ProviderValidationResult.Ok());
            public Task<TokenUsage?> GetTokenUsageAsync(CancellationToken cancellationToken = default) => Task.FromResult<TokenUsage?>(null);
        }

        private sealed class ImageGenerationProvider(bool success, byte[]? imageBytes = null, string? errorMessage = null)
            : IAiProvider, IImageGenerationProvider
        {
            public string ProviderType => "Image";
            public string DisplayName => "Image";
            public ProviderCapabilities Capabilities => ProviderCapabilities.SystemPrompt;
            public ToolCallMode ToolCallMode => ToolCallMode.TextBased;
            public ToolOutputFormat ToolOutputFormat => ToolOutputFormat.AireText;
            public bool SupportsImageGeneration => true;

            public bool Has(ProviderCapabilities cap) => (Capabilities & cap) == cap;
            public void Initialize(ProviderConfig config) { }
            public void PrepareForCapabilityTesting() { }
            public void SetToolsEnabled(bool enabled) { }
            public void SetEnabledToolCategories(IEnumerable<string>? categories) { }

            public Task<AiResponse> SendChatAsync(IEnumerable<Aire.Providers.ChatMessage> messages, CancellationToken cancellationToken = default)
                => Task.FromResult(new AiResponse { IsSuccess = true, Content = string.Empty });

            public async IAsyncEnumerable<string> StreamChatAsync(IEnumerable<Aire.Providers.ChatMessage> messages, [EnumeratorCancellation] CancellationToken cancellationToken = default)
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
                    IsSuccess = success,
                    ImageBytes = imageBytes,
                    ErrorMessage = errorMessage,
                    ImageMimeType = "image/png"
                });
        }

        [Fact]
        public async Task CapabilityTestRunner_RunOne_CoversSuccessAndFailureBranches()
        {
            CapabilityTest test = CapabilityTestRunner.AllTests.First(t => t.Id == "list_dir");
            
            CapabilityTestResult success = await CapabilityTestRunner.RunOneAsync(new StubProvider(_ => new AiResponse
            {
                IsSuccess = true,
                Content = "<tool_call>{\"tool\":\"list_directory\"}</tool_call>"
            }), test, CancellationToken.None);
            
            Assert.True(success.Passed);
            Assert.Equal("list_directory", success.ActualTool);

            CapabilityTestResult apiError = await CapabilityTestRunner.RunOneAsync(new StubProvider(_ => new AiResponse
            {
                IsSuccess = false,
                ErrorMessage = "network"
            }), test, CancellationToken.None);
            
            Assert.False(apiError.Passed);
            Assert.Contains("API error", apiError.Error, StringComparison.OrdinalIgnoreCase);

            CapabilityTestResult noToolCall = await CapabilityTestRunner.RunOneAsync(new StubProvider(_ => new AiResponse
            {
                IsSuccess = true,
                Content = "plain answer"
            }), test, CancellationToken.None);
            
            Assert.False(noToolCall.Passed);
            Assert.Equal("No tool call in response", noToolCall.Error);

            CapabilityTestResult wrongTool = await CapabilityTestRunner.RunOneAsync(new StubProvider(_ => new AiResponse
            {
                IsSuccess = true,
                Content = "<tool_call>{\"tool\":\"read_file\"}</tool_call>"
            }), test, CancellationToken.None);
            
            Assert.False(wrongTool.Passed);
            Assert.Equal("read_file", wrongTool.ActualTool);
            Assert.Contains("Expected:", wrongTool.Error, StringComparison.Ordinal);

            CapabilityTestResult exception = await CapabilityTestRunner.RunOneAsync(new ThrowingProvider("boom"), test, CancellationToken.None);
            
            Assert.False(exception.Passed);
            Assert.Equal("Capability test failed.", exception.Error);
        }

        [Fact]
        public async Task CapabilityTestRunner_RunAll_EnumeratesEveryTest()
        {
            CapabilityTestRunner runner = new CapabilityTestRunner();
            StubProvider provider = new StubProvider(messages =>
            {
                string content = messages.Last().Content;
                string text = content.Contains("browser", StringComparison.OrdinalIgnoreCase) ? "list_browser_tabs" : "list_directory";
                return new AiResponse
                {
                    IsSuccess = true,
                    Content = $"<tool_call>{{\"tool\":\"{text}\"}}</tool_call>"
                };
            });

            await runner.RunAsync(provider, CancellationToken.None);
            Assert.Equal(CapabilityTestRunner.AllTests.Count, runner.Results.Count);
        }

        [Theory]
        [InlineData(ToolOutputFormat.Hermes, "{\"name\": \"TOOL_NAME\", \"arguments\": {...parameters}}")]
        [InlineData(ToolOutputFormat.React, "{\"action\": \"TOOL_NAME\", \"action_input\": {...parameters}}")]
        [InlineData(ToolOutputFormat.NativeToolCalls, "<tool_call>{\"tool\": \"TOOL_NAME\", ...parameters}</tool_call>")]
        public async Task CapabilityTestRunner_RunOne_UsesExpectedPromptFormat(ToolOutputFormat format, string expectedSystemPromptText)
        {
            CapabilityTest test = CapabilityTestRunner.AllTests.First(t => t.Id == "list_dir");
            PromptCapturingProvider provider = new PromptCapturingProvider(format, "<tool_call>{\"tool\":\"list_directory\"}</tool_call>");

            CapabilityTestResult result = await CapabilityTestRunner.RunOneAsync(provider, test, CancellationToken.None);

            Assert.True(result.Passed);
            Assert.Equal("system", provider.LastMessages[0].Role);
            Assert.Contains(expectedSystemPromptText, provider.LastMessages[0].Content, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(test.Prompt, provider.LastMessages[1].Content);
        }

        [Fact]
        public async Task CapabilityTestRunner_RunAll_CallsPrepareForCapabilityTesting_Once()
        {
            CapabilityTestRunner runner = new CapabilityTestRunner();
            PromptCapturingProvider provider = new PromptCapturingProvider(
                ToolOutputFormat.AireText,
                "<tool_call>{\"tool\":\"list_directory\"}</tool_call>");

            await runner.RunAsync(provider, CancellationToken.None);

            Assert.Equal(1, provider.PrepareCalls);
        }

        [Fact]
        public async Task CapabilityTestRunner_RunOne_SupportsImageGenerationTests()
        {
            CapabilityTest test = CapabilityTestRunner.AllTests.First(t => t.Id == "generate_image");

            CapabilityTestResult success = await CapabilityTestRunner.RunOneAsync(
                new ImageGenerationProvider(true, [1, 2, 3]),
                test,
                CancellationToken.None);

            Assert.True(success.Passed);
            Assert.Equal("generate_image", success.ActualTool);
        }

        [Fact]
        public async Task CapabilityTestRunner_RunOne_FailsImageGeneration_WhenUnsupported()
        {
            CapabilityTest test = CapabilityTestRunner.AllTests.First(t => t.Id == "generate_image");

            CapabilityTestResult result = await CapabilityTestRunner.RunOneAsync(
                new StubProvider(_ => new AiResponse { IsSuccess = true, Content = string.Empty }),
                test,
                CancellationToken.None);

            Assert.False(result.Passed);
            Assert.Equal("Provider does not support image generation", result.Error);
        }

        [Fact]
        public async Task CapabilityTestRunner_RunOne_FailsImageGeneration_WhenProviderReturnsNoBytes()
        {
            CapabilityTest test = CapabilityTestRunner.AllTests.First(t => t.Id == "generate_image");

            CapabilityTestResult result = await CapabilityTestRunner.RunOneAsync(
                new ImageGenerationProvider(true, []),
                test,
                CancellationToken.None);

            Assert.False(result.Passed);
            Assert.Equal("Provider returned no image data", result.Error);
        }

        [Fact]
        public void CapabilityTestRunner_AllTests_GroupsImageCapabilitiesUnderImages()
        {
            CapabilityTest generate = CapabilityTestRunner.AllTests.First(t => t.Id == "generate_image");
            CapabilityTest showFile = CapabilityTestRunner.AllTests.First(t => t.Id == "show_image_file");
            CapabilityTest showUrl = CapabilityTestRunner.AllTests.First(t => t.Id == "show_image_url");

            Assert.Equal("Images", generate.Category);
            Assert.Equal(CapabilityTestKind.ImageGeneration, generate.Kind);
            Assert.Equal("Images", showFile.Category);
            Assert.Equal("Images", showUrl.Category);
        }
    }
}
