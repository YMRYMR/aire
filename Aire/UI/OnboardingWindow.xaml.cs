using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Aire.Data;
using Aire.Providers;
using Aire.Services;
using Aire.UI.Controls;
using ProviderChatMessage = Aire.Providers.ChatMessage;
// Explicit WPF aliases to avoid ambiguity with System.Windows.Forms / System.Drawing
using WpfBrush      = System.Windows.Media.Brush;
using WpfColor      = System.Windows.Media.Color;
using WpfSolidBrush = System.Windows.Media.SolidColorBrush;
using WpfFontFamily = System.Windows.Media.FontFamily;
using WpfButton     = System.Windows.Controls.Button;
using WpfCursors    = System.Windows.Input.Cursors;
using WpfOrientation = System.Windows.Controls.Orientation;
using WpfComboBoxItem = System.Windows.Controls.ComboBoxItem;
using WpfColumnDefinition = System.Windows.Controls.ColumnDefinition;
using WpfStackPanel  = System.Windows.Controls.StackPanel;
using WpfTextBlock   = System.Windows.Controls.TextBlock;
using WpfFlowDir     = System.Windows.FlowDirection;
using WpfHorizontalAlignment = System.Windows.HorizontalAlignment;
using WpfGrid        = System.Windows.Controls.Grid;
using WpfPoint       = System.Windows.Point;
using WpfRowDefinition = System.Windows.Controls.RowDefinition;
using WpfUIElement   = System.Windows.UIElement;
using WpfVerticalAlignment = System.Windows.VerticalAlignment;
using WpfBrushes     = System.Windows.Media.Brushes;
using WpfColorConverter = System.Windows.Media.ColorConverter;
using WpfEllipse     = System.Windows.Shapes.Ellipse;
using WpfPath        = System.Windows.Shapes.Path;
using WpfPolygon     = System.Windows.Shapes.Polygon;
using WpfRectangle   = System.Windows.Shapes.Rectangle;
using WpfImage       = System.Windows.Controls.Image;
using WpfBitmapImage = System.Windows.Media.Imaging.BitmapImage;

namespace Aire.UI
{
    public partial class OnboardingWindow : Window
    {
        public Action? OpenSettingsAction { get; set; }

        private int _step = 1;
        private CancellationTokenSource? _testCts;
        private CancellationTokenSource? _modelFetchCts;
        internal bool _claudeSessionActive;

        // ── Language data ─────────────────────────────────────────────────────
        // Languages and translations are now loaded dynamically from
        // Translations/*.json files via LocalizationService.
        // To add a new language: create a new JSON file in Translations/.
        // No C# changes needed.

        // ── Provider display names and docs are sourced from ProviderCatalog ──

        internal static string ProviderDisplayName(string type) => ProviderCatalog.GetDisplayName(type);

        // ── Model combo fields ────────────────────────────────────────────────

        private CollectionViewSource   _standardModelViewSource = new();
        private bool                   _suppressStandardModelFilter;
        private ModelDefinition?       _preStandardModelFilterSelection;
        private static readonly string[][] ProviderRows =
        [
            ["OpenAI", "Codex", "Ollama"],
            ["Anthropic", "ClaudeWeb", "OpenRouter"],
            ["DeepSeek", "Zai", "Inception"],
            ["GoogleAI", "GoogleAIImage", "Groq"]
        ];

        private sealed record ProviderCardDefinition(
            string Tag,
            string Title,
            string Subtitle,
            string CircleBrush,
            Func<WpfUIElement> CreateLogo);

        // ── Init ──────────────────────────────────────────────────────────────

