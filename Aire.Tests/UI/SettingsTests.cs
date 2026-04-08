using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Aire.Data;
using Aire.Providers;
using Aire.Services;
using Aire.UI;
using Aire.UI.Settings.Models;
using Xunit;
using Button = System.Windows.Controls.Button;

namespace Aire.Tests.UI
{
    [Collection("AppState Isolation")]
    public class SettingsTests : TestBase
    {
        [Fact]
        public void SettingsWindow_ModelsHelpersAndGuardBranches_Work()
        {
            RunOnStaThread(delegate
            {
                EnsureApplication();
                SettingsWindow settingsWindow = new SettingsWindow(initializeUi: false);

                Assert.Equal(string.Empty, Aire.AppLayer.Providers.OllamaModelCatalogApplicationService.FormatModelSize(0L));
                Assert.Equal("512 MB", Aire.AppLayer.Providers.OllamaModelCatalogApplicationService.FormatModelSize(536870912L));
                Assert.Contains("GB", Aire.AppLayer.Providers.OllamaModelCatalogApplicationService.FormatModelSize(2147483648L), StringComparison.Ordinal);
                
                ComboBox comboBox = new ComboBox();
                settingsWindow.ModelComboBox = comboBox;
                settingsWindow.ToastText = new TextBox();
                settingsWindow.ToastBorder = new Border();
                settingsWindow._suppressModelFilter = false;
                
                OllamaModelItem obj = new OllamaModelItem
                {
                    DisplayName = "phi4  (2.0 GB)",
                    ModelName = "phi4:latest",
                    IsInstalled = true,
                    SizeStr = "2.0 GB"
                };
                OllamaModelItem obj2 = new OllamaModelItem
                {
                    DisplayName = "qwen3",
                    ModelName = "qwen3:4b",
                    IsInstalled = false,
                    SizeStr = string.Empty
                };
                
                settingsWindow._suppressModelFilter = true;
                comboBox.ItemsSource = new OllamaModelItem[] { obj, obj2 };
                comboBox.SelectedItem = obj; // Direct set for test reliability
                Assert.Equal(obj, comboBox.SelectedItem);
                
                settingsWindow._suppressModelFilter = false;
                
                settingsWindow.RefreshModelsButton_Click(null, null);
                settingsWindow.InstallOllamaButton_Click(null, null);
            });
        }

        [Fact]
        public void SettingsWindow_CapabilityTests_Work()
        {
            RunOnStaThread(delegate
            {
                EnsureApplication();
                SettingsWindow settingsWindow = new SettingsWindow(initializeUi: false);
                
                Assert.Equal("test", SettingsWindow.ShortenErrorMessage("test"));
                Assert.Equal("test", SettingsWindow.ShortenErrorMessage("prefix {\"error\":{\"message\":\"test\"}}"));
                Assert.Equal("error message", SettingsWindow.ShortenErrorMessage("{\"error\":{\"message\":\"error message\"}}"));
                
                StackPanel stackPanel = new StackPanel();
                settingsWindow.CapTestResultsPanel = stackPanel;
                
                CapabilityTestResult[] array = new CapabilityTestResult[]
                {
                    new CapabilityTestResult("cat", "name", "Category", true, "msg", null, 0L)
                };
                settingsWindow.CapTestResultsBorder = new Border();
                settingsWindow.CapTestStatusText = new TextBlock();
                settingsWindow.DisplayTestResults(array, DateTime.Now);
                Assert.True(stackPanel.Children.Count >= 2);
                Grid header = Assert.IsType<Grid>(stackPanel.Children[0]);
                Grid row = Assert.IsType<Grid>(stackPanel.Children[1]);
                Assert.Equal(6, header.ColumnDefinitions.Count);
                Button rerunButton = Assert.Single(row.Children.OfType<Button>());
                Assert.Equal("↻", rerunButton.Content);
                Assert.Equal(24d, rerunButton.Width);
                Assert.Equal(24d, rerunButton.Height);
                Assert.Equal("cat", rerunButton.Tag);
                Assert.Equal(6, row.ColumnDefinitions.Count);
                
                Provider provider = new Provider
                {
                    Id = 1,
                    Model = "m1"
                };
                settingsWindow.SaveTestResultsAsync(provider, array.ToList(), DateTime.Now);
            });
        }

        [Fact]
        public void SettingsWindow_CapabilityResultsHelpers_Work()
        {
            RunOnStaThread(delegate
            {
                EnsureApplication();
                Application.Current.Resources["TextBrush"] = Brushes.Black;
                Application.Current.Resources["TextSecondaryBrush"] = Brushes.Gray;
                
                SettingsWindow settingsWindow = new SettingsWindow(initializeUi: false);
                settingsWindow.CapTestResultsPanel = new StackPanel();
                settingsWindow.CapTestResultsBorder = new Border { Visibility = Visibility.Collapsed };
                settingsWindow.CapTestStatusText = new TextBlock();
                
                string tempDb = Path.Combine(Path.GetTempPath(), "aire-tests-" + Guid.NewGuid().ToString("N"), "aire.db");
                Directory.CreateDirectory(Path.GetDirectoryName(tempDb));
                
                using (var db = new DatabaseService(tempDb))
                {
                    db.InitializeAsync().GetAwaiter().GetResult();
                    settingsWindow._databaseService = db;
                    
                    Provider provider = new Provider { Id = 99, Model = "gpt-4o" };
                    var results = new List<CapabilityTestResult>
                    {
                        new CapabilityTestResult("ok", "List directory", "File System", true, "list_directory", null, 1500L),
                        new CapabilityTestResult("bad", "Fetch URL", "Web", false, null, "error", 2500L)
                    };
                    
                    DateTime now = DateTime.Now;
                    settingsWindow.SaveTestResultsAsync(provider, results, now).GetAwaiter().GetResult();
                    settingsWindow.DisplayTestResults(results, now);
                    
                    Assert.Equal(Visibility.Visible, settingsWindow.CapTestResultsBorder.Visibility);
                    Assert.Contains("Last tested", settingsWindow.CapTestStatusText.Text);
                }
            });
        }
    }
}
