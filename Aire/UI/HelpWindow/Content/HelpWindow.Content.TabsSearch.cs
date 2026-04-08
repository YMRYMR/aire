using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Aire.Services;
using Brushes = System.Windows.Media.Brushes;

namespace Aire.UI
{
    public partial class HelpWindow
    {
        private void RebuildTabs()
        {
            TabButtonsPanel.Children.Clear();

            var tabNames = _allSections.Select(s => s.Tab ?? "General").Distinct().ToList();

            foreach (var tabName in tabNames)
            {
                var displayName = LocalizeTabName(tabName);
                var border = new Border
                {
                    Padding = new Thickness(16, 10, 16, 10),
                    BorderThickness = new Thickness(0, 0, 0, 2),
                    BorderBrush = Brushes.Transparent,
                    Cursor = System.Windows.Input.Cursors.Hand,
                    Tag = tabName,
                };
                var text = new TextBlock
                {
                    Text = displayName,
                    FontSize = 13,
                    Foreground = GetBrush("TextSecondaryBrush"),
                };
                border.Child = text;
                border.MouseLeftButtonDown += (_, _) => SelectTab(tabName);
                TabButtonsPanel.Children.Add(border);
            }

            var toSelect = tabNames.Contains(_activeTab) ? _activeTab : tabNames.FirstOrDefault() ?? "";
            SelectTab(toSelect);
        }

        private void SelectTab(string tabName)
        {
            _activeTab = tabName;

            foreach (Border btn in TabButtonsPanel.Children.OfType<Border>())
            {
                bool active = (string?)btn.Tag == tabName;
                btn.BorderBrush = active ? GetBrush("TextBrush") : Brushes.Transparent;
                if (btn.Child is TextBlock tb)
                {
                    tb.Foreground = active ? GetBrush("TextBrush") : GetBrush("TextSecondaryBrush");
                    tb.FontWeight = active ? FontWeights.SemiBold : FontWeights.Normal;
                }
            }

            RebuildContentForTab(tabName);
        }

        private void RebuildContentForTab(string tabName)
        {
            HelpContentPanel.Children.Clear();
            ContentScroller.ScrollToTop();

            foreach (var section in _allSections.Where(s => (s.Tab ?? "General") == tabName))
                RenderSection(section, HelpContentPanel);
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var query = SearchBox.Text.Trim();
            SearchPlaceholder.Visibility = string.IsNullOrEmpty(SearchBox.Text) ? Visibility.Visible : Visibility.Collapsed;
            ClearSearchButton.Visibility = string.IsNullOrEmpty(query) ? Visibility.Collapsed : Visibility.Visible;

            if (string.IsNullOrEmpty(query))
            {
                TabStrip.Visibility = Visibility.Visible;
                SelectTab(_activeTab);
                return;
            }

            TabStrip.Visibility = Visibility.Collapsed;
            RebuildSearchResults(query);
        }

        private void ClearSearchButton_Click(object sender, RoutedEventArgs e)
        {
            SearchBox.Text = "";
            SearchBox.Focus();
        }

        private void RebuildSearchResults(string query)
        {
            HelpContentPanel.Children.Clear();
            ContentScroller.ScrollToTop();

            var lq = query.ToLowerInvariant();
            var matches = _allSections.Where(s => SectionMatchesQuery(s, lq)).ToList();

            HelpContentPanel.Children.Add(new TextBlock
            {
                Text = matches.Count == 0
                    ? $"No results for \u201c{query}\u201d"
                    : $"{matches.Count} result{(matches.Count == 1 ? "" : "s")} for \u201c{query}\u201d",
                Foreground = GetBrush("TextSecondaryBrush"),
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 20),
            });

            foreach (var section in matches)
            {
                if (!string.IsNullOrEmpty(section.Tab))
                {
                    HelpContentPanel.Children.Add(new TextBlock
                    {
                        Text = LocalizeTabName(section.Tab).ToUpperInvariant(),
                        Foreground = GetBrush("TextSecondaryBrush"),
                        FontSize = 10,
                        FontWeight = FontWeights.SemiBold,
                        Margin = new Thickness(0, 0, 0, 2),
                    });
                }
                RenderSection(section, HelpContentPanel);
            }
        }
    }
}
