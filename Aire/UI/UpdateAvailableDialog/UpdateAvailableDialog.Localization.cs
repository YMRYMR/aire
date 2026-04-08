using System;
using System.Windows;
using Aire.Services;

namespace Aire.UI;

public partial class UpdateAvailableDialog
{
    private void ApplyLocalization()
    {
        var L = LocalizationService.S;

        TitleText.Text = string.Format(L("update.available", "Aire {0} is available"), _update.LatestVersion);
        HeadlineText.Text = L("update.headline", "A newer Aire version is ready.");

        VersionText.Text = string.IsNullOrWhiteSpace(_update.ReleaseName)
            ? string.Format(L("update.current", "Current version: {0}"), _update.CurrentVersion)
            : $"{_update.ReleaseName} • {string.Format(L("update.current", "Current version: {0}"), _update.CurrentVersion)}";

        var notes = string.IsNullOrWhiteSpace(_update.ReleaseNotes)
            ? L("update.description", "Install the latest update to get the newest fixes and improvements.")
            : _update.ReleaseNotes;
        RenderNotes(NotesText, notes);

        LaterButton.Content = L("update.later", "Later");
        ReleaseNotesButton.Content = L("update.releasePage", "Release page");
        InstallButton.Content = L("update.installNow", "Install now");
        CloseButton.ToolTip = L("tooltip.close", "Close");
    }

    private void OnThemeChanged() => Dispatcher.Invoke(() => FontSize = AppearanceService.FontSize);
}
