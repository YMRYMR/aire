using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Aire.AppLayer.Abstractions;

namespace Aire.AppLayer.Tools
{
    /// <summary>
    /// Manages reusable auto-accept tool presets plus the active runtime configuration.
    /// </summary>
    public sealed class AutoAcceptProfilesApplicationService
    {
        private const string ActiveConfigKey = "auto_accept_settings";
        private const string ProfilesKey = "auto_accept_profiles";
        private const string SelectedProfileKey = "auto_accept_selected_profile";
        private readonly ISettingsRepository _settings;

        public AutoAcceptProfilesApplicationService(ISettingsRepository settings)
        {
            _settings = settings;
        }

        public sealed record AutoAcceptConfiguration(
            bool Enabled,
            IReadOnlyList<string> AllowedTools,
            bool AllowMouseTools,
            bool AllowKeyboardTools);

        public sealed record AutoAcceptProfile(
            string Name,
            AutoAcceptConfiguration Configuration,
            bool IsBuiltIn);

        public async Task<AutoAcceptConfiguration> LoadActiveConfigurationAsync()
        {
            var json = await _settings.GetSettingAsync(ActiveConfigKey);
            if (string.IsNullOrWhiteSpace(json))
                return BuiltInProfiles[0].Configuration;

            try
            {
                var parsed = JsonSerializer.Deserialize<AutoAcceptConfigurationDto>(json);
                return Normalize(parsed?.ToModel());
            }
            catch
            {
                return BuiltInProfiles[0].Configuration;
            }
        }

        public Task SaveActiveConfigurationAsync(AutoAcceptConfiguration configuration)
            => _settings.SetSettingAsync(ActiveConfigKey, JsonSerializer.Serialize(AutoAcceptConfigurationDto.FromModel(Normalize(configuration))));

        public async Task<IReadOnlyList<AutoAcceptProfile>> LoadProfilesAsync()
        {
            var result = BuiltInProfiles.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);
            var json = await _settings.GetSettingAsync(ProfilesKey);
            if (!string.IsNullOrWhiteSpace(json))
            {
                try
                {
                    var parsed = JsonSerializer.Deserialize<List<AutoAcceptProfileDto>>(json) ?? new();
                    foreach (var dto in parsed.Where(p => !string.IsNullOrWhiteSpace(p.Name)))
                    {
                        result[dto.Name.Trim()] = new AutoAcceptProfile(
                            dto.Name.Trim(),
                            Normalize(dto.Configuration?.ToModel()),
                            IsBuiltIn: false);
                    }
                }
                catch
                {
                }
            }

            return result.Values
                .OrderByDescending(p => p.IsBuiltIn)
                .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public async Task SaveProfileAsync(string name, AutoAcceptConfiguration configuration)
        {
            var normalizedName = name.Trim();
            if (string.IsNullOrWhiteSpace(normalizedName))
                throw new ArgumentException("Profile name is required.", nameof(name));

            var customProfiles = await LoadCustomProfilesAsync();
            customProfiles.RemoveAll(p => string.Equals(p.Name, normalizedName, StringComparison.OrdinalIgnoreCase));
            customProfiles.Add(new AutoAcceptProfileDto
            {
                Name = normalizedName,
                Configuration = AutoAcceptConfigurationDto.FromModel(Normalize(configuration))
            });

            await _settings.SetSettingAsync(ProfilesKey, JsonSerializer.Serialize(customProfiles));
            await SaveSelectedProfileNameAsync(normalizedName);
        }

