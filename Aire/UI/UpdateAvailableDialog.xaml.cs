using System;
using System.Windows;
using Aire.Services;

namespace Aire.UI;

public partial class UpdateAvailableDialog : Window
{
    private readonly GitHubReleaseUpdateInfo _update;

    public bool InstallRequested { get; private set; }

    public UpdateAvailableDialog(GitHubReleaseUpdateInfo update)
    {
        _update = update ?? throw new ArgumentNullException(nameof(update));
        InitializeComponent();
        FontSize = AppearanceService.FontSize;
        FlowDirection = LocalizationService.IsRightToLeftLanguage(LocalizationService.CurrentCode)
            ? System.Windows.FlowDirection.RightToLeft
            : System.Windows.FlowDirection.LeftToRight;
        AppearanceService.AppearanceChanged += OnThemeChanged;
        Closed += (_, _) => AppearanceService.AppearanceChanged -= OnThemeChanged;
        Loaded += OnLoaded;
    }

    public static bool? ShowDialog(Window? owner, GitHubReleaseUpdateInfo update)
    {
        var dialog = new UpdateAvailableDialog(update)
        {
            Owner = owner,
            Topmost = owner?.Topmost ?? true,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };

        return dialog.ShowDialog();
    }
}
