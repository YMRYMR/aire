using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Aire.Data;
using Aire.Services;
using Aire.Services.Policies;
using Aire.UI;
using Aire.UI.MainWindow.Controls;
using Aire.UI.MainWindow.Models;
using Aire.UI.Settings.Controls;
using Xunit;

namespace Aire.Tests.UI;

[Collection("AppState Isolation")]
public class UiWorkflowRegressionTests : TestBase
{
    [Fact]
    public void SettingsWindow_AutoAcceptSettings_RoundTripPersistsSecurityChoices()
    {
        RunOnStaThread(() =>
        {
            WithTempLocalAppData(() =>
            {
                EnsureApplication();
                var settings = CreateUninitializedSettings();
                var db       = new DatabaseService();
                db.InitializeAsync().GetAwaiter().GetResult();
                SetField(settings, "_databaseService", db);
                SetField(settings, "_appSettingsApplicationService", new Aire.AppLayer.Settings.AppSettingsApplicationService(db));
                SetField(settings, "_autoAcceptProfilesApplicationService", new Aire.AppLayer.Tools.AutoAcceptProfilesApplicationService(db));

                var pane = new AutoAcceptPaneControl();
                SetField(settings, "AutoAcceptPaneControl", pane);

                pane.AutoAcceptEnabledCheckBox.IsChecked          = true;
                pane.AutoAcceptReadFileCheckBox.IsChecked          = true;
                pane.AutoAcceptExecuteCommandCheckBox.IsChecked    = true;
                pane.AutoAcceptEditFileTextCheckBox.IsChecked      = true;
                pane.AutoAcceptMouseToolsCheckBox.IsChecked        = true;
                pane.AutoAcceptKeyboardToolsCheckBox.IsChecked     = false;

                settings.SaveAutoAcceptSettings().GetAwaiter().GetResult();

                string? saved = db.GetSettingAsync("auto_accept_settings").GetAwaiter().GetResult();
                Assert.False(string.IsNullOrWhiteSpace(saved));
                using (var doc = JsonDocument.Parse(saved!))
                {
                    var root = doc.RootElement;
                    Assert.True(root.GetProperty("Enabled").GetBoolean());
                    Assert.True(root.GetProperty("AllowMouseTools").GetBoolean());
                    Assert.False(root.GetProperty("AllowKeyboardTools").GetBoolean());
                    var tools = root.GetProperty("AllowedTools").EnumerateArray()
                                    .Select(x => x.GetString()).ToList();
                    Assert.Contains("read_file",       (IEnumerable<string?>)tools);
                    Assert.Contains("execute_command", (IEnumerable<string?>)tools);
                    Assert.Contains("edit_file_text",  (IEnumerable<string?>)tools);
                }

                // Reset UI and reload — values should be restored from DB.
                pane.AutoAcceptEnabledCheckBox.IsChecked          = false;
                pane.AutoAcceptReadFileCheckBox.IsChecked          = false;
                pane.AutoAcceptExecuteCommandCheckBox.IsChecked    = false;
                pane.AutoAcceptEditFileTextCheckBox.IsChecked      = false;
                pane.AutoAcceptMouseToolsCheckBox.IsChecked        = false;

                settings.LoadAutoAcceptSettings().GetAwaiter().GetResult();

                Assert.True(pane.AutoAcceptEnabledCheckBox.IsChecked);
                Assert.True(pane.AutoAcceptReadFileCheckBox.IsChecked);
                Assert.True(pane.AutoAcceptExecuteCommandCheckBox.IsChecked);
                Assert.True(pane.AutoAcceptEditFileTextCheckBox.IsChecked);
                Assert.True(pane.AutoAcceptMouseToolsCheckBox.IsChecked);
                Assert.False(pane.AutoAcceptKeyboardToolsCheckBox.IsChecked);

                db.Dispose();
                SettingsWindow.SetAutoAcceptCache(string.Empty);
            });
        });
    }

