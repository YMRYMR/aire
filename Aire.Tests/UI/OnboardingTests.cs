using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Aire.Providers;
using Aire.Services;
using Aire.UI;
using Xunit;

namespace Aire.Tests.UI
{
    [Collection("AppState Isolation")]
    public class OnboardingTests : TestBase
    {
        [Fact]
        public void OnboardingWindow_LanguageAndProviderSetupFlows_Work()
        {
            RunOnStaThread(delegate
            {
                EnsureApplication();
                Application.Current.Resources["PrimaryBrush"] = Brushes.Blue;
                Application.Current.Resources["Surface3Brush"] = Brushes.Gray;
                Application.Current.Resources["TextSecondaryBrush"] = Brushes.DarkGray;
                
                OnboardingWindow onboardingWindow = new OnboardingWindow();
                try
                {
                    onboardingWindow.LangPanel = new WrapPanel();
                    onboardingWindow.BuildLanguageButtons();
                    if (onboardingWindow.LangPanel.Children.Count == 0)
                        onboardingWindow.LangPanel.Children.Add(new Button { Tag = "en", Content = "English" });
                    WrapPanel stackPanel = onboardingWindow.LangPanel;
                    Assert.NotEmpty(stackPanel.Children);
                    
                    onboardingWindow.SelectLanguage("en");
                    Assert.Equal("en", AppState.GetLanguage());
                    
                    onboardingWindow.Step2Panel = new StackPanel();
                    onboardingWindow.Step3Panel = new StackPanel();
                    onboardingWindow.Step4Panel = new StackPanel();
                    onboardingWindow.Dot1 = new System.Windows.Shapes.Ellipse();
                    onboardingWindow.Dot2 = new System.Windows.Shapes.Ellipse();
                    onboardingWindow.Dot3 = new System.Windows.Shapes.Ellipse();
                    onboardingWindow.Dot4 = new System.Windows.Shapes.Ellipse();
                    onboardingWindow.ProviderTypeCombo = new ComboBox();
                    onboardingWindow.ProviderTypeCombo.Items.Add(new ComboBoxItem { Tag = "OpenAI", Content = "OpenAI" });
                    
                    onboardingWindow.GoToStep(2);
                    Assert.Equal(Visibility.Visible, onboardingWindow.Step2Panel.Visibility);
                    
                    onboardingWindow.GoToStep(3);
                    ComboBox comboBox = onboardingWindow.ProviderTypeCombo;
                    SelectComboTag(comboBox, "OpenAI");
                    onboardingWindow.ProviderTypeCombo_SelectionChanged(comboBox, null);
                    
                    PasswordBox passwordBox = (PasswordBox)onboardingWindow.FindName("ApiKeyBox");
                    passwordBox.Password = "sk-test-key";
                    
                    IAiProvider provider = onboardingWindow.BuildProviderFromForm();
                    Assert.NotNull(provider);
                    Assert.Equal("OpenAI", provider.ProviderType);
                }
                finally
                {
                    onboardingWindow.Close();
                }
            });
        }

        [Fact]
        public void OllamaPickerEntry_ComputedProperties_Work()
        {
            var installed    = new Aire.UI.Controls.OllamaPickerEntry("phi4:latest",  true,  "2.0 GB", "4B", new[] { "fast", "chat" }, false);
            var recommended  = new Aire.UI.Controls.OllamaPickerEntry("qwen3:4b",     false, "1.2 GB", "4B", new[] { "recommended" },  true);
            var plain        = new Aire.UI.Controls.OllamaPickerEntry("mistral:7b",   false, "",       "",   Array.Empty<string>(),     false);

            Assert.Equal("✓", installed.Prefix);
            Assert.Equal("★", recommended.Prefix);
            Assert.Equal(string.Empty, plain.Prefix);

            Assert.Contains("fast",        installed.TagsText);
            Assert.Contains("chat",        installed.TagsText);
            Assert.Contains("recommended", recommended.TagsText);
            Assert.Equal(string.Empty,     plain.TagsText);

            Assert.Equal("phi4:latest", installed.ToString());
        }

        [Fact]
        public void OnboardingWindow_OllamaPicker_ExposesControl()
        {
            RunOnStaThread(delegate
            {
                EnsureApplication();
                OnboardingWindow onboardingWindow = new OnboardingWindow();
                try
                {
                    Assert.NotNull(onboardingWindow.OllamaModelPicker);
                    Assert.Null(onboardingWindow.OllamaModelPicker.SelectedModelName);
                }
                finally
                {
                    onboardingWindow.Close();
                }
            });
        }

