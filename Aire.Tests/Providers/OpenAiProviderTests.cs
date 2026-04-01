using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using Aire.Providers;
using OpenAI.ObjectModels.RequestModels;
using Xunit;

namespace Aire.Tests.Providers
{
    public class OpenAiProviderTests : TestBase
    {
        private sealed class InspectableOpenAiProvider : OpenAiProvider
        {
            public IList<ToolDefinition> ExposeFunctionDefinitions() => GetFunctionDefinitions();

            public OpenAI.ObjectModels.RequestModels.ChatMessage ExposeConvert(Aire.Providers.ChatMessage message)
                => ConvertToOpenAiMessage(message);
        }

        [Fact]
        public void OpenAiProvider_HelperPaths_Work()
        {
            var (host, version) = OpenAiProvider.SplitSdkUrl("https://api.groq.com/openai/");
            Assert.Equal("https://api.groq.com", host);
            Assert.Equal("openai/v1", version);

            string toolCallStr = OpenAiProvider.ConvertFunctionCallToToolCall("execute_command", "{\"command\":\"dir\",\"timeout_seconds\":5,\"shell\":null,\"interactive\":false}");
            Assert.StartsWith("<tool_call>", toolCallStr, StringComparison.Ordinal);
            
            using (JsonDocument doc = JsonDocument.Parse(toolCallStr.Replace("<tool_call>", "").Replace("</tool_call>", "")))
            {
                Assert.Equal("execute_command", doc.RootElement.GetProperty("tool").GetString());
                Assert.Equal("dir", doc.RootElement.GetProperty("command").GetString());
                Assert.Equal(5, doc.RootElement.GetProperty("timeout_seconds").GetInt32());
                Assert.False(doc.RootElement.GetProperty("interactive").GetBoolean());
                Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("shell").ValueKind);
            }

            InspectableOpenAiProvider provider = new InspectableOpenAiProvider();
            provider.Initialize(new ProviderConfig
            {
                ApiKey = "sk-test-key",
                Model = "gpt-4o",
                ModelCapabilities = new List<string> { "tools", "mouse" }
            });

            var definitions = provider.ExposeFunctionDefinitions();
            Assert.NotEmpty(definitions);
            Assert.Contains(definitions, d => d.Function.Name == "begin_mouse_session");
            Assert.DoesNotContain(definitions, d => d.Function.Name == "type_text");

            provider.SetEnabledToolCategories(new[] { "filesystem" });
            var filesystemOnly = provider.ExposeFunctionDefinitions();
            Assert.Contains(filesystemOnly, d => d.Function.Name == "read_file");
            Assert.DoesNotContain(filesystemOnly, d => d.Function.Name == "begin_mouse_session");
            Assert.DoesNotContain(filesystemOnly, d => d.Function.Name == "list_browser_tabs");

            var chatMessage = provider.ExposeConvert(new Aire.Providers.ChatMessage
            {
                Role = "user",
                Content = "describe",
                ImageBytes = new byte[] { 1, 2, 3, 4 },
                ImageMimeType = "image/jpeg"
            });

            Assert.Null(chatMessage.Content);
            Assert.NotNull(chatMessage.Contents);
            Assert.Equal(2, chatMessage.Contents.Count);
            Assert.Equal("text", chatMessage.Contents[0].Type);
            Assert.Equal("image_url", chatMessage.Contents[1].Type);
        }

        [Fact]
        public async Task OpenAiProvider_UninitializedAndNoKeyPaths_Work()
        {
            OpenAiProvider provider = new OpenAiProvider();
            var validation = await provider.ValidateConfigurationAsync(CancellationToken.None);
            Assert.False(validation.IsValid);
            Assert.NotNull(validation.Error);
            await Assert.ThrowsAsync<InvalidOperationException>(() => provider.SendChatAsync(Array.Empty<Aire.Providers.ChatMessage>(), CancellationToken.None));
            
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await foreach (var item in provider.StreamChatAsync(Array.Empty<Aire.Providers.ChatMessage>(), CancellationToken.None))
                {
                }
            });
        }
    }
}
