using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Aire.Data;
using Aire.Providers;
using Aire.Services;
using Aire.UI.Settings.Models;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfBrush = System.Windows.Media.Brush;
using WpfColor = System.Windows.Media.Color;
using WpfPoint = System.Windows.Point;
using WpfSolidColorBrush = System.Windows.Media.SolidColorBrush;

namespace Aire.UI
{
    public partial class SettingsWindow
    {
        private async void UsageRefreshButton_Click(object sender, RoutedEventArgs e)
            => await LoadUsageDashboardAsync();

        private void UsageCurrencyButton_Click(object sender, RoutedEventArgs e)
        {
            var menu = new System.Windows.Controls.ContextMenu
            {
                PlacementTarget = UsageCurrencyButton,
                Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom
            };

            var current = CurrencyExchangeService.GetPreferredCurrency();
            foreach (var currency in CurrencyExchangeService.SupportedCurrencies)
            {
                var item = new System.Windows.Controls.MenuItem
                {
                    Header = currency,
                    IsCheckable = true,
                    IsChecked = string.Equals(currency, current, StringComparison.OrdinalIgnoreCase),
                };

                item.Click += async (_, _) =>
                {
                    CurrencyExchangeService.SetPreferredCurrency(currency);
                    await LoadUsageDashboardAsync();
                };

                menu.Items.Add(item);
            }

            menu.IsOpen = true;
        }

        private async Task LoadUsageDashboardAsync()
        {
            try
            {
                var snapshot = await _databaseService.GetUsageDashboardSnapshotAsync();
                var preferredCurrency = CurrencyExchangeService.GetPreferredCurrency();
                var liveUsageByProviderId = await LoadProviderUsageMapAsync(snapshot.Providers);

                UsageTotalTokensText.Text = FormatTokens(snapshot.TotalTokens);
                UsageProviderCountText.Text = FormatCount(snapshot.ProviderCount);
                UsageConversationCountText.Text = FormatCount(snapshot.ConversationCount);
                UsageAssistantMessageCountText.Text = FormatCount(snapshot.AssistantMessageCount);

                _usageProviderVms = new ObservableCollection<UsageProviderRowViewModel>(
                    snapshot.Providers
                        .OrderByDescending(provider => provider.TokensUsed)
                        .ThenBy(provider => provider.ProviderName, StringComparer.OrdinalIgnoreCase)
                        .Select(provider =>
                            BuildProviderRow(
                                provider,
                                liveUsageByProviderId.TryGetValue(provider.ProviderId, out var usage) ? usage : null,
                                preferredCurrency)));
                _usageConversationVms = new ObservableCollection<UsageConversationRowViewModel>(
                    snapshot.Conversations.Select(conversation => BuildConversationRow(conversation, preferredCurrency)));

                UsageProvidersListView.ItemsSource = _usageProviderVms;
                UsageConversationsListView.ItemsSource = _usageConversationVms;
                _usageTrendSeries = snapshot.TrendSeries;
                _usageTrendLegendVms = new ObservableCollection<UsageTrendLegendItemViewModel>(
                    BuildUsageTrendLegendItems(_usageTrendSeries));
                UsageTrendLegendItemsControl.ItemsSource = _usageTrendLegendVms;

                UpdateUsageHeaderLocalization();
                RenderUsageTrendChart();
                await LoadLiveProviderUsageAsync();
            }
            catch (Exception ex)
            {
                AppLogger.Warn("SettingsWindow.LoadUsageDashboardAsync", "Could not refresh usage data", ex);
                var L = LocalizationService.S;
                UsageTotalTokensText.Text = "0";
                UsageProviderCountText.Text = "0";
                UsageConversationCountText.Text = "0";
                UsageAssistantMessageCountText.Text = "0";
                UsageProvidersListView.ItemsSource = null;
                UsageConversationsListView.ItemsSource = null;
                UsageTrendLegendItemsControl.ItemsSource = null;
                UsageLiveProviderText.Text = L("settings.usageNoProviderSelected", "No provider selected");
                UsageLiveUsageText.Text = L("settings.usageLiveUnavailable", "Unavailable: live usage unavailable");
                UsageLiveUsageDetailText.Text = L("settings.usageLiveUnavailableDetail", "Aire could not fetch live usage for the selected provider.");
            }
        }

