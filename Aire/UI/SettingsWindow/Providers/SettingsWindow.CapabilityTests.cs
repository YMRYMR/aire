using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Aire.AppLayer.Abstractions;
using Aire.AppLayer.Providers;
using Aire.Data;
using Aire.Domain.Providers;
using Aire.Providers;
using Aire.Services;
using Button = System.Windows.Controls.Button;

namespace Aire.UI
{
    public partial class SettingsWindow
    {
        private readonly ProviderCapabilityTestApplicationService _capabilityTestApplicationService = new();
        private readonly ProviderCapabilityTestSessionService _capabilitySessionService = new();
        private readonly List<CapabilityTestResult> _capabilityTestResults = new();

        private IAiProvider? BuildProviderFromForm()
        {
            if (_selectedProvider == null)
            {
                return null;
            }

            try
            {
                var type = (TypeComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? _selectedProvider.Type;
                var service = new ProviderSetupApplicationService();
                return service.BuildRuntimeProvider(new ProviderRuntimeRequest(
                    type,
                    ApiKeyPasswordBox.Password,
                    BaseUrlTextBox.Text.Trim(),
                    ModelComboBox.SelectedValue as string ?? ModelComboBox.Text.Trim(),
                    type == "ClaudeWeb" && ClaudeAiSession.Instance.IsReady));
            }
            catch
            {
                ShowToast("Could not create provider.", isError: true);
                return null;
            }
        }

        private async Task LoadAndDisplayTestResultsAsync(Provider provider)
        {
            try
            {
                CapTestResultsBorder.Visibility = Visibility.Collapsed;
                CapTestStatusText.Text = LocalizationService.S("captest.notTested", "Not yet tested");

                var sessionService = _capabilitySessionService ?? new ProviderCapabilityTestSessionService();
                var session = await sessionService.LoadAsync(
                    provider.Id,
                    provider.Model ?? string.Empty,
                    _databaseService);
                if (session == null)
                    return;

                DisplayTestResults(session.Results, session.TestedAt);
            }
            catch
            {
                AppLogger.Warn("CapabilityTests.LoadSavedResults", "Failed to load previous test results");
            }
        }

        private async void RunTestsButton_Click(object sender, RoutedEventArgs e)
        {
            _testCts?.Cancel();
            _testCts = new System.Threading.CancellationTokenSource();
            var ct = _testCts.Token;

            var provider = BuildProviderFromForm();
            if (provider == null)
            {
                return;
            }

            SetProvidersTabEnabled(false);
            RunTestsButton.IsEnabled = false;
            StopTestsButton.IsEnabled = true;
            StopTestsButton.Visibility = Visibility.Visible;
            CapTestProgressBorder.Visibility = Visibility.Visible;
            CapTestProgressBar.IsIndeterminate = true;
            CapTestProgressText.Text = LocalizationService.S("captest.validating", "Validating\u2026");
            CapTestStatusText.Text = LocalizationService.S("captest.running", "Running\u2026");

            // Clear the results panel now so rows appear as they arrive.
            CapTestResultsPanel.Children.Clear();
            _capabilityTestResults.Clear();
            CapTestResultsBorder.Visibility = Visibility.Visible;

            try
            {
                var progress = new Progress<ProviderCapabilityTestApplicationService.ProgressUpdate>(update =>
                {
                    _capabilityTestResults.Add(update.Result);
                    CapTestProgressText.Text =
                        $"Tested: {update.Result.Name} ({update.CompletedCount}/{CapabilityTestRunner.AllTests.Count})";

                    // Append the result row immediately so the user sees it in real time.
                    AppendTestResultRow(update.Result);
                });

                var executionResult = await (_capabilityTestApplicationService ?? new ProviderCapabilityTestApplicationService()).ValidateRunAndPersistAsync(
                    provider,
                    _selectedProvider?.Id,
                    _selectedProvider?.Model ?? string.Empty,
                    (p, token) => _testRunner.RunAllAsync(p, token),
                    _databaseService,
                    progress,
                    ct);

                if (!executionResult.Started)
                {
                    ShowToast(executionResult.BlockingMessage ?? "Test run failed.", isError: true);
                    CapTestStatusText.Text = executionResult.BlockingMessage?.StartsWith("Validation failed:", StringComparison.Ordinal) == true
                        ? "Validation failed."
                        : "Test run failed.";
                    CapTestResultsBorder.Visibility = Visibility.Collapsed;
                    return;
                }

                if (!string.IsNullOrWhiteSpace(executionResult.WarningMessage))
                    ShowToast(executionResult.WarningMessage, isError: false);

                _capabilityTestResults.Clear();
                _capabilityTestResults.AddRange(executionResult.Results);
                // Results are already rendered row by row; just update the timestamp label.
                UpdateTestedAtLabel(executionResult.TestedAt ?? DateTime.Now);
            }
            catch (OperationCanceledException)
            {
                CapTestStatusText.Text = LocalizationService.S("captest.cancelled", "Tests cancelled.");
            }
            catch
            {
                ShowToast(LocalizationService.S("captest.failed", "Test run failed."), isError: true);
                CapTestStatusText.Text = LocalizationService.S("captest.failed", "Test run failed.");
            }
            finally
            {
                CapTestProgressBar.IsIndeterminate = false;
                CapTestProgressBorder.Visibility = Visibility.Collapsed;
                StopTestsButton.Visibility = Visibility.Collapsed;
                SetProvidersTabEnabled(true);
                RunTestsButton.IsEnabled = true;
            }
        }

        private async void RerunCapabilityTestButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not string testId)
            {
                return;
            }

