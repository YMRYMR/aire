using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Aire.Services;
using Brushes = System.Windows.Media.Brushes;
using FontFamily = System.Windows.Media.FontFamily;
using TextBox = System.Windows.Controls.TextBox;

namespace Aire.UI
{
    public partial class HelpWindow
    {
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

            var panel = new WrapPanel
            {
                Margin = new Thickness(0, -8, 0, 24),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
            };
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
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                Child = panel
            });
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
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
            };

            panel.Children.Add(new System.Windows.Controls.Image
            {
                Source = bitmap,
                Stretch = Stretch.Uniform,
                MaxWidth = 520,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
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
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    TextAlignment = TextAlignment.Center,
                    MaxWidth = 520,
                });
            }

            target.Children.Add(panel);
        }
    }
}
