using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Aire.Services;
using Xunit;

namespace Aire.Tests.Services
{
    public class ApplicationLauncherServiceTests : TestBase
    {
        [Fact]
        public void ApplicationLauncherService_HelperPaths_Work()
        {
            ApplicationLauncherService launcher = new ApplicationLauncherService();
            
            // _resolvedCache is internal
            ApplicationLauncherService._resolvedCache["cached-app"] = @"C:\tools\cached-app.exe";
            
            Assert.Null(launcher.FindApplication("   "));
            Assert.Equal(@"C:\tools\cached-app.exe", launcher.FindApplication("cached-app"));
            
            string tempDir = Path.Combine(Path.GetTempPath(), "aire-launcher-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            
            try
            {
                string cmdFile = Path.Combine(tempDir, "demo.cmd");
                File.WriteAllText(cmdFile, "@echo off");
                
                string desktopFile = Path.Combine(tempDir, "demo.desktop");
                File.WriteAllText(desktopFile, "[Ignored]\r\nExec=/should/not/be/used\r\n[Desktop Entry]\r\nName=Demo\r\nExec=" + cmdFile + " --flag %f\r\n[Other]\r\nExec=ignored");
                
                // ParseDesktopExec is internal
                Assert.Equal(cmdFile, launcher.ParseDesktopExec(desktopFile));
                
                string nestedDir = Path.Combine(tempDir, "nested", "deep");
                Directory.CreateDirectory(nestedDir);
                string exeFile = Path.Combine(nestedDir, "tool.exe");
                File.WriteAllText(exeFile, "binary");
                
                // SearchDirRobust is internal
                Assert.Equal(exeFile, ApplicationLauncherService.SearchDirRobust(tempDir, "tool.exe"));
                Assert.Null(ApplicationLauncherService.SearchDirRobust(tempDir, "missing.exe"));
                
                // IsExecutable is internal
                Assert.True(ApplicationLauncherService.IsExecutable(cmdFile));
                Assert.False(ApplicationLauncherService.IsExecutable(Path.Combine(tempDir, "readme.txt")));
            }
            finally
            {
                ApplicationLauncherService._resolvedCache.Remove("cached-app");
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }
    }
}
