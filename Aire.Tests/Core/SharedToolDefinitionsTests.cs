using System;
using System.Collections.Generic;
using System.Linq;
using Aire.Providers;
using Xunit;

namespace Aire.Tests.Core;

public class SharedToolDefinitionsTests
{
    [Fact]
    public void AllTools_IsNotEmpty()
    {
        IReadOnlyList<ToolDescriptor> allTools = SharedToolDefinitions.AllTools;
        Assert.NotEmpty(allTools);
    }

    [Fact]
    public void AllTools_HaveUniqueNames()
    {
        IReadOnlyList<ToolDescriptor> allTools = SharedToolDefinitions.AllTools;
        List<string> list = allTools.Select((ToolDescriptor t) => t.Name).ToList();
        List<string> list2 = list.Distinct().ToList();
        Assert.Equal(list2.Count, list.Count);
    }

    [Fact]
    public void ToGeminiFunctionDeclarations_DoesNotCrash()
    {
        Exception ex = Record.Exception(() => SharedToolDefinitions.ToGeminiFunctionDeclarations());
        Assert.Null(ex);
    }

    [Fact]
    public void Tools_Parameters_HaveDescriptions()
    {
        IReadOnlyList<ToolDescriptor> allTools = SharedToolDefinitions.AllTools;
        foreach (ToolDescriptor item in allTools)
        {
            if (item.Parameters == null)
            {
                continue;
            }
            foreach (ToolParam value in item.Parameters.Values)
            {
                Assert.False(string.IsNullOrWhiteSpace(value.Description), "Tool " + item.Name + " is missing a description for a parameter.");
            }
        }
    }
}
