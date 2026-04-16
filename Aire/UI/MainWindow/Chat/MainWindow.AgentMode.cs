using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Aire.Data;
using Aire.Domain.Tools;
using Aire.Providers;
using Aire.Services;
using ProviderChatMessage = Aire.Providers.ChatMessage;

namespace Aire
{
    public partial class MainWindow
    {
        private System.Windows.Threading.DispatcherTimer? _agentModeRefreshTimer;
        private bool _orchestratorResumedExistingConversation;

        private async void AgentModeButton_Click(object sender, RoutedEventArgs e)
        {
            if (_agentModeService == null)
                return;

            if (_agentModeService.IsActive)
            {
                StopOrchestratorMode("user stopped");
                return;
            }

            AgentModeButton.IsChecked = false;
            var config = UI.AgentModeConfigDialog.ShowConfigDialog(this);
            if (config == null)
                return;

            if (!await StartOrchestratorModeAsync(config))
                AgentModeButton.IsChecked = false;
        }

        private void StopAgentButton_Click(object sender, RoutedEventArgs e)
        {
            StopOrchestratorMode("user stopped");
        }

        private void StopOrchestratorMode(string? reason = null)
        {
            if (_agentModeService == null)
                return;

            _aiCancellationTokenSource?.Cancel();
            var conversationId = _currentConversationId;
            _ = PauseOrchestratorConversationAsync(conversationId, reason, clearSessionStatus: true, refreshSidebar: true);

            if (string.Equals(reason, "user stopped", StringComparison.OrdinalIgnoreCase))
            {
                _ = AddOrchestratorNarrativeAsync(LocalizationService.S(
                    "orchestrator.paused",
                    "Orchestrator Mode paused. I saved the current session so it can resume later."));
            }
        }

        private void InitializeAgentModeHandlers()
        {
            if (_agentModeService == null)
                return;

            _agentModeService.BudgetExhausted += () =>
            {
                Dispatcher.Invoke(async () =>
                {
                    StopOrchestratorMode("token budget exhausted");
                    await AddOrchestratorNarrativeAsync(LocalizationService.S(
                        "orchestrator.budgetExhausted",
                        "Orchestrator Mode stopped: token budget exhausted. I will wait here until you change the budget or stop the session."));
                });
            };

            _agentModeService.Blocked += message =>
            {
                Dispatcher.Invoke(async () =>
                {
                    StopOrchestratorMode("blocked");
                    await AddOrchestratorNarrativeAsync(message);
                    _ttsService.Speak(message);
                });
            };

            _agentModeService.GoalsChanged += () => Dispatcher.Invoke(() => PersistOrchestratorSessionSnapshot());
            _agentModeService.GoalCompleted += goal => Dispatcher.Invoke(async () =>
            {
                PersistOrchestratorSessionSnapshot();
                await AddOrchestratorNarrativeAsync(string.Format(
                    LocalizationService.S("orchestrator.goalComplete", "Goal completed: {0}. I’m moving on to the remaining goals."),
                    goal));
            });
            _agentModeService.Heartbeat += _ => Dispatcher.Invoke(() => PersistOrchestratorSessionSnapshot());

            _agentModeService.ModeChanged += () =>
            {
                Dispatcher.Invoke(() =>
                {
                    UpdateAgentModeTooltip();
                    if (_agentModeService.IsActive)
                        StartAgentModeRefreshTimer();
                    else
                    {
                        StopAgentModeRefreshTimer();
                        if (string.Equals(_agentModeService.StopReason, "goals completed", StringComparison.OrdinalIgnoreCase))
                        {
                            var doneMessage = LocalizationService.S(
                                "orchestrator.goalsComplete",
                                "Orchestrator Mode finished all goals.");
                            _ = AddOrchestratorNarrativeAsync(doneMessage);
                            _ttsService.Speak(doneMessage);
                        }
                    }

                    PersistOrchestratorSessionSnapshot();
                });
            };
        }

