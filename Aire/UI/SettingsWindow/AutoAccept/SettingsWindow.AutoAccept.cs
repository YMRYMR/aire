using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Aire.UI.Settings.Models;

namespace Aire.UI
{
    public partial class SettingsWindow
    {
        internal async Task LoadAutoAcceptSettings()
        {
            _suppressAutoAccept = true;
            try
            {
                var json = await _appSettingsApplicationService.GetSettingAsync("auto_accept_settings");
                if (!string.IsNullOrEmpty(json))
                {
                    var settings = JsonSerializer.Deserialize<AutoAcceptSettings>(json);
                    if (settings != null)
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
                }
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

        private void HookAutoAcceptEvents()
        {
            if (AutoAcceptToolsPanel == null)
            {
                return;
            }

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
                {
                    yield return match;
                }

                foreach (var nested in FindDescendants<T>(child))
                {
                    yield return nested;
                }
            }
        }

        internal async Task SaveAutoAcceptSettings()
        {
            if (_suppressAutoAccept)
            {
                return;
            }

            try
            {
                var settings = new AutoAcceptSettings
                {
                    Enabled = AutoAcceptEnabledCheckBox.IsChecked == true,
                    AllowedTools = new List<string>(),
                    AllowMouseTools = AutoAcceptMouseToolsCheckBox.IsChecked == true,
                    AllowKeyboardTools = AutoAcceptKeyboardToolsCheckBox.IsChecked == true
                };

                if (AutoAcceptOpenUrlCheckBox.IsChecked == true) settings.AllowedTools.Add("open_url");
                if (AutoAcceptHttpRequestCheckBox.IsChecked == true) settings.AllowedTools.Add("http_request");
                if (AutoAcceptOpenBrowserTabCheckBox.IsChecked == true) settings.AllowedTools.Add("open_browser_tab");
                if (AutoAcceptListBrowserTabsCheckBox.IsChecked == true) settings.AllowedTools.Add("list_browser_tabs");
                if (AutoAcceptReadBrowserTabCheckBox.IsChecked == true) settings.AllowedTools.Add("read_browser_tab");
                if (AutoAcceptSwitchBrowserTabCheckBox.IsChecked == true) settings.AllowedTools.Add("switch_browser_tab");
                if (AutoAcceptCloseBrowserTabCheckBox.IsChecked == true) settings.AllowedTools.Add("close_browser_tab");
                if (AutoAcceptGetBrowserHtmlCheckBox.IsChecked == true) settings.AllowedTools.Add("get_browser_html");
                if (AutoAcceptExecuteBrowserScriptCheckBox.IsChecked == true) settings.AllowedTools.Add("execute_browser_script");
                if (AutoAcceptGetBrowserCookiesCheckBox.IsChecked == true) settings.AllowedTools.Add("get_browser_cookies");
                if (AutoAcceptListFilesCheckBox.IsChecked == true) settings.AllowedTools.Add("list_files");
                if (AutoAcceptReadFileCheckBox.IsChecked == true) settings.AllowedTools.Add("read_file");
                if (AutoAcceptSearchFilesCheckBox.IsChecked == true) settings.AllowedTools.Add("search_files");
                if (AutoAcceptSearchFileContentCheckBox.IsChecked == true) settings.AllowedTools.Add("search_file_content");
                if (AutoAcceptWriteToFileCheckBox.IsChecked == true) settings.AllowedTools.Add("write_to_file");
                if (AutoAcceptApplyDiffCheckBox.IsChecked == true) settings.AllowedTools.Add("apply_diff");
                if (AutoAcceptCreateDirectoryCheckBox.IsChecked == true) settings.AllowedTools.Add("create_directory");
                if (AutoAcceptDeleteFileCheckBox.IsChecked == true) settings.AllowedTools.Add("delete_file");
                if (AutoAcceptMoveFileCheckBox.IsChecked == true) settings.AllowedTools.Add("move_file");
                if (AutoAcceptOpenFileCheckBox.IsChecked == true) settings.AllowedTools.Add("open_file");
                if (AutoAcceptExecuteCommandCheckBox.IsChecked == true) settings.AllowedTools.Add("execute_command");
                if (AutoAcceptReadCommandOutputCheckBox.IsChecked == true) settings.AllowedTools.Add("read_command_output");
                if (AutoAcceptGetClipboardCheckBox.IsChecked == true) settings.AllowedTools.Add("get_clipboard");
                if (AutoAcceptSetClipboardCheckBox.IsChecked == true) settings.AllowedTools.Add("set_clipboard");
                if (AutoAcceptNotifyCheckBox.IsChecked == true) settings.AllowedTools.Add("show_notification");
                if (AutoAcceptGetSystemInfoCheckBox.IsChecked == true) settings.AllowedTools.Add("get_system_info");
                if (AutoAcceptGetRunningProcessesCheckBox.IsChecked == true) settings.AllowedTools.Add("get_running_processes");
                if (AutoAcceptGetActiveWindowCheckBox.IsChecked == true) settings.AllowedTools.Add("get_active_window");
                if (AutoAcceptGetSelectedTextCheckBox.IsChecked == true) settings.AllowedTools.Add("get_selected_text");
                if (AutoAcceptRememberCheckBox.IsChecked == true) settings.AllowedTools.Add("remember");
                if (AutoAcceptRecallCheckBox.IsChecked == true) settings.AllowedTools.Add("recall");
                if (AutoAcceptSetReminderCheckBox.IsChecked == true) settings.AllowedTools.Add("set_reminder");
                if (AutoAcceptReadEmailsCheckBox.IsChecked == true) settings.AllowedTools.Add("read_emails");
                if (AutoAcceptSearchEmailsCheckBox.IsChecked == true) settings.AllowedTools.Add("search_emails");
                if (AutoAcceptSendEmailCheckBox.IsChecked == true) settings.AllowedTools.Add("send_email");
                if (AutoAcceptReplyToEmailCheckBox.IsChecked == true) settings.AllowedTools.Add("reply_to_email");
                if (AutoAcceptNewTaskCheckBox.IsChecked == true) settings.AllowedTools.Add("new_task");
                if (AutoAcceptAskFollowupQuestionCheckBox.IsChecked == true) settings.AllowedTools.Add("ask_followup_question");
                if (AutoAcceptAttemptCompletionCheckBox.IsChecked == true) settings.AllowedTools.Add("attempt_completion");
                if (AutoAcceptSkillCheckBox.IsChecked == true) settings.AllowedTools.Add("skill");
                if (AutoAcceptSwitchModeCheckBox.IsChecked == true) settings.AllowedTools.Add("switch_mode");
                if (AutoAcceptSwitchModelCheckBox.IsChecked == true) settings.AllowedTools.Add("switch_model");
                if (AutoAcceptUpdateTodoListCheckBox.IsChecked == true) settings.AllowedTools.Add("update_todo_list");
                if (AutoAcceptShowImageCheckBox.IsChecked == true) settings.AllowedTools.Add("show_image");

                var json = JsonSerializer.Serialize(settings);
                await _appSettingsApplicationService.SaveSettingAsync("auto_accept_settings", json);
                AutoAcceptJsonCache = json;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to save auto‑accept settings: {ex}");
            }
        }

        private void AutoAcceptEnabled_Changed(object sender, RoutedEventArgs e)
        {
            if (_suppressAutoAccept)
            {
                return;
            }

            _ = SaveAutoAcceptSettings();
        }

        private void AutoAcceptCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (_suppressAutoAccept)
            {
                return;
            }

            _ = SaveAutoAcceptSettings();
        }
    }
}
