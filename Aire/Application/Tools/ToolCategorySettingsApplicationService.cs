using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Aire.AppLayer.Abstractions;
using Aire.Domain.Tools;

namespace Aire.AppLayer.Tools
{
    /// <summary>
    /// Loads and saves the user's enabled tool categories independently of any one provider.
    /// </summary>
    public sealed class ToolCategorySettingsApplicationService
    {
        internal const string SettingsKey = "enabled_tool_categories";

        private readonly ISettingsRepository _settings;

        public ToolCategorySettingsApplicationService(ISettingsRepository settings)
        {
            _settings = settings;
        }

        public async Task<ToolCategorySelection> LoadAsync()
        {
            var json = await _settings.GetSettingAsync(SettingsKey);
            return Parse(json);
        }

        public Task SaveAsync(IEnumerable<string> enabledCategories)
        {
            var normalized = ToolCategoryCatalog.Normalize(enabledCategories);
            var payload = JsonSerializer.Serialize(normalized.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
            return _settings.SetSettingAsync(SettingsKey, payload);
        }

        internal static ToolCategorySelection Parse(string? json)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(json))
                {
                    var categories = JsonSerializer.Deserialize<List<string>>(json);
                    if (categories != null)
                        return new ToolCategorySelection(ToolCategoryCatalog.Normalize(categories).OrderBy(x => x).ToArray());
                }
            }
            catch
            {
            }

            return new ToolCategorySelection(ToolCategoryCatalog.AllEnabled().OrderBy(x => x).ToArray());
        }
    }
}
