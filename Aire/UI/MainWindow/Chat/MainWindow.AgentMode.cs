using System;
using System.Windows;
using Aire.Services;

namespace Aire
{
    public partial class MainWindow
    {
        private async void AgentModeButton_Click(object sender, RoutedEventArgs e)
        {
            if (_agentModeService == null) return;

            if (_agentModeService.IsActive)
            {
                _agentModeService.Stop();
                AgentModeButton.ToolTip = LocalizationService.S("tooltip.agentModeOff", "Agent mode — click to activate");
                AgentModeButton.IsChecked = false;
            }
            else
            {
                _agentModeService.Start();
                AgentModeButton.ToolTip = LocalizationService.S("tooltip.agentModeOn", "Agent mode active — all tools auto-approved");
                AgentModeButton.IsChecked = true;
                await AddSystemMessageAsync(LocalizationService.S("agentMode.started", "Agent mode activated. Tool calls will be auto-approved."));
            }
        }

        private void InitializeAgentModeHandlers()
        {
            if (_agentModeService == null) return;

            _agentModeService.BudgetExhausted += async () =>
            {
                _agentModeService.Stop();
                await AddSystemMessageAsync(LocalizationService.S("agentMode.budgetExhausted", "Agent mode stopped: token budget exhausted."));
                AgentModeButton.ToolTip = LocalizationService.S("tooltip.agentModeOff", "Agent mode — click to activate");
                AgentModeButton.IsChecked = false;
            };
        }
    }
}
