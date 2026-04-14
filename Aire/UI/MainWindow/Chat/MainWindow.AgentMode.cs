using System;
using System.Windows;
using Aire.Services;

namespace Aire
{
    public partial class MainWindow
    {
        private System.Windows.Threading.DispatcherTimer? _agentModeRefreshTimer;

        private async void AgentModeButton_Click(object sender, RoutedEventArgs e)
        {
            if (_agentModeService == null) return;

            if (_agentModeService.IsActive)
            {
                StopAgentMode();
            }
            else
            {
                var config = UI.AgentModeConfigDialog.ShowConfigDialog(this);
                if (config == null) return; // user cancelled

                var (budget, categories) = config.Value;
                _agentModeService.Start(budget, categories.Count > 0 ? categories : null);
                AgentModeButton.ToolTip = LocalizationService.S("tooltip.agentModeOn", "Agent mode active — all tools auto-approved");
                AgentModeButton.IsChecked = true;
                UpdateAgentModeBanner();
                AgentModeBanner.Visibility = Visibility.Visible;
                await AddSystemMessageAsync(LocalizationService.S("agentMode.started", "Agent mode activated. Tool calls will be auto-approved."));
            }
        }

        private void StopAgentButton_Click(object sender, RoutedEventArgs e)
        {
            StopAgentMode();
        }

        private void StopAgentMode()
        {
            if (_agentModeService == null) return;
            _agentModeService.Stop();
            AgentModeButton.ToolTip = LocalizationService.S("tooltip.agentModeOff", "Agent mode — click to activate");
            AgentModeButton.IsChecked = false;
            AgentModeBanner.Visibility = Visibility.Collapsed;
            StopAgentModeRefreshTimer();
        }

        private void InitializeAgentModeHandlers()
        {
            if (_agentModeService == null) return;

            _agentModeService.BudgetExhausted += async () =>
            {
                StopAgentMode();
                await AddSystemMessageAsync(LocalizationService.S("agentMode.budgetExhausted", "Agent mode stopped: token budget exhausted."));
            };

            _agentModeService.ModeChanged += () =>
            {
                if (_agentModeService.IsActive)
                {
                    StartAgentModeRefreshTimer();
                }
            };
        }

        private void UpdateAgentModeBanner()
        {
            if (_agentModeService == null || !_agentModeService.IsActive) return;

            var consumed = _agentModeService.TokensConsumed;
            var budget = _agentModeService.TokenBudget;
            var consumedText = FormatTokenCount(consumed);

            if (budget > 0)
            {
                var budgetText = FormatTokenCount(budget);
                var pct = Math.Min(100, (int)((double)consumed / budget * 100));
                AgentModeStatusText.Text = $"Agent: {consumedText} / {budgetText} tokens ({pct}%)";
            }
            else
            {
                AgentModeStatusText.Text = $"Agent: {consumedText} tokens used";
            }
        }

        private static string FormatTokenCount(int tokens)
        {
            return tokens >= 1000 ? $"{tokens / 1000.0:0.#}k" : tokens.ToString();
        }

        private void StartAgentModeRefreshTimer()
        {
            StopAgentModeRefreshTimer();
            _agentModeRefreshTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            _agentModeRefreshTimer.Tick += (_, _) => UpdateAgentModeBanner();
            _agentModeRefreshTimer.Start();
        }

        private void StopAgentModeRefreshTimer()
        {
            _agentModeRefreshTimer?.Stop();
            _agentModeRefreshTimer = null;
        }
    }
}