        private async Task<bool> StartOrchestratorModeAsync(UI.AgentModeConfigDialog.OrchestratorConfig config)
        {
            if (_agentModeService == null)
                return false;

            try
            {
                PersistOrchestratorConfigSnapshot(config.TokenBudget, config.Goals, config.SelectedCategories);
                var resumeState = TryLoadResumableOrchestratorSession(_currentConversationId, config);
                var hasSavedSession = resumeState != null;

                if (!await EnsureOrchestratorConversationAsync(config.Goals, resumeState?.ConversationId, !hasSavedSession))
                {
                    _agentModeService.Stop("no provider selected");
                    await AddErrorMessageAsync(LocalizationService.S(
                        "orchestrator.noProvider",
                        "Orchestrator Mode needs an AI provider selected before it can start."));
                    return false;
                }

                _agentModeService.Start(
                    config.TokenBudget,
                    config.SelectedCategories.Count > 0 ? config.SelectedCategories : null,
                    config.Goals,
                    restoreSnapshot: resumeState);

                _orchestratorResumedExistingConversation =
                    hasSavedSession &&
                    _currentConversationId.HasValue &&
                    resumeState?.ConversationId == _currentConversationId.Value;
                var resumedExistingConversation = _orchestratorResumedExistingConversation;

                if (_currentConversationId.HasValue)
                    await SetConversationOrchestratorModeAsync(_currentConversationId.Value, true, refreshSidebar: true);

                PersistOrchestratorSessionSnapshot(
                    _currentConversationId!.Value,
                    resumedExistingConversation
                        ? LocalizationService.S(
                            "orchestrator.started",
                            "Orchestrator Mode activated. It will keep working on the listed goals until you stop it or it blocks.")
                        : hasSavedSession
                            ? LocalizationService.S(
                                "orchestrator.restarted",
                                "Orchestrator Mode started a new conversation from the saved goals and settings.")
                            : LocalizationService.S(
                                "orchestrator.resumed",
                                "Orchestrator Mode resumed from the saved session and will continue from the last checkpoint."));

                UpdateAgentModeTooltip();
                AgentModeButton.IsChecked = true;
                StartAgentModeRefreshTimer();
                await AddOrchestratorNarrativeAsync(resumedExistingConversation
                    ? BuildOrchestratorStartedMessage()
                    : hasSavedSession
                        ? BuildOrchestratorRestartedMessage()
                        : BuildOrchestratorStartedMessage());

                _ = StartOrchestratorKickoffAsync(resumedExistingConversation);
                return true;
            }
            catch (Exception ex)
            {
                AppLogger.Warn("OrchestratorMode", "Failed to start orchestrator mode", ex);
                AgentModeButton.IsChecked = false;
                _agentModeService.Stop("startup failed");
                await AddErrorMessageAsync(LocalizationService.S(
                    "orchestrator.startFailed",
                    "Orchestrator Mode could not start. Please try again."));
                return false;
            }
        }

        private void PersistOrchestratorConfigSnapshot(
            int tokenBudget,
            IEnumerable<string>? goals,
            IEnumerable<string>? selectedCategories)
        {
            var goalList = goals == null
                ? new List<string>()
                : goals
                    .Select(goal => goal?.Trim() ?? string.Empty)
                    .Where(goal => !string.IsNullOrWhiteSpace(goal))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

            var categoryList = selectedCategories == null || !selectedCategories.Any()
                ? ToolCategoryCatalog.Options.Select(option => option.Id).ToList()
                : selectedCategories
                    .Select(category => category?.Trim() ?? string.Empty)
                    .Where(category => !string.IsNullOrWhiteSpace(category))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

            AppState.SetOrchestratorConfig(new OrchestratorConfigSnapshot(tokenBudget, goalList, categoryList));
        }