        private async Task<Dictionary<int, TokenUsage?>> LoadProviderUsageMapAsync(IEnumerable<ProviderUsageSummary> providerSummaries)
        {
            var providerLookup = _providers.ToDictionary(provider => provider.Id);
            var tasks = providerSummaries.Select(async summary =>
            {
                if (!providerLookup.TryGetValue(summary.ProviderId, out var provider))
                    return (summary.ProviderId, usage: (TokenUsage?)null);

                try
                {
                    var runtimeProvider = _providerFactory.CreateProvider(provider);
                    var usage = await TokenUsageService.GetTokenUsageAsync(runtimeProvider, forceRefresh: true);
                    return (summary.ProviderId, usage);
                }
                catch
                {
                    return (summary.ProviderId, usage: (TokenUsage?)null);
                }
            });

            var results = await Task.WhenAll(tasks);
            return results.ToDictionary(result => result.ProviderId, result => result.usage);
        }

        private async Task LoadLiveProviderUsageAsync()
        {
            var L = LocalizationService.S;
            var preferredCurrency = CurrencyExchangeService.GetPreferredCurrency();

            var provider = _selectedProvider ?? _providers.FirstOrDefault(p => p.IsEnabled);
            if (provider == null)
            {
                UsageLiveProviderText.Text = L("settings.usageNoProviderSelected", "No provider selected");
                UsageLiveUsageText.Text = L("settings.usageNoProviderUsage", "Select a provider in the AI Providers tab to see live quota or spend.");
                UsageLiveUsageDetailText.Text = L("settings.usageHistoricalNote", "Historical totals below still track stored assistant turns.");
                return;
            }

            UsageLiveProviderText.Text = $"{provider.Name} · {provider.DisplayType} · {provider.Model}";

            try
            {
                var runtimeProvider = _providerFactory.CreateProvider(provider);
                var usage = await TokenUsageService.GetTokenUsageAsync(runtimeProvider, forceRefresh: true);
                if (usage == null)
                {
                    UsageLiveUsageText.Text = L("settings.usageNoLiveUsage", "Unavailable: no live usage data");
                    UsageLiveUsageDetailText.Text = L("settings.usageNoLiveUsageDetail", "This provider does not expose quota or spend information.");
                    return;
                }

                UsageLiveUsageText.Text = string.Format(
                    L("settings.usageLiveSpendLabel", "Live: {0}"),
                    FormatLiveUsageText(usage, preferredCurrency));
                UsageLiveUsageDetailText.Text = BuildLiveUsageDetailText(usage, preferredCurrency);
            }
            catch
            {
                UsageLiveUsageText.Text = L("settings.usageLiveUnavailable", "Unavailable: live usage unavailable");
                UsageLiveUsageDetailText.Text = L("settings.usageLiveUnavailableDetail", "Aire could not fetch live usage for the selected provider.");
            }
        }

        private void UpdateUsageHeaderLocalization()
        {
            var L = LocalizationService.S;
            UsageOverviewTitleText.Text = L("settings.usageOverview", "Usage overview");
            UsageRecordedTokensLabel.Text = L("settings.usageRecordedTokens", "Recorded tokens");
            UsageConversationCountLabel.Text = L("settings.usageConversations", "Conversations with usage");
            UsageProviderCountLabel.Text = L("settings.usageProviders", "Providers with usage");
            UsageAssistantTurnsLabel.Text = L("settings.usageAssistantTurns", "Assistant turns tracked");
            UsageLiveTitleText.Text = L("settings.usageLiveTitle", "Live provider usage");
            UsageOverviewHintText.Text = L("settings.usageHint",
                "Tokens are recorded from assistant turns that return usage data. When a provider does not expose live spend, Aire estimates it from recorded tokens and published model pricing.");
            UsageTrustTitleText.Text = L("settings.usageTrustTitle", "What Aire tracks");
            UsageRecordedSourceTitleText.Text = L("settings.usageRecordedSourceTitle", "Recorded");
            UsageRecordedSourceText.Text = L("settings.usageRecordedSource", "Stored assistant turns with token counts.");
            UsageLiveSourceTitleText.Text = L("settings.usageLiveSourceTitle", "Live-reported");
            UsageLiveSourceText.Text = L("settings.usageLiveSource", "Provider-reported quota or balance when the API exposes it.");
            UsageEstimatedSourceTitleText.Text = L("settings.usageEstimatedSourceTitle", "Estimated");
            UsageEstimatedSourceText.Text = L("settings.usageEstimatedSource", "When live usage is unavailable, Aire estimates spend from recorded tokens and published model pricing.");
            UsageTrendTitleText.Text = L("settings.usageTrendTitle", "Recorded usage trend");
            UsageTrendHintText.Text = L("settings.usageTrendHint", "Line-spline chart of stored assistant tokens by provider and model.");
            UsageTrendEmptyText.Text = L("settings.usageTrendEmpty", "No recorded assistant tokens yet.");

            UsageRefreshButton.Content = "↻";
            UsageRefreshButton.ToolTip = L("settings.refreshUsageTooltip", "Refresh usage data");

            UsageCurrencyButton.Content = CurrencyExchangeService.GetPreferredCurrency();
            UsageCurrencyButton.ToolTip = string.Format(
                L("settings.usageCurrencyTooltip", "Preferred currency: {0}"),
                CurrencyExchangeService.GetPreferredCurrency());
        }

