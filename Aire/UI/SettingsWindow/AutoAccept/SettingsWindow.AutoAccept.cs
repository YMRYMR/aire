using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Aire.AppLayer.Tools;
using Aire.UI.Settings.Models;
using Microsoft.VisualBasic;

namespace Aire.UI
{
    public partial class SettingsWindow
    {
        internal async Task LoadAutoAcceptSettings()
        {
            _suppressAutoAccept = true;
            try
            {
                var settings = await _autoAcceptProfilesApplicationService.LoadActiveConfigurationAsync();
                ApplyAutoAcceptConfiguration(settings);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to load auto‑accept settings: {ex}");
            }
            finally
            {
                _suppressAutoAccept = false;
            }
        }

        internal async Task LoadAutoAcceptProfilesAsync()
        {
            _suppressAutoAcceptProfileSelection = true;
            try
            {
                var profiles = await _autoAcceptProfilesApplicationService.LoadProfilesAsync();
                AutoAcceptProfileComboBox.ItemsSource = profiles;

                var selectedName = await _autoAcceptProfilesApplicationService.LoadSelectedProfileNameAsync();
                AutoAcceptProfileComboBox.SelectedItem = profiles.FirstOrDefault(p =>
                        string.Equals(p.Name, selectedName, StringComparison.OrdinalIgnoreCase))
                    ?? profiles.FirstOrDefault();

                UpdateAutoAcceptProfileButtons();
            }
            finally
            {
                _suppressAutoAcceptProfileSelection = false;
            }
        }

        private void HookAutoAcceptEvents()
        {
            if (AutoAcceptToolsPanel == null)
                return;

            foreach (var cb in FindDescendants<System.Windows.Controls.CheckBox>(AutoAcceptToolsPanel))
            {
                cb.Checked += AutoAcceptCheckBox_Changed;
                cb.Unchecked += AutoAcceptCheckBox_Changed;
            }
        }

        private static IEnumerable<T> FindDescendants<T>(DependencyObject parent) where T : DependencyObject
        {
            foreach (var child in LogicalTreeHelper.GetChildren(parent).OfType<DependencyObject>())
            {
                if (child is T match)
                    yield return match;

                foreach (var nested in FindDescendants<T>(child))
                    yield return nested;
            }
        }

