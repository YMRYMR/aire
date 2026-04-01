using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
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
using WpfStackPanel  = System.Windows.Controls.StackPanel;
using WpfTextBlock   = System.Windows.Controls.TextBlock;
using WpfFlowDir     = System.Windows.FlowDirection;

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

        // ── API key doc links ─────────────────────────────────────────────────

        private static readonly Dictionary<string, string> ApiKeyUrls = new()
        {
            ["OpenAI"]     = "https://platform.openai.com/api-keys",
            ["Groq"]       = "https://console.groq.com/keys",
            ["OpenRouter"] = "https://openrouter.ai/keys",
            ["Codex"]      = "",
            ["Anthropic"]  = "https://console.anthropic.com/settings/keys",
            ["ClaudeWeb"]  = "",
            ["GoogleAI"]   = "https://aistudio.google.com/app/apikey",
            ["DeepSeek"]   = "https://platform.deepseek.com/api_keys",
            ["Inception"]  = "https://platform.inceptionlabs.ai/",
            ["Ollama"]     = "",
            ["Zai"]        = "https://www.bigmodel.cn/usercenter/apikeys",
        };

        // ── Sign-up URLs ──────────────────────────────────────────────────────

        private static readonly Dictionary<string, string> SignUpUrls = new()
        {
            ["OpenAI"]     = "https://platform.openai.com/signup",
            ["Groq"]       = "https://console.groq.com/",
            ["OpenRouter"] = "https://openrouter.ai/",
            ["Codex"]      = "https://openai.com/codex/",
            ["Anthropic"]  = "https://console.anthropic.com/",
            ["ClaudeWeb"]  = "https://claude.ai/",
            ["GoogleAI"]   = "https://aistudio.google.com/",
            ["DeepSeek"]   = "https://platform.deepseek.com/",
            ["Inception"]  = "https://platform.inceptionlabs.ai/",
            ["Ollama"]     = "https://ollama.com/",
            ["Zai"]        = "https://www.bigmodel.cn/",
        };

        // ── Provider display names ─────────────────────────────────────────────

        internal static string ProviderDisplayName(string type) => type switch
        {
            "OpenAI"     => "OpenAI",
            "Groq"       => "Groq",
            "OpenRouter" => "OpenRouter",
            "Codex"      => "Codex",
            "Anthropic"  => "Anthropic API",
            "ClaudeWeb"  => "Claude.ai",
            "DeepSeek"   => "DeepSeek",
            "GoogleAI"   => "Google AI",
            "Inception"  => "Inception",
            "Ollama"     => "Ollama",
            "Zai"        => "Zhipu AI (z.ai)",
            _            => type
        };

        // ── Model combo fields ────────────────────────────────────────────────

        private CollectionViewSource   _standardModelViewSource = new();
        private bool                   _suppressStandardModelFilter;
        private ModelDefinition?       _preStandardModelFilterSelection;

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
                ProviderTypeCombo.SelectedIndex = 0;
            };
        }

    }
}