        private void UsageTrendChartHost_SizeChanged(object sender, SizeChangedEventArgs e)
            => RenderUsageTrendChart();

        private void RenderUsageTrendChart()
        {
            if (UsageTrendCanvas == null || UsageTrendChartHost == null || UsageTrendEmptyText == null)
                return;

            UsageTrendCanvas.Children.Clear();

            var series = _usageTrendSeries
                .Where(item => item.Points.Count > 0)
                .ToList();

            if (series.Count == 0)
            {
                UsageTrendEmptyText.Visibility = Visibility.Visible;
                return;
            }

            var allPoints = series.SelectMany(item => item.Points).ToList();
            if (allPoints.Count == 0)
            {
                UsageTrendEmptyText.Visibility = Visibility.Visible;
                return;
            }

            UsageTrendEmptyText.Visibility = Visibility.Collapsed;

            var width = UsageTrendChartHost.ActualWidth;
            var height = UsageTrendChartHost.ActualHeight;
            if (width <= 0 || height <= 0)
                return;

            const double leftPadding = 46;
            const double rightPadding = 16;
            const double topPadding = 14;
            const double bottomPadding = 22;

            var plotWidth = Math.Max(1, width - leftPadding - rightPadding);
            var plotHeight = Math.Max(1, height - topPadding - bottomPadding);

            var minBucket = allPoints.Min(point => point.Bucket);
            var maxBucket = allPoints.Max(point => point.Bucket);
            var totalDays = Math.Max(1, maxBucket.DayNumber - minBucket.DayNumber);
            var maxTokens = Math.Max(1, allPoints.Max(point => point.TokensUsed));

            DrawGridLines(leftPadding, topPadding, plotWidth, plotHeight, maxTokens);
            DrawAxisLabels(leftPadding, topPadding, plotWidth, plotHeight, minBucket, maxBucket);

            foreach (var trend in series)
            {
                var brush = CreateBrush(trend.Color);
                var points = trend.Points
                    .Select(point =>
                    {
                        var xRatio = (double)(point.Bucket.DayNumber - minBucket.DayNumber) / totalDays;
                        var yRatio = (double)point.TokensUsed / maxTokens;
                        return new WpfPoint(
                            leftPadding + (xRatio * plotWidth),
                            topPadding + plotHeight - (yRatio * plotHeight));
                    })
                    .ToList();

                if (points.Count == 1)
                {
                    AddPointMarker(points[0], brush);
                    continue;
                }

                var path = new Path
                {
                    Data = BuildSmoothGeometry(points),
                    Stroke = brush,
                    StrokeThickness = 2.5,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round,
                    StrokeLineJoin = PenLineJoin.Round,
                    SnapsToDevicePixels = true,
                };

                UsageTrendCanvas.Children.Add(path);

                foreach (var point in points.Where((_, index) => index == 0 || index == points.Count - 1))
                    AddPointMarker(point, brush);
            }
        }

        private static ObservableCollection<UsageTrendLegendItemViewModel> BuildUsageTrendLegendItems(IEnumerable<UsageTrendSeries> series)
        {
            return new ObservableCollection<UsageTrendLegendItemViewModel>(
                series.Select(item => new UsageTrendLegendItemViewModel(
                    item.Label,
                    FormatTokens(item.TotalTokens),
                    CreateBrush(item.Color))));
        }

