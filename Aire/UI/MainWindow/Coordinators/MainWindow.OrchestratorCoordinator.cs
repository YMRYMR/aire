using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Aire.Data;
using Aire.Domain.Tools;
using Aire.Providers;
using Aire.Services;
using ProviderChatMessage = Aire.Providers.ChatMessage;

namespace Aire
{
    public partial class MainWindow
    {
        private OrchestratorCoordinator? _orchestratorCoordinator;
        private OrchestratorCoordinator OrchestratorFlow => _orchestratorCoordinator ??= new OrchestratorCoordinator(this);

        /// <summary>
        /// Coordinates orchestrator / agent mode lifecycle: start, stop, pause,
        /// session persistence, timer management, narrative messages, and prompt assembly.
        /// </summary>
        private sealed partial class OrchestratorCoordinator
        {
            private readonly MainWindow _owner;
            private System.Windows.Threading.DispatcherTimer? _refreshTimer;

            public OrchestratorCoordinator(MainWindow owner)
            {
                _owner = owner;
            }

            // ── Public API (called from MainWindow forwarding wrappers) ──────────

            public async Task<bool> StartOrchestratorModeAsync(UI.AgentModeConfigDialog.OrchestratorConfig config)
            {
                if (_owner._agentModeService == null)
                    return false;

                try
                {
                    PersistOrchestratorConfigSnapshot(config.TokenBudget, config.Goals, config.SelectedCategories);
                    var resumeState = TryLoadResumableOrchestratorSession(_owner._currentConversationId, config);
                    var hasSavedSession = resumeState != null;

                    if (!await EnsureOrchestratorConversationAsync(config.Goals, resumeState?.ConversationId, !hasSavedSession))
                    {
                        _owner._agentModeService.Stop("no provider selected");
                        await _owner.AddErrorMessageAsync(LocalizationService.S(
                            "orchestrator.noProvider",
                            "Orchestrator Mode needs an AI provider selected before it can start."));
                        return false;
                    }

                    _owner._agentModeService.Start(
                        config.TokenBudget,
                        config.SelectedCategories.Count > 0 ? config.SelectedCategories : null,
                        config.Goals,
                        restoreSnapshot: resumeState);

                    _owner._orchestratorResumedExistingConversation =
                        hasSavedSession &&
                        _owner._currentConversationId.HasValue &&
                        resumeState?.ConversationId == _owner._currentConversationId.Value;
                    var resumedExistingConversation = _owner._orchestratorResumedExistingConversation;

                    if (_owner._currentConversationId.HasValue)
                        await SetConversationOrchestratorModeAsync(_owner._currentConversationId.Value, true, refreshSidebar: true);

                    PersistOrchestratorSessionSnapshot(
                        _owner._currentConversationId!.Value,
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
                    _owner.AgentModeButton.IsChecked = true;
                    StartRefreshTimer();
                    await _owner.AddOrchestratorNarrativeAsync(resumedExistingConversation
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
                    _owner.AgentModeButton.IsChecked = false;
                    _owner._agentModeService.Stop("startup failed");
                    await _owner.AddErrorMessageAsync(LocalizationService.S(
                        "orchestrator.startFailed",
                        "Orchestrator Mode could not start. Please try again."));
                    return false;
                }
            }

            public void StopOrchestratorMode(string? reason = null)
            {
                if (_owner._agentModeService == null)
                    return;

                _owner._aiCancellationTokenSource?.Cancel();
                var conversationId = _owner._currentConversationId;
                _ = PauseOrchestratorConversationAsync(conversationId, reason, clearSessionStatus: true, refreshSidebar: true);

                if (string.Equals(reason, "user stopped", StringComparison.OrdinalIgnoreCase))
                {
                    _ = _owner.AddOrchestratorNarrativeAsync(LocalizationService.S(
                        "orchestrator.paused",
                        "Orchestrator Mode paused. I saved the current session so it can resume later."));
                }
            }

            public async Task PauseOrchestratorConversationAsync(int? conversationId, string? reason = null, bool clearSessionStatus = true, bool refreshSidebar = false)
            {
                if (_owner._agentModeService == null)
                    return;

                _owner._agentModeService.Stop(reason);
                UpdateAgentModeTooltip();
                _owner.AgentModeButton.IsChecked = false;
                StopRefreshTimer();

                var targetConversationId = conversationId ?? _owner._currentConversationId;
                if (targetConversationId.HasValue)
                {
                    PersistOrchestratorSessionSnapshot(targetConversationId.Value, reason);
                    if (clearSessionStatus)
                        await _owner._conversationApplicationService.UpdateConversationOrchestratorModeAsync(targetConversationId.Value, false);
                }

                if (refreshSidebar && _owner._sidebarOpen)
                    await _owner.RefreshSidebarAsync(_owner.ConversationSidebar.SearchText.Trim());
            }

            public void PersistOrchestratorConfigSnapshot(
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

            public void PersistOrchestratorSessionSnapshot(int conversationId, string? lastNarrative = null)
            {
                if (_owner._agentModeService == null)
                    return;

                AppState.SetOrchestratorSession(_owner._agentModeService.BuildSnapshot(conversationId, lastNarrative));
            }

            public void PersistOrchestratorSessionSnapshot(string? lastNarrative = null)
            {
                if (_owner._currentConversationId.HasValue)
                    PersistOrchestratorSessionSnapshot(_owner._currentConversationId.Value, lastNarrative);
            }

            public void UpdateAgentModeTooltip()
            {
                if (_owner._agentModeService == null)
                    return;

                _owner.AgentModeButton.ToolTip = _owner._agentModeService.IsActive
                    ? LocalizationService.S("tooltip.orchestratorOn", "Orchestrator Mode active \u2014 goals are being worked and allowed tools run automatically")
                    : LocalizationService.S("tooltip.orchestratorOff", "Orchestrator Mode \u2014 click to start a goal-driven session");
            }

            public string BuildOrchestratorModePromptSection()
            {
                if (_owner._agentModeService == null || !_owner._agentModeService.IsActive)
                    return string.Empty;

                var goals = _owner._agentModeService.Goals.Count == 0
                    ? "No goals currently set."
                    : string.Join("\n", _owner._agentModeService.Goals.Select(goal => $"\u2022 {goal}"));

                var categories = _owner._agentModeService.AllowedCategories == null || _owner._agentModeService.AllowedCategories.Count == 0
                    ? "All tool categories are allowed."
                    : $"Allowed tool categories: {string.Join(", ", _owner._agentModeService.AllowedCategories.OrderBy(c => c))}.";

                var stopReason = string.IsNullOrWhiteSpace(_owner._agentModeService.StopReason)
                    ? "Continue until the goals are complete, the user stops the mode, or you become blocked."
                    : $"Last stop reason: {_owner._agentModeService.StopReason}.";

                return
                    "ORCHESTRATOR MODE:\n" +
                    "You are running an autonomous goal-driven session. Work through the goals below, use allowed tools automatically, and keep the user informed in plain language.\n" +
                    "Before a meaningful action, briefly explain what you are going to do and why.\n" +
                    "Treat new user messages as steering input and adapt the plan when the user adds clarification.\n" +
                    $"Goals:\n{goals}\n" +
                    $"{categories}\n" +
                    $"{stopReason}\n" +
                    "If a goal is completed, say so clearly and move to the next one. If you are blocked after repeated failures, ask the user for guidance.\n" +
                    "If the session was resumed after a stop or crash, continue from the saved checkpoint instead of restarting from scratch.";
            }

            public string BuildOrchestratorRecoveryModePromptSection()
            {
                if (_owner._agentModeService == null || !_owner._agentModeService.IsActive)
                    return string.Empty;

                return
                    "ORCHESTRATOR RECOVERY:\n" +
                    "Continue from the interrupted step using the smallest possible reply.\n" +
                    "If you need a tool, emit only the next tool call.\n" +
                    "Do not repeat prior narration or restate the goals unless it is necessary to resume safely.";
            }

            public void InitializeHandlers()
            {
                if (_owner._agentModeService == null)
                    return;

                _owner._agentModeService.BudgetExhausted += () =>
                {
                    _owner.Dispatcher.Invoke(async () =>
                    {
                        StopOrchestratorMode("token budget exhausted");
                        await _owner.AddOrchestratorNarrativeAsync(LocalizationService.S(
                            "orchestrator.budgetExhausted",
                            "Orchestrator Mode stopped: token budget exhausted. I will wait here until you change the budget or stop the session."));
                    });
                };

                _owner._agentModeService.Blocked += message =>
                {
                    _owner.Dispatcher.Invoke(async () =>
                    {
                        StopOrchestratorMode("blocked");
                        await _owner.AddOrchestratorNarrativeAsync(message);
                        _owner._ttsService.Speak(message);
                    });
                };

                _owner._agentModeService.GoalsChanged += () => _owner.Dispatcher.Invoke(() => PersistOrchestratorSessionSnapshot());
                _owner._agentModeService.GoalCompleted += goal => _owner.Dispatcher.Invoke(async () =>
                {
                    PersistOrchestratorSessionSnapshot();
                    await _owner.AddOrchestratorNarrativeAsync(string.Format(
                        LocalizationService.S("orchestrator.goalComplete", "Goal completed: {0}. I\u2019m moving on to the remaining goals."),
                        goal));
                });
                _owner._agentModeService.Heartbeat += _ => _owner.Dispatcher.Invoke(() => PersistOrchestratorSessionSnapshot());

                _owner._agentModeService.ModeChanged += () =>
                {
                    _owner.Dispatcher.Invoke(() =>
                    {
                        UpdateAgentModeTooltip();
                        if (_owner._agentModeService.IsActive)
                            StartRefreshTimer();
                        else
                        {
                            StopRefreshTimer();
                            if (string.Equals(_owner._agentModeService.StopReason, "goals completed", StringComparison.OrdinalIgnoreCase))
                            {
                                var doneMessage = LocalizationService.S(
                                    "orchestrator.goalsComplete",
                                    "Orchestrator Mode finished all goals.");
                                _ = _owner.AddOrchestratorNarrativeAsync(doneMessage);
                                _owner._ttsService.Speak(doneMessage);
                            }
                        }

                        PersistOrchestratorSessionSnapshot();
                    });
                };
            }

            // ── Private helpers ──────────────────────────────────────────────────

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
                var provider = _owner.ProviderComboBox.SelectedItem as Provider
                    ?? _owner.ProviderComboBox.Items.OfType<Provider>()
                        .FirstOrDefault(p => p.Id == _owner._currentProviderId);

                if (provider == null)
                    return false;

                _owner._orchestratorResumedExistingConversation = false;
                if (_owner._currentConversationId.HasValue)
                    return true;

                if (!createFresh && conversationId.HasValue)
                {
                    var conversation = await _owner._conversationApplicationService.GetConversationAsync(conversationId.Value);
                    if (conversation != null)
                    {
                        _owner._currentConversationId = conversationId;
                        await _owner.LoadConversationMessages(conversationId.Value, syncProviderSelection: true);
                        _owner._orchestratorResumedExistingConversation = true;
                        return true;
                    }
                }

                var title = LocalizationService.S("orchestrator.conversationTitle", "Orchestrator");
                var goalsText = goals == null
                    ? LocalizationService.S("orchestrator.noGoals", "No goals set.")
                    : string.Join(Environment.NewLine, goals.Select(goal => $"\u2022 {goal}"));

                await _owner.ConversationFlow.CreateConversationAsync(
                    provider,
                    title,
                    string.Format(
                        LocalizationService.S("orchestrator.newConversation", "Starting a fresh conversation for these goals:\n{0}"),
                        goalsText));

                if (_owner._sidebarOpen)
                    await _owner.RefreshSidebarAsync();

                return true;
            }

            private async Task SetConversationOrchestratorModeAsync(int conversationId, bool enabled, bool refreshSidebar = false)
            {
                if (conversationId <= 0)
                    return;

                await _owner._conversationApplicationService.UpdateConversationOrchestratorModeAsync(conversationId, enabled);
                if (refreshSidebar && _owner._sidebarOpen)
                    await _owner.RefreshSidebarAsync(_owner.ConversationSidebar.SearchText.Trim());
            }

            private async Task StartOrchestratorKickoffAsync(bool resumedExistingConversation)
            {
                if (_owner._agentModeService == null || !_owner._agentModeService.IsActive || _owner._isProcessing)
                    return;

                try
                {
                    var kickoffText = resumedExistingConversation
                        ? BuildOrchestratorResumeText()
                        : BuildOrchestratorKickoffText();
                    if (!string.IsNullOrWhiteSpace(kickoffText))
                        await _owner.AddOrchestratorNarrativeAsync(kickoffText, persistToConversation: true);

                    _owner._conversationHistory.Add(new ProviderChatMessage
                    {
                        Role = "user",
                        Content = BuildOrchestratorKickoffUserPrompt(resumedExistingConversation)
                    });

                    _owner._aiCancellationTokenSource?.Cancel();
                    _owner._aiCancellationTokenSource?.Dispose();
                    _owner._aiCancellationTokenSource = new System.Threading.CancellationTokenSource();

                    await _owner.RunAiTurnAsync(cancellationToken: _owner._aiCancellationTokenSource.Token);
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
                if (_owner._agentModeService == null)
                    return string.Empty;

                var goals = _owner._agentModeService.Goals.Count == 0
                    ? LocalizationService.S("orchestrator.noGoals", "No goals set.")
                    : string.Join(Environment.NewLine, _owner._agentModeService.Goals.Select(goal => $"\u2022 {goal}"));

                return string.Format(
                    LocalizationService.S("orchestrator.startedChat", "I\u2019m starting a new Orchestrator session. I\u2019ll work through these goals and keep you updated:\n{0}"),
                    goals);
            }

            private string BuildOrchestratorResumedMessage()
            {
                if (_owner._agentModeService == null)
                    return string.Empty;

                var goals = _owner._agentModeService.Goals.Count == 0
                    ? LocalizationService.S("orchestrator.noGoals", "No goals set.")
                    : string.Join(Environment.NewLine, _owner._agentModeService.Goals.Select(goal => $"\u2022 {goal}"));

                return string.Format(
                    LocalizationService.S("orchestrator.resumedChat", "I\u2019m resuming the saved Orchestrator session and picking up from the last checkpoint.\nRemaining goals:\n{0}"),
                    goals);
            }

            private string BuildOrchestratorRestartedMessage()
            {
                if (_owner._agentModeService == null)
                    return string.Empty;

                var goals = _owner._agentModeService.Goals.Count == 0
                    ? LocalizationService.S("orchestrator.noGoals", "No goals set.")
                    : string.Join(Environment.NewLine, _owner._agentModeService.Goals.Select(goal => $"\u2022 {goal}"));

                return string.Format(
                    LocalizationService.S("orchestrator.restartedChat", "I found saved Orchestrator goals, but I\u2019m starting a new conversation for them.\nGoals:\n{0}"),
                    goals);
            }

            private string BuildOrchestratorKickoffText()
            {
                if (_owner._agentModeService == null)
                    return string.Empty;

                var goals = _owner._agentModeService.Goals.Count == 0
                    ? LocalizationService.S("orchestrator.noGoals", "No goals set.")
                    : string.Join("\n", _owner._agentModeService.Goals.Select(goal => $"\u2022 {goal}"));

                return string.Format(
                    LocalizationService.S("orchestrator.kickoff", "Orchestrator Mode is starting with these goals:\n{0}"),
                    goals);
            }

            private string BuildOrchestratorResumeText()
            {
                return LocalizationService.S(
                    "orchestrator.resumeKickoff",
                    "I\u2019m picking up the saved Orchestrator session. Continue from the remaining goals and keep the user informed in plain language.");
            }

            private string BuildOrchestratorKickoffUserPrompt(bool resumed)
            {
                if (_owner._agentModeService == null)
                    return "Please start.";

                var goals = _owner._agentModeService.Goals.Count == 0
                    ? "No goals were provided."
                    : string.Join("\n", _owner._agentModeService.Goals.Select(goal => $"- {goal}"));

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

            private void StartRefreshTimer()
            {
                StopRefreshTimer();
                _refreshTimer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(2)
                };
                _refreshTimer.Tick += (_, _) =>
                {
                    if (_owner._agentModeService?.IsActive == true)
                        PersistOrchestratorSessionSnapshot();
                };
                _refreshTimer.Start();
            }

            private void StopRefreshTimer()
            {
                _refreshTimer?.Stop();
                _refreshTimer = null;
            }

            // ── Static helpers ──────────────────────────────────────────────────

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
        }
    }
}
