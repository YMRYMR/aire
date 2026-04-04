using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Aire.AppLayer.Abstractions;
using Aire.AppLayer.Providers;
using Aire.Data;
using Aire.Domain.Providers;
using Aire.Providers;
using Aire.Services;

namespace Aire.UI
{
    public partial class SettingsWindow
    {
        private readonly ProviderCapabilityTestApplicationService _capabilityTestApplicationService = new();
        private readonly ProviderCapabilityTestSessionService _capabilitySessionService = new();

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
                CapTestStatusText.Text = "Not yet tested";

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
            CapTestProgressText.Text = "Validating…";
            CapTestStatusText.Text = "Running…";

            // Clear the results panel now so rows appear as they arrive.
            CapTestResultsPanel.Children.Clear();
            _lastRenderedCategory = null;
            CapTestResultsBorder.Visibility = Visibility.Visible;

            var results = new List<CapabilityTestResult>();
            try
            {
                var progress = new Progress<ProviderCapabilityTestApplicationService.ProgressUpdate>(update =>
                {
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

                results = executionResult.Results.ToList();
                // Results are already rendered row by row; just update the timestamp label.
                UpdateTestedAtLabel(executionResult.TestedAt ?? DateTime.Now);
            }
            catch (OperationCanceledException)
            {
                CapTestStatusText.Text = "Tests cancelled.";
            }
            catch
            {
                ShowToast("Test run failed.", isError: true);
                CapTestStatusText.Text = "Test run failed.";
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

        private void StopTestsButton_Click(object sender, RoutedEventArgs e)
        {
            _testCts?.Cancel();
            StopTestsButton.IsEnabled = false;
            CapTestProgressText.Text = "Cancelling…";
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

        // Tracks the last category header written during an incremental run so we only
        // insert a new header when the category changes.
        private string? _lastRenderedCategory;

        /// <summary>
        /// Appends a single result row (and a category header when needed) to the results panel.
        /// Call this while a test run is in progress to show results as they arrive.
        /// </summary>
        private void AppendTestResultRow(CapabilityTestResult r)
        {
            // Category header — only when the category changes
            if (r.Category != _lastRenderedCategory)
            {
                bool isFirst = _lastRenderedCategory == null;
                CapTestResultsPanel.Children.Add(new TextBlock
                {
                    Text       = r.Category,
                    Foreground = (System.Windows.Media.Brush)System.Windows.Application.Current.Resources["TextSecondaryBrush"],
                    FontSize   = 10,
                    FontWeight = FontWeights.SemiBold,
                    Margin     = new Thickness(0, isFirst ? 0 : 10, 0, 4),
                });
                _lastRenderedCategory = r.Category;
            }

            // Result row
            var row = new StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                Margin      = new Thickness(0, 0, 0, 3),
            };

            row.Children.Add(new TextBlock
            {
                Text       = r.Passed ? "✓" : "✗",
                Foreground = new System.Windows.Media.SolidColorBrush(
                    r.Passed
                        ? System.Windows.Media.Color.FromRgb(0x4C, 0xAF, 0x50)
                        : System.Windows.Media.Color.FromRgb(0xF4, 0x43, 0x36)),
                Width    = 14,
                FontSize = 12,
            });

            row.Children.Add(new TextBlock
            {
                Text       = r.Name,
                Foreground = (System.Windows.Media.Brush)System.Windows.Application.Current.Resources["TextBrush"],
                Width    = 128,
                FontSize = 12,
            });

            var detail = r.Passed ? (r.ActualTool ?? string.Empty) : ShortenErrorMessage(r.Error ?? "failed");
            var detailBlock = new TextBlock
            {
                Text         = detail,
                Foreground   = (System.Windows.Media.Brush)System.Windows.Application.Current.Resources["TextSecondaryBrush"],
                FontStyle    = r.Passed ? FontStyles.Normal : FontStyles.Italic,
                MinWidth     = 80,
                FontSize     = 11,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth     = 180,
            };
            if (!r.Passed && !string.IsNullOrEmpty(r.Error) && r.Error != detail)
                detailBlock.ToolTip = r.Error;

            row.Children.Add(detailBlock);
            row.Children.Add(new TextBlock
            {
                Text       = $"  {r.DurationMs / 1000.0:F1}s",
                Foreground = (System.Windows.Media.Brush)System.Windows.Application.Current.Resources["TextSecondaryBrush"],
                FontSize   = 11,
            });

            CapTestResultsPanel.Children.Add(row);
        }

        internal void DisplayTestResults(IEnumerable<CapabilityTestResult> results, DateTime testedAt)
        {
            CapTestResultsPanel.Children.Clear();
            _lastRenderedCategory = null;

            foreach (var r in results)
                AppendTestResultRow(r);

            CapTestResultsBorder.Visibility = Visibility.Visible;
            UpdateTestedAtLabel(testedAt);
        }

        private void UpdateTestedAtLabel(DateTime testedAt)
        {
            var ago = DateTime.Now - testedAt;
            CapTestStatusText.Text = ago.TotalSeconds < 10 ? "Last tested: just now"
                : ago.TotalMinutes < 1  ? $"Last tested: {(int)ago.TotalSeconds}s ago"
                : ago.TotalMinutes < 60 ? $"Last tested: {(int)ago.TotalMinutes}m ago"
                : ago.TotalHours   < 24 ? $"Last tested: {(int)ago.TotalHours}h ago"
                : $"Last tested: {testedAt:d}";
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
