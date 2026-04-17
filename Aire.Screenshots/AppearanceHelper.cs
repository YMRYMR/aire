using System;
using System.IO;
using System.Text;

namespace Aire.Screenshots;

/// <summary>
/// Pins the UI defaults version file so the app doesn't delete windowstate.json
/// on startup. Does NOT modify theme/appearance settings — those are set by the
/// user in the running app and must be preserved as-is.
/// </summary>
internal static class AppearanceHelper
{
    // Must match App.xaml.cs UiDefaultsVersion so the app doesn't delete windowstate.json on startup.
    private const string CurrentUiDefaultsVersion = "2";

    /// <summary>
    /// Pins the UI defaults version so EnsureUiDefaultsApplied() won't delete windowstate.json.
    /// Preserves all existing appearance settings untouched.
    /// </summary>
    public static void SetDarkTheme()
    {
        var appDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Aire");

        Directory.CreateDirectory(appDataDir);

        var versionPath = Path.Combine(appDataDir, "ui-defaults.version");
        File.WriteAllText(versionPath, CurrentUiDefaultsVersion, new UTF8Encoding(false));

        Console.WriteLine($"UI defaults version pinned to {CurrentUiDefaultsVersion}");
    }
}