        private void DrawGridLines(double leftPadding, double topPadding, double plotWidth, double plotHeight, long maxTokens)
        {
            var gridBrush = WithOpacity(GetThemeBrush("BorderBrush", WpfBrushes.Gray), 0.45);
            var labelBrush = GetThemeBrush("TextSecondaryBrush", WpfBrushes.Gray);

            for (var i = 0; i <= 4; i++)
            {
                var ratio = i / 4d;
                var y = topPadding + plotHeight - (ratio * plotHeight);
                var line = new Line
                {
                    X1 = leftPadding,
                    X2 = leftPadding + plotWidth,
                    Y1 = y,
                    Y2 = y,
                    Stroke = gridBrush,
                    StrokeThickness = i == 0 ? 1.2 : 1,
                };
                UsageTrendCanvas.Children.Add(line);

                var label = new TextBlock
                {
                    Text = Math.Round(maxTokens * ratio).ToString("N0"),
                    Foreground = labelBrush,
                    FontSize = 10,
                };
                Canvas.SetLeft(label, 0);
                Canvas.SetTop(label, y - 7);
                UsageTrendCanvas.Children.Add(label);
            }
        }

        private void DrawAxisLabels(double leftPadding, double topPadding, double plotWidth, double plotHeight, DateOnly minBucket, DateOnly maxBucket)
        {
            var labelBrush = GetThemeBrush("TextSecondaryBrush", WpfBrushes.Gray);

            var leftLabel = new TextBlock
            {
                Text = minBucket.ToString("MMM d"),
                Foreground = labelBrush,
                FontSize = 10,
            };
            Canvas.SetLeft(leftLabel, leftPadding);
            Canvas.SetTop(leftLabel, topPadding + plotHeight + 2);
            UsageTrendCanvas.Children.Add(leftLabel);

            var rightLabel = new TextBlock
            {
                Text = maxBucket.ToString("MMM d"),
                Foreground = labelBrush,
                FontSize = 10,
            };
            Canvas.SetLeft(rightLabel, leftPadding + plotWidth - 28);
            Canvas.SetTop(rightLabel, topPadding + plotHeight + 2);
            UsageTrendCanvas.Children.Add(rightLabel);
        }

        private void AddPointMarker(WpfPoint point, WpfBrush brush)
        {
            var marker = new Ellipse
            {
                Width = 6,
                Height = 6,
                Fill = brush,
                Stroke = new WpfSolidColorBrush(WpfColor.FromArgb(210, 32, 32, 32)),
                StrokeThickness = 1,
            };

            Canvas.SetLeft(marker, point.X - 3);
            Canvas.SetTop(marker, point.Y - 3);
            UsageTrendCanvas.Children.Add(marker);
        }

        private static StreamGeometry BuildSmoothGeometry(IReadOnlyList<WpfPoint> points)
        {
            var geometry = new StreamGeometry();
            using (var context = geometry.Open())
            {
                context.BeginFigure(points[0], false, false);
                if (points.Count == 2)
                {
                    context.LineTo(points[1], true, false);
                }
                else
                {
                    for (var i = 0; i < points.Count - 1; i++)
                    {
                        var p0 = i == 0 ? points[i] : points[i - 1];
                        var p1 = points[i];
                        var p2 = points[i + 1];
                        var p3 = i + 2 < points.Count ? points[i + 2] : p2;

                        var c1 = new WpfPoint(
                            p1.X + (p2.X - p0.X) / 6.0,
                            p1.Y + (p2.Y - p0.Y) / 6.0);
                        var c2 = new WpfPoint(
                            p2.X - (p3.X - p1.X) / 6.0,
                            p2.Y - (p3.Y - p1.Y) / 6.0);

                        context.BezierTo(c1, c2, p2, true, false);
                    }
                }
            }

            geometry.Freeze();
            return geometry;
        }

        private static WpfSolidColorBrush CreateBrush(string color)
        {
            try
            {
                var brush = (WpfBrush)new BrushConverter().ConvertFromString(color)!;
                if (brush is WpfSolidColorBrush solid)
                    return solid;
            }
            catch
            {
            }

            return new WpfSolidColorBrush(WpfColor.FromRgb(96, 165, 250));
        }