            var test = CapabilityTestRunner.AllTests.FirstOrDefault(t => t.Id == testId);
            if (test == null)
            {
                return;
            }

            await RunSingleCapabilityTestAsync(test);
        }

        private async Task RunSingleCapabilityTestAsync(CapabilityTest test)
        {
            var provider = BuildProviderFromForm();
            if (provider == null)
            {
                return;
            }

            SetProvidersTabEnabled(false);
            RunTestsButton.IsEnabled = false;
            StopTestsButton.Visibility = Visibility.Collapsed;
            CapTestProgressBorder.Visibility = Visibility.Visible;
            CapTestProgressBar.IsIndeterminate = true;
            CapTestProgressText.Text = string.Format(
                LocalizationService.S("captest.runningOne", "Running {0}…"),
                test.Name);
            CapTestStatusText.Text = string.Format(
                LocalizationService.S("captest.rerunning", "Rerunning {0}…"),
                test.Name);

            try
            {
                var executionResult = await _capabilityTestApplicationService.RunSingleAndPersistAsync(
                    provider,
                    _selectedProvider?.Id,
                    _selectedProvider?.Model ?? string.Empty,
                    test,
                    CapabilityTestRunner.RunOneAsync,
                    _databaseService,
                    CancellationToken.None);

                var index = _capabilityTestResults.FindIndex(r => r.Id == executionResult.Result.Id);
                if (index >= 0)
                    _capabilityTestResults[index] = executionResult.Result;
                else
                    _capabilityTestResults.Add(executionResult.Result);

                DisplayTestResults(_capabilityTestResults, executionResult.TestedAt);
            }
            catch
            {
                ShowToast(LocalizationService.S("captest.failed", "Test run failed."), isError: true);
                CapTestStatusText.Text = LocalizationService.S("captest.failed", "Test run failed.");
            }
            finally
            {
                CapTestProgressBar.IsIndeterminate = false;
                CapTestProgressBorder.Visibility = Visibility.Collapsed;
                SetProvidersTabEnabled(true);
                RunTestsButton.IsEnabled = true;
            }
        }

        private void StopTestsButton_Click(object sender, RoutedEventArgs e)
        {
            _testCts?.Cancel();
            StopTestsButton.IsEnabled = false;
            CapTestProgressText.Text = LocalizationService.S("captest.cancelling", "Cancelling\u2026");
            // RunTestsButton stays disabled until the finally block in RunTestsButton_Click fires
        }

