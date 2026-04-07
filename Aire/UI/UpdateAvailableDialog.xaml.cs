using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using Aire.Services;

namespace Aire.UI;

public partial class UpdateAvailableDialog : Window
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

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

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ApplyLocalization();

        try
        {
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            if (hwnd != IntPtr.Zero)
            {
                var value = 1;
                DwmSetWindowAttribute(hwnd, 20, ref value, sizeof(int));
                DwmSetWindowAttribute(hwnd, 19, ref value, sizeof(int));
            }
        }
        catch
        {
            // Non-fatal. The dialog still works without the backdrop attribute.
        }
    }

    private void OnThemeChanged() => Dispatcher.Invoke(() => FontSize = AppearanceService.FontSize);

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }

    private void InstallButton_Click(object sender, RoutedEventArgs e)
    {
        InstallRequested = true;
        DialogResult = true;
        Close();
    }

    private void LaterButton_Click(object sender, RoutedEventArgs e)
    {
        InstallRequested = false;
        DialogResult = false;
        Close();
    }

    private void ReleaseNotesButton_Click(object sender, RoutedEventArgs e)
    {
        if (_update.ReleasePageUrl == null)
            return;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = _update.ReleasePageUrl.AbsoluteUri,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            AppLogger.Warn("App.Update", "Failed to open release page", ex);
        }
    }

    // ── Markdown renderer ────────────────────────────────────────────────────
    // Supports the subset of Markdown that GitHub release notes typically use:
    //   ## / ###  headings   |   **bold**   |   `code`   |   bare URLs
    //   * / -     bullets    |   blank lines as paragraph breaks

    private static readonly Regex InlinePattern = new(
        @"(\*\*(?<bold>.+?)\*\*|`(?<code>[^`]+)`|(?<url>https?://\S+))",
        RegexOptions.Compiled);

    private static void RenderNotes(System.Windows.Controls.TextBlock tb, string markdown)
    {
        tb.Inlines.Clear();

        var lines = markdown.Split('\n');
        bool firstLine = true;

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd('\r');

            if (!firstLine)
                tb.Inlines.Add(new LineBreak());
            firstLine = false;

            // Heading (## or ###)
            if (line.StartsWith("### ", StringComparison.Ordinal))
            {
                tb.Inlines.Add(new Run(line[4..].Trim()) { FontWeight = FontWeights.SemiBold });
                continue;
            }
            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                tb.Inlines.Add(new Run(line[3..].Trim()) { FontWeight = FontWeights.Bold });
                continue;
            }

            // Blank line → extra visual gap
            if (string.IsNullOrWhiteSpace(line))
            {
                tb.Inlines.Add(new Run(" "));
                continue;
            }

            // Bullet point
            string bulletPrefix = string.Empty;
            string content = line;
            if (line.StartsWith("* ", StringComparison.Ordinal) || line.StartsWith("- ", StringComparison.Ordinal))
            {
                bulletPrefix = "• ";
                content = line[2..];
            }
            else if (line.StartsWith("  * ", StringComparison.Ordinal) || line.StartsWith("  - ", StringComparison.Ordinal))
            {
                bulletPrefix = "    • ";
                content = line[4..];
            }

            if (bulletPrefix.Length > 0)
                tb.Inlines.Add(new Run(bulletPrefix));

            AddInlineMarkdown(tb.Inlines, content);
        }
    }

    private static void AddInlineMarkdown(InlineCollection inlines, string text)
    {
        int lastIndex = 0;
        foreach (Match m in InlinePattern.Matches(text))
        {
            // Plain text before this match
            if (m.Index > lastIndex)
                inlines.Add(new Run(text[lastIndex..m.Index]));

            if (m.Groups["bold"].Success)
            {
                inlines.Add(new Run(m.Groups["bold"].Value) { FontWeight = FontWeights.SemiBold });
            }
            else if (m.Groups["code"].Success)
            {
                inlines.Add(new Run(m.Groups["code"].Value)
                {
                    FontFamily = new System.Windows.Media.FontFamily("Consolas, Courier New"),
                    Foreground = (System.Windows.Media.Brush)System.Windows.Application.Current.Resources["CodeForegroundBrush"],
                });
            }
            else if (m.Groups["url"].Success)
            {
                var uriStr = m.Groups["url"].Value.TrimEnd('.', ',', ')');
                if (Uri.TryCreate(uriStr, UriKind.Absolute, out var uri))
                {
                    var link = new Hyperlink(new Run(uriStr)) { NavigateUri = uri };
                    link.RequestNavigate += (_, e) =>
                    {
                        try { Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true }); }
                        catch (Exception ex) { AppLogger.Warn("App.Update", "Failed to open link", ex); }
                    };
                    inlines.Add(link);
                }
                else
                {
                    inlines.Add(new Run(m.Value));
                }
            }

            lastIndex = m.Index + m.Length;
        }

        // Remaining plain text
        if (lastIndex < text.Length)
            inlines.Add(new Run(text[lastIndex..]));
    }

    // ─────────────────────────────────────────────────────────────────────────

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
