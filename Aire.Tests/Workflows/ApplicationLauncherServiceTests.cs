using System;
using System.IO;
using System.Linq;
using Aire.Services;
using Xunit;

namespace Aire.Tests.Workflows;

public class ApplicationLauncherServiceTests
{
    private readonly ApplicationLauncherService _launcher = new ApplicationLauncherService();

    [Fact]
    [Trait("Category", "Unit")]
    public void FindApplication_Notepad_ReturnsNonNullPath()
    {
        if (OperatingSystem.IsWindows())
        {
            string text = _launcher.FindApplication("notepad");
            Assert.NotNull(text);
            Assert.True(File.Exists(text), "notepad path '" + text + "' should exist on disk");
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void FindApplication_NonExistentApp_ReturnsNull()
    {
        string text = _launcher.FindApplication("this_app_does_not_exist_xyz_123");
        Assert.Null(text);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void FindApplication_Gimp_ReturnsPathWhenInstalled()
    {
        if (OperatingSystem.IsWindows())
        {
            string path = "C:\\Program Files\\GIMP 2\\bin";
            if (Directory.Exists(path) && Directory.EnumerateFiles(path, "gimp*.exe").Any())
            {
                string text = _launcher.FindApplication("gimp");
                Assert.NotNull(text);
                Assert.True(File.Exists(text), "GIMP executable at '" + text + "' should exist");
                Assert.Contains("gimp", Path.GetFileNameWithoutExtension(text), StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void FindApplication_GimpFullExeName_ReturnsPath()
    {
        if (OperatingSystem.IsWindows())
        {
            string path = "C:\\Program Files\\GIMP 2\\bin\\gimp-2.10.exe";
            if (File.Exists(path))
            {
                string text = _launcher.FindApplication("gimp-2.10");
                Assert.NotNull(text);
                Assert.True(File.Exists(text));
            }
        }
    }
}
