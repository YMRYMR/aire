using System;
using System.Linq;
using System.Reflection;
using System.Windows.Controls;
using System.Windows.Documents;
using Aire.Services;
using Aire.UI;
using Xunit;

namespace Aire.Tests.UI;

public sealed class UpdateAvailableDialogTests : TestBase
{
    [Fact]
    public void UpdateAvailableDialog_OnLoadedAppliesReleaseMetadata_AndRendersMarkdownNotes()
    {
        RunOnStaThread(() =>
        {
            EnsureApplication();

            var update = new GitHubReleaseUpdateInfo(
                new Version(1, 2, 3),
                new Version(1, 2, 4),
                "v1.2.4",
                "Aire 1.2.4",
                new Uri("https://github.com/YMRYMR/aire/releases/tag/v1.2.4"),
                new GitHubReleaseAsset("Aire.msi", new Uri("https://example.com/Aire.msi"), 1234),
                "## Improvements\n- **Faster** startup\n- Release page: https://example.com/releases\n\n`code` sample");

            var dialog = new UpdateAvailableDialog(update);
            InvokePrivate(dialog, "OnLoaded", dialog, new System.Windows.RoutedEventArgs());

            var titleText = GetField<TextBlock>(dialog, "TitleText");
            var versionText = GetField<TextBlock>(dialog, "VersionText");
            var notesText = GetField<TextBlock>(dialog, "NotesText");
            var laterButton = GetField<System.Windows.Controls.Button>(dialog, "LaterButton");
            var releaseNotesButton = GetField<System.Windows.Controls.Button>(dialog, "ReleaseNotesButton");
            var installButton = GetField<System.Windows.Controls.Button>(dialog, "InstallButton");

            Assert.Contains("Aire 1.2.4", titleText.Text, StringComparison.Ordinal);
            Assert.Contains("Aire 1.2.4", versionText.Text, StringComparison.Ordinal);
            Assert.Contains("Current version: 1.2.3", versionText.Text, StringComparison.Ordinal);
            Assert.False(string.IsNullOrWhiteSpace(laterButton.Content?.ToString()));
            Assert.False(string.IsNullOrWhiteSpace(releaseNotesButton.Content?.ToString()));
            Assert.False(string.IsNullOrWhiteSpace(installButton.Content?.ToString()));

            var heading = notesText.Inlines.OfType<Run>().FirstOrDefault(run => run.Text.Contains("Improvements", StringComparison.Ordinal));
            Assert.NotNull(heading);
            Assert.Equal(System.Windows.FontWeights.Bold, heading!.FontWeight);
            Assert.Contains(notesText.Inlines.OfType<Hyperlink>(), link =>
                link.NavigateUri?.AbsoluteUri == "https://example.com/releases" &&
                new TextRange(link.ContentStart, link.ContentEnd).Text.Contains("https://example.com/releases", StringComparison.Ordinal));
            Assert.Contains(notesText.Inlines.OfType<Run>(), run => run.Text.Contains("code", StringComparison.Ordinal));

            dialog.Close();
        });
    }

    [Fact]
    public void UpdateAvailableDialog_RenderNotes_HandlesBlankLinesAndNestedMarkdown()
    {
        RunOnStaThread(() =>
        {
            EnsureApplication();

            var notes = new TextBlock();
            InvokePrivateStatic(
                typeof(UpdateAvailableDialog),
                "RenderNotes",
                notes,
                "### Nested heading\n\n* **Bold** item with `code`\n  * child item\n- plain item");

            var plainText = new TextRange(notes.ContentStart, notes.ContentEnd).Text;
            Assert.Contains("Nested heading", plainText, StringComparison.Ordinal);
            Assert.Contains("Bold", plainText, StringComparison.Ordinal);
            Assert.Contains("code", plainText, StringComparison.Ordinal);
            Assert.Contains("child item", plainText, StringComparison.Ordinal);
            Assert.Contains("plain item", plainText, StringComparison.Ordinal);
            Assert.Contains(notes.Inlines.OfType<Run>(), run => run.Text.Trim() == "•");
        });
    }

    private static T GetField<T>(object instance, string fieldName)
        where T : class
        => (T)instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(instance)!;

    private static void InvokePrivate(object instance, string methodName, params object[] args)
    {
        var method = instance.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic,
            null,
            new[] { typeof(object), typeof(System.Windows.RoutedEventArgs) },
            null);
        Assert.NotNull(method);
        method!.Invoke(instance, args);
    }

    private static void InvokePrivateStatic(Type type, string methodName, params object[] args)
        => type.GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, args);
}
