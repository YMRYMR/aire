using System.Reflection;
using Aire.Services;
using Xunit;

namespace Aire.Tests.Services;

public sealed class MouseControlServiceTests
{
    [Theory]
    [InlineData("ENTER", 0x0D)]
    [InlineData("return", 0x0D)]
    [InlineData("Tab", 0x09)]
    [InlineData("escape", 0x1B)]
    [InlineData("esc", 0x1B)]
    [InlineData("BACKSPACE", 0x08)]
    [InlineData("delete", 0x2E)]
    [InlineData("del", 0x2E)]
    [InlineData("home", 0x24)]
    [InlineData("end", 0x23)]
    [InlineData("pageup", 0x21)]
    [InlineData("pgup", 0x21)]
    [InlineData("pagedown", 0x22)]
    [InlineData("pgdn", 0x22)]
    [InlineData("left", 0x25)]
    [InlineData("up", 0x26)]
    [InlineData("right", 0x27)]
    [InlineData("down", 0x28)]
    [InlineData("ctrl", 0x11)]
    [InlineData("control", 0x11)]
    [InlineData("alt", 0x12)]
    [InlineData("shift", 0x10)]
    [InlineData("win", 0x5B)]
    [InlineData("windows", 0x5B)]
    [InlineData("lwin", 0x5B)]
    [InlineData("space", 0x20)]
    [InlineData("F1", 0x70)]
    [InlineData("F5", 0x74)]
    [InlineData("F12", 0x7B)]
    [InlineData("A", (ushort)'A')]
    [InlineData("z", (ushort)'Z')]
    [InlineData("7", (ushort)'7')]
    public void ParseVirtualKey_MapsKnownKeys(string key, ushort expected)
    {
        var method = typeof(MouseControlService).GetMethod("ParseVirtualKey", BindingFlags.Static | BindingFlags.NonPublic)!;

        var actual = Assert.IsType<ushort>(method.Invoke(null, [key]));

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("")]
    [InlineData("unknown-key")]
    [InlineData("aa")]
    public void ParseVirtualKey_ReturnsZero_ForUnknownKeys(string key)
    {
        var method = typeof(MouseControlService).GetMethod("ParseVirtualKey", BindingFlags.Static | BindingFlags.NonPublic)!;

        var actual = Assert.IsType<ushort>(method.Invoke(null, [key]));

        Assert.Equal((ushort)0, actual);
    }
}
