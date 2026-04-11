using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Aire.AppLayer.Abstractions;

namespace Aire.AppLayer.Chat
{
    /// <summary>
    /// Loads and persists typed context-window settings over the shared settings repository.
    /// </summary>
    public sealed class ContextSettingsApplicationService
    {
        private const string SettingKey = "context_window_settings";
        private readonly ISettingsRepository _settings;

        public ContextSettingsApplicationService(ISettingsRepository settings)
        {
            _settings = settings;
        }

        public async Task<ContextWindowSettings> LoadAsync()
        {
            var json = await _settings.GetSettingAsync(SettingKey);
            if (string.IsNullOrWhiteSpace(json))
                return ContextWindowSettings.Default;

            try
            {
                var parsed = JsonSerializer.Deserialize<ContextWindowSettings>(json);
                return Normalize(parsed);
            }
            catch
            {
                return ContextWindowSettings.Default;
            }
        }

        public Task SaveAsync(ContextWindowSettings settings)
            => _settings.SetSettingAsync(SettingKey, JsonSerializer.Serialize(Normalize(settings)));

        private static ContextWindowSettings Normalize(ContextWindowSettings? settings)
        {
            var source = settings ?? ContextWindowSettings.Default;
            var maxMessages = Math.Clamp(source.MaxMessages, 8, 200);
            var anchorMessages = Math.Clamp(source.AnchorMessages, 0, Math.Min(40, maxMessages));
            var uncachedRecentMessages = Math.Clamp(source.UncachedRecentMessages, 1, maxMessages);

            // Token-based settings validation
            int? maxTokens = source.MaxTokens;
            if (maxTokens.HasValue)
            {
                maxTokens = Math.Clamp(maxTokens.Value, 512, 2_000_000);
            }

            int anchorTokens = Math.Clamp(source.AnchorTokens, 0, maxTokens ?? 100_000);
            int tailTokens = Math.Clamp(source.TailTokens, 0, maxTokens ?? 100_000);

            // Ensure anchor+tail does not exceed maxTokens when maxTokens is set
            if (maxTokens.HasValue)
            {
                int total = anchorTokens + tailTokens;
                if (total > maxTokens.Value)
                {
                    // Scale down proportionally
                    double scale = (double)maxTokens.Value / total;
                    anchorTokens = (int)(anchorTokens * scale);
                    tailTokens = (int)(tailTokens * scale);
                    // Ensure at least one token for each if possible
                    if (anchorTokens == 0 && tailTokens > 0) anchorTokens = 1;
                    if (tailTokens == 0 && anchorTokens > 0) tailTokens = 1;
                }
            }

            IReadOnlyDictionary<string, int>? perMessageTypeLimits = source.PerMessageTypeLimits;
            if (perMessageTypeLimits != null)
            {
                var normalized = new Dictionary<string, int>();
                foreach (var kvp in perMessageTypeLimits)
                {
                    if (string.IsNullOrWhiteSpace(kvp.Key)) continue;
                    // Clamp each limit between 1 and 100_000
                    int limit = Math.Clamp(kvp.Value, 1, 100_000);
                    normalized[kvp.Key.Trim()] = limit;
                }
                perMessageTypeLimits = normalized;
            }

            return source with
            {
                MaxMessages = maxMessages,
                AnchorMessages = anchorMessages,
                UncachedRecentMessages = uncachedRecentMessages,
                SummaryMaxCharacters = Math.Clamp(source.SummaryMaxCharacters, 160, 2000),
                MaxTokens = maxTokens,
                AnchorTokens = anchorTokens,
                TailTokens = tailTokens,
                EnableGradualCompaction = source.EnableGradualCompaction,
                CompactionTokenThreshold = Math.Clamp(source.CompactionTokenThreshold, 0, 10_000_000),
                PerMessageTypeLimits = perMessageTypeLimits
                // Note: EnableTokenAwareTruncation, EnableToolFocusWindow, EnableRetryFollowUpWindow are booleans, no normalization needed
            };
        }
    }
}