    [Fact]
    public async Task MainWindow_IsToolAutoAcceptedAsync_UsesAliasesAndMouseKeyboardFlags()
    {
        // Tests the ToolAutoAcceptPolicyService that MainWindow.IsToolAutoAcceptedAsync delegates to.
        // Tested directly here because the window setup is not relevant to the policy logic.
        const string cache =
            "{\"Enabled\":true,\"AllowedTools\":[\"write_to_file\",\"list_files\"],\"AllowMouseTools\":true,\"AllowKeyboardTools\":true}";
        var policy = new ToolAutoAcceptPolicyService(() => Task.FromResult<string?>(null));

        Assert.True(await policy.IsAutoAcceptedAsync("write_file",     cache)); // alias: write_to_file → write_file
        Assert.True(await policy.IsAutoAcceptedAsync("list_directory", cache)); // alias: list_files → list_directory
        Assert.True(await policy.IsAutoAcceptedAsync("click",          cache)); // mouse tool (AllowMouseTools)
        Assert.True(await policy.IsAutoAcceptedAsync("type_text",      cache)); // keyboard tool (AllowKeyboardTools)
        Assert.False(await policy.IsAutoAcceptedAsync("delete_file",   cache)); // not in allowed list
    }

    [Fact]
    public void HelpWindow_RendersVisibleActionButtons_And_SettingsLinkRequestsTab()
    {
        RunOnStaThread(() =>
        {
            EnsureApplication();
            var help         = new HelpWindow();
            string? tabArg   = null;
            SettingsWindow.OpenRequested += Handler;
            try
            {
                var section    = new HelpSection("text", "Quick actions", null, null, null, null, null, new LinkItem[]
                {
                    new("Open providers",   "settings:providers"),
                    new("Open connections", "settings:connections")
                });
                var panel = new StackPanel();
                help.RenderSection(section, panel);

                var buttons = panel.Children.OfType<Border>()
                                   .SelectMany(b => ((WrapPanel)b.Child).Children.OfType<Button>())
                                   .ToList();

                Assert.Equal(2, buttons.Count);
                Assert.Equal("Open providers", buttons[0].Content);

                buttons[0].RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                Assert.Equal("providers", tabArg);

                buttons[1].RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                Assert.Equal("connections", tabArg);
            }
            finally
            {
                SettingsWindow.OpenRequested -= Handler;
                help.Close();
            }

            void Handler(string? tab) => tabArg = tab;
        });
    }

    [Fact]
    public void SettingsWindow_TimeoutSliderState_RequiresEnabledFormAndModel()
    {
        RunOnStaThread(() =>
        {
            EnsureApplication();
            var settings = CreateUninitializedSettings();
            var combo    = new ComboBox();
            var slider   = new Slider();
            SetField(settings, "ModelComboBox",  combo);
            SetField(settings, "TimeoutSlider",  slider);

            settings.UpdateTimeoutSliderEnabledState(formEnabled: false);
            Assert.False(slider.IsEnabled);

            combo.Text = "";
            combo.SelectedValue = null;
            settings.UpdateTimeoutSliderEnabledState(formEnabled: true);
            Assert.False(slider.IsEnabled);

            combo.Text = "gpt-4.1";
            settings.UpdateTimeoutSliderEnabledState(formEnabled: true);
            Assert.True(slider.IsEnabled);

            combo.Text = "";
            combo.SelectedValue = "gpt-4o";
            settings.UpdateTimeoutSliderEnabledState(formEnabled: true);
            Assert.True(slider.IsEnabled);
        });
    }

