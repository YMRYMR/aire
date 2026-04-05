using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.IO;

namespace Aire.Screenshots;

internal sealed class LocalApiClient
{
    private const int Port = 51234;
    private const string SecurePrefix = "dpapi:";
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("Aire-SecureStorage-v1");
    private static readonly JsonSerializerOptions CompactJson = new() { PropertyNameCaseInsensitive = true };

    public async Task SetLanguageAsync(string languageCode)
    {
        await SendAsync("set_language", new Dictionary<string, object?>
        {
            ["languageCode"] = languageCode
        });
    }

    public async Task SetActiveProviderByNameAsync(string providerName)
    {
        var providers = await ListProvidersAsync();
        var provider = providers.FirstOrDefault(snapshot =>
            string.Equals(snapshot.Name, providerName, StringComparison.OrdinalIgnoreCase));

        if (provider == null)
            throw new InvalidOperationException($"Local API could not find provider '{providerName}'.");

        await SendAsync("set_provider", new Dictionary<string, object?>
        {
            ["providerId"] = provider.Id
        });
    }

    private async Task<IReadOnlyList<ApiProviderSnapshot>> ListProvidersAsync()
    {
        var response = await SendAsync("list_providers", null);
        var result = response.RootElement.GetProperty("result");

        return JsonSerializer.Deserialize<List<ApiProviderSnapshot>>(result.GetRawText(), JsonOptions.Default)
            ?? [];
    }

    private static async Task<JsonDocument> SendAsync(string method, Dictionary<string, object?>? parameters)
    {
        var token = GetLocalApiToken();

        using var client = new TcpClient();
        await client.ConnectAsync("127.0.0.1", Port);
        await using var stream = client.GetStream();
        var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        using var writer = new StreamWriter(stream, utf8, leaveOpen: true) { AutoFlush = true };
        using var reader = new StreamReader(stream, utf8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);

        var payload = JsonSerializer.Serialize(new
        {
            method,
            parameters,
            token
        }, CompactJson);

        await writer.WriteLineAsync(payload);
        var line = await reader.ReadLineAsync()
            ?? throw new InvalidOperationException("Local API closed the connection without responding.");

        var response = JsonDocument.Parse(line);
        if (!response.RootElement.TryGetProperty("ok", out var okElement) || !okElement.GetBoolean())
        {
            var message = response.RootElement.TryGetProperty("errorMessage", out var errorElement)
                ? errorElement.GetString()
                : "Unknown Local API error.";
            throw new InvalidOperationException(message);
        }

        return response;
    }

    private static string GetLocalApiToken()
    {
        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Aire",
            "appstate_strings.json");

        if (!File.Exists(path))
            throw new InvalidOperationException("Aire local API token file was not found.");

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        if (!document.RootElement.TryGetProperty("apiAccessToken", out var tokenElement))
            throw new InvalidOperationException("Aire local API token is missing.");

        var protectedValue = tokenElement.GetString();
        if (string.IsNullOrWhiteSpace(protectedValue))
            throw new InvalidOperationException("Aire local API token is empty.");

        if (!protectedValue.StartsWith(SecurePrefix, StringComparison.Ordinal))
            return protectedValue;

        var encrypted = Convert.FromBase64String(protectedValue[SecurePrefix.Length..]);
        var plain = ProtectedData.Unprotect(encrypted, Entropy, DataProtectionScope.CurrentUser);
        return Encoding.UTF8.GetString(plain);
    }

    private sealed class ApiProviderSnapshot
    {
        public int Id { get; init; }
        public string Name { get; init; } = string.Empty;
    }
}
