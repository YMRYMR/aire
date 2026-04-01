using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Aire.Services;
using Aire.Services.Email;
using Aire.UI.Settings.Models;

namespace Aire.UI
{
    public partial class SettingsWindow
    {
        private void OpenEmailEdit(EmailAccount account, bool isNew)
        {
            _editingEmailVm = isNew ? null : _emailVms.FirstOrDefault(v => v.Model.Id == account.Id);
            _editingOAuthRefreshToken = account.UseOAuth
                ? (SecureStorage.Unprotect(account.OAuthRefreshToken) ?? string.Empty)
                : string.Empty;

            EmailDisplayNameBox.Text = account.DisplayName;
            EmailImapHostBox.Text = account.ImapHost;
            EmailImapPortBox.Text = account.ImapPort > 0 ? account.ImapPort.ToString() : "993";
            EmailSmtpHostBox.Text = account.SmtpHost;
            EmailSmtpPortBox.Text = account.SmtpPort > 0 ? account.SmtpPort.ToString() : "587";
            EmailUsernameBox.Text = account.Username;
            EmailPasswordBox.Password = string.Empty;
            OAuthStatusText.Text = account.UseOAuth && !string.IsNullOrEmpty(_editingOAuthRefreshToken)
                ? "\u2713 Authorized" : string.Empty;
            EmailEditTitle.Text = isNew ? "Add email account" : "Edit email account";

            foreach (ComboBoxItem item in EmailProviderCombo.Items)
            {
                if (item.Tag?.ToString() == account.Provider.ToString())
                {
                    EmailProviderCombo.SelectedItem = item;
                    break;
                }
            }

            UpdateEmailPanelsForProvider(account.Provider == EmailProvider.Gmail);
            EmailTestResult.Text = string.Empty;
            EmailEditPanel.Visibility = Visibility.Visible;
        }

        private void UpdateEmailPanelsForProvider(bool isGmail)
        {
            var vis = isGmail ? Visibility.Collapsed : Visibility.Visible;
            EmailHostsPanel.Visibility = vis;
            EmailPasswordPanel.Visibility = vis;
            EmailOAuthPanel.Visibility = isGmail ? Visibility.Visible : Visibility.Collapsed;
        }

        private void EmailProviderCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (EmailProviderCombo.SelectedItem is not ComboBoxItem item)
            {
                return;
            }

            var tag = item.Tag?.ToString() ?? "";
            if (tag == "Gmail")
            {
                EmailImapHostBox.Text = "imap.gmail.com";
                EmailImapPortBox.Text = "993";
                EmailSmtpHostBox.Text = "smtp.gmail.com";
                EmailSmtpPortBox.Text = "587";
                UpdateEmailPanelsForProvider(true);
            }
            else
            {
                if (tag == "Outlook")
                {
                    EmailImapHostBox.Text = "outlook.office365.com";
                    EmailImapPortBox.Text = "993";
                    EmailSmtpHostBox.Text = "smtp.office365.com";
                    EmailSmtpPortBox.Text = "587";
                }

                UpdateEmailPanelsForProvider(false);
            }
        }

        private void EmailAccountsList_SelectionChanged(object sender, SelectionChangedEventArgs e) { }