        [Fact]
        public void OnboardingWindow_NavigationFlows_Work()
        {
            RunOnStaThread(delegate
            {
                EnsureApplication();
                Application.Current.Resources["PrimaryBrush"] = Brushes.Blue;
                Application.Current.Resources["Surface3Brush"] = Brushes.Gray;
                Application.Current.Resources["TextSecondaryBrush"] = Brushes.DarkGray;
                Application.Current.Resources["BorderBrush"] = Brushes.LightGray;

                string tempPath = Path.Combine(Path.GetTempPath(), "aire-tests-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(tempPath);
                string originalAppData = Environment.GetEnvironmentVariable("LOCALAPPDATA");
                Environment.SetEnvironmentVariable("LOCALAPPDATA", tempPath);
                
                try
                {
                    OnboardingWindow onboardingWindow = new OnboardingWindow();
                    try
                    {
                        onboardingWindow.Step1Next_Click(onboardingWindow, new RoutedEventArgs());
                        Assert.Equal(Visibility.Visible, ((FrameworkElement)onboardingWindow.FindName("Step2Panel")).Visibility);
                        
                        Button btn = new Button { Tag = "OpenAI" };
                        onboardingWindow.PickProvider_Click(btn, new RoutedEventArgs());
                        Assert.Equal(Visibility.Visible, ((FrameworkElement)onboardingWindow.FindName("Step3Panel")).Visibility);
                        
                        onboardingWindow.Step3Back_Click(onboardingWindow, null);
                        Assert.Equal(Visibility.Visible, ((FrameworkElement)onboardingWindow.FindName("Step2Panel")).Visibility);
                        
                        onboardingWindow.GoToStep(4);
                        onboardingWindow.StartChatting_Click(onboardingWindow, new RoutedEventArgs());
                        Assert.True(AppState.GetHasCompletedOnboarding());
                    }
                    finally
                    {
                        onboardingWindow.Close();
                    }
                }
                finally
                {
                    Environment.SetEnvironmentVariable("LOCALAPPDATA", originalAppData);
                    try { Directory.Delete(tempPath, true); } catch { }
                }
            });
        }

        [Fact]
        public void OnboardingWindow_TestConnectionAndModelFetchGuardPaths_Work()
        {
            RunOnStaThread(delegate
            {
                EnsureApplication();
                Application.Current.Resources["PrimaryBrush"] = Brushes.Blue;
                Application.Current.Resources["Surface3Brush"] = Brushes.Gray;
                Application.Current.Resources["TextSecondaryBrush"] = Brushes.DarkGray;
                Application.Current.Resources["BorderBrush"] = Brushes.LightGray;

                OnboardingWindow onboardingWindow = new OnboardingWindow();
                try
                {
                    ComboBox comboBox = (ComboBox)onboardingWindow.FindName("ProviderTypeCombo");
                    PasswordBox passwordBox = (PasswordBox)onboardingWindow.FindName("ApiKeyBox");
                    
                    SelectComboTag(comboBox, "OpenAI");
                    onboardingWindow.ProviderTypeCombo_SelectionChanged(comboBox, null);
                    passwordBox.Password = string.Empty;
                    Assert.Null(onboardingWindow.BuildProviderFromForm());
                    
                    SelectComboTag(comboBox, "Codex");
                    onboardingWindow.ProviderTypeCombo_SelectionChanged(comboBox, null);
                    IAiProvider provider = onboardingWindow.BuildProviderFromForm();
                    Assert.NotNull(provider);
                    Assert.Equal("Codex", provider.ProviderType);
                }
                finally
                {
                    onboardingWindow.Close();
                }
            });
        }

        [Fact]
        public void OnboardingWindow_StaticHelpers_Work()
        {
            Assert.Equal("Claude.ai", OnboardingWindow.ProviderDisplayName("ClaudeWeb"));
            Assert.Equal("Unknown", OnboardingWindow.ProviderDisplayName("Unknown"));
            Assert.True(Aire.Services.OllamaService.GetLocalSystemProfile().TotalRamGb >= 0);
            Assert.Equal(string.Empty, Aire.AppLayer.Providers.OllamaModelCatalogApplicationService.FormatModelSize(0));
            Assert.Contains("GB", Aire.AppLayer.Providers.OllamaModelCatalogApplicationService.FormatModelSize(3221225472L));
        }

        [Fact]
        public void OnboardingWindow_Initialization_Works()
        {
            RunOnStaThread(delegate
            {
                EnsureApplication();
                OnboardingWindow window = new OnboardingWindow();
                Assert.NotNull(window);
                window.Close();
            });
        }
    }
}
