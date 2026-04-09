using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Aire.AppLayer.Providers;
using Aire.Data;
using Aire.Domain.Providers;
using Aire.Providers;
using Aire.Services;
using WpfApplication = System.Windows.Application;
using Button = System.Windows.Controls.Button;
using WpfBrush = System.Windows.Media.Brush;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfColor = System.Windows.Media.Color;
using WpfFontFamily = System.Windows.Media.FontFamily;
using WpfHorizontalAlignment = System.Windows.HorizontalAlignment;
using WpfVerticalAlignment = System.Windows.VerticalAlignment;

namespace Aire.UI
{
    public partial class SettingsWindow
    {
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
                Foreground          = new SolidColorBrush(
                    r.Passed
                        ? WpfColor.FromRgb(0x4C, 0xAF, 0x50)
                        : WpfColor.FromRgb(0xF4, 0x43, 0x36)),
                FontSize            = 12,
                HorizontalAlignment = WpfHorizontalAlignment.Center,
                VerticalAlignment   = WpfVerticalAlignment.Center,
            }, 0);

            AddCell(row, new TextBlock
            {
                Text              = r.Name,
                Foreground        = (WpfBrush)WpfApplication.Current.Resources["TextBrush"],
                FontSize          = 12,
                TextTrimming      = TextTrimming.CharacterEllipsis,
                VerticalAlignment = WpfVerticalAlignment.Center,
            }, 1);

            AddCell(row, new TextBlock
            {
                Text              = r.Category,
                Foreground        = (WpfBrush)WpfApplication.Current.Resources["TextSecondaryBrush"],
                FontSize          = 11,
                TextTrimming      = TextTrimming.CharacterEllipsis,
                VerticalAlignment = WpfVerticalAlignment.Center,
            }, 2);

            var detail = r.Passed ? (r.ActualTool ?? string.Empty) : ShortenErrorMessage(r.Error ?? "failed");
            var detailBlock = new TextBlock
            {
                Text         = detail,
                Foreground   = (WpfBrush)WpfApplication.Current.Resources["TextSecondaryBrush"],
                FontStyle    = r.Passed ? FontStyles.Normal : FontStyles.Italic,
                FontSize     = 11,
                TextWrapping = TextWrapping.NoWrap,
                TextTrimming = TextTrimming.CharacterEllipsis,
            };
            if (!r.Passed && !string.IsNullOrEmpty(r.Error) && r.Error != detail)
                detailBlock.ToolTip = r.Error;
            else if (r.Passed && !string.IsNullOrEmpty(detail))
                detailBlock.ToolTip = detail;

            AddCell(row, detailBlock, 3);
            AddCell(row, new TextBlock
            {
                Text                = $"{r.DurationMs / 1000.0:F1}s",
                Foreground          = (WpfBrush)WpfApplication.Current.Resources["TextSecondaryBrush"],
                FontSize            = 11,
                HorizontalAlignment = WpfHorizontalAlignment.Right,
                VerticalAlignment   = WpfVerticalAlignment.Center,
            }, 4);

            var rerunButton = new Button
            {
                Content                    = new TextBlock
                {
                    Text              = "\uE72C",
                    FontFamily        = new WpfFontFamily("Segoe MDL2 Assets"),
                    FontSize          = 12,
                    Foreground        = (WpfBrush)WpfApplication.Current.Resources["LinkBrush"],
                    TextAlignment     = TextAlignment.Center,
                    VerticalAlignment = WpfVerticalAlignment.Center,
                },
                Width                      = 22,
                Height                     = 22,
                Padding                    = new Thickness(0),
                Margin                     = new Thickness(0, 0, 0, 0),
                MinWidth                   = 22,
                MinHeight                  = 22,
                Tag                        = r.Id,
                ToolTip                    = "Run this test again",
                Foreground                 = (WpfBrush)WpfApplication.Current.Resources["LinkBrush"],
                HorizontalContentAlignment = WpfHorizontalAlignment.Center,
                VerticalContentAlignment   = WpfVerticalAlignment.Center,
                IsEnabled                  = !_isCapabilitySuiteRunning,
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
            SetCapabilityRerunButtonsEnabled(!_isCapabilitySuiteRunning);
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

            AddCell(header, CreateHeaderText(" "), 0);
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
                Foreground = (WpfBrush)WpfApplication.Current.Resources["TextSecondaryBrush"],
                VerticalAlignment = WpfVerticalAlignment.Center,
            };

        private static void AddCapabilityResultColumns(Grid grid)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(18) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(132) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(88) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 120 });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(56) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
        }

        private void SetCapabilityRerunButtonsEnabled(bool enabled)
        {
            foreach (var row in CapTestResultsPanel.Children.OfType<Grid>())
            {
                foreach (var button in row.Children.OfType<Button>())
                {
                    if (button.Tag is string)
                    {
                        button.IsEnabled = enabled;
                    }
                }
            }
        }
    }
}
