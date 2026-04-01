using System;
using Aire.Services;
using Xunit;

namespace Aire.Tests.Core;

public class LocalApiModelsTests
{
    [Fact]
    public void ApiToolExecutionResult_DefaultsMatchApprovalAwareShape()
    {
        ApiToolExecutionResult apiToolExecutionResult = new ApiToolExecutionResult();
        Assert.Equal("completed", apiToolExecutionResult.Status);
        Assert.Equal(string.Empty, apiToolExecutionResult.TextResult);
        Assert.Null(apiToolExecutionResult.PendingApprovalIndex);
        Assert.Null(apiToolExecutionResult.DirectoryPath);
        Assert.Null(apiToolExecutionResult.DirectorySummary);
        Assert.Null(apiToolExecutionResult.ScreenshotPath);
    }

    [Fact]
    public void ApiTraceEntry_StoresAssignedValues()
    {
        DateTimeOffset utcNow = DateTimeOffset.UtcNow;
        ApiTraceEntry apiTraceEntry = new ApiTraceEntry
        {
            Id = 7L,
            Timestamp = utcNow,
            Kind = "request",
            Method = "execute_tool",
            Message = "ok",
            Success = true,
            Data = new
            {
                Name = "x"
            }
        };
        Assert.Equal(7L, apiTraceEntry.Id);
        Assert.Equal(utcNow, apiTraceEntry.Timestamp);
        Assert.Equal("request", apiTraceEntry.Kind);
        Assert.Equal("execute_tool", apiTraceEntry.Method);
        Assert.True(apiTraceEntry.Success);
        Assert.NotNull(apiTraceEntry.Data);
    }

    [Fact]
    public void SnapshotAndProviderModels_HaveExpectedDefaults()
    {
        ApiStateSnapshot apiStateSnapshot = new ApiStateSnapshot();
        ApiPendingApproval apiPendingApproval = new ApiPendingApproval();
        ApiProviderSnapshot apiProviderSnapshot = new ApiProviderSnapshot();
        Assert.False(apiStateSnapshot.ApiAccessEnabled);
        Assert.Equal(0, apiStateSnapshot.PendingApprovals);
        Assert.Equal(string.Empty, apiPendingApproval.Tool);
        Assert.Equal(string.Empty, apiPendingApproval.Description);
        Assert.Equal("#007ACC", apiProviderSnapshot.Color);
        Assert.Equal(string.Empty, apiProviderSnapshot.Name);
    }
}