        public OnboardingWindow()
        {
            InitializeComponent();
            ModelCatalog.EnsureDefaults();
            FontSize = AppearanceService.FontSize;
            AppearanceService.AppearanceChanged += ApplyThemeFontSize;

            ModelCombo.IsTextSearchEnabled = false;
            ModelCombo.AddHandler(
                System.Windows.Controls.Primitives.TextBoxBase.TextChangedEvent,
                new System.Windows.Controls.TextChangedEventHandler(OnStandardModelComboTextChanged));
            ModelCombo.DropDownOpened += (_, _) =>
            {
                EditableComboBoxFilterHelper.FocusEditableTextBox(ModelCombo);
            };
            ModelCombo.PreviewTextInput += (_, e) => EditableComboBoxFilterHelper.HandlePreviewTextInput(ModelCombo, e);
            ModelCombo.PreviewKeyDown += (_, e) => EditableComboBoxFilterHelper.HandlePreviewKeyDown(ModelCombo, e);
            ModelCombo.DropDownClosed += StandardModelCombo_DropDownClosed;

            Loaded += (_, _) =>
            {
                BuildLanguageButtons();
                DetectAndSelectLanguage();
                BuildProviderCards();
                ShuffleProviderRows();
                ProviderTypeCombo.SelectedIndex = 0;
            };
        }

        private void BuildProviderCards()
        {
            if (ProviderCardGrid is null)
            {
                return;
            }

            ProviderCardGrid.Children.Clear();

            Dictionary<string, ProviderCardDefinition> cards = GetProviderCardDefinitions();

            foreach (string tag in ProviderRows.SelectMany(row => row))
            {
                if (cards.TryGetValue(tag, out ProviderCardDefinition? card))
                {
                    ProviderCardGrid.Children.Add(CreateProviderButton(card));
                }
            }
        }

        private void ShuffleProviderRows()
        {
            if (ProviderCardGrid is null || ProviderCardGrid.Children.Count == 0)
            {
                return;
            }

            var cardsByTag = ProviderCardGrid.Children
                .OfType<WpfButton>()
                .Where(button => button.Tag is string)
                .ToDictionary(button => (string)button.Tag, button => (WpfUIElement)button, StringComparer.Ordinal);

            if (ProviderRows.Any(row => row.Any(tag => !cardsByTag.ContainsKey(tag))))
            {
                return;
            }

            var shuffledRows = ProviderRows
                .OrderBy(_ => Random.Shared.Next())
                .ToArray();

            ProviderCardGrid.Children.Clear();

            foreach (string[] row in shuffledRows)
            {
                foreach (string tag in row)
                {
                    ProviderCardGrid.Children.Add(cardsByTag[tag]);
                }
            }
        }

        private static Dictionary<string, ProviderCardDefinition> GetProviderCardDefinitions()
        {
            return new(StringComparer.Ordinal)
            {
                ["OpenAI"] = new("OpenAI", "OpenAI", "GPT-4o, o3 & more", "#10A37F", CreateOpenAiLogo),
                ["Codex"] = new("Codex", "Codex", "Local CLI bridge", "#0F172A", CreateCodexLogo),
                ["Ollama"] = new("Ollama", "Ollama", "Local, free, private", "#5A5A5A", CreateOllamaLogo),
                ["Anthropic"] = new("Anthropic", "Anthropic API", "Claude Sonnet, Haiku", "#D97757", CreateAnthropicLogo),
                ["ClaudeWeb"] = new("ClaudeWeb", "Claude.ai", "Browser sign-in", "#111111", CreateClaudeLogo),
                ["OpenRouter"] = new("OpenRouter", "OpenRouter", "100+ models, free tier", "#FFFFFF", CreateOpenRouterLogo),
                ["DeepSeek"] = new("DeepSeek", "DeepSeek", "Chat, Reasoner, V3", "#FFFFFF", CreateDeepSeekLogo),
                ["Zai"] = new("Zai", "Zhipu AI", "GLM models · z.ai", "#000000", CreateZaiLogo),
                ["Inception"] = new("Inception", "Inception", "Mercury and Inception", "#001A18", CreateInceptionLogo),
                ["GoogleAI"] = new("GoogleAI", "Google AI", "Gemini Pro & Flash", "#FFFFFF", CreateGoogleAiLogo),
                ["GoogleAIImage"] = new("GoogleAIImage", "Google AI Images", "Gemini image generation", "#FFFFFF", CreateGoogleAiImageLogo),
                ["Groq"] = new("Groq", "Groq", "Free tier, very fast", "#F55036", CreateGroqLogo)
            };
        }

