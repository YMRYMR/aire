extern alias AireCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Aire.Data;
using Aire.Providers;
using Xunit;

namespace Aire.Tests.Providers;

[Collection("AppProviderCoverage")]
public class AppProviderCoverageTests
{
    private static string SeedModelsFolder()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"aire_app_provider_{Guid.NewGuid():N}");
        Environment.SetEnvironmentVariable("AIRE_MODELS_FOLDER", folder);
        ModelCatalog.EnsureDefaults();
        return folder;
    }

    private static void ClearModelsFolder(string? folder)
    {
        Environment.SetEnvironmentVariable("AIRE_MODELS_FOLDER", null);
        if (string.IsNullOrWhiteSpace(folder))
            return;

        try
        {
            if (Directory.Exists(folder))
                Directory.Delete(folder, recursive: true);
        }
        catch
        {
        }
    }

    private sealed class InspectableZaiProvider : ZaiProvider
    {
        public string[] ExposeModelPrefixes()
            => (string[])typeof(OpenAiProvider)
                .GetProperty("ModelIdPrefixes", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(this)!;

        public string ExposeBuildModelsUrl(string baseUrl)
            => (string)typeof(ZaiProvider)
                .GetMethod("BuildModelsUrl", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(this, new object[] { baseUrl })!;

        public string ExposeBuildChatCompletionsUrl(string baseUrl)
            => (string)typeof(ZaiProvider)
                .GetMethod("BuildChatCompletionsUrl", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(this, new object[] { baseUrl })!;
    }

    [Fact]
    public async Task ClaudeAiProvider_MetadataAndFailurePaths_Work()
    {
        var provider = new ClaudeAiProvider();
        provider.Initialize(new ProviderConfig
        {
            ApiKey         = string.Empty,
            Model          = "claude-sonnet-4-5",
            TimeoutMinutes = 7
        });

        Assert.Equal("Anthropic",     provider.ProviderType);
        Assert.Equal("Anthropic API", provider.DisplayName);
        Assert.False(provider.FieldHints.ApiKeyRequired);
        Assert.True(provider.Capabilities.HasFlag(ProviderCapabilities.ToolCalling));
        Assert.True(provider.Capabilities.HasFlag(ProviderCapabilities.ImageInput));
        Assert.NotNull(provider.GetDefaultModels());
        Assert.Null(await provider.FetchLiveModelsAsync(null, null, CancellationToken.None));
        Assert.Null(await provider.GetTokenUsageAsync(CancellationToken.None));
        var claudeValidation = await provider.ValidateConfigurationAsync(CancellationToken.None);
        Assert.False(claudeValidation.IsValid);
        Assert.NotNull(claudeValidation.Error);
        Assert.NotEmpty(claudeValidation.Error);

        var response = await provider.SendChatAsync(new[] { new ChatMessage { Role = "user", Content = "hello" } });

        Assert.Equal(TimeSpan.FromMinutes(7), provider.ConfiguredTimeout);
        Assert.False(response.IsSuccess);
        Assert.Equal("Anthropic request failed.", response.ErrorMessage);
    }

    [Fact]
    public async Task ClaudeWebProvider_MetadataAndSessionDependentPaths_Work()
    {
        var seededFolder = SeedModelsFolder();
        var provider = new AireCore::Aire.Providers.ClaudeWebProvider();
        try
        {
            provider.Initialize(new ProviderConfig { Model = "claude-sonnet-4-5" });

            var session        = ClaudeAiSession.Instance;
            bool originalReady = session.IsReady;
            var originalPrompt  = ClaudeAiSession.PromptLogin;
            try
            {
            session.IsReady           = false;
            ClaudeAiSession.PromptLogin = null;

            Assert.Equal("ClaudeWeb",  provider.ProviderType);
            Assert.Equal("Claude.ai",  provider.DisplayName);
            Assert.False(provider.FieldHints.ShowApiKey);
            Assert.False(provider.FieldHints.ApiKeyRequired);
                var webValidation = await provider.ValidateConfigurationAsync(CancellationToken.None);
                Assert.False(webValidation.IsValid);
                Assert.NotNull(webValidation.Error);

                var response = await provider.SendChatAsync(new[] { new ChatMessage { Role = "user", Content = "hello" } });

                Assert.False(response.IsSuccess);
                Assert.NotNull(response.ErrorMessage);
                Assert.NotEmpty(provider.GetDefaultModels());
            }
            finally
            {
                session.IsReady = originalReady;
                ClaudeAiSession.PromptLogin = originalPrompt;
            }
        }
        finally
        {
            ClearModelsFolder(seededFolder);
        }
    }

    [Fact]
    public void ClaudeCodeProvider_MetadataAndFailurePaths_Work()
    {
        var seededFolder = SeedModelsFolder();
        try
        {
            var provider = new ClaudeCodeProvider();
            provider.Initialize(new ProviderConfig { Model = "claude-sonnet-4-5" });

            Assert.Equal("ClaudeCode", provider.ProviderType);
            Assert.Equal("Claude Code", provider.DisplayName);
            Assert.False(provider.FieldHints.ShowApiKey);
            Assert.False(provider.FieldHints.ApiKeyRequired);
            Assert.False(provider.FieldHints.ShowBaseUrl);
            Assert.NotNull(provider.GetDefaultModels());
            Assert.NotEmpty(provider.GetDefaultModels());
            Assert.NotNull(ClaudeCodeProvider.GetCliStatus());
        }
        finally
        {
            ClearModelsFolder(seededFolder);
        }
    }

    [Fact]
    public async Task OllamaProvider_MetadataInitializationAndOfflineValidation_Work()
    {
        string? originalEnv = Environment.GetEnvironmentVariable("OLLAMA_API_KEY");
        Environment.SetEnvironmentVariable("OLLAMA_API_KEY", "env-key");
        try
        {
            var provider = new OllamaProvider();
            provider.Initialize(new ProviderConfig
            {
                ApiKey         = string.Empty,
                BaseUrl        = "http://127.0.0.1:1/",
                Model          = "qwen3:4b",
                TimeoutMinutes = 2,
                Temperature    = 0.3
            });

            Assert.Equal("Ollama",        provider.ProviderType);
            Assert.Equal("Ollama (Local)", provider.DisplayName);
            Assert.False(provider.FieldHints.ApiKeyRequired);
            Assert.Contains(provider.Actions, a => a.Id == "ollama-refresh");
            Assert.Equal("http://127.0.0.1:1",    provider.ConfiguredBaseUrl);
            Assert.Equal("env-key",               provider.ConfiguredApiKey);
            Assert.Equal(TimeSpan.FromMinutes(2), provider.ConfiguredHttpClient.Timeout);
        var ollamaValidation = await provider.ValidateConfigurationAsync(CancellationToken.None);
        Assert.False(ollamaValidation.IsValid);
        Assert.NotNull(ollamaValidation.Error);
            Assert.Null(await provider.FetchLiveModelsAsync(null, "http://127.0.0.1:1", CancellationToken.None));

            var response = await provider.SendChatAsync(new[] { new ChatMessage { Role = "user", Content = "hello" } });

            Assert.False(response.IsSuccess);
            Assert.Contains("127.0.0.1:1", response.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Environment.SetEnvironmentVariable("OLLAMA_API_KEY", originalEnv);
        }
    }

    [Fact]
    public async Task ZaiProvider_MetadataAndEndpointShape_Work()
    {
        var seededFolder = SeedModelsFolder();
        try
        {
            var provider = new InspectableZaiProvider();
            provider.Initialize(new ProviderConfig
            {
                ApiKey = "zai-test-key",
                BaseUrl = "https://api.z.ai/api/paas/v4/",
                Model = "glm-5"
            });

            Assert.Equal("Zai", provider.ProviderType);
            Assert.Equal("Zhipu AI (z.ai)", provider.DisplayName);
            Assert.True(provider.FieldHints.ShowBaseUrl);
            Assert.Contains("glm-", provider.ExposeModelPrefixes());
            Assert.Equal("https://api.z.ai/api/paas/v4/models", provider.ExposeBuildModelsUrl("https://api.z.ai/api/paas/v4"));
            Assert.Equal("https://api.z.ai/api/paas/v4/chat/completions", provider.ExposeBuildChatCompletionsUrl("https://api.z.ai/api/paas/v4"));
            Assert.Null(await provider.GetTokenUsageAsync(CancellationToken.None));
            Assert.NotEmpty(provider.GetDefaultModels());
            Assert.Contains(provider.GetDefaultModels(), m => m.Id.StartsWith("glm-", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            ClearModelsFolder(seededFolder);
        }
    }

    [Fact]
    public void ZaiProvider_NormalizesInsufficientBalanceErrors()
    {
        var message = ZaiProvider.NormalizeZaiError("Insufficient balance or no resource package. Please recharge.");
        Assert.Contains("coding base URL", message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("GLM-4.7", message, StringComparison.OrdinalIgnoreCase);
    }
}