        internal static string ShortenErrorMessage(string error)
        {
            const int maxLen = 60;
            var readable = ProviderErrorClassifier.ExtractReadableMessage(error);
            if (!string.IsNullOrWhiteSpace(readable))
                error = readable;

            try
            {
                var jsonStart = error.IndexOf('{');
                if (jsonStart >= 0)
                {
                    var prefix = error[..jsonStart].Trim();
                    var jsonPart = error[jsonStart..];
                    using var doc = JsonDocument.Parse(jsonPart);
                    var root = doc.RootElement;

                    if (root.TryGetProperty("error", out var errObj) &&
                        errObj.TryGetProperty("message", out var msgProp))
                    {
                        var msg = msgProp.GetString() ?? string.Empty;
                        var combined = string.IsNullOrEmpty(prefix) ? msg : $"{prefix} {msg}";
                        return combined.Length <= maxLen ? combined : combined[..maxLen] + "…";
                    }
                }
            }
            catch
            {
            }

            return error.Length <= maxLen ? error : error[..maxLen] + "…";
        }

        /// <summary>
        /// Appends a single result row to the results panel.
        /// Call this while a test run is in progress to show results as they arrive.
        /// </summary>
        private void AppendTestResultRow(CapabilityTestResult r)
        {
            EnsureCapabilityResultsHeaderRow();

            var row = new Grid
            {
                Margin = new Thickness(0, 0, 0, 2),
            };

            AddCapabilityResultColumns(row);

            AddCell(row, new TextBlock
            {
                Text                = r.Passed ? "✓" : "✗",
                Foreground          = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x4C, 0xAF, 0x50)),
                FontSize            = 12,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment   = System.Windows.VerticalAlignment.Center,
            }, 0);

            AddCell(row, new TextBlock
            {
                Text              = r.Name,
                Foreground        = (System.Windows.Media.Brush)System.Windows.Application.Current.Resources["TextBrush"],
                FontSize          = 12,
                TextTrimming      = TextTrimming.CharacterEllipsis,
                VerticalAlignment = System.Windows.VerticalAlignment.Center,
            }, 1);

            AddCell(row, new TextBlock
            {
                Text              = r.Category,
                Foreground        = (System.Windows.Media.Brush)System.Windows.Application.Current.Resources["TextSecondaryBrush"],
                FontSize          = 11,
                TextTrimming      = TextTrimming.CharacterEllipsis,
                VerticalAlignment = System.Windows.VerticalAlignment.Center,
            }, 2);

            var detail = r.Passed ? (r.ActualTool ?? string.Empty) : ShortenErrorMessage(r.Error ?? "failed");
            var detailBlock = new TextBlock
            {
                Text         = detail,
                Foreground   = (System.Windows.Media.Brush)System.Windows.Application.Current.Resources["TextSecondaryBrush"],
                FontStyle    = r.Passed ? FontStyles.Normal : FontStyles.Italic,
                FontSize     = 11,
                TextWrapping = TextWrapping.Wrap,
            };
            if (!r.Passed && !string.IsNullOrEmpty(r.Error) && r.Error != detail)
                detailBlock.ToolTip = r.Error;

            AddCell(row, detailBlock, 3);
            AddCell(row, new TextBlock
            {
                Text                = $"{r.DurationMs / 1000.0:F1}s",
                Foreground          = (System.Windows.Media.Brush)System.Windows.Application.Current.Resources["TextSecondaryBrush"],
                FontSize            = 11,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                VerticalAlignment   = System.Windows.VerticalAlignment.Center,
            }, 4);

            var rerunButton = new Button
            {
                Content                 = "↻",
                Width                   = 24,
                Height                  = 24,
                Padding                 = new Thickness(0),
                Margin                  = new Thickness(0, 0, 0, 0),
                MinWidth                = 24,
                MinHeight               = 24,
                Tag                     = r.Id,
                ToolTip                 = "Run this test again",
                Foreground              = (System.Windows.Media.Brush)System.Windows.Application.Current.Resources["LinkBrush"],
                HorizontalContentAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalContentAlignment   = System.Windows.VerticalAlignment.Center,
            };
            rerunButton.Click += RerunCapabilityTestButton_Click;
            AddCell(row, rerunButton, 5);

            CapTestResultsPanel.Children.Add(row);
        }

        internal void DisplayTestResults(IEnumerable<CapabilityTestResult> results, DateTime testedAt)
        {
            var snapshot = results.ToList();
            _capabilityTestResults.Clear();
            _capabilityTestResults.AddRange(snapshot);

            CapTestResultsPanel.Children.Clear();
            EnsureCapabilityResultsHeaderRow();

            foreach (var r in snapshot)
                AppendTestResultRow(r);

            CapTestResultsBorder.Visibility = Visibility.Visible;
            UpdateTestedAtLabel(testedAt);
        }

        private void UpdateTestedAtLabel(DateTime testedAt)
        {
            var L = LocalizationService.S;
            var ago = DateTime.Now - testedAt;
            string when = ago.TotalSeconds < 10 ? L("captest.justNow", "just now")
                : ago.TotalMinutes < 1  ? string.Format(L("captest.secondsAgo", "{0}s ago"), (int)ago.TotalSeconds)
                : ago.TotalMinutes < 60 ? string.Format(L("captest.minutesAgo", "{0}m ago"), (int)ago.TotalMinutes)
                : ago.TotalHours   < 24 ? string.Format(L("captest.hoursAgo", "{0}h ago"), (int)ago.TotalHours)
                : testedAt.ToString("d");
            CapTestStatusText.Text = string.Format(L("captest.lastTested", "Last tested: {0}"), when);
        }

        private void EnsureCapabilityResultsHeaderRow()
        {
            if (CapTestResultsPanel.Children.Count > 0)
                return;

            var header = new Grid
            {
                Margin = new Thickness(0, 0, 0, 6),
            };

            AddCapabilityResultColumns(header);

            header.Children.Add(CreateHeaderText(" "));
            AddCell(header, CreateHeaderText("Test"), 1);
            AddCell(header, CreateHeaderText("Category"), 2);
            AddCell(header, CreateHeaderText("Result / details"), 3);
            AddCell(header, CreateHeaderText("Time"), 4);
            AddCell(header, CreateHeaderText(" "), 5);

            CapTestResultsPanel.Children.Add(header);
        }

        private static void AddCell(Grid grid, UIElement element, int column)
        {
            Grid.SetColumn(element, column);
            grid.Children.Add(element);
        }

        private static TextBlock CreateHeaderText(string text) =>
            new()
            {
                Text = text,
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = (System.Windows.Media.Brush)System.Windows.Application.Current.Resources["TextSecondaryBrush"],
                VerticalAlignment = System.Windows.VerticalAlignment.Center,
            };

        private static void AddCapabilityResultColumns(Grid grid)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto, SharedSizeGroup = "CapTestStatus" });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), SharedSizeGroup = "CapTestName" });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), SharedSizeGroup = "CapTestCategory" });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star), SharedSizeGroup = "CapTestDetail" });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto, SharedSizeGroup = "CapTestDuration" });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto, SharedSizeGroup = "CapTestAction" });
        }

        internal async Task SaveTestResultsAsync(Provider provider, List<CapabilityTestResult> results, DateTime testedAt)
        {
            try
            {
                var sessionService = _capabilitySessionService ?? new ProviderCapabilityTestSessionService();
                await sessionService.SaveAsync(
                    provider.Id,
                    provider.Model ?? string.Empty,
                    results,
                    testedAt,
                    _databaseService);
            }
            catch
            {
                AppLogger.Warn("CapabilityTests.SaveResults", "Failed to persist capability test results");
            }
        }
    }
}
