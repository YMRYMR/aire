using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Aire.Services;
using Aire.Services.Mcp;
using Xunit;

namespace Aire.Tests.Services;

public class MiscAppServiceCoverageTests
{
    [Fact]
    public void McpToolPromptBuilder_BuildSection_FormatsTools()
    {
        string empty = McpToolPromptBuilder.BuildSection(Array.Empty<McpToolDefinition>());
        Assert.Equal(string.Empty, empty);

        var tools = new[]
        {
            new McpToolDefinition
            {
                Name        = "search_docs",
                Description = "Searches the docs",
                ServerName  = "docs"
            }
        };
        string section = McpToolPromptBuilder.BuildSection(tools);

        Assert.Contains("MCP TOOLS", section);
        Assert.Contains("search_docs", section);
        Assert.Contains("[server: docs]", section);
    }

    [Fact]
    public async Task AppStartupState_MarkReady_UnblocksWaiters()
    {
        if (!AppStartupState.IsReady)
            AppStartupState.MarkReady();

        await AppStartupState.WaitUntilReadyAsync(CancellationToken.None);

        Assert.True(AppStartupState.IsReady);
    }

    [Fact]
    public void GpuPreferenceService_SetsWebView2EnvironmentHint()
    {
        string? original = Environment.GetEnvironmentVariable("WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS");
        try
        {
            Environment.SetEnvironmentVariable("WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS", "--existing");
            GpuPreferenceService.ApplyHighPerformancePreference();
            string? result = Environment.GetEnvironmentVariable("WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS");

            Assert.NotNull(result);
            Assert.Contains("--existing", result);
            Assert.Contains("--force_high_performance_gpu", result);
        }
        finally
        {
            Environment.SetEnvironmentVariable("WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS", original);
        }
    }

    [Fact]
    public async Task OllamaService_FallbackAndMetadata_AreUsableWithoutLiveServer()
    {
        using var service = new OllamaService();

        Assert.Equal("http://localhost:11434", OllamaService.DefaultBaseUrl);
        Assert.True(OllamaService.KnownModelMeta.ContainsKey("qwen3:4b"));
        Assert.Contains("tools", (IEnumerable<string>)OllamaService.KnownModelMeta["qwen3:4b"].Tags);

        bool reachable = await service.IsOllamaReachableAsync("http://127.0.0.1:1");
        List<OllamaService.OllamaModel> models = await service.GetAvailableModelsAsync(new CancellationToken(canceled: true));

        Assert.False(reachable);
        Assert.NotEmpty(models);
        Assert.Contains(models, m => string.Equals(m.Name, "qwen3:4b", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task OllamaService_InvalidEndpoints_ReturnEmptyOrThrowSafely()
    {
        using var service = new OllamaService();

        List<OllamaService.OllamaModel> installed = await service.GetInstalledModelsAsync("http://127.0.0.1:1");
        Assert.NotNull(installed);

        await Assert.ThrowsAnyAsync<Exception>(() => service.DeleteModelAsync("missing-model", "http://127.0.0.1:1"));
        await Assert.ThrowsAnyAsync<Exception>(() => service.PullModelAsync("missing-model", "http://127.0.0.1:1"));
    }

    [Fact]
    public void OllamaService_PathDetection_RespectsPathEnvironment()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"ollama_path_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        File.WriteAllText(Path.Combine(tempDir, "ollama.exe"), string.Empty);
        string? original = Environment.GetEnvironmentVariable("PATH");
        try
        {
            Environment.SetEnvironmentVariable("PATH", tempDir);
            Assert.True(OllamaService.IsOllamaInPath());
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", original);
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    [Fact]
    public void LocalApiRequestAndResponse_DefaultsAndFactories_Work()
    {
        var request = new LocalApiRequest();
        Assert.Equal(string.Empty, request.Method);
        Assert.Null(request.Parameters);
        Assert.Null(request.Token);

        var ok  = LocalApiResponse.OkResult(123);
        var err = LocalApiResponse.Error("bad");

        Assert.True(ok.Ok);
        Assert.Equal(123, (int)ok.Result!);
        Assert.False(err.Ok);
        Assert.Equal("bad", err.ErrorMessage);
    }
}
