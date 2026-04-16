using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Timers;
using Aire.Providers;

namespace Aire.Services
{
    /// <summary>
    /// Session-scoped orchestrator mode: when active, the app can keep running a goal-driven
    /// workflow with tool auto-approval, heartbeat updates, retry tracking, and token budgeting.
    /// </summary>
    public class OrchestratorModeService
    {
        private static readonly Lazy<IReadOnlyDictionary<string, string>> ToolCategoryLookupLazy =
            new(BuildToolCategoryLookup);

        private readonly object _gate = new();
        private readonly Dictionary<string, HashSet<string>> _failureVariants = new(StringComparer.OrdinalIgnoreCase);
        private System.Timers.Timer? _heartbeatTimer;
        private int _tokenBudget;
        private int _tokensConsumed;
        private int _heartbeatCount;
        private HashSet<string>? _allowedCategories;
        private List<string> _goals = new();
        private bool _isActive;
        private string? _stopReason;
        private TimeSpan _heartbeatInterval = TimeSpan.FromSeconds(2);

        /// <summary>Whether orchestrator mode is currently active.</summary>
        public bool IsActive => _isActive;

        /// <summary>Maximum tokens the orchestrator may consume (0 = unlimited).</summary>
        public int TokenBudget => _tokenBudget;

        /// <summary>Tokens consumed so far in this orchestrator session.</summary>
        public int TokensConsumed => _tokensConsumed;

        /// <summary>Tool categories allowed during orchestrator mode (null = all).</summary>
        public HashSet<string>? AllowedCategories => _allowedCategories;

        /// <summary>Goals supplied by the user for the orchestrator to work through.</summary>
        public IReadOnlyList<string> Goals => _goals;

        /// <summary>How many heartbeat ticks have been emitted since activation.</summary>
        public int HeartbeatCount => _heartbeatCount;

        /// <summary>Reason the orchestrator stopped, if it stopped because of a limit or blockage.</summary>
        public string? StopReason => _stopReason;

        /// <summary>Fires when <see cref="IsActive"/> changes.</summary>
        public event Action? ModeChanged;

        /// <summary>Fires on each heartbeat tick while orchestrator mode is active.</summary>
        public event Action<int>? Heartbeat;

        /// <summary>Fires when the token budget is exhausted and orchestrator mode stops.</summary>
        public event Action? BudgetExhausted;

        /// <summary>Fires when repeated failures block the orchestrator and user guidance is needed.</summary>
        public event Action<string>? Blocked;

        /// <summary>Fires when the goal list changes.</summary>
        public event Action? GoalsChanged;

        /// <summary>Fires when one goal is explicitly marked complete.</summary>
        public event Action<string>? GoalCompleted;

        /// <summary>
        /// Activates orchestrator mode with the given budget, optional category filter, and optional goals.
        /// </summary>
        /// <param name="tokenBudget">Max tokens (0 = unlimited).</param>
        /// <param name="allowedCategories">Allowed tool categories (null = all).</param>
        /// <param name="goals">User-defined goal list to keep on the orchestrator state.</param>
        /// <param name="heartbeatInterval">Optional heartbeat cadence. Defaults to 2 seconds.</param>
        public virtual void Start(
            int tokenBudget = 0,
            HashSet<string>? allowedCategories = null,
            IEnumerable<string>? goals = null,
            TimeSpan? heartbeatInterval = null,
            OrchestratorSessionSnapshot? restoreSnapshot = null)
        {
            lock (_gate)
            {
                StopTimer_NoLock();
                _heartbeatInterval = NormalizeHeartbeatInterval(heartbeatInterval);

                if (restoreSnapshot != null)
                {
                    _tokenBudget = restoreSnapshot.TokenBudget;
                    _tokensConsumed = Math.Max(0, restoreSnapshot.TokensConsumed);
                    _heartbeatCount = Math.Max(0, restoreSnapshot.HeartbeatCount);
                    _allowedCategories = restoreSnapshot.SelectedCategories.Count == 0
                        ? null
                        : new HashSet<string>(restoreSnapshot.SelectedCategories, StringComparer.OrdinalIgnoreCase);
                    _goals = NormalizeGoals(restoreSnapshot.Goals);
                    _stopReason = string.IsNullOrWhiteSpace(restoreSnapshot.StopReason)
                        ? null
                        : restoreSnapshot.StopReason;
                    _failureVariants.Clear();
                    foreach (var (taskKey, variants) in restoreSnapshot.FailureVariants ?? new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase))
                    {
                        if (string.IsNullOrWhiteSpace(taskKey))
                            continue;

                        _failureVariants[taskKey] = new HashSet<string>(
                            variants?.Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v.Trim()) ?? Enumerable.Empty<string>(),
                            StringComparer.OrdinalIgnoreCase);
                    }
                }
                else
                {
                    _tokenBudget = tokenBudget;
                    _tokensConsumed = 0;
                    _heartbeatCount = 0;
                    _allowedCategories = allowedCategories == null
                        ? null
                        : new HashSet<string>(allowedCategories, StringComparer.OrdinalIgnoreCase);
                    _goals = NormalizeGoals(goals);
                    _stopReason = null;
                    _failureVariants.Clear();
                }

