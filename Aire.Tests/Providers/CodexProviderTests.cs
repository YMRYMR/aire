using Aire.Providers;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using Xunit;

namespace Aire.Tests.Providers;

public class CodexProviderTests
{
    [Fact]
    public void ProviderType_IsCodex()
    {
        Assert.Equal("Codex", new CodexProvider().ProviderType);
    }

    [Fact]
    public void DisplayName_ContainsCodex()
    {
        Assert.Contains("Codex", new CodexProvider().DisplayName);
    }

    [Fact]
    public void FieldHints_HideApiKeyAndBaseUrl()
    {
        var hints = new CodexProvider().FieldHints;
        Assert.False(hints.ShowApiKey);
        Assert.False(hints.ShowBaseUrl);
        Assert.False(hints.ApiKeyRequired);
    }

    [Fact]
    public void Actions_ExposeCodexInstallAction()
    {
        var action = Assert.Single(new CodexProvider().Actions);

        Assert.Equal("codex-install", action.Id);
        Assert.Equal("Install Codex CLI", action.Label);
        Assert.Equal(ProviderActionPlacement.ApiKeyArea, action.Placement);
    }

    [Fact]
    public void SelectCodexCliPath_PrefersLaunchableCandidates_AndIgnoresStorePackageBinary()
    {
        var preferred = new[]
        {
            @"C:\Users\raul_\AppData\Local\Microsoft\WindowsApps\codex.exe",
            @"C:\Users\raul_\AppData\Roaming\npm\codex.cmd"
        };
        var whereResults = new[]
        {
            @"C:\Program Files\WindowsApps\OpenAI.Codex_26.325.2171.0_x64__2p2nqsd0c76g0\app\resources\codex.exe"
        };

        var (path, sawStore) = CodexProvider.SelectCodexCliPath(
            preferred, whereResults,
            p => p.EndsWith("codex.cmd", StringComparison.OrdinalIgnoreCase));

        Assert.Equal(@"C:\Users\raul_\AppData\Roaming\npm\codex.cmd", path);
        Assert.False(sawStore);
    }

    [Fact]
    public void SelectCodexCliPath_ReportsStorePackageWhenNoLaunchableCliExists()
    {
        var whereResults = new[]
        {
            @"C:\Program Files\WindowsApps\OpenAI.Codex_26.325.2171.0_x64__2p2nqsd0c76g0\app\resources\codex.exe"
        };

        var (path, sawStore) = CodexProvider.SelectCodexCliPath(
            preferredCandidates: [],
            whereResults:        whereResults,
            fileExists:          _ => false);

        Assert.Null(path);
        Assert.True(sawStore);
    }

    [Fact]
    public void BuildPrompt_RendersSystemMessagesAsHighestPriorityBlocks()
    {
        string prompt = CodexProvider.BuildPrompt(
        [
            new ChatMessage { Role = "system", Content = "System tool rules." },
            new ChatMessage { Role = "user", Content = "Hello" }
        ]);

        Assert.Contains("SYSTEM INSTRUCTIONS (highest priority):", prompt, StringComparison.Ordinal);
        Assert.Contains("<system_instruction>", prompt, StringComparison.Ordinal);
        Assert.Contains("System tool rules.", prompt, StringComparison.Ordinal);
        Assert.Contains("CONVERSATION:", prompt, StringComparison.Ordinal);
        Assert.Contains("USER:", prompt, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("List the files in C:\\dev\\aire using a tool.", true)]
    [InlineData("Search files for TODO", true)]
    [InlineData("What is 2+2?", false)]
    public void RequiresForcedToolOnlyResponse_DetectsToolEligibleRequests(string prompt, bool expected)
    {
        Assert.Equal(expected, CodexProvider.RequiresForcedToolOnlyResponse(prompt));
    }

    [Fact]
    public void BuildPrompt_ForToolEligibleRequest_EndsWithToolOnlyRule()
    {
        string prompt = CodexProvider.BuildPrompt(
        [
            new ChatMessage { Role = "system", Content = "Use Aire tools." },
            new ChatMessage { Role = "user", Content = "List the files in C:\\dev\\aire using a tool." }
        ]);

        Assert.Contains("FINAL RESPONSE RULE FOR THIS TURN:", prompt, StringComparison.Ordinal);
        Assert.Contains("Respond with ONLY one <tool_call>{...}</tool_call> block.", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void CreateCodexProcessStartInfo_UsesUtf8Streams()
    {
        CodexProvider provider = new();
        provider.Initialize(new ProviderConfig { Model = "gpt-5.4-mini" });
        MethodInfo method = typeof(CodexProvider).GetMethod("CreateCodexProcessStartInfo", BindingFlags.Instance | BindingFlags.NonPublic)!;

        ProcessStartInfo psi = Assert.IsType<ProcessStartInfo>(method.Invoke(provider, ["codex.exe", "C:\\Temp\\out.txt"]));

        Assert.Equal(Encoding.UTF8.WebName, psi.StandardInputEncoding?.WebName);
        Assert.Equal(Encoding.UTF8.WebName, psi.StandardOutputEncoding?.WebName);
        Assert.Equal(Encoding.UTF8.WebName, psi.StandardErrorEncoding?.WebName);
        Assert.Contains("--sandbox", psi.ArgumentList);
        Assert.Contains("read-only", psi.ArgumentList);
        Assert.Contains("shell_environment_policy.inherit=none", psi.ArgumentList);
    }

    [Fact]
    public void CreateCodexProcessStartInfo_WrapsCmdLaunchersAndOmitsDefaultModelOverride()
    {
        CodexProvider provider = new();
        provider.Initialize(new ProviderConfig { Model = " default " });
        MethodInfo method = typeof(CodexProvider).GetMethod("CreateCodexProcessStartInfo", BindingFlags.Instance | BindingFlags.NonPublic)!;

        ProcessStartInfo psi = Assert.IsType<ProcessStartInfo>(method.Invoke(provider, [@"C:\Users\raul_\AppData\Roaming\npm\codex.cmd", "C:\\Temp\\out.txt"]));

        Assert.Equal("cmd.exe", psi.FileName);
        Assert.Equal("/d", psi.ArgumentList[0]);
        Assert.Equal("/c", psi.ArgumentList[1]);
        Assert.Equal(@"C:\Users\raul_\AppData\Roaming\npm\codex.cmd", psi.ArgumentList[2]);
        Assert.DoesNotContain("--model", psi.ArgumentList);
        Assert.Equal("-", psi.ArgumentList[^1]);
    }

    [Fact]
    public void BuildPrompt_TruncatesLongConversationMessages()
    {
        var longContent = new string('x', 2100);

        string prompt = CodexProvider.BuildPrompt(
        [
            new ChatMessage { Role = "user", Content = longContent }
        ]);

        Assert.Contains("[Truncated for Codex CLI prompt size]", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain(longContent, prompt, StringComparison.Ordinal);
    }

}
