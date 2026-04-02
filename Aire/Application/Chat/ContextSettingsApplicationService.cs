using System;
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

            return source with
            {
                MaxMessages = maxMessages,
                AnchorMessages = anchorMessages,
                UncachedRecentMessages = uncachedRecentMessages,
                SummaryMaxCharacters = Math.Clamp(source.SummaryMaxCharacters, 160, 2000)
            };
        }
    }
}