        private static WpfBrush GetThemeBrush(string key, WpfBrush fallback)
            => System.Windows.Application.Current?.TryFindResource(key) as WpfBrush ?? fallback;

        private static WpfBrush WithOpacity(WpfBrush brush, double opacity)
        {
            if (brush is WpfSolidColorBrush solid)
            {
                var clone = solid.Clone();
                clone.Opacity = opacity;
                return clone;
            }

            return brush;
        }

        private static UsageProviderRowViewModel BuildProviderRow(
            ProviderUsageSummary summary,
            TokenUsage? liveUsage,
            string preferredCurrency)
        {
            var L = LocalizationService.S;
            var detail = $"{summary.ProviderType}";
            if (!string.IsNullOrWhiteSpace(summary.Model))
                detail += $" · {summary.Model}";
            detail += summary.ConversationCount == 1
                ? $" · {L("settings.usageConversationSingular", "1 conversation")}"
                : $" · {summary.ConversationCount} {L("settings.usageConversationPlural", "conversations")}";
            detail += summary.AssistantMessageCount == 1
                ? $" · {L("settings.usageAssistantTurnSingular", "1 assistant turn")}"
                : $" · {summary.AssistantMessageCount} {L("settings.usageAssistantTurnPlural", "assistant turns")}";
            detail += summary.LastUsedAt.HasValue
                ? $" · {string.Format(L("settings.usageLastUsed", "last used {0}"), summary.LastUsedAt.Value.ToString("g"))}"
                : $" · {L("settings.usageNeverUsed", "never used")}";

            var usageDetail = summary.IsEnabled
                ? L("settings.usageEnabled", "enabled")
                : L("settings.usageDisabled", "disabled");
            var tokensText = FormatTokens(summary.TokensUsed);
            var spendText = BuildSpendText(summary, liveUsage, preferredCurrency);
            return new UsageProviderRowViewModel(
                summary.ProviderId,
                summary.ProviderName,
                detail,
                summary.Color,
                tokensText,
                spendText,
                usageDetail);
        }

        private static UsageConversationRowViewModel BuildConversationRow(ConversationUsageSummary summary, string preferredCurrency)
        {
            var L = LocalizationService.S;
            var detail = $"{summary.ProviderName}";
            if (!string.IsNullOrWhiteSpace(summary.Model))
                detail += $" · {summary.Model}";
            detail += summary.AssistantMessageCount == 1
                ? $" · {L("settings.usageAssistantTurnSingular", "1 assistant turn")}"
                : $" · {summary.AssistantMessageCount} {L("settings.usageAssistantTurnPlural", "assistant turns")}";
            detail += $" · {string.Format(L("settings.usageUpdated", "updated {0}"), summary.UpdatedAt.ToString("g"))}";

            var usageDetail = summary.TokensUsed > 0
                ? string.Format(L("settings.usageUpdated", "updated {0}"), summary.UpdatedAt.ToString("g"))
                : L("settings.usageNoTokensRecorded", "no tokens recorded");
            var spendText = BuildSpendText(summary, preferredCurrency);

            return new UsageConversationRowViewModel(
                summary.ConversationId,
                summary.Title,
                detail,
                summary.Color,
                FormatTokens(summary.TokensUsed),
                spendText,
                usageDetail);
        }

        private static string BuildSpendText(ProviderUsageSummary summary, TokenUsage? liveUsage, string preferredCurrency)
        {
            if (liveUsage != null)
            {
                if (string.Equals(liveUsage.Unit, "USD", StringComparison.OrdinalIgnoreCase))
                    return string.Format(
                        LocalizationService.S("settings.usageSpendLiveLabel", "Live: {0}"),
                        CurrencyExchangeService.FormatFromUsd(liveUsage.Used / 100m, preferredCurrency));

                if (IsCurrencyUnit(liveUsage.Unit))
                    return string.Format(
                        LocalizationService.S("settings.usageSpendLiveLabel", "Live: {0}"),
                        CurrencyExchangeService.FormatFromMinorUnits(liveUsage.Used, liveUsage.Unit));

                if (liveUsage.Unit.Equals("credits", StringComparison.OrdinalIgnoreCase))
                    return string.Format(
                        LocalizationService.S("settings.usageSpendLiveUnitsLabel", "Live: {0}"),
                        $"{liveUsage.Used / 100m:N2} {liveUsage.Unit}");
            }

            if (UsageSpendEstimator.TryEstimateUsd(summary.ProviderType, summary.Model, summary.TokensUsed, out var estimatedUsd))
                return string.Format(
                    LocalizationService.S("settings.usageSpendEstimatedLabel", "Estimated: {0}"),
                    CurrencyExchangeService.FormatFromUsd(estimatedUsd, preferredCurrency));

            return LocalizationService.S("settings.usageSpendUnavailable", "Unavailable");
        }

