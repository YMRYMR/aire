using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace Aire.UI
{
    public partial class SettingsWindow
    {
        private async void AutoSave(object sender, RoutedEventArgs e) => await PerformAutoSave();

        private async Task PerformAutoSave()
        {
            try
            {
                await ProviderWorkflow.PerformAutoSaveAsync();
            }
            catch (Exception ex)
            {
                ShowToast($"Could not save provider: {ex.Message}", isError: true);
            }
        }

        private async void ClaudeAiLoginButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedProvider == null) return;

            var loginWindow = new ClaudeAiLoginWindow();
            if (loginWindow.ShowDialog() == true)
            {
                _selectedProvider.Type = "ClaudeWeb";
                foreach (ComboBoxItem item in TypeComboBox.Items)
                {
                    if (item.Tag?.ToString() == "ClaudeWeb")
                    {
                        TypeComboBox.SelectedItem = item;
                        break;
                    }
                }
                _selectedProvider.ApiKey = "claude.ai-session";
                await PerformAutoSave();
            }
        }
    }
}
