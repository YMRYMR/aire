using System;
using System.Windows;

namespace Aire
{
    public partial class MainWindow
    {
        private void UpdateMouseSessionBanner()
        {
            var sessionService = _toolControlSessionApplicationService
                ?? new Aire.AppLayer.Tools.ToolControlSessionApplicationService(
                    _toolApprovalApplicationService
                    ?? new Aire.AppLayer.Tools.ToolApprovalApplicationService(
                        new Aire.Services.Policies.ToolAutoAcceptPolicyService(() => System.Threading.Tasks.Task.FromResult<string?>(UI.SettingsWindow.AutoAcceptJsonCache))));
            var now = DateTime.Now;
            var bannerPlan = sessionService.BuildBannerPlan(now);
            MouseSessionBanner.Visibility = bannerPlan.IsVisible ? Visibility.Visible : Visibility.Collapsed;
            if (!string.IsNullOrWhiteSpace(bannerPlan.BannerText))
                MouseSessionLabel.Text = bannerPlan.BannerText;

            if (bannerPlan.SessionActive)
            {
                if (_panicButton == null)
                {
                    _panicButton = new Aire.UI.SessionPanicButton();
                    _panicButton.StopRequested += () => Dispatcher.Invoke(EmergencyStopSession);
                    _panicButton.Topmost = Topmost;
                    _panicButton.Show();
                }
            }
            else
            {
                _panicButton?.Close();
                _panicButton = null;
            }
        }

        private void CancelMouseSession_Click(object sender, RoutedEventArgs e) => EmergencyStopSession();

        private void EmergencyStopSession()
        {
            (_toolControlSessionApplicationService
                ?? new Aire.AppLayer.Tools.ToolControlSessionApplicationService(
                    _toolApprovalApplicationService
                    ?? new Aire.AppLayer.Tools.ToolApprovalApplicationService(
                        new Aire.Services.Policies.ToolAutoAcceptPolicyService(() => System.Threading.Tasks.Task.FromResult<string?>(UI.SettingsWindow.AutoAcceptJsonCache)))))
                .Stop();
            UpdateMouseSessionBanner();
            AddSystemMessage("Session stopped by user.");
        }
    }
}
