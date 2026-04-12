using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Aire.Providers;
using Aire.Tests.Infrastructure;
using Xunit;

namespace Aire.Tests.Providers;

public class GoogleAiProviderTests
{
    [Fact]
    public void ProviderType_IsGoogleAI()
    {
        GoogleAiProvider googleAiProvider = new GoogleAiProvider();
        Assert.Equal("GoogleAI", googleAiProvider.ProviderType);
    }

    [Fact]
    public void DisplayName_IsGoogleAIGemini()
    {
        GoogleAiProvider googleAiProvider = new GoogleAiProvider();
        Assert.Equal("Google AI (Gemini)", googleAiProvider.DisplayName);
    }

    [Fact]
    public void FieldHints_ShowBaseUrl_IsFalse()
    {
        GoogleAiProvider googleAiProvider = new GoogleAiProvider();
        Assert.False(googleAiProvider.FieldHints.ShowBaseUrl);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(new object[] { "" })]
    [InlineData(new object[] { " " })]
    public async Task ValidateConfigurationAsync_EmptyApiKey_ReturnsFalse(string? apiKey)
    {
        GoogleAiProvider provider = new GoogleAiProvider();
        provider.Initialize(new ProviderConfig
        {
            ApiKey = apiKey,
            Model = "gemini-1.5-pro"
        });
        var validation = await provider.ValidateConfigurationAsync(CancellationToken.None);
        Assert.False(validation.IsValid);
        Assert.NotNull(validation.Error);
        Assert.NotEmpty(validation.Error);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(new object[] { "" })]
    [InlineData(new object[] { " " })]
    public async Task FetchLiveModelsAsync_EmptyApiKey_ReturnsNull(string? apiKey)
    {
        GoogleAiProvider provider = new GoogleAiProvider();
        Assert.Null(await provider.FetchLiveModelsAsync(apiKey, null, CancellationToken.None));
    }

