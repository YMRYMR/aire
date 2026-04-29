using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Aire.Services;
using Xunit;

namespace Aire.Tests.Services;

public class FileSystemServiceCoverageTests : IDisposable
{
    private readonly string _root;

    private readonly FileSystemService _service = new FileSystemService();

    public FileSystemServiceCoverageTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"aire-fs-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
        catch
        {
        }
    }

    [Fact]
    public async Task ExecuteAsync_ListDirectory_ReturnsStructuredListing()
    {
        Directory.CreateDirectory(Path.Combine(_root, "subdir"));
        await File.WriteAllTextAsync(Path.Combine(_root, "file.txt"), "hello");
        ToolExecutionResult result = await _service.ExecuteAsync(CreateRequest("list_directory", "{\"path\":\"" + Escape(_root) + "\" }"));
        Assert.Contains("Contents of:", result.TextResult);
        Assert.NotNull(result.DirectoryListing);
        Assert.Contains((IEnumerable<DirectoryEntry>)result.DirectoryListing.Entries, (Predicate<DirectoryEntry>)((DirectoryEntry entry) => entry.IsDirectory && entry.Name == "subdir"));
        Assert.Contains((IEnumerable<DirectoryEntry>)result.DirectoryListing.Entries, (Predicate<DirectoryEntry>)((DirectoryEntry entry) => !entry.IsDirectory && entry.Name == "file.txt"));
    }

    [Fact]
    public async Task ExecuteAsync_ListDirectory_HandlesRepoRootWithReservedEntries()
    {
        string repoRoot = FindRepositoryRoot();

        ToolExecutionResult result = await _service.ExecuteAsync(CreateRequest("list_directory", "{\"path\":\"" + Escape(repoRoot) + "\" }"));

        Assert.Contains("Contents of:", result.TextResult);
        Assert.NotNull(result.DirectoryListing);
        Assert.Contains("Aire", result.TextResult, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("File system operation failed", result.TextResult, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_ReadFile_SupportsOffsets()
    {
        string path = Path.Combine(_root, "chunk.txt");
        await File.WriteAllTextAsync(path, "abcdefghij");
        ToolExecutionResult first = await _service.ExecuteAsync(CreateRequest("read_file", "{\"path\":\"" + Escape(path) + "\",\"offset\":0,\"length\":4}"));
        ToolExecutionResult second = await _service.ExecuteAsync(CreateRequest("read_file", "{\"path\":\"" + Escape(path) + "\",\"offset\":4,\"length\":4}"));
        Assert.Contains("Remaining:", first.TextResult);
        Assert.EndsWith("abcd", first.TextResult);
        Assert.EndsWith("efgh", second.TextResult);
    }

    [Fact]
    public async Task ExecuteAsync_ReadFile_BeyondEnd_ReturnsError()
    {
        string path = Path.Combine(_root, "short.txt");
        await File.WriteAllTextAsync(path, "abc");
        Assert.Contains("beyond end of file", (await _service.ExecuteAsync(CreateRequest("read_file", "{\"path\":\"" + Escape(path) + "\",\"offset\":10}"))).TextResult);
    }

    [Fact]
    public async Task ExecuteAsync_WriteFile_ThenAppend_WritesExpectedContent()
    {
        string path = Path.Combine(_root, "write.txt");
        ToolExecutionResult write = await _service.ExecuteAsync(CreateRequest("write_file", "{\"path\":\"" + Escape(path) + "\",\"content\":\"hello\"}"));
        ToolExecutionResult append = await _service.ExecuteAsync(CreateRequest("write_file", "{\"path\":\"" + Escape(path) + "\",\"content\":\" world\",\"append\":true}"));
        Assert.Contains("Wrote 5 characters", write.TextResult);
        Assert.Contains("Appended 6 characters", append.TextResult);
        Assert.Equal("hello world", await File.ReadAllTextAsync(path));
    }

    [Fact]
    public async Task ExecuteAsync_WriteFile_WithTextAlias_WritesExpectedContent()
    {
        string path = Path.Combine(_root, "write-text.txt");
        ToolExecutionResult write = await _service.ExecuteAsync(CreateRequest("write_file", "{\"path\":\"" + Escape(path) + "\",\"text\":\"hello\"}"));

        Assert.Contains("Wrote 5 characters", write.TextResult);
        Assert.Equal("hello", await File.ReadAllTextAsync(path));
    }

    [Fact]
    public async Task ExecuteAsync_ApplyDiff_ReplacesMatchedBlock()
    {
        string path = Path.Combine(_root, "diff.txt");
        await File.WriteAllTextAsync(path, "alpha\nbeta\ngamma");
        string diff = "<<<<<<< SEARCH\r\nbeta\r\n=======\r\nbeta\r\n>>>>>>> REPLACE";
        Assert.Contains("Diff applied", (await _service.ExecuteAsync(CreateRequest("apply_diff", $"{{\"path\":\"{Escape(path)}\",\"diff\":\"{Escape(diff)}\" }}"))).TextResult);
        Assert.Contains("beta", await File.ReadAllTextAsync(path));
    }

    [Fact]
    public async Task ExecuteAsync_ApplyDiff_WithoutMatch_ReturnsWarning()
    {
        string path = Path.Combine(_root, "diff-warning.txt");
        await File.WriteAllTextAsync(path, "alpha");
        string diff = "<<<<<<< SEARCH\r\nmissing\r\n=======\r\nfound\r\n>>>>>>> REPLACE";
        Assert.Contains("Warning: Diff applied but no changes were made", (await _service.ExecuteAsync(CreateRequest("apply_diff", $"{{\"path\":\"{Escape(path)}\",\"diff\":\"{Escape(diff)}\" }}"))).TextResult);
    }

    [Fact]
    public async Task ExecuteAsync_CreateMoveAndDelete_Work()
    {
        string sourceDir = Path.Combine(_root, "source");
        string sourceFile = Path.Combine(sourceDir, "file.txt");
        Directory.CreateDirectory(sourceDir);
        await File.WriteAllTextAsync(sourceFile, "data");
        string targetFile = Path.Combine(_root, "moved", "file.txt");
        ToolExecutionResult create = await _service.ExecuteAsync(CreateRequest("create_directory", "{\"path\":\"" + Escape(Path.Combine(_root, "new-dir")) + "\" }"));
        ToolExecutionResult move = await _service.ExecuteAsync(CreateRequest("move_file", $"{{\"from\":\"{Escape(sourceFile)}\",\"to\":\"{Escape(targetFile)}\" }}"));
        ToolExecutionResult delete = await _service.ExecuteAsync(CreateRequest("delete_file", "{\"path\":\"" + Escape(targetFile) + "\" }"));
        Assert.Contains("Created directory", create.TextResult);
        Assert.Contains("Moved:", move.TextResult);
        Assert.Contains("Deleted:", delete.TextResult);
        Assert.False(File.Exists(targetFile));
    }

    [Fact]
    public async Task ExecuteAsync_SearchFiles_FindsMatches()
    {
        await File.WriteAllTextAsync(Path.Combine(_root, "one.cs"), "class One {}");
        await File.WriteAllTextAsync(Path.Combine(_root, "two.txt"), "ignore");
        ToolExecutionResult result = await _service.ExecuteAsync(CreateRequest("search_files", "{\"directory\":\"" + Escape(_root) + "\",\"pattern\":\"*.cs\"}"));
        Assert.Contains("Found 1 file(s)", result.TextResult);
        Assert.Contains("one.cs", result.TextResult);
    }

    [Fact]
    public async Task ExecuteAsync_SearchFileContent_SupportsRegexAndLiteralFallback()
    {
        string path = Path.Combine(_root, "content.txt");
        await File.WriteAllTextAsync(path, "alpha\nBeta value\ngamma");
        ToolExecutionResult regexResult = await _service.ExecuteAsync(CreateRequest("search_file_content", "{\"directory\":\"" + Escape(_root) + "\",\"pattern\":\"beta\\\\s+value\"}"));
        ToolExecutionResult literalResult = await _service.ExecuteAsync(CreateRequest("search_file_content", "{\"directory\":\"" + Escape(_root) + "\",\"pattern\":\"[\"}"));
        Assert.Contains("1 match(es)", regexResult.TextResult);
        Assert.Contains("Beta value", regexResult.TextResult);
        Assert.Contains("No matches found", literalResult.TextResult);
    }

    private static ToolCallRequest CreateRequest(string tool, string json)
    {
        using JsonDocument jsonDocument = JsonDocument.Parse(json);
        return new ToolCallRequest
        {
            Tool = tool,
            Parameters = jsonDocument.RootElement.Clone()
        };
    }

    private static string Escape(string value)
    {
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r")
            .Replace("\n", "\\n");
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            string solutionPath = Path.Combine(current.FullName, "Aire.sln");
            if (File.Exists(solutionPath))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Could not locate Aire.sln from the test execution directory.");
    }
}
