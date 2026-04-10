using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Aire.Services;
using Xunit;

namespace Aire.Tests.Services;

[Collection("AppState Isolation")]
public class AppStateTests : IDisposable
{
    private readonly string _boolPath   = AppState.Path;
    private readonly string _stringPath = AppState.StringsPath;
    private readonly string? _boolBackup;
    private readonly string? _stringBackup;

    public AppStateTests()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_boolPath)!);
        _boolBackup   = File.Exists(_boolPath)   ? File.ReadAllText(_boolPath)   : null;
        _stringBackup = File.Exists(_stringPath)  ? File.ReadAllText(_stringPath) : null;
        if (File.Exists(_boolPath))   File.Delete(_boolPath);
        if (File.Exists(_stringPath)) File.Delete(_stringPath);
    }

    public void Dispose()
    {
        RestoreFile(_boolPath,   _boolBackup);
        RestoreFile(_stringPath, _stringBackup);
    }

    [Fact]
    public void BoolState_RoundTripsAndSidebarDefaultIsOpen()
    {
        Assert.True(AppState.GetSidebarOpen());

        AppState.SetBrowserOpen(true);
        AppState.SetSettingsOpen(true);
        AppState.SetHasCompletedOnboarding(true);
        AppState.CloseSidebar();

        Assert.True(AppState.GetBrowserOpen());
        Assert.True(AppState.GetSettingsOpen());
        Assert.True(AppState.GetHasCompletedOnboarding());
        Assert.False(AppState.GetSidebarOpen());
    }

    [Fact]
    public void LanguageAndApiToken_RoundTripAndMigrateLegacyPlaintext()
    {
        AppState.SetLanguage("es");
        Assert.Equal("es", AppState.GetLanguage());

        AppState.SetApiAccessToken("secret-token");
        Assert.Equal("secret-token", AppState.GetApiAccessToken());

        // Simulate a legacy plaintext token file and verify it gets migrated to encrypted form.
        var legacy = JsonSerializer.Serialize(new Dictionary<string, string> { ["apiAccessToken"] = "legacy-plaintext" });
        File.WriteAllText(_stringPath, legacy);
        Assert.Equal("legacy-plaintext", AppState.GetApiAccessToken());
        Assert.Contains("dpapi:", File.ReadAllText(_stringPath), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SelectedWindowId_RoundTrips()
    {
        AppState.SetSelectedWindowId("0000000000ABCDEF");
        Assert.Equal("0000000000ABCDEF", AppState.GetSelectedWindowId());
    }

    [Fact]
    public void ApiAccessHelpers_GenerateTokensAndRaiseEvents()
    {
        int changes = 0;
        AppState.ApiAccessChanged += Handler;
        try
        {
            AppState.SetApiAccessEnabled(true);
            string first  = AppState.EnsureApiAccessToken();
            string second = AppState.RegenerateApiAccessToken();

            Assert.True(AppState.GetApiAccessEnabled());
            Assert.False(string.IsNullOrWhiteSpace(second));
            if (!string.IsNullOrWhiteSpace(first))
                Assert.NotEqual(first, second);
            Assert.True(changes >= 1);
        }
        finally
        {
            AppState.ApiAccessChanged -= Handler;
        }

        void Handler() => changes++;
    }

    private static void RestoreFile(string path, string? content)
    {
        try
        {
            if (content == null)
            {
                if (File.Exists(path)) File.Delete(path);
            }
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, content);
            }
        }
        catch { /* best-effort restore */ }
    }
}
