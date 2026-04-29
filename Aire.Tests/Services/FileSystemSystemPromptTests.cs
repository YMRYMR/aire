using Aire.Services;
using Xunit;

namespace Aire.Tests.Services;

public sealed class FileSystemSystemPromptTests
{
    [Fact]
    public void BuildNativeCompact_OmitsRepositoryRule_WhenFilesystemDisabled()
    {
        string prompt = FileSystemSystemPrompt.BuildNativeCompact(["browser"]);

        Assert.DoesNotContain("REPOSITORY / CODEBASE ANALYSIS RULE", prompt, System.StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("list_directory(path=\"C:/dev/aire\")", prompt, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildNativeCompact_IncludesRepositoryRule_WhenFilesystemEnabled()
    {
        string prompt = FileSystemSystemPrompt.BuildNativeCompact(["filesystem"]);

        Assert.Contains("REPOSITORY / CODEBASE ANALYSIS RULE", prompt, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("list_directory(path=\"C:/dev/aire\")", prompt, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("edit_file_text", prompt, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("search_file_content", prompt, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildTextBased_PrefersNativeTextEditingTool()
    {
        string prompt = FileSystemSystemPrompt.BuildTextBased(["filesystem"]);

        Assert.Contains("edit_file_text", prompt, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("search_file_content", prompt, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Use execute_command only when a native file tool cannot do the job", prompt, System.StringComparison.OrdinalIgnoreCase);
    }
}
