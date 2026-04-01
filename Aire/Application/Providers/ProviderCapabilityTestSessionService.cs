using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Aire.AppLayer.Abstractions;
using Aire.Services;

namespace Aire.AppLayer.Providers
{
    /// <summary>
    /// Application-layer persistence for saved provider capability test sessions.
    /// </summary>
    public sealed class ProviderCapabilityTestSessionService
    {
        /// <summary>
        /// Loads the saved capability test session for one provider/model pair.
        /// </summary>
        /// <param name="providerId">Provider identifier used for the settings storage key.</param>
        /// <param name="model">Model identifier whose saved results should be returned.</param>
        /// <param name="settingsRepository">Settings repository used to store serialized sessions.</param>
        /// <returns>The saved session for that provider/model pair, or <see langword="null"/> when none exists.</returns>
        public async Task<CapabilityTestSession?> LoadAsync(
            int providerId,
            string model,
            ISettingsRepository settingsRepository)
        {
            var json = await settingsRepository.GetSettingAsync(BuildSettingsKey(providerId));
            if (string.IsNullOrEmpty(json))
                return null;

            Dictionary<string, CapabilityTestSession>? all;
            try
            {
                all = JsonSerializer.Deserialize<Dictionary<string, CapabilityTestSession>>(
                    json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (JsonException)
            {
                return null;
            }

            if (all == null || !all.TryGetValue(model ?? string.Empty, out var session))
                return null;

            return session;
        }

        /// <summary>
        /// Saves the latest capability test results for one provider/model pair.
        /// </summary>
        /// <param name="providerId">Provider identifier used for the settings storage key.</param>
        /// <param name="model">Model identifier whose results are being stored.</param>
        /// <param name="results">Capability test results to persist.</param>
        /// <param name="testedAt">Timestamp describing when the test run completed.</param>
        /// <param name="settingsRepository">Settings repository used to store serialized sessions.</param>
        public async Task SaveAsync(
            int providerId,
            string model,
            IReadOnlyList<CapabilityTestResult> results,
            DateTime testedAt,
            ISettingsRepository settingsRepository)
        {
            var key = BuildSettingsKey(providerId);
            var existing = await settingsRepository.GetSettingAsync(key);

            Dictionary<string, CapabilityTestSession> all;
            if (!string.IsNullOrEmpty(existing))
            {
                try
                {
                    all = JsonSerializer.Deserialize<Dictionary<string, CapabilityTestSession>>(
                              existing,
                              new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                          ?? new Dictionary<string, CapabilityTestSession>();
                }
                catch (JsonException)
                {
                    all = new Dictionary<string, CapabilityTestSession>();
                }
            }
            else
            {
                all = new Dictionary<string, CapabilityTestSession>();
            }

            all[model ?? string.Empty] = new CapabilityTestSession
            {
                Model = model ?? string.Empty,
                TestedAt = testedAt,
                Results = new List<CapabilityTestResult>(results),
            };

            var json = JsonSerializer.Serialize(all, new JsonSerializerOptions
            {
                WriteIndented = false,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            });
            await settingsRepository.SetSettingAsync(key, json);
        }

        private static string BuildSettingsKey(int providerId) => $"capability_tests_{providerId}";
    }
}
