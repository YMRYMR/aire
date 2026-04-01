using Aire.AppLayer.Abstractions;

namespace Aire.AppLayer.Settings
{
    /// <summary>
    /// Application-layer use cases for reading and persisting application settings.
    /// </summary>
    public sealed class AppSettingsApplicationService
    {
        private readonly ISettingsRepository _settings;

        /// <summary>
        /// Creates the service over the settings persistence boundary.
        /// </summary>
        /// <param name="settings">Settings persistence port.</param>
        public AppSettingsApplicationService(ISettingsRepository settings)
        {
            _settings = settings;
        }

        /// <summary>Reads one setting by key.</summary>
        public Task<string?> GetSettingAsync(string key)
            => _settings.GetSettingAsync(key);

        /// <summary>Persists one setting by key.</summary>
        public Task SaveSettingAsync(string key, string value)
            => _settings.SetSettingAsync(key, value);
    }
}
