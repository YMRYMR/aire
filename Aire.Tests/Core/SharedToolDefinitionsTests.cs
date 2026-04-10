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

    [Fact]
    public void GetDescription_ReturnsShortDescription_WhenCompactAndShortDescriptionSet()
    {
        var tool = new ToolDescriptor
        {
            Name = "test_tool",
            Description = "Long verbose description with coaching text.",
            ShortDescription = "Short description.",
            Category = "filesystem",
        };

        Assert.Equal("Short description.", tool.GetDescription(compact: true));
        Assert.Equal("Long verbose description with coaching text.", tool.GetDescription(compact: false));
    }

    [Fact]
    public void GetDescription_FallsBackToDescription_WhenShortDescriptionEmpty()
    {
        var tool = new ToolDescriptor
        {
            Name = "test_tool",
            Description = "Full description.",
            ShortDescription = string.Empty,
            Category = "filesystem",
        };

        Assert.Equal("Full description.", tool.GetDescription(compact: true));
        Assert.Equal("Full description.", tool.GetDescription(compact: false));
    }

    [Fact]
    public void AllTools_HaveNonEmptyShortDescriptions()
    {
        foreach (var tool in SharedToolDefinitions.AllTools)
        {
            Assert.False(string.IsNullOrWhiteSpace(tool.ShortDescription),
                $"Tool '{tool.Name}' is missing a ShortDescription.");
        }
    }

    [Fact]
    public void ToOpenAiFunctions_Compact_UsesShortDescriptions()
    {
        var functions = SharedToolDefinitions.ToOpenAiFunctions(
            capabilities: ["tools"],
            compact: true);

        Assert.NotEmpty(functions);
    }

    [Fact]
    public void ToAnthropicTools_Compact_UsesShortDescriptions()
    {
        var tools = SharedToolDefinitions.ToAnthropicTools(
            capabilities: ["tools"],
            compact: true);

        Assert.NotEmpty(tools);
    }
}