        private static string BuildSpendText(ConversationUsageSummary summary, string preferredCurrency)
        {
            if (UsageSpendEstimator.TryEstimateUsd(summary.ProviderType, summary.Model, summary.TokensUsed, out var estimatedUsd))
                return string.Format(
                    LocalizationService.S("settings.usageSpendEstimatedLabel", "Estimated: {0}"),
                    CurrencyExchangeService.FormatFromUsd(estimatedUsd, preferredCurrency));

            return LocalizationService.S("settings.usageSpendUnavailable", "Unavailable");
        }

        private static bool IsCurrencyUnit(string unit)
        {
            var normalized = CurrencyExchangeService.NormalizeCurrencyCode(unit);
            return CurrencyExchangeService.SupportedCurrencies.Contains(normalized, StringComparer.OrdinalIgnoreCase);
        }

        private static string FormatTokens(long tokens)
            => tokens.ToString("N0");

        private static string FormatCount(int count)
            => count.ToString("N0");

        private static string FormatLiveUsageText(TokenUsage usage, string currencyCode)
        {
            if (string.Equals(usage.Unit, "USD", StringComparison.OrdinalIgnoreCase))
            {
                var used = CurrencyExchangeService.FormatFromUsd(usage.Used / 100m, currencyCode);
                if (usage.Limit.HasValue)
                {
                    var limit = CurrencyExchangeService.FormatFromUsd(usage.Limit.Value / 100m, currencyCode);
                    return $"{used} / {limit}";
                }

                return used;
            }

            if (IsCurrencyUnit(usage.Unit))
            {
                var used = CurrencyExchangeService.FormatFromMinorUnits(usage.Used, usage.Unit);
                if (usage.Limit.HasValue)
                {
                    var limit = CurrencyExchangeService.FormatFromMinorUnits(usage.Limit.Value, usage.Unit);
                    return $"{used} / {limit}";
                }

                return used;
            }

            if (usage.Limit.HasValue)
                return $"{usage.Used:N0} / {usage.Limit.Value:N0} {usage.Unit}";

            return $"{usage.Used:N0} {usage.Unit}";
        }

        private static string BuildLiveUsageDetailText(TokenUsage usage, string currencyCode)
        {
            var parts = new List<string>();
            var L = LocalizationService.S;

            if (usage.Remaining.HasValue)
            {
                if (string.Equals(usage.Unit, "USD", StringComparison.OrdinalIgnoreCase))
                {
                    parts.Add(string.Format(
                        L("settings.usageRemainingMoney", "{0} remaining"),
                        CurrencyExchangeService.FormatFromUsd(usage.Remaining.Value / 100m, currencyCode)));
                }
                else if (IsCurrencyUnit(usage.Unit))
                {
                    parts.Add(string.Format(
                        L("settings.usageRemainingMoney", "{0} remaining"),
                        CurrencyExchangeService.FormatFromMinorUnits(usage.Remaining.Value, usage.Unit)));
                }
                else if (usage.Unit.Equals("credits", StringComparison.OrdinalIgnoreCase))
                {
                    parts.Add(string.Format(
                        L("settings.usageRemainingTokens", "{0} remaining"),
                        $"{usage.Remaining.Value / 100m:N2} {usage.Unit}"));
                }
                else
                {
                    parts.Add(string.Format(
                        L("settings.usageRemainingTokens", "{0} remaining"),
                        $"{usage.Remaining.Value:N0} {usage.Unit}"));
                }
            }

            if (usage.ResetDate.HasValue)
            {
                parts.Add(string.Format(
                    L("settings.usageResets", "resets {0}"),
                    usage.ResetDate.Value.ToString("g")));
            }

            if (parts.Count == 0)
                parts.Add(L("settings.usageNoLimitOrReset", "Live usage data is available, but the provider did not return a limit or reset time."));

            return string.Join(" · ", parts);
        }
    }
}