        private OrchestratorSessionSnapshot? TryLoadResumableOrchestratorSession(int? conversationId, UI.AgentModeConfigDialog.OrchestratorConfig config)
        {
            if (!conversationId.HasValue)
                return null;

            var session = AppState.GetOrchestratorSession(conversationId.Value);
            if (session == null)
                return null;

            var goalList = NormalizeGoals(config.Goals);
            var categoryList = NormalizeCategories(
                config.SelectedCategories.Count > 0 ? config.SelectedCategories : ToolCategoryCatalog.Options.Select(option => option.Id));

            if (session.TokenBudget != config.TokenBudget)
                return null;

            if (!GoalsMatch(session.Goals, goalList))
                return null;

            if (!CategoriesMatch(session.SelectedCategories, categoryList))
                return null;

            return session;
        }

        private async Task<bool> EnsureOrchestratorConversationAsync(IEnumerable<string>? goals, int? conversationId = null, bool createFresh = true)
        {
            var provider = ProviderComboBox.SelectedItem as Provider
                ?? ProviderComboBox.Items.OfType<Provider>()
                    .FirstOrDefault(p => p.Id == _currentProviderId);

            if (provider == null)
                return false;

            _orchestratorResumedExistingConversation = false;
            if (_currentConversationId.HasValue)
                return true;

            if (!createFresh && conversationId.HasValue)
            {
                var conversation = await _conversationApplicationService.GetConversationAsync(conversationId.Value);
                if (conversation != null)
                {
                    _currentConversationId = conversationId;
                    await LoadConversationMessages(conversationId.Value, syncProviderSelection: true);
                    _orchestratorResumedExistingConversation = true;
                    return true;
                }
            }

            var title = LocalizationService.S("orchestrator.conversationTitle", "Orchestrator");
            var goalsText = goals == null
                ? LocalizationService.S("orchestrator.noGoals", "No goals set.")
                : string.Join(Environment.NewLine, goals.Select(goal => $"• {goal}"));

            await ConversationFlow.CreateConversationAsync(
                provider,
                title,
                string.Format(
                    LocalizationService.S("orchestrator.newConversation", "Starting a fresh conversation for these goals:\n{0}"),
                    goalsText));

            if (_sidebarOpen)
                await RefreshSidebarAsync();

            return true;
        }

        private void PersistOrchestratorSessionSnapshot(int conversationId, string? lastNarrative = null)
        {
            if (_agentModeService == null)
                return;

            AppState.SetOrchestratorSession(_agentModeService.BuildSnapshot(conversationId, lastNarrative));
        }

        private void PersistOrchestratorSessionSnapshot(string? lastNarrative = null)
        {
            if (_currentConversationId.HasValue)
                PersistOrchestratorSessionSnapshot(_currentConversationId.Value, lastNarrative);
        }

        private async Task PauseOrchestratorConversationAsync(int? conversationId, string? reason = null, bool clearSessionStatus = true, bool refreshSidebar = false)
        {
            if (_agentModeService == null)
                return;

            _agentModeService.Stop(reason);
            UpdateAgentModeTooltip();
            AgentModeButton.IsChecked = false;
            StopAgentModeRefreshTimer();

            var targetConversationId = conversationId ?? _currentConversationId;
            if (targetConversationId.HasValue)
            {
                PersistOrchestratorSessionSnapshot(targetConversationId.Value, reason);
                if (clearSessionStatus)
                    await _conversationApplicationService.UpdateConversationOrchestratorModeAsync(targetConversationId.Value, false);
            }

            if (refreshSidebar && _sidebarOpen)
                await RefreshSidebarAsync(ConversationSidebar.SearchText.Trim());
        }

        private async Task SetConversationOrchestratorModeAsync(int conversationId, bool enabled, bool refreshSidebar = false)
        {
            if (conversationId <= 0)
                return;

            await _conversationApplicationService.UpdateConversationOrchestratorModeAsync(conversationId, enabled);
            if (refreshSidebar && _sidebarOpen)
                await RefreshSidebarAsync(ConversationSidebar.SearchText.Trim());
        }

        private static List<string> NormalizeGoals(IEnumerable<string>? goals)
        {
            if (goals == null)
                return new List<string>();

            return goals
                .Select(goal => goal?.Trim() ?? string.Empty)
                .Where(goal => !string.IsNullOrWhiteSpace(goal))
                .ToList();
        }