        private WpfButton CreateProviderButton(ProviderCardDefinition card)
        {
            WpfBrush textBrush = (WpfBrush)FindResource("TextBrush");
            WpfBrush secondaryBrush = (WpfBrush)FindResource("TextSecondaryBrush");

            var button = new WpfButton
            {
                Tag = card.Tag,
                Margin = new Thickness(4),
                Height = 122,
                Padding = new Thickness(8, 8, 8, 10),
                VerticalContentAlignment = WpfVerticalAlignment.Stretch,
                HorizontalContentAlignment = WpfHorizontalAlignment.Stretch
            };
            button.Click += PickProvider_Click;

            var layout = new WpfGrid();
            layout.RowDefinitions.Add(new WpfRowDefinition { Height = new GridLength(42) });
            layout.RowDefinitions.Add(new WpfRowDefinition { Height = new GridLength(30) });
            layout.RowDefinitions.Add(new WpfRowDefinition { Height = new GridLength(34) });

            var iconBorder = new Border
            {
                Width = 36,
                Height = 36,
                CornerRadius = new CornerRadius(18),
                Background = (WpfBrush)new BrushConverter().ConvertFromString(card.CircleBrush)!,
                HorizontalAlignment = WpfHorizontalAlignment.Center,
                VerticalAlignment = WpfVerticalAlignment.Top,
                Child = new Viewbox
                {
                    Width = 22,
                    Height = 22,
                    Stretch = Stretch.Uniform,
                    Child = card.CreateLogo()
                }
            };
            Grid.SetRow(iconBorder, 0);

            double titleFontSize = card.Tag == "GoogleAIImage" ? 12 : 13;
            double subtitleFontSize = card.Tag is "GoogleAIImage" or "Inception" or "Zai" ? 11 : 12;
            string titleText = card.Tag == "GoogleAIImage" ? "Google AI\nImages" : card.Title;

            var title = new WpfTextBlock
            {
                Text = titleText,
                FontSize = titleFontSize,
                FontWeight = FontWeights.SemiBold,
                Foreground = textBrush,
                HorizontalAlignment = WpfHorizontalAlignment.Center,
                VerticalAlignment = WpfVerticalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                LineHeight = titleFontSize + 2
            };
            Grid.SetRow(title, 1);

            var subtitle = new WpfTextBlock
            {
                Text = card.Subtitle,
                FontSize = subtitleFontSize,
                Foreground = secondaryBrush,
                HorizontalAlignment = WpfHorizontalAlignment.Center,
                VerticalAlignment = WpfVerticalAlignment.Top,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(4, 2, 4, 0),
                LineHeight = subtitleFontSize + 2
            };
            Grid.SetRow(subtitle, 2);

            layout.Children.Add(iconBorder);
            layout.Children.Add(title);
            layout.Children.Add(subtitle);
            button.Content = layout;
            return button;
        }

        private static WpfUIElement CreateOpenAiLogo()
            => CreateTextLogo("◎", 20, FontWeights.Regular);

        private static WpfUIElement CreateCodexLogo()
        {
            return new WpfPath
            {
                Width = 20,
                Height = 20,
                Stretch = Stretch.Uniform,
                Stroke = WpfBrushes.White,
                StrokeThickness = 1.9,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                StrokeLineJoin = PenLineJoin.Round,
                Fill = WpfBrushes.Transparent,
                Data = Geometry.Parse("M10.0,2.8 C11.5,2.0 13.4,2.2 14.6,3.5 C16.2,3.3 17.7,4.0 18.5,5.3 C19.8,6.0 20.6,7.4 20.4,8.8 C21.2,10.2 21.1,12.0 20.0,13.2 C20.1,14.9 19.0,16.5 17.4,17.0 C16.5,18.4 14.8,19.1 13.2,18.8 C11.9,19.8 10.0,19.9 8.6,19.0 C7.1,19.7 5.3,19.4 4.1,18.2 C2.5,18.0 1.2,16.8 0.8,15.2 C-0.1,13.9 -0.2,12.1 0.7,10.7 C0.3,9.1 0.9,7.4 2.2,6.4 C2.4,4.8 3.6,3.5 5.2,3.0 C6.4,1.9 8.3,1.7 10.0,2.8 Z M6.0,8.0 C4.6,9.1 4.3,11.1 5.2,12.6 C5.0,14.0 5.8,15.4 7.1,15.9 C8.1,17.0 9.8,17.2 11.1,16.4 C12.4,16.9 13.9,16.4 14.7,15.2 C16.1,14.9 17.0,13.5 16.9,12.1 C17.9,11.0 17.9,9.3 16.9,8.2 C16.9,6.7 15.9,5.4 14.4,5.1 C13.5,4.0 11.9,3.6 10.6,4.3 C9.2,3.7 7.6,4.2 6.8,5.5 C5.4,5.9 4.6,7.0 4.6,8.4")
            };
        }

