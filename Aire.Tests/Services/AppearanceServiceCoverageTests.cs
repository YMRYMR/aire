using System;
using System.IO;
using System.Text.Json;
using System.Windows;
using Aire.Services;
using Xunit;

namespace Aire.Tests.Services;

public sealed class AppearanceServiceCoverageTests : TestBase
{
    [Fact]
    public void ApplySaved_UsesDefaults_WhenStateFileIsMissing()
    {
        RunOnStaThread(() =>
        {
            EnsureApplication();
            string statePath = GetStatePath();
            string? backup = BackupFile(statePath);

            try
            {
                DeleteFile(statePath);
                AppearanceService.ResetForTesting();
                AppearanceService.ApplySaved();

                Assert.Equal(AppearanceService.Brightness < 0.5, AppearanceService.UsesDarkPalette);
                Assert.True(Application.Current!.Resources.Contains("BackgroundBrush"));
                Assert.True(Application.Current.Resources.Contains("AccentSurfaceBrush"));
            }
            finally
            {
                RestoreFile(statePath, backup);
            }
        });
    }

    [Fact]
    public void ApplySaved_RestoresSavedBrightnessTintAndAccentValues()
    {
        RunOnStaThread(() =>
        {
            EnsureApplication();
            string statePath = GetStatePath();
            string? backup = BackupFile(statePath);

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(statePath)!);
                File.WriteAllText(statePath, JsonSerializer.Serialize(new
                {
                    brightness = 0.25,
                    tintPosition = 0.75,
                    accentBrightness = 0.4,
                    accentTintPosition = 0.1
                }));

                AppearanceService.ResetForTesting();
                AppearanceService.ApplySaved();

                Assert.Equal(0.25, AppearanceService.Brightness, precision: 2);
                Assert.Equal(0.75, AppearanceService.TintPosition, precision: 2);
                Assert.Equal(0.4, AppearanceService.AccentBrightness, precision: 2);
                Assert.Equal(0.1, AppearanceService.AccentTintPosition, precision: 2);
                Assert.True(AppearanceService.UsesDarkPalette);
                Assert.NotNull(Application.Current!.Resources["BackgroundBrush"]);
            }
            finally
            {
                RestoreFile(statePath, backup);
            }
        });
    }

    [Fact]
    public void ApplySaved_RespectsLegacyDarkPaletteFlags()
    {
        RunOnStaThread(() =>
        {
            EnsureApplication();
            string statePath = GetStatePath();
            string? backup = BackupFile(statePath);

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(statePath)!);
                File.WriteAllText(statePath, JsonSerializer.Serialize(new
                {
                    usesDarkPalette = true,
                    tintPosition = 0.2,
                    accentBrightness = 0.9,
                    accentTintPosition = 0.3
                }));

                AppearanceService.ResetForTesting();
                AppearanceService.ApplySaved();

                Assert.Equal(0.0, AppearanceService.Brightness, precision: 2);
                Assert.True(AppearanceService.UsesDarkPalette);
                Assert.Equal(0.2, AppearanceService.TintPosition, precision: 2);
                Assert.Equal(0.9, AppearanceService.AccentBrightness, precision: 2);
                Assert.Equal(0.3, AppearanceService.AccentTintPosition, precision: 2);
            }
            finally
            {
                RestoreFile(statePath, backup);
            }
        });
    }

    [Fact]
    public void ApplySaved_RespectsLegacyIsDarkFlag()
    {
        RunOnStaThread(() =>
        {
            EnsureApplication();
            string statePath = GetStatePath();
            string? backup = BackupFile(statePath);

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(statePath)!);
                File.WriteAllText(statePath, JsonSerializer.Serialize(new
                {
                    isDark = false,
                    tintPosition = 0.8
                }));

                AppearanceService.ResetForTesting();
                AppearanceService.ApplySaved();

                Assert.Equal(1.0, AppearanceService.Brightness, precision: 2);
                Assert.False(AppearanceService.UsesDarkPalette);
                Assert.Equal(0.8, AppearanceService.TintPosition, precision: 2);
            }
            finally
            {
                RestoreFile(statePath, backup);
            }
        });
    }

    [Fact]
    public void ApplySaved_FallsBackToDefaults_WhenSavedJsonIsInvalid()
    {
        RunOnStaThread(() =>
        {
            EnsureApplication();
            string statePath = GetStatePath();
            string? backup = BackupFile(statePath);

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(statePath)!);
                File.WriteAllText(statePath, "{ this is not valid json");

                AppearanceService.ResetForTesting();
                AppearanceService.ApplySaved();

                Assert.NotNull(Application.Current!.Resources["BackgroundBrush"]);
                Assert.NotNull(Application.Current.Resources["AccentSurfaceBrush"]);
            }
            finally
            {
                RestoreFile(statePath, backup);
            }
        });
    }

    private static string GetStatePath()
        => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Aire", "windowstate.json");

    private static string? BackupFile(string path)
    {
        if (!File.Exists(path))
            return null;

        string backup = Path.Combine(Path.GetTempPath(), $"aire-windowstate-{Guid.NewGuid():N}.bak");
        File.Copy(path, backup, overwrite: true);
        return backup;
    }

    private static void RestoreFile(string path, string? backup)
    {
        try
        {
            if (backup is not null && File.Exists(backup))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.Copy(backup, path, overwrite: true);
                File.Delete(backup);
            }
            else
            {
                DeleteFile(path);
            }
        }
        catch
        {
        }
    }

    private static void DeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
        }
    }
}