        public async Task DeleteProfileAsync(string name)
        {
            var customProfiles = await LoadCustomProfilesAsync();
            customProfiles.RemoveAll(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
            await _settings.SetSettingAsync(ProfilesKey, JsonSerializer.Serialize(customProfiles));

            var selected = await LoadSelectedProfileNameAsync();
            if (string.Equals(selected, name, StringComparison.OrdinalIgnoreCase))
                await SaveSelectedProfileNameAsync(BuiltInProfiles[0].Name);
        }

        public async Task<string> LoadSelectedProfileNameAsync()
        {
            var stored = await _settings.GetSettingAsync(SelectedProfileKey);
            return string.IsNullOrWhiteSpace(stored) ? BuiltInProfiles[0].Name : stored.Trim();
        }

        public Task SaveSelectedProfileNameAsync(string name)
            => _settings.SetSettingAsync(SelectedProfileKey, name.Trim());

        private async Task<List<AutoAcceptProfileDto>> LoadCustomProfilesAsync()
        {
            var json = await _settings.GetSettingAsync(ProfilesKey);
            if (string.IsNullOrWhiteSpace(json))
                return new();

            try
            {
                return JsonSerializer.Deserialize<List<AutoAcceptProfileDto>>(json) ?? new();
            }
            catch
            {
                return new();
            }
        }

        private static AutoAcceptConfiguration Normalize(AutoAcceptConfiguration? configuration)
        {
            var source = configuration ?? BuiltInProfiles[0].Configuration;
            return new AutoAcceptConfiguration(
                source.Enabled,
                source.AllowedTools
                    .Where(tool => !string.IsNullOrWhiteSpace(tool))
                    .Select(tool => tool.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(tool => tool, StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                source.AllowMouseTools,
                source.AllowKeyboardTools);
        }

        private static readonly IReadOnlyList<AutoAcceptProfile> BuiltInProfiles =
        [
            new AutoAcceptProfile(
                "Developer",
                new AutoAcceptConfiguration(
                    Enabled: true,
                    AllowedTools:
                    [
                        "list_files", "read_file", "search_files", "search_file_content",
                        "write_to_file", "apply_diff", "create_directory", "move_file", "open_file",
                        "execute_command", "read_command_output",
                        "get_system_info", "get_running_processes", "get_active_window", "get_selected_text",
                        "ask_followup_question", "attempt_completion", "switch_mode", "switch_model", "update_todo_list"
                    ],
                    AllowMouseTools: false,
                    AllowKeyboardTools: false),
                IsBuiltIn: true),
            new AutoAcceptProfile(
                "News browser",
                new AutoAcceptConfiguration(
                    Enabled: true,
                    AllowedTools:
                    [
                        "open_url", "http_request",
                        "open_browser_tab", "list_browser_tabs", "read_browser_tab", "switch_browser_tab",
                        "close_browser_tab", "get_browser_html",
                        "ask_followup_question", "attempt_completion", "show_image"
                    ],
                    AllowMouseTools: false,
                    AllowKeyboardTools: false),
                IsBuiltIn: true),
            new AutoAcceptProfile(
                "Conservative",
                new AutoAcceptConfiguration(
                    Enabled: false,
                    AllowedTools: Array.Empty<string>(),
                    AllowMouseTools: false,
                    AllowKeyboardTools: false),
                IsBuiltIn: true)
        ];

        private sealed class AutoAcceptConfigurationDto
        {
            public bool Enabled { get; set; }
            public List<string> AllowedTools { get; set; } = new();
            public bool AllowMouseTools { get; set; }
            public bool AllowKeyboardTools { get; set; }

            public AutoAcceptConfiguration ToModel()
                => new(Enabled, AllowedTools, AllowMouseTools, AllowKeyboardTools);

            public static AutoAcceptConfigurationDto FromModel(AutoAcceptConfiguration configuration)
                => new()
                {
                    Enabled = configuration.Enabled,
                    AllowedTools = configuration.AllowedTools.ToList(),
                    AllowMouseTools = configuration.AllowMouseTools,
                    AllowKeyboardTools = configuration.AllowKeyboardTools
                };
        }

        private sealed class AutoAcceptProfileDto
        {
            public string Name { get; set; } = string.Empty;
            public AutoAcceptConfigurationDto? Configuration { get; set; }
        }
    }
}
