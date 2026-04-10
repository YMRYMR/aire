using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Aire.Services;

/// <summary>
/// Lightweight currency conversion helper for usage displays.
/// </summary>
public static class CurrencyExchangeService
{
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(8) };
    private static readonly SemaphoreSlim _refreshGate = new(1, 1);
    private static readonly Dictionary<string, decimal> _usdRates = new(StringComparer.OrdinalIgnoreCase)
    {
        ["USD"] = 1m,
        ["EUR"] = 0.92m,
        ["GBP"] = 0.78m,
        ["JPY"] = 155m,
        ["CAD"] = 1.37m,
        ["AUD"] = 1.52m,
        ["CHF"] = 0.88m,
        ["CNY"] = 7.24m,
        ["INR"] = 83m,
    };

    private static readonly string[] _supportedCurrencies =
    {
        "USD",
        "EUR",
        "GBP",
        "JPY",
        "CAD",
        "AUD",
        "CHF",
        "CNY",
        "INR",
    };

    public static IReadOnlyList<string> SupportedCurrencies => _supportedCurrencies;

    public static string GetPreferredCurrency()
    {
        var code = AppState.GetPreferredCurrency();
        return string.IsNullOrWhiteSpace(code) ? "USD" : NormalizeCurrencyCode(code);
    }

    public static void SetPreferredCurrency(string code)
        => AppState.SetPreferredCurrency(NormalizeCurrencyCode(code));

    public static string NormalizeCurrencyCode(string code)
        => string.IsNullOrWhiteSpace(code) ? "USD" : code.Trim().ToUpperInvariant();

    public static async Task RefreshAsync(CancellationToken ct = default)
    {
        if (!await _refreshGate.WaitAsync(0, ct).ConfigureAwait(false))
            return;

        try
        {
            var codes = string.Join(",", _supportedCurrencies.Skip(1));
            var url = $"https://api.frankfurter.app/latest?from=USD&to={codes}";
            using var response = await _http.GetAsync(url, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return;

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
            if (!doc.RootElement.TryGetProperty("rates", out var rates))
                return;

            foreach (var prop in rates.EnumerateObject())
            {
                if (prop.Value.ValueKind == JsonValueKind.Number && prop.Value.TryGetDecimal(out var rate) && rate > 0)
                    _usdRates[NormalizeCurrencyCode(prop.Name)] = rate;
            }
        }
        catch
        {
            // Keep the last known or bundled fallback rates.
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    public static decimal ConvertFromUsd(decimal usdAmount, string currencyCode)
    {
        var normalized = NormalizeCurrencyCode(currencyCode);
        if (!_usdRates.TryGetValue(normalized, out var rate))
            rate = 1m;
        return usdAmount * rate;
    }

    public static string FormatFromUsd(decimal usdAmount, string currencyCode)
    {
        var normalized = NormalizeCurrencyCode(currencyCode);
        var converted = ConvertFromUsd(usdAmount, normalized);
        return normalized == "USD"
            ? $"${converted:F2}"
            : $"{GetCurrencySymbol(normalized)}{converted:F2}";
    }

    public static string FormatFromMinorUnits(long amountMinorUnits, string currencyCode)
    {
        var normalized = NormalizeCurrencyCode(currencyCode);
        var amount = amountMinorUnits / 100m;
        return normalized == "USD"
            ? $"${amount:F2}"
            : $"{GetCurrencySymbol(normalized)}{amount:F2}";
    }

    public static string GetCurrencySymbol(string currencyCode)
    {
        return NormalizeCurrencyCode(currencyCode) switch
        {
            "USD" => "$",
            "EUR" => "€",
            "GBP" => "£",
            "JPY" => "¥",
            "CAD" => "C$",
            "AUD" => "A$",
            "CHF" => "CHF ",
            "CNY" => "¥",
            "INR" => "₹",
            _ => string.Empty,
        };
    }
}
