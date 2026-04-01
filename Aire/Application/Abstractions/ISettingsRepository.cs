namespace Aire.AppLayer.Abstractions
{
    /// <summary>
    /// Persistence boundary for simple application settings and audit entries.
    /// </summary>
    public interface ISettingsRepository
    {
        Task<string?> GetSettingAsync(string key);
        Task SetSettingAsync(string key, string value);
        Task LogFileAccessAsync(string operation, string path, bool allowed);
    }
}
