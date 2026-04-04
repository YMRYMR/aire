using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Aire.Data;

namespace Aire.Providers
{
    /// <summary>
    /// DeepSeek provider (OpenAI‑compatible API).
    /// </summary>
    public class DeepSeekProvider : OpenAiProvider
    {
        public override string ProviderType => "DeepSeek";
        public override string DisplayName => "DeepSeek (OpenAI‑compatible)";

        protected override ProviderCapabilities GetBaseCapabilities() =>
            ProviderCapabilities.TextChat    |
            ProviderCapabilities.Streaming   |
            ProviderCapabilities.ToolCalling |
            ProviderCapabilities.SystemPrompt;

        protected override string DefaultApiBaseUrl => "https://api.deepseek.com";

        public override ProviderFieldHints FieldHints => new() { ShowBaseUrl = false };

        // ── IProviderMetadata overrides ─────────────────────────────────────

        public override async Task<List<ModelDefinition>?> FetchLiveModelsAsync(
            string? apiKey, string? baseUrl, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(apiKey)) return null;
            try
            {
                var url = string.IsNullOrWhiteSpace(baseUrl)
                    ? "https://api.deepseek.com" : baseUrl.TrimEnd('/');

                using var req = new HttpRequestMessage(HttpMethod.Get, $"{url}/v1/models");
                req.Headers.Add("Authorization", $"Bearer {apiKey}");
                var res = await MetadataHttp.SendAsync(req, ct);
                if (!res.IsSuccessStatusCode) return null;

                var json = await res.Content.ReadAsStringAsync(ct);
                Debug.WriteLine($"DeepSeek raw models response: {json}");
                using var doc = JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty("data", out var dataEl)) return null;

                // Filter for DeepSeek‑specific model IDs
                var deepseekPrefixes = new[] { "deepseek-", "deepseek" };
                var models = dataEl.EnumerateArray()
                    .Select(m => m.GetProperty("id").GetString() ?? "")
                    .Where(id => !string.IsNullOrEmpty(id) &&
                                 deepseekPrefixes.Any(p => id.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
                    .OrderByDescending(id => id)
                    .Select(id => new ModelDefinition { Id = id, DisplayName = id })
                    .ToList();
                Debug.WriteLine($"DeepSeek filtered models: {string.Join(", ", models.Select(m => m.Id))}");
                return models;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DeepSeek live model fetch failed: {ex.GetType().Name}");
                return null;
            }
        }

        public override async Task<TokenUsage?> GetTokenUsageAsync(CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(Config.ApiKey))
                return null;

            try
            {
                var url = Config.BaseUrl?.TrimEnd('/') ?? "https://api.deepseek.com";
                using var req = new HttpRequestMessage(HttpMethod.Get, $"{url}/user/balance");
                req.Headers.Add("Authorization", $"Bearer {Config.ApiKey}");
                var response = await MetadataHttp.SendAsync(req, ct);
                if (!response.IsSuccessStatusCode)
                    return null;

                var json = await response.Content.ReadAsStringAsync(ct);
                Console.WriteLine($"DeepSeek raw JSON: {json}");
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (!root.TryGetProperty("balance_infos", out var balanceInfosEl) ||
                    !balanceInfosEl.EnumerateArray().Any())
                    return null;

                var firstBalance = balanceInfosEl.EnumerateArray().First();
                if (!firstBalance.TryGetProperty("total_balance", out var totalBalanceEl) ||
                    !firstBalance.TryGetProperty("currency", out var currencyEl) ||
                    !firstBalance.TryGetProperty("granted_balance", out var grantedBalanceEl) ||
                    !firstBalance.TryGetProperty("topped_up_balance", out var toppedUpBalanceEl))
                    return null;

                var totalBalanceStr = totalBalanceEl.GetString();
                var grantedBalanceStr = grantedBalanceEl.GetString();
                var toppedUpBalanceStr = toppedUpBalanceEl.GetString();
                var currency = currencyEl.GetString() ?? "USD";
                Console.WriteLine($"DeepSeek raw strings: total='{totalBalanceStr}', granted='{grantedBalanceStr}', toppedUp='{toppedUpBalanceStr}', currency='{currency}'");

                if (!decimal.TryParse(totalBalanceStr, NumberStyles.Number, CultureInfo.InvariantCulture, out var totalBalanceDecimal) ||
                    !decimal.TryParse(grantedBalanceStr, NumberStyles.Number, CultureInfo.InvariantCulture, out var grantedBalanceDecimal) ||
                    !decimal.TryParse(toppedUpBalanceStr, NumberStyles.Number, CultureInfo.InvariantCulture, out var toppedUpBalanceDecimal))
                    return null;

                // Convert to smallest unit (cents for USD)
                long ToCents(decimal d) => (long)(d * 100);
                var totalCents = ToCents(totalBalanceDecimal);
                var grantedCents = ToCents(grantedBalanceDecimal);
                var toppedUpCents = ToCents(toppedUpBalanceDecimal);

                Console.WriteLine($"DeepSeek balance parsing: totalBalance={totalBalanceDecimal}, granted={grantedBalanceDecimal}, toppedUp={toppedUpBalanceDecimal}");
                Console.WriteLine($"DeepSeek cents: total={totalCents}, granted={grantedCents}, toppedUp={toppedUpCents}");

                var initialBalanceCents = grantedCents + toppedUpCents;
                var usedCents = initialBalanceCents - totalCents;

                Debug.WriteLine($"DeepSeek token usage: Used={usedCents}, Limit={initialBalanceCents}, Currency={currency}");

                return new TokenUsage
                {
                    Used = usedCents,
                    Limit = initialBalanceCents,
                    ResetDate = null,
                    Unit = currency
                };
            }
            catch
            {
                return null;
            }
        }
    }
}
