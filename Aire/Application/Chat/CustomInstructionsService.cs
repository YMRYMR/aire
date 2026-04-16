using Aire.AppLayer.Abstractions;

namespace Aire.AppLayer.Chat
{
    /// <summary>
    /// Loads and persists user-defined custom instructions that are injected
    /// into every conversation's system prompt.
    /// </summary>
    public sealed class CustomInstructionsService
    {
        private const string SettingKey = "custom_instructions";
        private readonly ISettingsRepository _settings;

        public CustomInstructionsService(ISettingsRepository settings)
        {
            _settings = settings;
        }

        public async Task<string> LoadAsync()
        {
            var value = await _settings.GetSettingAsync(SettingKey);
            return value ?? string.Empty;
        }

        public Task SaveAsync(string instructions)
            => _settings.SetSettingAsync(SettingKey, instructions ?? string.Empty);
    }
}