        private static WpfUIElement CreateOllamaLogo()
            => CreateTextLogo("◖◗", 15, FontWeights.Bold);

        private static WpfUIElement CreateAnthropicLogo()
            => CreateTextLogo("✺", 17, FontWeights.Regular);

        private static WpfUIElement CreateClaudeLogo()
            => CreateTextLogo("✳", 16, FontWeights.Regular);

        private static WpfUIElement CreateOpenRouterLogo()
            => CreateBundledImageLogo("openrouter-favicon.png");

        private static WpfUIElement CreateDeepSeekLogo()
            => CreateBundledImageLogo("deepseek-favicon.png");

        private static WpfUIElement CreateZaiLogo()
        {
            var badge = new Border
            {
                Width = 18,
                Height = 18,
                CornerRadius = new CornerRadius(4),
                Background = WpfBrushes.Black,
                BorderBrush = WpfBrushes.White,
                BorderThickness = new Thickness(0.4)
            };

            var canvas = new Canvas
            {
                Width = 18,
                Height = 18
            };

            canvas.Children.Add(new WpfPath
            {
                Data = Geometry.Parse("M4.0,4.6 L10.0,4.6 L8.6,6.6 L4.0,6.6 Z"),
                Fill = WpfBrushes.White
            });

            canvas.Children.Add(new WpfPath
            {
                Data = Geometry.Parse("M9.2,6.0 L13.9,6.0 L8.8,13.4 L4.1,13.4 Z"),
                Fill = WpfBrushes.White
            });

            canvas.Children.Add(new WpfPath
            {
                Data = Geometry.Parse("M8.0,11.4 L14.0,11.4 L14.0,13.4 L6.6,13.4 Z"),
                Fill = WpfBrushes.White
            });

            badge.Child = canvas;
            return badge;
        }

        private static WpfUIElement CreateInceptionLogo()
            => CreateBundledImageLogo("inception-favicon-alpha.png");

        private static WpfUIElement CreateGroqLogo()
            => CreateTextLogo("⚡", 17, FontWeights.SemiBold);