        internal async Task SaveAutoAcceptSettings()
        {
            if (_suppressAutoAccept)
                return;

            try
            {
                var settings = ReadAutoAcceptConfiguration();
                await _autoAcceptProfilesApplicationService.SaveActiveConfigurationAsync(settings);
                AutoAcceptJsonCache = JsonSerializer.Serialize(new AutoAcceptSettings
                {
                    Enabled = settings.Enabled,
                    AllowedTools = settings.AllowedTools.ToList(),
                    AllowMouseTools = settings.AllowMouseTools,
                    AllowKeyboardTools = settings.AllowKeyboardTools
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to save auto‑accept settings: {ex}");
            }
        }

        private void ApplyAutoAcceptConfiguration(AutoAcceptProfilesApplicationService.AutoAcceptConfiguration settings)
        {
            AutoAcceptEnabledCheckBox.IsChecked = settings.Enabled;
            AutoAcceptOpenUrlCheckBox.IsChecked = settings.AllowedTools.Contains("open_url");
            AutoAcceptHttpRequestCheckBox.IsChecked = settings.AllowedTools.Contains("http_request");
            AutoAcceptOpenBrowserTabCheckBox.IsChecked = settings.AllowedTools.Contains("open_browser_tab");
            AutoAcceptListBrowserTabsCheckBox.IsChecked = settings.AllowedTools.Contains("list_browser_tabs");
            AutoAcceptReadBrowserTabCheckBox.IsChecked = settings.AllowedTools.Contains("read_browser_tab");
            AutoAcceptSwitchBrowserTabCheckBox.IsChecked = settings.AllowedTools.Contains("switch_browser_tab");
            AutoAcceptCloseBrowserTabCheckBox.IsChecked = settings.AllowedTools.Contains("close_browser_tab");
            AutoAcceptGetBrowserHtmlCheckBox.IsChecked = settings.AllowedTools.Contains("get_browser_html");
            AutoAcceptExecuteBrowserScriptCheckBox.IsChecked = settings.AllowedTools.Contains("execute_browser_script");
            AutoAcceptGetBrowserCookiesCheckBox.IsChecked = settings.AllowedTools.Contains("get_browser_cookies");
            AutoAcceptListFilesCheckBox.IsChecked = settings.AllowedTools.Contains("list_files");
            AutoAcceptReadFileCheckBox.IsChecked = settings.AllowedTools.Contains("read_file");
            AutoAcceptSearchFilesCheckBox.IsChecked = settings.AllowedTools.Contains("search_files");
            AutoAcceptSearchFileContentCheckBox.IsChecked = settings.AllowedTools.Contains("search_file_content");
            AutoAcceptWriteToFileCheckBox.IsChecked = settings.AllowedTools.Contains("write_to_file");
            AutoAcceptApplyDiffCheckBox.IsChecked = settings.AllowedTools.Contains("apply_diff");
            AutoAcceptCreateDirectoryCheckBox.IsChecked = settings.AllowedTools.Contains("create_directory");
            AutoAcceptDeleteFileCheckBox.IsChecked = settings.AllowedTools.Contains("delete_file");
            AutoAcceptMoveFileCheckBox.IsChecked = settings.AllowedTools.Contains("move_file");
            AutoAcceptOpenFileCheckBox.IsChecked = settings.AllowedTools.Contains("open_file");
            AutoAcceptExecuteCommandCheckBox.IsChecked = settings.AllowedTools.Contains("execute_command");
            AutoAcceptReadCommandOutputCheckBox.IsChecked = settings.AllowedTools.Contains("read_command_output");
            AutoAcceptGetClipboardCheckBox.IsChecked = settings.AllowedTools.Contains("get_clipboard");
            AutoAcceptSetClipboardCheckBox.IsChecked = settings.AllowedTools.Contains("set_clipboard");
            AutoAcceptNotifyCheckBox.IsChecked = settings.AllowedTools.Contains("show_notification");
            AutoAcceptGetSystemInfoCheckBox.IsChecked = settings.AllowedTools.Contains("get_system_info");
            AutoAcceptGetRunningProcessesCheckBox.IsChecked = settings.AllowedTools.Contains("get_running_processes");
            AutoAcceptGetActiveWindowCheckBox.IsChecked = settings.AllowedTools.Contains("get_active_window");
            AutoAcceptGetSelectedTextCheckBox.IsChecked = settings.AllowedTools.Contains("get_selected_text");
            AutoAcceptRememberCheckBox.IsChecked = settings.AllowedTools.Contains("remember");
            AutoAcceptRecallCheckBox.IsChecked = settings.AllowedTools.Contains("recall");
            AutoAcceptSetReminderCheckBox.IsChecked = settings.AllowedTools.Contains("set_reminder");
            AutoAcceptReadEmailsCheckBox.IsChecked = settings.AllowedTools.Contains("read_emails");
            AutoAcceptSearchEmailsCheckBox.IsChecked = settings.AllowedTools.Contains("search_emails");
            AutoAcceptSendEmailCheckBox.IsChecked = settings.AllowedTools.Contains("send_email");
            AutoAcceptReplyToEmailCheckBox.IsChecked = settings.AllowedTools.Contains("reply_to_email");
            AutoAcceptNewTaskCheckBox.IsChecked = settings.AllowedTools.Contains("new_task");
            AutoAcceptAskFollowupQuestionCheckBox.IsChecked = settings.AllowedTools.Contains("ask_followup_question");
            AutoAcceptAttemptCompletionCheckBox.IsChecked = settings.AllowedTools.Contains("attempt_completion");
            AutoAcceptSkillCheckBox.IsChecked = settings.AllowedTools.Contains("skill");
            AutoAcceptSwitchModeCheckBox.IsChecked = settings.AllowedTools.Contains("switch_mode");
            AutoAcceptSwitchModelCheckBox.IsChecked = settings.AllowedTools.Contains("switch_model");
            AutoAcceptUpdateTodoListCheckBox.IsChecked = settings.AllowedTools.Contains("update_todo_list");
            AutoAcceptShowImageCheckBox.IsChecked = settings.AllowedTools.Contains("show_image");
            AutoAcceptMouseToolsCheckBox.IsChecked = settings.AllowMouseTools;
            AutoAcceptKeyboardToolsCheckBox.IsChecked = settings.AllowKeyboardTools;
        }

        private AutoAcceptProfilesApplicationService.AutoAcceptConfiguration ReadAutoAcceptConfiguration()
        {
            var allowedTools = new List<string>();

            if (AutoAcceptOpenUrlCheckBox.IsChecked == true) allowedTools.Add("open_url");
            if (AutoAcceptHttpRequestCheckBox.IsChecked == true) allowedTools.Add("http_request");
            if (AutoAcceptOpenBrowserTabCheckBox.IsChecked == true) allowedTools.Add("open_browser_tab");
            if (AutoAcceptListBrowserTabsCheckBox.IsChecked == true) allowedTools.Add("list_browser_tabs");
            if (AutoAcceptReadBrowserTabCheckBox.IsChecked == true) allowedTools.Add("read_browser_tab");
            if (AutoAcceptSwitchBrowserTabCheckBox.IsChecked == true) allowedTools.Add("switch_browser_tab");
            if (AutoAcceptCloseBrowserTabCheckBox.IsChecked == true) allowedTools.Add("close_browser_tab");
            if (AutoAcceptGetBrowserHtmlCheckBox.IsChecked == true) allowedTools.Add("get_browser_html");
            if (AutoAcceptExecuteBrowserScriptCheckBox.IsChecked == true) allowedTools.Add("execute_browser_script");
            if (AutoAcceptGetBrowserCookiesCheckBox.IsChecked == true) allowedTools.Add("get_browser_cookies");
            if (AutoAcceptListFilesCheckBox.IsChecked == true) allowedTools.Add("list_files");
            if (AutoAcceptReadFileCheckBox.IsChecked == true) allowedTools.Add("read_file");
            if (AutoAcceptSearchFilesCheckBox.IsChecked == true) allowedTools.Add("search_files");
            if (AutoAcceptSearchFileContentCheckBox.IsChecked == true) allowedTools.Add("search_file_content");
            if (AutoAcceptWriteToFileCheckBox.IsChecked == true) allowedTools.Add("write_to_file");
            if (AutoAcceptApplyDiffCheckBox.IsChecked == true) allowedTools.Add("apply_diff");
            if (AutoAcceptCreateDirectoryCheckBox.IsChecked == true) allowedTools.Add("create_directory");
            if (AutoAcceptDeleteFileCheckBox.IsChecked == true) allowedTools.Add("delete_file");
            if (AutoAcceptMoveFileCheckBox.IsChecked == true) allowedTools.Add("move_file");
            if (AutoAcceptOpenFileCheckBox.IsChecked == true) allowedTools.Add("open_file");
            if (AutoAcceptExecuteCommandCheckBox.IsChecked == true) allowedTools.Add("execute_command");
            if (AutoAcceptReadCommandOutputCheckBox.IsChecked == true) allowedTools.Add("read_command_output");
            if (AutoAcceptGetClipboardCheckBox.IsChecked == true) allowedTools.Add("get_clipboard");
            if (AutoAcceptSetClipboardCheckBox.IsChecked == true) allowedTools.Add("set_clipboard");
            if (AutoAcceptNotifyCheckBox.IsChecked == true) allowedTools.Add("show_notification");
            if (AutoAcceptGetSystemInfoCheckBox.IsChecked == true) allowedTools.Add("get_system_info");
            if (AutoAcceptGetRunningProcessesCheckBox.IsChecked == true) allowedTools.Add("get_running_processes");
            if (AutoAcceptGetActiveWindowCheckBox.IsChecked == true) allowedTools.Add("get_active_window");
            if (AutoAcceptGetSelectedTextCheckBox.IsChecked == true) allowedTools.Add("get_selected_text");
            if (AutoAcceptRememberCheckBox.IsChecked == true) allowedTools.Add("remember");
            if (AutoAcceptRecallCheckBox.IsChecked == true) allowedTools.Add("recall");
            if (AutoAcceptSetReminderCheckBox.IsChecked == true) allowedTools.Add("set_reminder");
            if (AutoAcceptReadEmailsCheckBox.IsChecked == true) allowedTools.Add("read_emails");
            if (AutoAcceptSearchEmailsCheckBox.IsChecked == true) allowedTools.Add("search_emails");
            if (AutoAcceptSendEmailCheckBox.IsChecked == true) allowedTools.Add("send_email");
            if (AutoAcceptReplyToEmailCheckBox.IsChecked == true) allowedTools.Add("reply_to_email");
            if (AutoAcceptNewTaskCheckBox.IsChecked == true) allowedTools.Add("new_task");
            if (AutoAcceptAskFollowupQuestionCheckBox.IsChecked == true) allowedTools.Add("ask_followup_question");
            if (AutoAcceptAttemptCompletionCheckBox.IsChecked == true) allowedTools.Add("attempt_completion");
            if (AutoAcceptSkillCheckBox.IsChecked == true) allowedTools.Add("skill");
            if (AutoAcceptSwitchModeCheckBox.IsChecked == true) allowedTools.Add("switch_mode");
            if (AutoAcceptSwitchModelCheckBox.IsChecked == true) allowedTools.Add("switch_model");
            if (AutoAcceptUpdateTodoListCheckBox.IsChecked == true) allowedTools.Add("update_todo_list");
            if (AutoAcceptShowImageCheckBox.IsChecked == true) allowedTools.Add("show_image");

            return new AutoAcceptProfilesApplicationService.AutoAcceptConfiguration(
                AutoAcceptEnabledCheckBox.IsChecked == true,
                allowedTools,
                AutoAcceptMouseToolsCheckBox.IsChecked == true,
                AutoAcceptKeyboardToolsCheckBox.IsChecked == true);
        }

        private void UpdateAutoAcceptProfileButtons()
        {
            var selected = AutoAcceptProfileComboBox.SelectedItem as AutoAcceptProfilesApplicationService.AutoAcceptProfile;
            DeleteAutoAcceptProfileButton.IsEnabled = selected is { IsBuiltIn: false };
        }

        private void AutoAcceptEnabled_Changed(object sender, RoutedEventArgs e)
        {
            if (_suppressAutoAccept)
                return;

            _ = SaveAutoAcceptSettings();
        }

        private void AutoAcceptCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (_suppressAutoAccept)
                return;

            _ = SaveAutoAcceptSettings();
        }

        private async void ApplyAutoAcceptProfile_Click(object sender, RoutedEventArgs e)
        {
            if (AutoAcceptProfileComboBox.SelectedItem is not AutoAcceptProfilesApplicationService.AutoAcceptProfile profile)
                return;

            _suppressAutoAccept = true;
            ApplyAutoAcceptConfiguration(profile.Configuration);
            _suppressAutoAccept = false;
            await _autoAcceptProfilesApplicationService.SaveSelectedProfileNameAsync(profile.Name);
            await SaveAutoAcceptSettings();
            ShowToast($"Applied auto-accept profile: {profile.Name}");
        }

        private async void SaveAutoAcceptProfile_Click(object sender, RoutedEventArgs e)
        {
            var suggestedName = (AutoAcceptProfileComboBox.SelectedItem as AutoAcceptProfilesApplicationService.AutoAcceptProfile)?.Name ?? "Custom profile";
            var name = Interaction.InputBox("Profile name:", "Save auto-accept profile", suggestedName)?.Trim();
            if (string.IsNullOrWhiteSpace(name))
                return;

            await _autoAcceptProfilesApplicationService.SaveProfileAsync(name, ReadAutoAcceptConfiguration());
            await LoadAutoAcceptProfilesAsync();
            ShowToast($"Saved auto-accept profile: {name}");
        }

        private async void DeleteAutoAcceptProfile_Click(object sender, RoutedEventArgs e)
        {
            if (AutoAcceptProfileComboBox.SelectedItem is not AutoAcceptProfilesApplicationService.AutoAcceptProfile profile ||
                profile.IsBuiltIn)
                return;

            if (!ConfirmationDialog.ShowCentered(this,
                title: "Delete profile?",
                message: $"Delete auto-accept profile '{profile.Name}'?"))
            {
                return;
            }

            await _autoAcceptProfilesApplicationService.DeleteProfileAsync(profile.Name);
            await LoadAutoAcceptProfilesAsync();
            ShowToast($"Deleted auto-accept profile: {profile.Name}");
        }

        private async void AutoAcceptProfile_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressAutoAcceptProfileSelection)
                return;

            UpdateAutoAcceptProfileButtons();
            if (AutoAcceptProfileComboBox.SelectedItem is AutoAcceptProfilesApplicationService.AutoAcceptProfile profile)
                await _autoAcceptProfilesApplicationService.SaveSelectedProfileNameAsync(profile.Name);
        }
    }
}
