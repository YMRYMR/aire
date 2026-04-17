using System.Windows;
using Aire.Services;

namespace Aire
{
    public partial class MainWindow
    {
        private bool _orchestratorResumedExistingConversation;

        private async void AgentModeButton_Click(object sender, RoutedEventArgs e)
        {
            if (_agentModeService == null)
                return;

            if (_agentModeService.IsActive)
            {
                OrchestratorFlow.StopOrchestratorMode("user stopped");
                return;
            }

            AgentModeButton.IsChecked = false;
            var config = UI.AgentModeConfigDialog.ShowConfigDialog(this);
            if (config == null)
                return;

            if (!await OrchestratorFlow.StartOrchestratorModeAsync(config))
                AgentModeButton.IsChecked = false;
        }

        private void StopAgentButton_Click(object sender, RoutedEventArgs e)
        {
            OrchestratorFlow.StopOrchestratorMode("user stopped");
        }

        // ── Orchestrator forwarding wrappers (called from other partial files) ──

        internal void StopOrchestratorMode(string? reason = null)
            => OrchestratorFlow.StopOrchestratorMode(reason);

        internal void InitializeAgentModeHandlers()
            => OrchestratorFlow.InitializeHandlers();

        internal void UpdateAgentModeTooltip()
            => OrchestratorFlow.UpdateAgentModeTooltip();

        internal void PersistOrchestratorConfigSnapshot(int tokenBudget, System.Collections.Generic.IEnumerable<string>? goals, System.Collections.Generic.IEnumerable<string>? selectedCategories)
            => OrchestratorFlow.PersistOrchestratorConfigSnapshot(tokenBudget, goals, selectedCategories);

        internal void PersistOrchestratorSessionSnapshot(int conversationId, string? lastNarrative = null)
            => OrchestratorFlow.PersistOrchestratorSessionSnapshot(conversationId, lastNarrative);

        internal void PersistOrchestratorSessionSnapshot(string? lastNarrative = null)
            => OrchestratorFlow.PersistOrchestratorSessionSnapshot(lastNarrative);

        internal System.Threading.Tasks.Task PauseOrchestratorConversationAsync(int? conversationId, string? reason = null, bool clearSessionStatus = true, bool refreshSidebar = false)
            => OrchestratorFlow.PauseOrchestratorConversationAsync(conversationId, reason, clearSessionStatus, refreshSidebar);

        internal System.Threading.Tasks.Task<bool> StartOrchestratorModeAsync(UI.AgentModeConfigDialog.OrchestratorConfig config)
            => OrchestratorFlow.StartOrchestratorModeAsync(config);

        internal string BuildOrchestratorModePromptSection()
            => OrchestratorFlow.BuildOrchestratorModePromptSection();

        internal string BuildOrchestratorRecoveryModePromptSection()
            => OrchestratorFlow.BuildOrchestratorRecoveryModePromptSection();
    }
}
