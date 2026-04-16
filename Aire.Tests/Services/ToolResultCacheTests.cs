using System.Text.Json;
using Aire.Services;
using Xunit;

namespace Aire.Tests.Services;

public sealed class ToolResultCacheTests
{
    [Fact]
    public void IsCacheable_ReadFile_ReturnsTrue()
        => Assert.True(ToolResultCache.IsCacheable("read_file"));

    [Fact]
    public void IsCacheable_ListDirectory_ReturnsTrue()
        => Assert.True(ToolResultCache.IsCacheable("list_directory"));

    [Fact]
    public void IsCacheable_GetSystemInfo_ReturnsTrue()
        => Assert.True(ToolResultCache.IsCacheable("get_system_info"));

    [Fact]
    public void IsCacheable_Recall_ReturnsTrue()
        => Assert.True(ToolResultCache.IsCacheable("recall"));

    [Fact]
    public void IsCacheable_WriteFile_ReturnsFalse()
        => Assert.False(ToolResultCache.IsCacheable("write_file"));

    [Fact]
    public void IsCacheable_ExecuteCommand_ReturnsFalse()
        => Assert.False(ToolResultCache.IsCacheable("execute_command"));

    [Fact]
    public void IsCacheable_SetClipboard_ReturnsFalse()
        => Assert.False(ToolResultCache.IsCacheable("set_clipboard"));

    [Fact]
    public void IsCacheable_CaseInsensitive()
        => Assert.True(ToolResultCache.IsCacheable("READ_FILE"));

    [Fact]
    public void TryGet_EmptyCache_ReturnsNull()
    {
        var cache = new ToolResultCache();
        Assert.Null(cache.TryGet("any_key"));
    }

    [Fact]
    public void Set_ThenTryGet_ReturnsCachedResult()
    {
        var cache = new ToolResultCache();
        var result = new ToolExecutionResult { TextResult = "Hello world" };
        cache.Set("key1", result);

        var retrieved = cache.TryGet("key1");
        Assert.NotNull(retrieved);
        Assert.Equal("Hello world", retrieved.TextResult);
    }

    [Fact]
    public void TryGet_WrongKey_ReturnsNull()
    {
        var cache = new ToolResultCache();
        cache.Set("key1", new ToolExecutionResult { TextResult = "Data" });

        Assert.Null(cache.TryGet("key2"));
    }

    [Fact]
    public void Clear_RemovesAllEntries()
    {
        var cache = new ToolResultCache();
        cache.Set("a", new ToolExecutionResult { TextResult = "A" });
        cache.Set("b", new ToolExecutionResult { TextResult = "B" });
        Assert.Equal(2, cache.Count);

        cache.Clear();

        Assert.Equal(0, cache.Count);
        Assert.Null(cache.TryGet("a"));
        Assert.Null(cache.TryGet("b"));
    }

    [Fact]
    public void BuildKey_SameToolSameParams_ReturnsSameKey()
    {
        var json = JsonDocument.Parse("{\"path\":\"/tmp/test.txt\"}").RootElement;
        var key1 = ToolResultCache.BuildKey("read_file", json);
        var key2 = ToolResultCache.BuildKey("read_file", json);
        Assert.Equal(key1, key2);
    }

    [Fact]
    public void BuildKey_DifferentTools_ReturnsDifferentKeys()
    {
        var json = JsonDocument.Parse("{\"path\":\"/tmp/test.txt\"}").RootElement;
        var key1 = ToolResultCache.BuildKey("read_file", json);
        var key2 = ToolResultCache.BuildKey("list_directory", json);
        Assert.NotEqual(key1, key2);
    }

    [Fact]
    public void BuildKey_DifferentParams_ReturnsDifferentKeys()
    {
        var json1 = JsonDocument.Parse("{\"path\":\"/tmp/a.txt\"}").RootElement;
        var json2 = JsonDocument.Parse("{\"path\":\"/tmp/b.txt\"}").RootElement;
        var key1 = ToolResultCache.BuildKey("read_file", json1);
        var key2 = ToolResultCache.BuildKey("read_file", json2);
        Assert.NotEqual(key1, key2);
    }

    [Fact]
    public void BuildKey_ObjectPropertyOrder_DoesNotMatter()
    {
        var json1 = JsonDocument.Parse("{\"a\":1,\"b\":2}").RootElement;
        var json2 = JsonDocument.Parse("{\"b\":2,\"a\":1}").RootElement;
        var key1 = ToolResultCache.BuildKey("read_file", json1);
        var key2 = ToolResultCache.BuildKey("read_file", json2);

        Assert.Equal(key1, key2);
    }

    [Fact]
    public void BuildKey_NoParameters_ReturnsStableKey()
    {
        var key = ToolResultCache.BuildKey("get_system_info", default);

        Assert.Equal("get_system_info:", key);
    }

    [Fact]
    public void Set_OverwritesPrevious()
    {
        var cache = new ToolResultCache();
        cache.Set("key", new ToolExecutionResult { TextResult = "First" });
        cache.Set("key", new ToolExecutionResult { TextResult = "Second" });

        var result = cache.TryGet("key");
        Assert.NotNull(result);
        Assert.Equal("Second", result.TextResult);
    }

    [Fact]
    public void Count_ReflectsNumberOfEntries()
    {
        var cache = new ToolResultCache();
        Assert.Equal(0, cache.Count);

        cache.Set("a", new ToolExecutionResult());
        Assert.Equal(1, cache.Count);

        cache.Set("b", new ToolExecutionResult());
        Assert.Equal(2, cache.Count);
    }
}