        private static List<string> NormalizeCategories(IEnumerable<string>? categories)
        {
            if (categories == null)
                return new List<string>();

            return categories
                .Select(category => category?.Trim() ?? string.Empty)
                .Where(category => !string.IsNullOrWhiteSpace(category))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(category => category, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static bool GoalsMatch(IReadOnlyList<string> first, IReadOnlyList<string> second)
        {
            if (first.Count != second.Count)
                return false;

            for (var i = 0; i < first.Count; i++)
            {
                if (!string.Equals(first[i]?.Trim(), second[i]?.Trim(), StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            return true;
        }

        private static bool CategoriesMatch(IReadOnlyList<string> first, IReadOnlyList<string> second)
        {
            IEnumerable<string> Expand(IReadOnlyList<string> values)
                => values.Count == 0
                    ? ToolCategoryCatalog.Options.Select(option => option.Id)
                    : values;

            var firstValues = Expand(first)
                .Select(category => category.Trim())
                .OrderBy(category => category, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var secondValues = Expand(second)
                .Select(category => category.Trim())
                .OrderBy(category => category, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return firstValues.SequenceEqual(secondValues, StringComparer.OrdinalIgnoreCase);
        }

        private void UpdateAgentModeTooltip()
        {
            if (_agentModeService == null)
                return;

            AgentModeButton.ToolTip = _agentModeService.IsActive
                ? LocalizationService.S("tooltip.orchestratorOn", "Orchestrator Mode active — goals are being worked and allowed tools run automatically")
                : LocalizationService.S("tooltip.orchestratorOff", "Orchestrator Mode — click to start a goal-driven session");
        }

        private async Task StartOrchestratorKickoffAsync(bool resumedExistingConversation)
        {
            if (_agentModeService == null || !_agentModeService.IsActive || _isProcessing)
                return;

            try
            {
                var kickoffText = resumedExistingConversation
                    ? BuildOrchestratorResumeText()
                    : BuildOrchestratorKickoffText();
                if (!string.IsNullOrWhiteSpace(kickoffText))
                    await AddOrchestratorNarrativeAsync(kickoffText, persistToConversation: true);

                _conversationHistory.Add(new ProviderChatMessage
                {
                    Role = "user",
                    Content = BuildOrchestratorKickoffUserPrompt(resumedExistingConversation)
                });

                _aiCancellationTokenSource?.Cancel();
                _aiCancellationTokenSource?.Dispose();
                _aiCancellationTokenSource = new System.Threading.CancellationTokenSource();

                await RunAiTurnAsync(cancellationToken: _aiCancellationTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                // Stop button or another session change cancelled the kickoff.
            }
            catch (Exception ex)
            {
                AppLogger.Warn("OrchestratorMode", "Kickoff failed", ex);
            }
        }

        private string BuildOrchestratorStartedMessage()
        {
            if (_agentModeService == null)
                return string.Empty;

            var goals = _agentModeService.Goals.Count == 0
                ? LocalizationService.S("orchestrator.noGoals", "No goals set.")
                : string.Join(Environment.NewLine, _agentModeService.Goals.Select(goal => $"• {goal}"));

            return string.Format(
                LocalizationService.S("orchestrator.startedChat", "I’m starting a new Orchestrator session. I’ll work through these goals and keep you updated:\n{0}"),
                goals);
        }

        private string BuildOrchestratorResumedMessage()
        {
            if (_agentModeService == null)
                return string.Empty;

            var goals = _agentModeService.Goals.Count == 0
                ? LocalizationService.S("orchestrator.noGoals", "No goals set.")
                : string.Join(Environment.NewLine, _agentModeService.Goals.Select(goal => $"• {goal}"));

            return string.Format(
                LocalizationService.S("orchestrator.resumedChat", "I’m resuming the saved Orchestrator session and picking up from the last checkpoint.\nRemaining goals:\n{0}"),
                goals);
        }

        private string BuildOrchestratorRestartedMessage()
        {
            if (_agentModeService == null)
                return string.Empty;

            var goals = _agentModeService.Goals.Count == 0
                ? LocalizationService.S("orchestrator.noGoals", "No goals set.")
                : string.Join(Environment.NewLine, _agentModeService.Goals.Select(goal => $"• {goal}"));

            return string.Format(
                LocalizationService.S("orchestrator.restartedChat", "I found saved Orchestrator goals, but I’m starting a new conversation for them.\nGoals:\n{0}"),
                goals);
        }

        private string BuildOrchestratorKickoffText()
        {
            if (_agentModeService == null)
                return string.Empty;

            var goals = _agentModeService.Goals.Count == 0
                ? LocalizationService.S("orchestrator.noGoals", "No goals set.")
                : string.Join("\n", _agentModeService.Goals.Select(goal => $"• {goal}"));

            return string.Format(
                LocalizationService.S("orchestrator.kickoff", "Orchestrator Mode is starting with these goals:\n{0}"),
                goals);
        }

        private string BuildOrchestratorResumeText()
        {
            return LocalizationService.S(
                "orchestrator.resumeKickoff",
                "I’m picking up the saved Orchestrator session. Continue from the remaining goals and keep the user informed in plain language.");
        }

        private string BuildOrchestratorKickoffUserPrompt(bool resumed)
        {
            if (_agentModeService == null)
                return "Please start.";

            var goals = _agentModeService.Goals.Count == 0
                ? "No goals were provided."
                : string.Join("\n", _agentModeService.Goals.Select(goal => $"- {goal}"));

            var filesystemTools =
                "- filesystem tools: list_directory, read_file, write_file, create_directory, delete_file, move_file, search_files, search_file_content, open_file, execute_command";

            var mouseTools =
                "- mouse tools: take_screenshot, mouse_move, mouse_click, mouse_double_click, mouse_drag, mouse_scroll";

            var webTools =
                "- browser tools: open_url, http_request, open_browser_tab, read_browser_tab, switch_browser_tab, close_browser_tab, get_browser_html, execute_browser_script, get_browser_cookies";

            var systemTools =
                "- system tools: get_clipboard, set_clipboard, show_notification, get_system_info, get_running_processes, get_active_window, get_selected_text, remember, recall, set_reminder";

            return
                (resumed
                    ? "Resume Orchestrator Mode now. Continue from the saved conversation and keep the existing goals in mind.\n"
                    : "Start Orchestrator Mode now.\n") +
                "Work through the goals below step by step, use the allowed tools automatically, and keep the user informed in short, readable explanations.\n" +
                "Before a major step, explain what you are going to do and why.\n" +
                "Prefer tools over guessing. For file and folder work, use the filesystem tools first. For desktop visuals or screenshots, use the mouse tools first. For web text or page content, use the browser tools. For desktop state, use the system tools.\n" +
                "If the task is about what is visible on the screen, what page is shown, or a screenshot, the first tool must be take_screenshot. Do not use read_browser_tab or get_browser_html to fake a screenshot.\n" +
                "For screenshot or visible-screen goals, do not list browser tabs or read browser text first. Go straight to take_screenshot.\n" +
                "If a goal needs multiple steps, continue until the result is complete or you are blocked.\n" +
                "If a filesystem goal needs to produce a file, write the file directly and confirm its path.\n" +
                "If a goal fails, try a different tool or a different wording before giving up.\n" +
                "Treat any new user message as steering input and adapt the session when the user adds clarification.\n" +
                $"{filesystemTools}\n" +
                $"{mouseTools}\n" +
                $"{webTools}\n" +
                $"{systemTools}\n" +
                "Goals:\n" +
                goals;
        }

        private void StartAgentModeRefreshTimer()
        {
            StopAgentModeRefreshTimer();
            _agentModeRefreshTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            _agentModeRefreshTimer.Tick += (_, _) =>
            {
                if (_agentModeService?.IsActive == true)
                    PersistOrchestratorSessionSnapshot();
            };
            _agentModeRefreshTimer.Start();
        }

        private void StopAgentModeRefreshTimer()
        {
            _agentModeRefreshTimer?.Stop();
            _agentModeRefreshTimer = null;
        }
    }
}
