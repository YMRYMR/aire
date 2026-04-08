using System;
using System.Windows;
using Aire.Services;

namespace Aire.UI
{
    public partial class HelpWindow
    {
        private void ExecuteLinkAction(string action)
        {
            if (action.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                action.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                Dispatcher.Invoke(() => WebViewWindow.OpenInNewTab(action));
                return;
            }

            if (action.StartsWith("browser:", StringComparison.OrdinalIgnoreCase))
            {
                var url = action["browser:".Length..];
                Dispatcher.Invoke(() => WebViewWindow.OpenInNewTab(url));
                return;
            }

            if (action.Equals("onboarding", StringComparison.OrdinalIgnoreCase))
            {
                Dispatcher.Invoke(() =>
                {
                    var wizard = new OnboardingWindow
                    {
                        Owner = this
                    };
                    wizard.OpenSettingsAction = () => SettingsWindow.RequestOpen("providers");
                    wizard.ShowDialog();
                });
                return;
            }

            if (action == "settings" || action.StartsWith("settings:", StringComparison.OrdinalIgnoreCase))
            {
                var tab = action.Contains(':') ? action[(action.IndexOf(':') + 1)..] : null;
                SettingsWindow.RequestOpen(tab);
            }
        }
    }
}
