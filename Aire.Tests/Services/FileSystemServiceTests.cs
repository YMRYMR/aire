using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Aire.Services;
using Xunit;

namespace Aire.Tests.Services;

[Collection("NonParallelCoreUtilities")]
public class FileSystemServiceTests : IDisposable
{
    private readonly string _root;

    private readonly FileSystemService _service = new FileSystemService();

    public FileSystemServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"aire_fs_{Guid.NewGuid():N}");
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
    public async Task ExecuteAsync_ListDirectory_ReturnsEntriesAndSummary()
    {
        Directory.CreateDirectory(Path.Combine(_root, "sub"));
        await File.WriteAllTextAsync(Path.Combine(_root, "a.txt"), "hello");
        ToolExecutionResult result = await _service.ExecuteAsync(BuildRequest("list_directory", new Dictionary<string, object> { ["path"] = _root }));
        Assert.NotNull(result.DirectoryListing);
        Assert.Equal("1 folder, 1 file", result.DirectoryListing.Summary);
        Assert.Contains("[DIR]  sub/", result.TextResult);
        Assert.Contains("[FILE] a.txt", result.TextResult);
    }

    [Fact]
    public async Task ExecuteAsync_ReadFile_ReturnsChunkMetadata()
    {
        string path = Path.Combine(_root, "readme.txt");
        await File.WriteAllTextAsync(path, "abcdefghijklmnopqrstuvwxyz");
        ToolExecutionResult result = await _service.ExecuteAsync(BuildRequest("read_file", new Dictionary<string, object>
        {
            ["path"] = path,
            ["offset"] = 5,
            ["length"] = 4
        }));
        Assert.Contains("offset 5–8", result.TextResult);
        Assert.EndsWith("fghi", result.TextResult);
    }

    [Fact]
    public async Task ExecuteAsync_ReadFile_RejectsOffsetBeyondEnd()
    {
        string path = Path.Combine(_root, "tiny.txt");
        await File.WriteAllTextAsync(path, "abc");
        Assert.Contains("beyond end of file", (await _service.ExecuteAsync(BuildRequest("read_file", new Dictionary<string, object>
        {
            ["path"] = path,
            ["offset"] = 10
        }))).TextResult, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_WriteFileAndAppend_PersistsContent()
    {
        string path = Path.Combine(_root, "nested", "notes.txt");
        ToolExecutionResult first = await _service.ExecuteAsync(BuildRequest("write_file", new Dictionary<string, object>
        {
            ["path"] = path,
            ["content"] = "hello"
        }));
        ToolExecutionResult second = await _service.ExecuteAsync(BuildRequest("write_file", new Dictionary<string, object>
        {
            ["path"] = path,
            ["content"] = " world",
            ["append"] = true
        }));
        Assert.Contains("Wrote 5 characters", first.TextResult);
        Assert.Contains("Appended 6 characters", second.TextResult);
        Assert.Equal("hello world", await File.ReadAllTextAsync(path));
    }

    [Fact]
    public async Task ExecuteAsync_ApplyDiff_ReplacesMatchedBlock()
    {
        string path = Path.Combine(_root, "diff.txt");
        await File.WriteAllTextAsync(path, "line1\nold value\nline3");
        Assert.Equal(actual: (await _service.ExecuteAsync(BuildRequest("apply_diff", new Dictionary<string, object>
        {
            ["path"] = path,
            ["diff"] = "<<<<<<<\nold value\n=======\nnew value\n>>>>>>>"
        }))).TextResult, expected: "Diff applied to: " + path);
        Assert.Contains("new value", await File.ReadAllTextAsync(path));
    }

    [Fact]
    public async Task ExecuteAsync_ApplyDiff_HandlesCrLfInputAndDiff()
    {
        string path = Path.Combine(_root, "crlf.txt");
        await File.WriteAllTextAsync(path, "line1\r\nold value\r\nline3");
        Assert.Equal(actual: (await _service.ExecuteAsync(BuildRequest("apply_diff", new Dictionary<string, object>
        {
            ["path"] = path,
            ["diff"] = "<<<<<<<\nold value\n=======\nnew value\n>>>>>>>"
        }))).TextResult, expected: "Diff applied to: " + path);
        Assert.Equal("line1\r\nnew value\r\nline3", await File.ReadAllTextAsync(path));
    }

    [Fact]
    public async Task ExecuteAsync_ApplyDiff_IgnoresIndentationMismatch()
    {
        string path = Path.Combine(_root, "indent.txt");
        await File.WriteAllTextAsync(path, "class Demo\n{\n    void Old()\n    {\n        return;\n    }\n}");
        Assert.Equal(actual: (await _service.ExecuteAsync(BuildRequest("apply_diff", new Dictionary<string, object>
        {
            ["path"] = path,
            ["diff"] = "<<<<<<<\n        return;\n=======\n        Console.WriteLine(\"x\");\n>>>>>>>"
        }))).TextResult, expected: "Diff applied to: " + path);
        Assert.Contains("Console.WriteLine(\"x\")", await File.ReadAllTextAsync(path));
    }

    [Fact]
    public async Task ExecuteAsync_ApplyDiff_ReturnsWarningWhenNoChange()
    {
        string path = Path.Combine(_root, "unchanged.txt");
        await File.WriteAllTextAsync(path, "content");
        Assert.Contains("no changes were made", (await _service.ExecuteAsync(BuildRequest("apply_diff", new Dictionary<string, object>
        {
            ["path"] = path,
            ["diff"] = "<<<<<<<\nmissing\n=======\nreplacement\n>>>>>>>"
        }))).TextResult, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_CreateMoveAndDelete_WorksForFiles()
    {
        string sourceDir = Path.Combine(_root, "source");
        string destDir = Path.Combine(_root, "dest");
        string sourceFile = Path.Combine(sourceDir, "x.txt");
        string destFile = Path.Combine(destDir, "y.txt");
        ToolExecutionResult create = await _service.ExecuteAsync(BuildRequest("create_directory", new Dictionary<string, object> { ["path"] = sourceDir }));
        await File.WriteAllTextAsync(sourceFile, "payload");
        ToolExecutionResult move = await _service.ExecuteAsync(BuildRequest("move_file", new Dictionary<string, object>
        {
            ["from"] = sourceFile,
            ["to"] = destFile
        }));
        ToolExecutionResult delete = await _service.ExecuteAsync(BuildRequest("delete_file", new Dictionary<string, object> { ["path"] = destFile }));
        Assert.Contains("Created directory", create.TextResult);
        Assert.Contains("Moved:", move.TextResult);
        Assert.Contains("Deleted:", delete.TextResult);
        Assert.False(File.Exists(destFile));
    }

    [Fact]
    public async Task ExecuteAsync_SearchFiles_ReturnsMatches()
    {
        Directory.CreateDirectory(Path.Combine(_root, "sub"));
        await File.WriteAllTextAsync(Path.Combine(_root, "sub", "one.cs"), "x");
        await File.WriteAllTextAsync(Path.Combine(_root, "sub", "two.txt"), "x");
        ToolExecutionResult result = await _service.ExecuteAsync(BuildRequest("search_files", new Dictionary<string, object>
        {
            ["directory"] = _root,
            ["pattern"] = "*.cs"
        }));
        Assert.Contains("Found 1 file(s)", result.TextResult);
        Assert.Contains("one.cs", result.TextResult);
    }

    [Fact]
    public async Task ExecuteAsync_SearchFileContent_SupportsLiteralAndRegex()
    {
        string path = Path.Combine(_root, "content.txt");
        await File.WriteAllTextAsync(path, "alpha\nBeta value\nbeta final");
        ToolExecutionResult literal = await _service.ExecuteAsync(BuildRequest("search_file_content", new Dictionary<string, object>
        {
            ["directory"] = _root,
            ["pattern"] = "beta",
            ["file_pattern"] = "*.txt",
            ["max_results"] = 10
        }));
        ToolExecutionResult regex = await _service.ExecuteAsync(BuildRequest("search_file_content", new Dictionary<string, object>
        {
            ["directory"] = _root,
            ["pattern"] = "B.ta",
            ["file_pattern"] = "*.txt",
            ["max_results"] = 10
        }));
        Assert.Contains("2 match(es)", literal.TextResult);
        Assert.Contains("2 match(es)", regex.TextResult);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownTool_ReturnsError()
    {
        Assert.Contains("Unknown tool", (await _service.ExecuteAsync(BuildRequest("something_else", new Dictionary<string, object>()))).TextResult);
    }

    private static ToolCallRequest BuildRequest(string tool, Dictionary<string, object?> parameters)
    {
        string text = JsonSerializer.Serialize(parameters);
        using JsonDocument jsonDocument = JsonDocument.Parse(text);
        return new ToolCallRequest
        {
            Tool = tool,
            Parameters = jsonDocument.RootElement.Clone(),
            RawJson = text
        };
    }
}
