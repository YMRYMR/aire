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
            Assert.Equal("boom", exception.Error);
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
    }
}