                _isActive = true;
                StartTimer_NoLock();
            }

            GoalsChanged?.Invoke();
            ModeChanged?.Invoke();
        }

        /// <summary>Deactivates orchestrator mode and resets counters.</summary>
        public virtual void Stop(string? reason = null)
        {
            lock (_gate)
            {
                if (!_isActive)
                    return;

                StopTimer_NoLock();
                _isActive = false;
                _stopReason = reason;
            }

            ModeChanged?.Invoke();
        }

        /// <summary>
        /// Checks whether a tool should be auto-approved.
        /// Returns true when orchestrator mode is active and the tool's category is allowed.
        /// </summary>
        public virtual bool ShouldAutoApprove(string toolName)
        {
            if (!_isActive || string.IsNullOrWhiteSpace(toolName))
                return false;

            if (_allowedCategories == null)
                return true;

            if (_allowedCategories.Count == 0)
                return false;

            if (!TryResolveToolCategory(toolName, out var category))
                return false;

            return category != null && _allowedCategories.Contains(category);
        }

        private static bool TryResolveToolCategory(string toolName, out string? category)
        {
            var canonicalTool = ToolExecutionMetadata.NormalizeToolName(toolName);
            if (string.IsNullOrWhiteSpace(canonicalTool))
            {
                category = null;
                return false;
            }

            if (ToolCategoryLookupLazy.Value.TryGetValue(canonicalTool, out category))
                return true;

            if (ToolExecutionMetadata.IsMouseTool(canonicalTool))
            {
                category = "mouse";
                return true;
            }

            if (ToolExecutionMetadata.IsKeyboardTool(canonicalTool))
            {
                category = "keyboard";
                return true;
            }

            category = null;
            return false;
        }

        /// <summary>
        /// Records token usage. Auto-stops if budget is exceeded.
        /// </summary>
        public virtual void RecordTokenUsage(int tokens)
        {
            if (!_isActive || tokens <= 0)
                return;

            bool exhausted = false;
            lock (_gate)
            {
                if (!_isActive)
                    return;

                _tokensConsumed += tokens;
                exhausted = _tokenBudget > 0 && _tokensConsumed >= _tokenBudget;
                if (exhausted)
                {
                    _stopReason = "token budget exhausted";
                    StopTimer_NoLock();
                    _isActive = false;
                }
            }

            if (exhausted)
            {
                BudgetExhausted?.Invoke();
                ModeChanged?.Invoke();
            }
        }

        /// <summary>
        /// Marks a goal as complete. When no goals remain the orchestrator stops automatically.
        /// </summary>
        public virtual void MarkGoalCompleted(string goal)
        {
            if (string.IsNullOrWhiteSpace(goal))
                return;

            bool finished = false;
            lock (_gate)
            {
                if (!_isActive)
                    return;

                finished = RemoveGoal_NoLock(goal);
                if (finished && _goals.Count == 0)
                {
                    _stopReason = "goals completed";
                    StopTimer_NoLock();
                    _isActive = false;
                }
            }

            if (finished)
            {
                GoalCompleted?.Invoke(goal);
                GoalsChanged?.Invoke();
                if (!_isActive)
                    ModeChanged?.Invoke();
            }
        }

        /// <summary>
        /// Records a failed attempt for a task or goal. If the same task fails in three distinct
        /// ways, the orchestrator blocks and asks the user for guidance.
        /// </summary>
        public virtual void RecordTaskFailure(string taskKey, string failureSignature)
        {
            if (!_isActive || string.IsNullOrWhiteSpace(taskKey))
                return;

            string? blockedMessage = null;
            lock (_gate)
            {
                if (!_isActive)
                    return;

                if (!_failureVariants.TryGetValue(taskKey, out var variants))
                {
                    variants = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    _failureVariants[taskKey] = variants;
                }

                if (!string.IsNullOrWhiteSpace(failureSignature))
                    variants.Add(failureSignature.Trim());

                if (variants.Count >= 3)
                {
                    _stopReason = $"blocked after repeated failures for '{taskKey}'";
                    StopTimer_NoLock();
                    _isActive = false;
                    blockedMessage = $"Orchestrator Mode is blocked on '{taskKey}' after three different failed attempts. Please review the goal or provide guidance.";
                }
            }

            if (blockedMessage != null)
            {
                Blocked?.Invoke(blockedMessage);
                ModeChanged?.Invoke();
            }
        }

        /// <summary>
        /// Resets failure tracking for a task, for example after a successful retry path.
        /// </summary>
        public virtual void ClearTaskFailures(string taskKey)
        {
            if (string.IsNullOrWhiteSpace(taskKey))
                return;

            lock (_gate)
            {
                _failureVariants.Remove(taskKey);
            }
        }

        /// <summary>
        /// Returns the recorded failure signatures for a task so callers can avoid retrying
        /// providers or strategies that have already failed during this session.
        /// </summary>
        public virtual IReadOnlyCollection<string> GetTaskFailureSignatures(string taskKey)
        {
            if (string.IsNullOrWhiteSpace(taskKey))
                return Array.Empty<string>();

            lock (_gate)
            {
                if (!_failureVariants.TryGetValue(taskKey, out var variants) || variants.Count == 0)
                    return Array.Empty<string>();

                return variants.ToArray();
            }
        }

        /// <summary>
        /// Replaces the current goal list while staying active.
        /// </summary>
        public virtual void SetGoals(IEnumerable<string>? goals)
        {
            lock (_gate)
            {
                _goals = NormalizeGoals(goals);
            }

            GoalsChanged?.Invoke();
        }

        /// <summary>
        /// Builds a serializable snapshot of the current orchestrator session for persistence.
        /// </summary>
        /// <param name="conversationId">Current conversation id, if there is one.</param>
        /// <param name="lastNarrative">Latest human-readable narrative shown to the user, if any.</param>
        /// <returns>A snapshot of the current orchestrator runtime state.</returns>
        public virtual OrchestratorSessionSnapshot BuildSnapshot(int? conversationId = null, string? lastNarrative = null)
        {
            lock (_gate)
            {
                return new OrchestratorSessionSnapshot(
                    conversationId,
                    _tokenBudget,
                    _tokensConsumed,
                    _heartbeatCount,
                    _goals.ToList(),
                    _allowedCategories?.OrderBy(category => category, StringComparer.OrdinalIgnoreCase).ToList() ?? new List<string>(),
                    _failureVariants.ToDictionary(
                        pair => pair.Key,
                        pair => pair.Value.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToList(),
                        StringComparer.OrdinalIgnoreCase),
                    _stopReason,
                    lastNarrative,
                    DateTimeOffset.UtcNow);
            }
        }

        private void StartTimer_NoLock()
        {
            _heartbeatTimer = new System.Timers.Timer(_heartbeatInterval.TotalMilliseconds)
            {
                AutoReset = true,
                Enabled = true
            };
            _heartbeatTimer.Elapsed += HeartbeatTimer_Elapsed;
            _heartbeatTimer.Start();
        }

        private void StopTimer_NoLock()
        {
            if (_heartbeatTimer == null)
                return;

            _heartbeatTimer.Elapsed -= HeartbeatTimer_Elapsed;
            _heartbeatTimer.Stop();
            _heartbeatTimer.Dispose();
            _heartbeatTimer = null;
        }

        private void HeartbeatTimer_Elapsed(object? sender, ElapsedEventArgs e)
        {
            if (!_isActive)
                return;

            var count = Interlocked.Increment(ref _heartbeatCount);
            Heartbeat?.Invoke(count);
        }

        private static TimeSpan NormalizeHeartbeatInterval(TimeSpan? heartbeatInterval)
        {
            var interval = heartbeatInterval ?? TimeSpan.FromSeconds(2);
            if (interval < TimeSpan.FromMilliseconds(250))
                return TimeSpan.FromMilliseconds(250);
            return interval;
        }

        private static List<string> NormalizeGoals(IEnumerable<string>? goals)
        {
            if (goals == null)
                return new List<string>();

            return goals
                .Select(g => g?.Trim() ?? string.Empty)
                .Where(g => !string.IsNullOrWhiteSpace(g))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private bool RemoveGoal_NoLock(string goal)
        {
            var index = _goals.FindIndex(g => string.Equals(g, goal, StringComparison.OrdinalIgnoreCase));
            if (index < 0)
                return false;

            _goals.RemoveAt(index);
            return true;
        }

        private static IReadOnlyDictionary<string, string> BuildToolCategoryLookup()
        {
            return SharedToolDefinitions.AllTools
                .GroupBy(tool => tool.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First().Category, StringComparer.OrdinalIgnoreCase);
        }
    }
}
