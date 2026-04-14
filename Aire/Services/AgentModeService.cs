using System;
using System.Collections.Generic;

namespace Aire.Services
{
    /// <summary>
    /// Session-scoped agent mode: when active, tool calls are auto-approved
    /// without showing the approval UI, subject to category and token-budget limits.
    /// </summary>
    public sealed class AgentModeService
    {
        private int _tokenBudget;
        private int _tokensConsumed;
        private HashSet<string>? _allowedCategories;
        private bool _isActive;

        /// <summary>Whether agent mode is currently active.</summary>
        public bool IsActive => _isActive;

        /// <summary>Maximum tokens the agent may consume (0 = unlimited).</summary>
        public int TokenBudget => _tokenBudget;

        /// <summary>Tokens consumed so far in this agent session.</summary>
        public int TokensConsumed => _tokensConsumed;

        /// <summary>Tool categories allowed during agent mode (null = all).</summary>
        public HashSet<string>? AllowedCategories => _allowedCategories;

        /// <summary>Fires when <see cref="IsActive"/> changes.</summary>
        public event Action? ModeChanged;

        /// <summary>Fires when the token budget is exhausted and agent mode auto-stops.</summary>
        public event Action? BudgetExhausted;

        /// <summary>
        /// Activates agent mode with the given budget and optional category filter.
        /// </summary>
        /// <param name="tokenBudget">Max tokens (0 = unlimited).</param>
        /// <param name="allowedCategories">Allowed tool categories (null = all).</param>
        public void Start(int tokenBudget = 0, HashSet<string>? allowedCategories = null)
        {
            _tokenBudget = tokenBudget;
            _tokensConsumed = 0;
            _allowedCategories = allowedCategories;
            _isActive = true;
            ModeChanged?.Invoke();
        }

        /// <summary>Deactivates agent mode and resets counters.</summary>
        public void Stop()
        {
            if (!_isActive) return;
            _isActive = false;
            _tokenBudget = 0;
            _tokensConsumed = 0;
            _allowedCategories = null;
            ModeChanged?.Invoke();
        }

        /// <summary>
        /// Checks whether a tool should be auto-approved.
        /// Returns true when agent mode is active and the tool's category is allowed.
        /// </summary>
        public bool ShouldAutoApprove(string toolName)
        {
            if (!_isActive) return false;

            if (_allowedCategories == null || _allowedCategories.Count == 0)
                return true;

            // Match by tool name prefix (e.g. "read_file" -> "file", "take_screenshot" -> "system")
            foreach (var cat in _allowedCategories)
            {
                if (toolName.Contains(cat, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Records token usage. Auto-stops if budget is exceeded.
        /// </summary>
        public void RecordTokenUsage(int tokens)
        {
            if (!_isActive || tokens <= 0) return;

            _tokensConsumed += tokens;

            if (_tokenBudget > 0 && _tokensConsumed >= _tokenBudget)
            {
                _isActive = false;
                BudgetExhausted?.Invoke();
                ModeChanged?.Invoke();
            }
        }
    }
}
