using Aire.Services;
using Xunit;

namespace Aire.Tests.Core;

[Collection("ApiTraceLog")]
public class ApiTraceLogTests
{
    [Fact]
    public void Record_And_GetSince_ReturnOrderedEntries()
    {
        ApiTraceLog.Clear();
        var first = ApiTraceLog.Record("test", "one", "message-1", true, null);
        ApiTraceLog.Record("test", "two", "message-2", false, 42);

        var entries = ApiTraceLog.GetSince(first.Id, limit: 10);

        Assert.Single(entries);
        Assert.Equal("two", entries[0].Method);
        Assert.Equal(42,    entries[0].Data);
        Assert.False(entries[0].Success);
    }

    [Fact]
    public void GetSince_EnforcesMinimumLimitOfOne()
    {
        ApiTraceLog.Clear();
        ApiTraceLog.Record("test", "only", "message", true, null);

        var entries = ApiTraceLog.GetSince(afterId: 0, limit: 0);

        Assert.Single(entries);
    }

    [Fact]
    public void Record_TruncatesToMaximumEntries()
    {
        ApiTraceLog.Clear();
        for (int i = 0; i < 510; i++)
            ApiTraceLog.Record("kind", $"m{i}", $"msg{i}", null, null);

        var entries = ApiTraceLog.GetSince(afterId: 0, limit: 1000);

        Assert.Equal(500, entries.Length);
        Assert.Equal("m10",  entries[0].Method);
        Assert.Equal("m509", entries[entries.Length - 1].Method);
    }

    [Fact]
    public void Clear_RemovesAllEntries()
    {
        ApiTraceLog.Record("kind", "m", "msg", true, null);
        ApiTraceLog.Clear();

        var entries = ApiTraceLog.GetSince(afterId: 0, limit: 100);

        Assert.Empty(entries);
    }
}