    [Fact]
    public void MainWindow_SearchAndChatExport_WorkFromRealWindowState()
    {
            RunOnStaThread(() =>
            {
                EnsureApplication();
                var window      = new MainWindow(initializeUi: false);
            var searchPanel = new SearchPanelControl();
            var itemsCtrl   = new ItemsControl();
            SetField(window, "SearchPanelControl",    searchPanel);
            SetField(window, "MessagesItemsControl",  itemsCtrl);
            SetField(window, "_searchMatchIndices",   new List<int>());
            window.Messages = new ObservableCollection<ChatMessage>
            {
                new() { Sender = "Date",   Text = "Friday" },
                new() { Sender = "You",    Timestamp = "10:00", Text = "hello there" },
                new() { Sender = "Aire",   Timestamp = "10:01", Text = "HELLO back" },
                new() { Sender = "System", Timestamp = "10:02", Text = "internal" },
                new() { Sender = "You",    Timestamp = "10:03", Text = "nothing to see" }
            };
            itemsCtrl.ItemsSource = window.Messages;

            window.OpenSearch();
            searchPanel.SearchTextBox.Text = "hello";
            window.PerformSearch("hello");

            Assert.Equal(Visibility.Visible, searchPanel.SearchPanel.Visibility);
            Assert.Equal("1/2", searchPanel.SearchCountText.Text);
            Assert.True(window.Messages[1].IsSearchMatch);
            Assert.True(window.Messages[2].IsSearchMatch);
            Assert.False(window.Messages[4].IsSearchMatch);

            window.NavigateSearchNext();
            Assert.Equal("2/2", searchPanel.SearchCountText.Text);

            string chat = window.BuildChatText();
            Assert.Contains("── Friday ──",          chat, StringComparison.Ordinal);
            Assert.Contains("[10:00] You: hello there", chat, StringComparison.Ordinal);
            Assert.Contains("[10:01] Aire: HELLO back", chat, StringComparison.Ordinal);
            Assert.DoesNotContain("internal",         chat, StringComparison.Ordinal);

            window.CloseSearch();
            Assert.Equal(Visibility.Collapsed, searchPanel.SearchPanel.Visibility);
            Assert.Equal(string.Empty, searchPanel.SearchCountText.Text);
            Assert.All(window.Messages, m => Assert.False(m.IsSearchMatch));
        });
    }

    [Fact]
    public void MainWindow_WindowRestoreBounds_AllowsSecondaryMonitorCoordinates_AndRejectsOffscreenState()
    {
        var method = typeof(MainWindow).GetMethod(
            "ResolveRestoreBounds",
            BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)!;

        var leftMonitor = new Rect(-1920, 0, 1920, 1080);
        var primaryMonitor = new Rect(0, 0, 1920, 1080);

        var restored = (Rect?)method.Invoke(null, new object[]
        {
            -1600d, 120d, 1200d, 900d, 400d, 300d, new[] { leftMonitor, primaryMonitor }
        });

        Assert.True(restored.HasValue);
        Assert.Equal(-1600d, restored.Value.Left);
        Assert.Equal(120d, restored.Value.Top);
        Assert.Equal(1200d, restored.Value.Width);
        Assert.Equal(900d, restored.Value.Height);

        var offscreen = (Rect?)method.Invoke(null, new object[]
        {
            9000d, 9000d, 1200d, 900d, 400d, 300d, new[] { leftMonitor, primaryMonitor }
        });

        Assert.Null(offscreen);
    }

    // ── helpers ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a SettingsWindow without running its constructor (which calls InitializeComponent
    /// and would require a full XAML/WPF startup). Required XAML-named fields are injected
    /// individually by each test via <see cref="SetField"/>.
    /// </summary>
        private static SettingsWindow CreateUninitializedSettings()
            => new SettingsWindow(initializeUi: false);

    private static void WithTempLocalAppData(Action action)
    {
        string dir      = Path.Combine(Path.GetTempPath(), "aire-ui-tests", Guid.NewGuid().ToString("N"));
        string? original = Environment.GetEnvironmentVariable("LOCALAPPDATA");
        Directory.CreateDirectory(dir);
        Environment.SetEnvironmentVariable("LOCALAPPDATA", dir);
        try
        {
            action();
        }
        finally
        {
            Environment.SetEnvironmentVariable("LOCALAPPDATA", original);
            try { Directory.Delete(dir, recursive: true); } catch { }
        }
    }

    /// <summary>
    /// Injects a value into a private or internal field on an uninitialized WPF object.
    /// Used only for test setup where the window cannot be fully initialized via XAML.
    /// </summary>
    private static void SetField(object instance, string fieldName, object? value)
    {
        var field = instance.GetType().GetField(
            fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        field?.SetValue(instance, value);
    }
}
