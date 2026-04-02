using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
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

        [Fact]
        public void ParseDesktopExec_UsesPathLookup_AndStopsOutsideDesktopEntry()
        {
            ApplicationLauncherService launcher = new ApplicationLauncherService();
            string tempDir = Path.Combine(Path.GetTempPath(), "aire-launcher-path-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            string? oldPath = Environment.GetEnvironmentVariable("PATH");

            try
            {
                string cmdFile = Path.Combine(tempDir, RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "demo.cmd" : "demo");
                File.WriteAllText(cmdFile, "@echo off");
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    File.SetUnixFileMode(cmdFile, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
                }

                Environment.SetEnvironmentVariable("PATH", tempDir);

                string desktopFile = Path.Combine(tempDir, "demo.desktop");
                File.WriteAllText(desktopFile,
                    "[Desktop Entry]\n" +
                    "Name=Demo\n" +
                    $"Exec={Path.GetFileName(cmdFile)} --flag %U\n" +
                    "[Desktop Action Extra]\n" +
                    "Exec=/should/not/be/used\n");

                Assert.Equal(cmdFile, launcher.ParseDesktopExec(desktopFile));
            }
            finally
            {
                Environment.SetEnvironmentVariable("PATH", oldPath);
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }

        [Fact]
        public void ParseDesktopExec_ReturnsNull_ForMissingEntryOrMissingCommand()
        {
            ApplicationLauncherService launcher = new ApplicationLauncherService();
            string tempDir = Path.Combine(Path.GetTempPath(), "aire-launcher-null-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                string noEntry = Path.Combine(tempDir, "no-entry.desktop");
                File.WriteAllText(noEntry, "Exec=/should/not/be/used\n");
                Assert.Null(launcher.ParseDesktopExec(noEntry));

                string missingCommand = Path.Combine(tempDir, "missing-command.desktop");
                File.WriteAllText(missingCommand, "[Desktop Entry]\nExec=/missing/app --flag %f\n");
                Assert.Null(launcher.ParseDesktopExec(missingCommand));
            }
            finally
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }

        [Fact]
        public void LaunchApplication_AndOpenFileWithDefaultApplication_ReturnNull_ForMissingTargets()
        {
            ApplicationLauncherService launcher = new ApplicationLauncherService();

            Assert.Null(launcher.LaunchApplication("this_app_definitely_does_not_exist_xyz"));
            Assert.Null(launcher.OpenFileWithDefaultApplication(Path.Combine(Path.GetTempPath(), "missing-file-" + Guid.NewGuid().ToString("N"))));
        }

        [Fact]
        public void LaunchApplication_WithRootedExecutablePath_ReturnsProcessId()
        {
            ApplicationLauncherService launcher = new ApplicationLauncherService();
            string cmdPath = Path.Combine(Environment.SystemDirectory, "cmd.exe");

            int? processId = launcher.LaunchApplication(cmdPath, "/c exit 0");

            Assert.True(processId.HasValue);
        }

        [Fact]
        public void FindApplicationLinux_FindsExecutableFromPath_AndDesktopFallback()
        {
            ApplicationLauncherService launcher = new ApplicationLauncherService();
            MethodInfo findLinux = typeof(ApplicationLauncherService).GetMethod("FindApplicationLinux", BindingFlags.Instance | BindingFlags.NonPublic)!;
            string tempDir = Path.Combine(Path.GetTempPath(), "aire-launcher-linux-tests-" + Guid.NewGuid().ToString("N"));
            string localAppDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "applications");
            string? oldPath = Environment.GetEnvironmentVariable("PATH");

            Directory.CreateDirectory(tempDir);
            Directory.CreateDirectory(localAppDir);

            string pathExecutable = Path.Combine(tempDir, "demo.exe");
            string fallbackExecutable = Path.Combine(tempDir, "fallback.exe");
            string desktopFile = Path.Combine(localAppDir, "aire-fallback-" + Guid.NewGuid().ToString("N") + ".desktop");

            try
            {
                File.WriteAllText(pathExecutable, "binary");
                File.WriteAllText(fallbackExecutable, "binary");
                File.WriteAllText(desktopFile,
                    "[Desktop Entry]\n" +
                    "Name=Fallback\n" +
                    "Exec=fallback.exe --arg %f\n");

                Environment.SetEnvironmentVariable("PATH", tempDir);
                Assert.Equal(pathExecutable, findLinux.Invoke(launcher, ["demo.exe"]));

                Environment.SetEnvironmentVariable("PATH", tempDir + Path.PathSeparator + oldPath);
                Assert.Equal(fallbackExecutable, findLinux.Invoke(launcher, ["aire-fallback"]));
            }
            finally
            {
                Environment.SetEnvironmentVariable("PATH", oldPath);
                try { File.Delete(desktopFile); } catch { }
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }

        [Fact]
        public void FindApplicationMac_FindsExecutableFromPath_AndAppBundle()
        {
            ApplicationLauncherService launcher = new ApplicationLauncherService();
            MethodInfo findMac = typeof(ApplicationLauncherService).GetMethod("FindApplicationMac", BindingFlags.Instance | BindingFlags.NonPublic)!;
            string tempDir = Path.Combine(Path.GetTempPath(), "aire-launcher-mac-tests-" + Guid.NewGuid().ToString("N"));
            string localAppsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Applications");
            string? oldPath = Environment.GetEnvironmentVariable("PATH");

            Directory.CreateDirectory(tempDir);
            Directory.CreateDirectory(localAppsDir);

            string pathExecutable = Path.Combine(tempDir, "mac-demo.exe");
            string bundleRoot = Path.Combine(localAppsDir, "BundleDemo.app");
            string macosDir = Path.Combine(bundleRoot, "Contents", "MacOS");
            string bundleExecutable = Path.Combine(macosDir, "BundleDemo");

            try
            {
                File.WriteAllText(pathExecutable, "binary");
                Directory.CreateDirectory(macosDir);
                File.WriteAllText(bundleExecutable, "binary");

                Environment.SetEnvironmentVariable("PATH", tempDir);
                Assert.Equal(pathExecutable, findMac.Invoke(launcher, ["mac-demo.exe"]));

                Environment.SetEnvironmentVariable("PATH", oldPath);
                Assert.Equal(bundleExecutable, findMac.Invoke(launcher, ["BundleDemo"]));
            }
            finally
            {
                Environment.SetEnvironmentVariable("PATH", oldPath);
                try { Directory.Delete(bundleRoot, true); } catch { }
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }
    }
}
