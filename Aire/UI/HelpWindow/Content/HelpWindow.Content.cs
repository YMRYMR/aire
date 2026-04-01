using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using Aire.Services;
using Application = System.Windows.Application;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using FontFamily = System.Windows.Media.FontFamily;
using TextBox = System.Windows.Controls.TextBox;

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
                    Text = tabName,
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
                        Text = section.Tab.ToUpperInvariant(),
                        Foreground = GetBrush("TextSecondaryBrush"),
                        FontSize = 10,
                        FontWeight = FontWeights.SemiBold,
                        Margin = new Thickness(0, 0, 0, 2),
                    });
                }
                RenderSection(section, HelpContentPanel);
            }
        }

        internal static bool SectionMatchesQuery(HelpSection section, string lq)
        {
            if (section.Title.Contains(lq, StringComparison.OrdinalIgnoreCase)) return true;
            if (section.Content?.Contains(lq, StringComparison.OrdinalIgnoreCase) == true) return true;
            if (section.Intro?.Contains(lq, StringComparison.OrdinalIgnoreCase) == true) return true;
            if (section.ImageCaption?.Contains(lq, StringComparison.OrdinalIgnoreCase) == true) return true;
            if (section.Rows != null)
            {
                foreach (var row in section.Rows)
                {
                    foreach (var cell in row)
                    {
                        if (cell.Contains(lq, StringComparison.OrdinalIgnoreCase)) return true;
                    }
                }
            }
            return false;
        }

        internal void RenderSection(HelpSection section, StackPanel target)
        {
            if (section.Type == "table") AddTableSection(section, target);
            else if (section.Type == "code") AddCodeSection(section, target);
            else AddTextSection(section, target);
            AddImageBlock(section, target);
            RenderLinks(section, target);
        }

        private void RenderLinks(HelpSection section, StackPanel target)
        {
            if (section.Links == null || section.Links.Length == 0) return;

            var panel = new WrapPanel { Margin = new Thickness(0, -8, 0, 24) };
            foreach (var link in section.Links)
            {
                var action = link.Action;
                var button = new System.Windows.Controls.Button
                {
                    Content = link.Label,
                    Margin = new Thickness(0, 0, 8, 8),
                    Padding = new Thickness(10, 6, 10, 6),
                    Cursor = System.Windows.Input.Cursors.Hand,
                    Background = GetBrush("AccentSurfaceBrush"),
                    Foreground = GetBrush("TextBrush"),
                    BorderBrush = GetBrush("AccentBorderBrush"),
                    BorderThickness = new Thickness(1),
                    MinHeight = 30,
                };
                button.Click += (_, _) => ExecuteLinkAction(action);
                panel.Children.Add(button);
            }

            target.Children.Add(new Border
            {
                Background = Brushes.Transparent,
                Child = panel
            });
        }

        private void ExecuteLinkAction(string action)
        {
            if (action.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                action.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                Dispatcher.Invoke(() => WebViewWindow.OpenInNewTab(action));
                return;
            }

            if (action.StartsWith("browser:", StringComparison.OrdinalIgnoreCase))
            {
                var url = action["browser:".Length..];
                Dispatcher.Invoke(() => WebViewWindow.OpenInNewTab(url));
                return;
            }

            if (action.Equals("onboarding", StringComparison.OrdinalIgnoreCase))
            {
                Dispatcher.Invoke(() =>
                {
                    var wizard = new OnboardingWindow
                    {
                        Owner = this
                    };
                    wizard.OpenSettingsAction = () => SettingsWindow.RequestOpen("providers");
                    wizard.ShowDialog();
                });
                return;
            }

            if (action == "settings" || action.StartsWith("settings:", StringComparison.OrdinalIgnoreCase))
            {
                var tab = action.Contains(':') ? action[(action.IndexOf(':') + 1)..] : null;
                SettingsWindow.RequestOpen(tab);
            }
        }

        private void AddTextSection(HelpSection section, StackPanel target)
        {
            target.Children.Add(new TextBlock
            {
                Text = section.Title,
                Foreground = GetBrush("TextBrush"),
                FontSize = 15,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 6),
                TextWrapping = TextWrapping.Wrap,
            });

            if (!string.IsNullOrEmpty(section.Content))
            {
                target.Children.Add(new TextBox
                {
                    Text = section.Content,
                    Foreground = GetBrush("TextSecondaryBrush"),
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    IsReadOnly = true,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 0, 0, 24),
                    IsTabStop = false,
                    Padding = new Thickness(0),
                });
            }
        }

        private void AddCodeSection(HelpSection section, StackPanel target)
        {
            target.Children.Add(new TextBlock
            {
                Text = section.Title,
                Foreground = GetBrush("TextBrush"),
                FontSize = 15,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 6),
                TextWrapping = TextWrapping.Wrap,
            });

            if (!string.IsNullOrEmpty(section.Intro))
            {
                target.Children.Add(new TextBox
                {
                    Text = section.Intro,
                    Foreground = GetBrush("TextSecondaryBrush"),
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    IsReadOnly = true,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 0, 0, 6),
                    IsTabStop = false,
                    Padding = new Thickness(0),
                });
            }

            if (!string.IsNullOrEmpty(section.Content))
            {
                var border = new Border
                {
                    Background = GetBrush("Surface2Brush"),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(10),
                    Margin = new Thickness(0, 0, 0, 24),
                };
                border.Child = new TextBox
                {
                    Text = section.Content,
                    Foreground = GetBrush("TextBrush"),
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    IsReadOnly = true,
                    TextWrapping = TextWrapping.Wrap,
                    IsTabStop = false,
                    Padding = new Thickness(0),
                    FontFamily = new FontFamily("Consolas, Courier New"),
                    FontSize = 12,
                };
                target.Children.Add(border);
            }
        }

        private void AddTableSection(HelpSection section, StackPanel target)
        {
            target.Children.Add(new TextBlock
            {
                Text = section.Title,
                Foreground = GetBrush("TextBrush"),
                FontSize = 15,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 6),
                TextWrapping = TextWrapping.Wrap,
            });

            if (!string.IsNullOrEmpty(section.Intro))
            {
                target.Children.Add(new TextBox
                {
                    Text = section.Intro,
                    Foreground = GetBrush("TextSecondaryBrush"),
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    IsReadOnly = true,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 0, 0, 6),
                    IsTabStop = false,
                    Padding = new Thickness(0),
                });
            }

            var cols = section.Cols ?? Array.Empty<string>();
            var rows = section.Rows ?? Array.Empty<string[]>();

            var grid = new Grid { Margin = new Thickness(0) };
            for (int c = 0; c < cols.Length; c++)
            {
                grid.ColumnDefinitions.Add(c == 0
                    ? new ColumnDefinition { Width = new GridLength(160) }
                    : new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            }
            for (int r = 0; r < rows.Length + 1; r++)
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            for (int c = 0; c < cols.Length; c++)
            {
                var header = new TextBlock
                {
                    Text = cols[c],
                    Foreground = GetBrush("TextSecondaryBrush"),
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    Margin = new Thickness(0, 0, 0, 6),
                };
                Grid.SetRow(header, 0);
                Grid.SetColumn(header, c);
                grid.Children.Add(header);
            }

            for (int r = 0; r < rows.Length; r++)
            {
                var row = rows[r];
                bool isLast = r == rows.Length - 1;
                for (int c = 0; c < cols.Length && c < row.Length; c++)
                {
                    var cell = new TextBox
                    {
                        Text = row[c],
                        Foreground = GetBrush(c == 0 ? "TextBrush" : "TextSecondaryBrush"),
                        Background = Brushes.Transparent,
                        BorderThickness = new Thickness(0),
                        IsReadOnly = true,
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(0, 0, 0, isLast ? 0 : 6),
                        IsTabStop = false,
                        Padding = new Thickness(0),
                        VerticalAlignment = VerticalAlignment.Top,
                    };
                    Grid.SetRow(cell, r + 1);
                    Grid.SetColumn(cell, c);
                    grid.Children.Add(cell);
                }
            }

            target.Children.Add(new Border
            {
                Background = GetBrush("Surface2Brush"),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12, 10, 12, 10),
                Margin = new Thickness(0, 0, 0, 24),
                Child = grid,
            });
        }

        private void AddImageBlock(HelpSection section, StackPanel target)
        {
            if (string.IsNullOrWhiteSpace(section.ImagePath)) return;

            var imagePath = ResolveHelpImagePath(section.ImagePath);
            if (imagePath == null || !File.Exists(imagePath)) return;

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(imagePath, UriKind.Absolute);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();

            var panel = new StackPanel
            {
                Margin = new Thickness(0, 0, 0, section.Links is { Length: > 0 } ? 12 : 24),
            };

            panel.Children.Add(new Border
            {
                Background = GetBrush("Surface2Brush"),
                BorderBrush = GetBrush("BorderBrush"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(8),
                Child = new System.Windows.Controls.Image
                {
                    Source = bitmap,
                    Stretch = Stretch.Uniform,
                    MaxWidth = 520,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
                }
            });

            if (!string.IsNullOrWhiteSpace(section.ImageCaption))
            {
                panel.Children.Add(new TextBlock
                {
                    Text = section.ImageCaption,
                    Foreground = GetBrush("TextSecondaryBrush"),
                    FontSize = 11,
                    Margin = new Thickness(2, 8, 2, 0),
                    TextWrapping = TextWrapping.Wrap,
                });
            }

            target.Children.Add(panel);
        }

        private static string? ResolveHelpImagePath(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath)) return null;
            if (Path.IsPathRooted(relativePath)) return relativePath;
            return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, relativePath));
        }

        private static Brush GetBrush(string key)
        {
            var res = Application.Current.Resources[key];
            return res is Brush b ? b : Brushes.Gray;
        }
    }
}