    [Fact]
    public void BuildBody_IncludesSystemInstructionRoleMappingAndToolDeclarations()
    {
        GoogleAiProvider provider = new GoogleAiProvider();
        provider.Initialize(new ProviderConfig
        {
            ApiKey = "test-key",
            BaseUrl = "https://generativelanguage.googleapis.com/",
            Model = "gemini-2.0-flash",
            Temperature = 0.25,
            MaxTokens = 512,
            ModelCapabilities = ["tools", "filesystem", "mouse"]
        });

        var buildBody = typeof(GoogleAiProvider).GetMethod("BuildBody", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var json = Assert.IsType<string>(buildBody.Invoke(provider, [new[]
        {
            new ChatMessage { Role = "system", Content = "Follow the rules." },
            new ChatMessage { Role = "user", Content = "Hello" },
            new ChatMessage { Role = "assistant", Content = "Hi" }
        }, null, true, true]));

        using JsonDocument doc = JsonDocument.Parse(json);
        Assert.Equal("Follow the rules.", doc.RootElement.GetProperty("system_instruction").GetProperty("parts")[0].GetProperty("text").GetString());
        Assert.Equal(2, doc.RootElement.GetProperty("contents").GetArrayLength());
        Assert.Equal("user", doc.RootElement.GetProperty("contents")[0].GetProperty("role").GetString());
        Assert.Equal("model", doc.RootElement.GetProperty("contents")[1].GetProperty("role").GetString());
        Assert.Equal(0.25, doc.RootElement.GetProperty("generationConfig").GetProperty("temperature").GetDouble());
        Assert.Equal(512, doc.RootElement.GetProperty("generationConfig").GetProperty("maxOutputTokens").GetInt32());
        Assert.True(doc.RootElement.GetProperty("tools")[0].GetProperty("function_declarations").GetArrayLength() > 0);
    }

    [Fact]
    public void BuildBody_OmitsSystemInstruction_WhenNoSystemMessageExists()
    {
        GoogleAiProvider provider = new GoogleAiProvider();
        provider.Initialize(new ProviderConfig
        {
            ApiKey = "test-key",
            Model = "gemini-2.0-flash",
            ModelCapabilities = ["tools"]
        });

        var buildBody = typeof(GoogleAiProvider).GetMethod("BuildBody", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var json = Assert.IsType<string>(buildBody.Invoke(provider, [new[]
        {
            new ChatMessage { Role = "user", Content = "Hello" }
        }, null, true, true]));

        using JsonDocument doc = JsonDocument.Parse(json);
        Assert.False(doc.RootElement.TryGetProperty("system_instruction", out _));
        Assert.Single(doc.RootElement.GetProperty("contents").EnumerateArray());
    }

    [Fact]
    public void ApiBaseAndStreamUrl_RespectTrimmedBaseUrl()
    {
        GoogleAiProvider provider = new GoogleAiProvider();
        provider.Initialize(new ProviderConfig
        {
            ApiKey = "test-key",
            BaseUrl = "https://example.test/google/",
            Model = "gemini-2.0-flash"
        });

        var apiBase = Assert.IsType<string>(typeof(GoogleAiProvider).GetProperty("ApiBase", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(provider));
        var streamUrl = Assert.IsType<string>(typeof(GoogleAiProvider).GetProperty("StreamUrl", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(provider));

        Assert.Equal("https://example.test/google", apiBase);
        Assert.Equal("https://example.test/google/v1beta/models/gemini-2.0-flash:streamGenerateContent?key=test-key&alt=sse", streamUrl);
    }

    [Fact]
    public async Task ValidateConfigurationAsync_UsesConfiguredBaseUrl()
    {
        using var server = new SimpleJsonServer((method, path, _) =>
        {
            if (method == "GET" && path.StartsWith("/v1beta/models", StringComparison.Ordinal))
                return SimpleJsonServer.Json(200, """{"models":[{"name":"models/gemini-2.0-flash"}]}""");

            return SimpleJsonServer.Json(404, """{"error":"missing"}""");
        });

        GoogleAiProvider provider = new GoogleAiProvider();
        provider.Initialize(new ProviderConfig
        {
            ApiKey = "test-key",
            BaseUrl = server.BaseUrl,
            Model = "gemini-2.0-flash"
        });

        var validation = await provider.ValidateConfigurationAsync(CancellationToken.None);

        Assert.True(validation.IsValid);
    }

    [Fact]
    public async Task SendChatAsync_NetworkFailure_ReturnsSanitizedError()
    {
        GoogleAiProvider provider = new GoogleAiProvider();
        provider.Initialize(new ProviderConfig
        {
            ApiKey = "test-key",
            BaseUrl = "http://127.0.0.1:1/",
            Model = "gemini-2.0-flash"
        });

        var response = await provider.SendChatAsync([new ChatMessage { Role = "user", Content = "Hello" }], CancellationToken.None);

        Assert.False(response.IsSuccess);
        Assert.NotNull(response.ErrorMessage);
        Assert.DoesNotContain("127.0.0.1", response.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(":1", response.ErrorMessage);
    }

    [Fact]
    public async Task ValidateConfigurationAsync_NetworkFailure_ReturnsSanitizedError()
    {
        GoogleAiProvider provider = new GoogleAiProvider();
        provider.Initialize(new ProviderConfig
        {
            ApiKey = "test-key",
            BaseUrl = "http://127.0.0.1:1/",
            Model = "gemini-2.0-flash"
        });

        var validation = await provider.ValidateConfigurationAsync(CancellationToken.None);

        Assert.False(validation.IsValid);
        Assert.Equal("Google AI configuration validation failed.", validation.Error);
        Assert.DoesNotContain("127.0.0.1", validation.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FetchLiveModelsAsync_FiltersToGeminiModelsWithGenerateContent()
    {
        using var server = new SimpleJsonServer((method, path, _) =>
        {
            if (method == "GET" && path.StartsWith("/v1beta/models", StringComparison.Ordinal))
            {
                return SimpleJsonServer.Json(200,
                    """
                    {
                      "models": [
                        { "name": "models/gemini-2.0-flash", "displayName": "Gemini Flash", "supportedGenerationMethods": ["generateContent"] },
                        { "name": "models/gemini-1.5-pro", "displayName": "Gemini Pro", "supportedGenerationMethods": ["generateContent"] },
                        { "name": "models/text-embedding-004", "displayName": "Embedding", "supportedGenerationMethods": ["embedContent"] },
                        { "name": "models/gemini-tuned", "displayName": "Tuned", "supportedGenerationMethods": ["countTokens"] }
                      ]
                    }
                    """);
            }

            return SimpleJsonServer.Json(404, """{"error":"missing"}""");
        });

        GoogleAiProvider provider = new GoogleAiProvider();

        var models = await provider.FetchLiveModelsAsync("test-key", server.BaseUrl, CancellationToken.None);

        Assert.NotNull(models);
        string[] ids = models!.Select(m => m.Id).ToArray();
        Assert.Equal(new[] { "gemini-2.0-flash", "gemini-1.5-pro" }, ids);
        Assert.Equal("Gemini Flash", models[0].DisplayName);
    }

    [Fact]
    public async Task StreamChatAsync_ParsesTextAndFunctionCalls_FromSse()
    {
        using var server = new SimpleJsonServer((method, path, _) =>
        {
            if (method == "POST" && path.StartsWith("/v1beta/models/gemini-2.0-flash:streamGenerateContent", StringComparison.Ordinal))
            {
                return SimpleJsonServer.Sse(200,
                [
                    """data: {"candidates":[{"content":{"parts":[{"text":"Hello "}]}}]}""",
                    """data: {"candidates":[{"content":{"parts":[{"functionCall":{"name":"read_file","args":{"path":"C:\\repo\\file.txt"}}}]}}]}""",
                    "data: [DONE]"
                ]);
            }

            return SimpleJsonServer.Json(404, """{"error":"missing"}""");
        });

        GoogleAiProvider provider = new GoogleAiProvider();
        provider.Initialize(new ProviderConfig
        {
            ApiKey = "test-key",
            BaseUrl = server.BaseUrl,
            Model = "gemini-2.0-flash",
            ModelCapabilities = ["tools"]
        });

        var chunks = new List<string>();
        await foreach (var chunk in provider.StreamChatAsync([new ChatMessage { Role = "user", Content = "Read the file." }], CancellationToken.None))
            chunks.Add(chunk);

        Assert.Equal("Hello ", chunks[0]);
        Assert.Contains("<tool_call>", chunks[1], StringComparison.Ordinal);
        Assert.Contains("\"tool\":\"read_file\"", chunks[1], StringComparison.Ordinal);
        Assert.Contains("C:\\\\repo\\\\file.txt", chunks[1], StringComparison.Ordinal);
    }

    [Fact]
    public async Task StreamChatAsync_CreatesAndUsesCachedContent_ForCachePreferredPrefix()
    {
        var cacheCreateCount = 0;
        string? cacheCreateBody = null;
        string? streamBody = null;

        using var server = new SimpleJsonServer((method, path, body) =>
        {
            if (method == "POST" && path.StartsWith("/v1beta/cachedContents", StringComparison.Ordinal))
            {
                cacheCreateCount++;
                cacheCreateBody = body;
                return SimpleJsonServer.Json(200, """{"name":"cachedContents/prefix-1"}""");
            }

            if (method == "POST" && path.StartsWith("/v1beta/models/gemini-2.0-flash:streamGenerateContent", StringComparison.Ordinal))
            {
                streamBody = body;
                return SimpleJsonServer.Sse(200,
                [
                    """data: {"candidates":[{"content":{"parts":[{"text":"Cached hello"}]}}]}""",
                    "data: [DONE]"
                ]);
            }

            return SimpleJsonServer.Json(404, """{"error":"missing"}""");
        });

        GoogleAiProvider provider = new GoogleAiProvider();
        provider.Initialize(new ProviderConfig
        {
            ApiKey = "test-key",
            BaseUrl = server.BaseUrl,
            Model = "gemini-2.0-flash",
            ModelCapabilities = ["text"]
        });

        var messages = new[]
        {
            new ChatMessage { Role = "system", Content = "Use plain answers." },
            new ChatMessage { Role = "user", Content = "Stable context", PreferPromptCache = true },
            new ChatMessage { Role = "assistant", Content = "Stable reply", PreferPromptCache = true },
            new ChatMessage { Role = "user", Content = "Fresh question" }
        };

        var chunks = new List<string>();
        await foreach (var chunk in provider.StreamChatAsync(messages, CancellationToken.None))
            chunks.Add(chunk);

        Assert.Equal("Cached hello", string.Concat(chunks));
        Assert.Equal(1, cacheCreateCount);
        Assert.NotNull(cacheCreateBody);
        Assert.NotNull(streamBody);

        using (JsonDocument cacheDoc = JsonDocument.Parse(cacheCreateBody!))
        {
            Assert.Equal("models/gemini-2.0-flash", cacheDoc.RootElement.GetProperty("model").GetString());
            Assert.Equal("Use plain answers.", cacheDoc.RootElement.GetProperty("systemInstruction").GetProperty("parts")[0].GetProperty("text").GetString());
            Assert.Equal(2, cacheDoc.RootElement.GetProperty("contents").GetArrayLength());
        }

        using (JsonDocument streamDoc = JsonDocument.Parse(streamBody!))
        {
            Assert.Equal("cachedContents/prefix-1", streamDoc.RootElement.GetProperty("cachedContent").GetString());
            Assert.Single(streamDoc.RootElement.GetProperty("contents").EnumerateArray());
            Assert.False(streamDoc.RootElement.TryGetProperty("system_instruction", out _));
            Assert.False(streamDoc.RootElement.TryGetProperty("tools", out _));
        }

        await foreach (var _ in provider.StreamChatAsync(messages, CancellationToken.None))
        {
        }

        Assert.Equal(1, cacheCreateCount);
    }

    [Fact]
    public async Task StreamChatAsync_FallsBack_WhenCachedContentCreationFails()
    {
        string? streamBody = null;

        using var server = new SimpleJsonServer((method, path, body) =>
        {
            if (method == "POST" && path.StartsWith("/v1beta/cachedContents", StringComparison.Ordinal))
                return SimpleJsonServer.Json(500, """{"error":"cache failed"}""");

            if (method == "POST" && path.StartsWith("/v1beta/models/gemini-2.0-flash:streamGenerateContent", StringComparison.Ordinal))
            {
                streamBody = body;
                return SimpleJsonServer.Sse(200,
                [
                    """data: {"candidates":[{"content":{"parts":[{"text":"Fallback hello"}]}}]}""",
                    "data: [DONE]"
                ]);
            }

            return SimpleJsonServer.Json(404, """{"error":"missing"}""");
        });

        GoogleAiProvider provider = new GoogleAiProvider();
        provider.Initialize(new ProviderConfig
        {
            ApiKey = "test-key",
            BaseUrl = server.BaseUrl,
            Model = "gemini-2.0-flash",
            ModelCapabilities = ["text"]
        });

        var messages = new[]
        {
            new ChatMessage { Role = "system", Content = "Use plain answers." },
            new ChatMessage { Role = "user", Content = "Stable context", PreferPromptCache = true },
            new ChatMessage { Role = "user", Content = "Fresh question" }
        };

        var chunks = new List<string>();
        await foreach (var chunk in provider.StreamChatAsync(messages, CancellationToken.None))
            chunks.Add(chunk);

        Assert.Equal("Fallback hello", string.Concat(chunks));
        Assert.NotNull(streamBody);
        using JsonDocument streamDoc = JsonDocument.Parse(streamBody!);
        Assert.False(streamDoc.RootElement.TryGetProperty("cachedContent", out _));
        Assert.True(streamDoc.RootElement.TryGetProperty("system_instruction", out _));
    }

    [Fact]
    public async Task SendChatAsync_ReturnsError_WhenStreamingEndpointFails()
    {
        using var server = new SimpleJsonServer((method, path, _) =>
        {
            if (method == "POST" && path.StartsWith("/v1beta/models/gemini-2.0-flash:streamGenerateContent", StringComparison.Ordinal))
                return SimpleJsonServer.Json(500, """{"error":"boom"}""");

            return SimpleJsonServer.Json(404, """{"error":"missing"}""");
        });

        GoogleAiProvider provider = new GoogleAiProvider();
        provider.Initialize(new ProviderConfig
        {
            ApiKey = "test-key",
            BaseUrl = server.BaseUrl,
            Model = "gemini-2.0-flash"
        });

        var response = await provider.SendChatAsync([new ChatMessage { Role = "user", Content = "Hello" }], CancellationToken.None);

        Assert.False(response.IsSuccess);
        Assert.Contains("500", response.ErrorMessage);
    }

    [Fact]
    public async Task GenerateImageAsync_ReturnsInlineImageBytes_ForImageGenerationModel()
    {
        using var server = new SimpleJsonServer((method, path, _) =>
        {
            if (method == "POST" && path == "/v1beta/models/gemini-2.5-flash-image:generateContent")
            {
                return SimpleJsonServer.Json(200,
                    """
                    {
                      "candidates": [
                        {
                          "content": {
                            "parts": [
                              { "text": "Bright neon skyline" },
                              {
                                "inlineData": {
                                  "mimeType": "image/png",
                                  "data": "AQIDBA=="
                                }
                              }
                            ]
                          }
                        }
                      ]
                    }
                    """);
            }

            return SimpleJsonServer.Json(404, """{"error":"missing"}""");
        });

        GoogleAiProvider provider = new GoogleAiProvider();
        provider.Initialize(new ProviderConfig
        {
            ApiKey = "test-key",
            BaseUrl = server.BaseUrl,
            Model = "gemini-2.5-flash-image",
            ModelCapabilities = ["vision", "imagegeneration"]
        });

        var result = await provider.GenerateImageAsync("Draw a neon skyline", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, result.ImageBytes);
        Assert.Equal("image/png", result.ImageMimeType);
        Assert.Equal("Bright neon skyline", result.RevisedPrompt);
        Assert.True(provider.SupportsImageGeneration);
    }

    [Fact]
    public async Task GenerateImageAsync_Fails_WhenImageGenerationModelReturnsNoInlineData()
    {
        using var server = new SimpleJsonServer((method, path, _) =>
        {
            if (method == "POST" && path == "/v1beta/models/gemini-2.5-flash-image:generateContent")
            {
                return SimpleJsonServer.Json(200,
                    """
                    {
                      "candidates": [
                        {
                          "content": {
                            "parts": [
                              { "text": "Only text" }
                            ]
                          }
                        }
                      ]
                    }
                    """);
            }

            return SimpleJsonServer.Json(404, """{"error":"missing"}""");
        });

        GoogleAiProvider provider = new GoogleAiProvider();
        provider.Initialize(new ProviderConfig
        {
            ApiKey = "test-key",
            BaseUrl = server.BaseUrl,
            Model = "gemini-2.5-flash-image",
            ModelCapabilities = ["vision", "imagegeneration"]
        });

        var result = await provider.GenerateImageAsync("Draw a neon skyline", CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("no image data", result.ErrorMessage, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GenerateImageAsync_ReturnsSanitizedError_OnTransportFailure()
    {
        var provider = new GoogleAiProvider();
        provider.Initialize(new ProviderConfig
        {
            ApiKey = "test-key",
            BaseUrl = "http://127.0.0.1:1/",
            Model = "gemini-2.5-flash-image",
            ModelCapabilities = ["imagegeneration"]
        });

        var result = await provider.GenerateImageAsync("Draw a neon skyline", CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Google AI image generation failed.", result.ErrorMessage);
    }

}