        private async void TestEmailBtn_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as System.Windows.Controls.Button)?.Tag is not EmailAccountViewModel vm)
            {
                return;
            }

            vm.StatusColor = "#888888";
            vm.Model.PlaintextPassword = SecureStorage.Unprotect(vm.Model.EncryptedPassword);
            var svc = new EmailService(vm.Model);
            var (ok, _) = await svc.TestConnectionAsync();
            vm.StatusColor = ok ? "#3CB371" : "#CC3333";
        }

        private async void DeleteEmailBtn_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as System.Windows.Controls.Button)?.Tag is not EmailAccountViewModel vm)
            {
                return;
            }

            if (System.Windows.MessageBox.Show($"Delete '{vm.DisplayName}'?", "Confirm", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
            {
                return;
            }

            await _emailAccountApplicationService.DeleteEmailAccountAsync(vm.Model.Id);
            _emailVms.Remove(vm);
        }

        private async void SignInWithGoogleBtn_Click(object sender, RoutedEventArgs e)
        {
            OAuthStatusText.Text = "Opening browser...";
            try
            {
                var result = await GoogleOAuthService.AuthorizeAsync();
                _editingOAuthRefreshToken = result.RefreshToken;
                OAuthStatusText.Text = "\u2713 Authorized";
                OAuthStatusText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(60, 179, 113));
            }
            catch (Exception ex)
            {
                OAuthStatusText.Text = $"\u2717 {ex.Message}";
                OAuthStatusText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(204, 51, 51));
            }
        }

        private async void TestEmailConnBtn_Click(object sender, RoutedEventArgs e)
        {
            EmailTestResult.Text = "Testing...";
            var account = BuildEmailAccountFromForm();
            if (!account.UseOAuth)
            {
                account.PlaintextPassword = EmailPasswordBox.Password;
            }

            var svc = new EmailService(account);
            var (ok, error) = await svc.TestConnectionAsync();
            EmailTestResult.Text = ok ? "\u2713 Connected" : $"\u2717 {error}";
            EmailTestResult.Foreground = ok
                ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(60, 179, 113))
                : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(204, 51, 51));
        }

        private async void SaveEmailBtn_Click(object sender, RoutedEventArgs e)
        {
            var account = BuildEmailAccountFromForm();
            if (!account.UseOAuth)
            {
                account.PlaintextPassword = string.IsNullOrEmpty(EmailPasswordBox.Password) ? null : EmailPasswordBox.Password;
            }

            if (string.IsNullOrWhiteSpace(account.DisplayName) || string.IsNullOrWhiteSpace(account.Username))
            {
                EmailTestResult.Text = "Display name and email address are required.";
                return;
            }

            if (account.UseOAuth && string.IsNullOrEmpty(_editingOAuthRefreshToken))
            {
                EmailTestResult.Text = "Sign in with Google first.";
                return;
            }

            if (_editingEmailVm == null)
            {
                var id = await _emailAccountApplicationService.InsertEmailAccountAsync(account);
                account.Id = id;
                _emailVms.Add(new EmailAccountViewModel(account));
            }
            else
            {
                account.Id = _editingEmailVm.Model.Id;
                await _emailAccountApplicationService.UpdateEmailAccountAsync(account);
                await LoadConnectionsTabAsync();
            }

            EmailEditPanel.Visibility = Visibility.Collapsed;
            _editingEmailVm = null;
        }

        private void CancelEmailBtn_Click(object sender, RoutedEventArgs e)
        {
            EmailEditPanel.Visibility = Visibility.Collapsed;
            _editingEmailVm = null;
            _editingOAuthRefreshToken = string.Empty;
        }

        private EmailAccount BuildEmailAccountFromForm()
        {
            var providerTag = (EmailProviderCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Custom";
            _ = Enum.TryParse<EmailProvider>(providerTag, out var prov);
            _ = int.TryParse(EmailImapPortBox.Text, out var imapPort);
            if (imapPort == 0) imapPort = 993;
            _ = int.TryParse(EmailSmtpPortBox.Text, out var smtpPort);
            if (smtpPort == 0) smtpPort = 587;

            return new EmailAccount
            {
                DisplayName = EmailDisplayNameBox.Text.Trim(),
                Provider = prov,
                ImapHost = EmailImapHostBox.Text.Trim(),
                ImapPort = imapPort,
                SmtpHost = EmailSmtpHostBox.Text.Trim(),
                SmtpPort = smtpPort,
                Username = EmailUsernameBox.Text.Trim(),
                IsEnabled = true,
                UseOAuth = prov == EmailProvider.Gmail,
                OAuthRefreshToken = prov == EmailProvider.Gmail
                    ? (SecureStorage.Protect(_editingOAuthRefreshToken) ?? string.Empty)
                    : string.Empty,
            };
        }
    }
}