        private static WpfUIElement CreateGoogleAiLogo()
        {
            var grid = new WpfGrid { Width = 20, Height = 20 };
            grid.ColumnDefinitions.Add(new WpfColumnDefinition());
            grid.RowDefinitions.Add(new WpfRowDefinition());

            grid.Children.Add(new WpfEllipse
            {
                Width = 18,
                Height = 18,
                StrokeThickness = 2.6,
                Stroke = new SolidColorBrush((WpfColor)WpfColorConverter.ConvertFromString("#34A853")!),
                Margin = new Thickness(1)
            });

            grid.Children.Add(new WpfRectangle
            {
                Width = 9,
                Height = 8,
                Fill = WpfBrushes.White,
                HorizontalAlignment = WpfHorizontalAlignment.Right,
                VerticalAlignment = WpfVerticalAlignment.Center,
                Margin = new Thickness(0, 0, 1, 0)
            });

            grid.Children.Add(new WpfRectangle
            {
                Width = 8,
                Height = 2.6,
                Fill = new SolidColorBrush((WpfColor)WpfColorConverter.ConvertFromString("#4285F4")!),
                HorizontalAlignment = WpfHorizontalAlignment.Right,
                VerticalAlignment = WpfVerticalAlignment.Center,
                Margin = new Thickness(0, 0, 1, 0)
            });

            grid.Children.Add(new WpfEllipse
            {
                Width = 18,
                Height = 18,
                StrokeThickness = 2.6,
                Stroke = new SolidColorBrush((WpfColor)WpfColorConverter.ConvertFromString("#EA4335")!),
                Margin = new Thickness(1),
                StrokeDashArray = new DoubleCollection([11, 100]),
                StrokeDashOffset = 0.8
            });

            grid.Children.Add(new WpfEllipse
            {
                Width = 18,
                Height = 18,
                StrokeThickness = 2.6,
                Stroke = new SolidColorBrush((WpfColor)WpfColorConverter.ConvertFromString("#FBBC05")!),
                Margin = new Thickness(1),
                StrokeDashArray = new DoubleCollection([8, 100]),
                StrokeDashOffset = 10.4
            });

            grid.Children.Add(new WpfEllipse
            {
                Width = 18,
                Height = 18,
                StrokeThickness = 2.6,
                Stroke = new SolidColorBrush((WpfColor)WpfColorConverter.ConvertFromString("#4285F4")!),
                Margin = new Thickness(1),
                StrokeDashArray = new DoubleCollection([10, 100]),
                StrokeDashOffset = 18.8
            });

            return grid;
        }

        private static WpfUIElement CreateGoogleAiImageLogo()
        {
            var grid = new WpfGrid { Width = 20, Height = 20 };
            grid.Children.Add(new WpfRectangle
            {
                Width = 18,
                Height = 14,
                RadiusX = 2,
                RadiusY = 2,
                Stroke = new SolidColorBrush((WpfColor)WpfColorConverter.ConvertFromString("#4285F4")!),
                StrokeThickness = 1.8,
                Fill = WpfBrushes.Transparent,
                HorizontalAlignment = WpfHorizontalAlignment.Center,
                VerticalAlignment = WpfVerticalAlignment.Center,
                Margin = new Thickness(1, 3, 1, 1)
            });
            grid.Children.Add(new WpfPolygon
            {
                Points = new PointCollection([new WpfPoint(4, 15), new WpfPoint(9, 9), new WpfPoint(12, 12), new WpfPoint(16, 7), new WpfPoint(16, 15)]),
                Fill = new SolidColorBrush((WpfColor)WpfColorConverter.ConvertFromString("#34A853")!)
            });
            grid.Children.Add(new WpfEllipse
            {
                Width = 3.5,
                Height = 3.5,
                Fill = new SolidColorBrush((WpfColor)WpfColorConverter.ConvertFromString("#FBBC05")!),
                HorizontalAlignment = WpfHorizontalAlignment.Left,
                VerticalAlignment = WpfVerticalAlignment.Top,
                Margin = new Thickness(4, 5, 0, 0)
            });
            return grid;
        }

        private static WpfUIElement CreateTextLogo(string text, double fontSize, FontWeight weight, WpfFontFamily? fontFamily = null)
        {
            return new WpfTextBlock
            {
                Text = text,
                FontSize = fontSize,
                FontWeight = weight,
                FontFamily = fontFamily ?? new WpfFontFamily("Segoe UI"),
                Foreground = WpfBrushes.White,
                HorizontalAlignment = WpfHorizontalAlignment.Center,
                VerticalAlignment = WpfVerticalAlignment.Center,
                TextAlignment = TextAlignment.Center
            };
        }

        private static WpfUIElement CreateBundledImageLogo(string fileName)
        {
            string imagePath = System.IO.Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Assets",
                "Providers",
                fileName);

            var bitmap = new WpfBitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(imagePath, UriKind.Absolute);
            bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();

            return new WpfImage
            {
                Source = bitmap,
                Width = 20,
                Height = 20,
                Stretch = Stretch.Uniform
            };
        }

    }
}

