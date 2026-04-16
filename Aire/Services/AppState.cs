using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;

namespace Aire.Services;

/// <summary>
/// Lightweight helper that persists application state across launches.
/// </summary>
internal static class AppState
{
    private static readonly string BasePath = System.IO.Path.Combine(
        string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("LOCALAPPDATA"))
            ? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
            : Environment.GetEnvironmentVariable("LOCALAPPDATA")!,
        "Aire");

    internal static readonly string Path = System.IO.Path.Combine(BasePath, "appstate.json");

    internal static readonly string StringsPath = System.IO.Path.Combine(BasePath, "appstate_strings.json");

    /// <summary>
    /// Set to true by App just before Shutdown() is called.
    /// Window Closed handlers check this so they do NOT overwrite the "open"
    /// state that was already saved — windows closing during shutdown should
    /// not be treated as the user manually closing them.
    /// </summary>
    public static bool IsShuttingDown { get; set; }

    /// <summary>
    /// Raised when local API access is enabled or disabled so listeners can update long-lived services.
    /// </summary>
    public static event Action? ApiAccessChanged;

    // ── Bool values ───────────────────────────────────────────────────────────

    /// <summary>Persists whether the browser window should reopen on the next app launch.</summary>
    public static void SetBrowserOpen(bool open)           => SetBool("browserOpen",           open);
    /// <summary>Persists whether the settings window should reopen on the next app launch.</summary>
    public static void SetSettingsOpen(bool open)          => SetBool("settingsOpen",          open);
    /// <summary>Persists whether the setup wizard has been completed.</summary>
    public static void SetHasCompletedOnboarding(bool val) => SetBool("hasCompletedOnboarding", val);
    /// <summary>
    /// Persists local API enablement, ensuring a token exists when access is turned on.
    /// </summary>
    /// <param name="enabled">Whether the loopback local API should be enabled.</param>
    public static void SetApiAccessEnabled(bool enabled)
    {
        SetBool("apiAccessEnabled", enabled);
        if (enabled)
            EnsureApiAccessToken();
        ApiTraceLog.Record("config", "api_access", enabled ? "Enabled local API access" : "Disabled local API access", enabled);
        ApiAccessChanged?.Invoke();
    }
    // "sidebarHidden" key — inverted so missing key (first launch) = sidebar open (default true)
    /// <summary>Persists that the chat sidebar is now open.</summary>
    public static void OpenSidebar()  => SetBool("sidebarHidden", false);
    /// <summary>Persists that the chat sidebar is now closed.</summary>
    public static void CloseSidebar() => SetBool("sidebarHidden", true);

    /// <summary>Returns whether the browser window was open on the previous app run.</summary>
    public static bool GetBrowserOpen()            => GetBool("browserOpen");
    /// <summary>Returns whether the settings window was open on the previous app run.</summary>
    public static bool GetSettingsOpen()           => GetBool("settingsOpen");
    /// <summary>Returns whether onboarding has already been completed.</summary>
    public static bool GetHasCompletedOnboarding() => GetBool("hasCompletedOnboarding");
    /// <summary>Returns whether the local loopback API is enabled.</summary>
    public static bool GetApiAccessEnabled()       => GetBool("apiAccessEnabled");
    /// <summary>Returns whether the chat sidebar should be shown.</summary>
    public static bool GetSidebarOpen()            => !GetBool("sidebarHidden"); // default true

    // ── Int values ─────────────────────────────────────────────────────────

    private const int DefaultApiPort = 51234;

    /// <summary>Persists the local API port. Takes effect on next API start.</summary>
    public static void SetApiPort(int port) => SetString("apiPort", port.ToString());
    /// <summary>Returns the configured local API port (default 51234).</summary>
    public static int GetApiPort() => int.TryParse(GetString("apiPort"), out var p) ? p : DefaultApiPort;

    // ── String values ─────────────────────────────────────────────────────────

    /// <summary>Persists the selected UI language code.</summary>
    public static void SetLanguage(string code) => SetString("language", code);
    /// <summary>Returns the persisted UI language code, or an empty string when none was saved.</summary>
    public static string GetLanguage()          => GetString("language");
    /// <summary>Persists the preferred currency code for usage display.</summary>
    public static void SetPreferredCurrency(string code) => SetString("preferredCurrency", code);
    /// <summary>Returns the preferred currency code, or empty when none was saved.</summary>
    public static string GetPreferredCurrency() => GetString("preferredCurrency");
    /// <summary>Persists the selected top-level window id for screenshot capture.</summary>
    public static void SetSelectedWindowId(string windowId) => SetString("selectedWindowId", windowId);
    /// <summary>Returns the selected top-level window id, or empty when none was saved.</summary>
    public static string GetSelectedWindowId() => GetString("selectedWindowId");
    /// <summary>Persists the latest orchestrator configuration for the next launch.</summary>
    public static void SetOrchestratorConfig(OrchestratorConfigSnapshot config)
        => SetString("orchestratorConfig", JsonSerializer.Serialize(config));
    /// <summary>Returns the latest persisted orchestrator configuration, if any.</summary>
    public static OrchestratorConfigSnapshot? GetOrchestratorConfig()
    {
        var json = GetString("orchestratorConfig");
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize<OrchestratorConfigSnapshot>(json);
        }
        catch (Exception ex)
        {
            AppLogger.Error("AppState.GetOrchestratorConfig", "Failed to read orchestrator config; defaulting to empty", ex);
            return null;
        }
    }
    /// <summary>Persists the latest orchestrator runtime session for one conversation so it can resume after a stop or crash.</summary>
    public static void SetOrchestratorSession(OrchestratorSessionSnapshot session)
    {
        if (session.ConversationId is not int conversationId)
            return;

        var sessions = LoadOrchestratorSessions();
        sessions[conversationId.ToString()] = session;
        SetString("orchestratorSessions", JsonSerializer.Serialize(sessions));
    }

    /// <summary>Returns the latest persisted orchestrator runtime session for one conversation, if any.</summary>
    public static OrchestratorSessionSnapshot? GetOrchestratorSession(int conversationId)
    {
        var sessions = LoadOrchestratorSessions();
        return sessions.TryGetValue(conversationId.ToString(), out var session) ? session : null;
    }

    /// <summary>Clears the persisted orchestrator runtime session for one conversation.</summary>
    public static void ClearOrchestratorSession(int conversationId)
    {
        var sessions = LoadOrchestratorSessions();
        if (!sessions.Remove(conversationId.ToString()))
            return;

        SetString("orchestratorSessions", JsonSerializer.Serialize(sessions));
    }
    /// <summary>Encrypts and persists the local API token.</summary>
    public static void SetApiAccessToken(string token) => SetSecureString("apiAccessToken", token);
    /// <summary>Loads and decrypts the persisted local API token.</summary>
    public static string GetApiAccessToken()           => GetSecureString("apiAccessToken");
    /// <summary>
    /// Returns the current local API token, generating and persisting one when none exists yet.
    /// </summary>
    /// <returns>A usable local API token.</returns>
    public static string EnsureApiAccessToken()
    {
        var token = GetApiAccessToken();
        if (!string.IsNullOrWhiteSpace(token))
            return token;

        token = GenerateToken();
        SetApiAccessToken(token);
        ApiTraceLog.Record("config", "api_token", "Generated API token", true);
        return token;
    }
    /// <summary>
    /// Replaces the local API token with a fresh random value and returns it.
    /// </summary>
    /// <returns>The newly generated local API token.</returns>
    public static string RegenerateApiAccessToken()
    {
        var token = GenerateToken();
        SetApiAccessToken(token);
        ApiTraceLog.Record("config", "api_token", "Regenerated API token", true);
        return token;
    }

    // ── Bool internals ────────────────────────────────────────────────────────

    /// <summary>
    /// Writes one boolean state value to the appstate JSON file.
    /// </summary>
    private static void SetBool(string key, bool value)
    {
        try
        {
            var data  = LoadAllBools();
            data[key] = value;
            Directory.CreateDirectory(BasePath);
            File.WriteAllText(Path, JsonSerializer.Serialize(data));
        }
        catch (Exception ex) { AppLogger.Error("AppState.SetBool", $"Failed to persist state key '{key}'", ex); }
    }

    /// <summary>
    /// Reads one boolean state value from the appstate JSON file.
    /// Missing keys default to <see langword="false"/>.
    /// </summary>
    private static bool GetBool(string key)
        => LoadAllBools().TryGetValue(key, out var v) && v;

    /// <summary>
    /// Loads the complete boolean-state dictionary from disk.
    /// </summary>
    private static Dictionary<string, bool> LoadAllBools()
    {
        try
        {
            if (!File.Exists(Path)) return new();
            var json = File.ReadAllText(Path);
            return JsonSerializer.Deserialize<Dictionary<string, bool>>(json) ?? new();
        }
        catch (Exception ex) { AppLogger.Error("AppState.LoadAllBools", "Failed to read state file; defaulting to empty", ex); return new(); }
    }

    // ── String internals ──────────────────────────────────────────────────────

    /// <summary>
    /// Writes one plain string state value to the string-state JSON file.
    /// </summary>
    private static void SetString(string key, string value)
    {
        try
        {
            var data  = LoadAllStrings();
            data[key] = value;
            Directory.CreateDirectory(BasePath);
            File.WriteAllText(StringsPath, JsonSerializer.Serialize(data));
        }
        catch (Exception ex) { AppLogger.Error("AppState.SetString", $"Failed to persist state key '{key}'", ex); }
    }

    /// <summary>
    /// Reads one plain string state value from the string-state JSON file.
    /// Missing keys default to an empty string.
    /// </summary>
    private static string GetString(string key)
        => LoadAllStrings().TryGetValue(key, out var v) ? v : string.Empty;

    /// <summary>
    /// Encrypts and stores one string value in the string-state JSON file.
    /// </summary>
    private static void SetSecureString(string key, string value)
    {
        var protectedValue = SecureStorage.Protect(value);
        SetString(key, protectedValue ?? string.Empty);
    }

    /// <summary>
    /// Reads and decrypts one stored string value, transparently migrating legacy plaintext values.
    /// </summary>
    private static string GetSecureString(string key)
    {
        var value = GetString(key);
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        if (SecureStorage.IsProtected(value))
            return SecureStorage.Unprotect(value) ?? string.Empty;

        // Legacy plaintext migration: re-save the value encrypted once we see it.
        SetSecureString(key, value);
        return value;
    }

    /// <summary>
    /// Loads the complete plain-string state dictionary from disk.
    /// </summary>
    private static Dictionary<string, string> LoadAllStrings()
    {
        try
        {
            if (!File.Exists(StringsPath)) return new();
            var json = File.ReadAllText(StringsPath);
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new();
        }
        catch (Exception ex) { AppLogger.Error("AppState.LoadAllStrings", "Failed to read state file; defaulting to empty", ex); return new(); }
    }

    /// <summary>
    /// Loads the complete orchestrator-session map from the string-state file.
    /// </summary>
    private static Dictionary<string, OrchestratorSessionSnapshot> LoadOrchestratorSessions()
    {
        try
        {
            var json = GetString("orchestratorSessions");
            if (!string.IsNullOrWhiteSpace(json))
                return JsonSerializer.Deserialize<Dictionary<string, OrchestratorSessionSnapshot>>(json)
                       ?? new Dictionary<string, OrchestratorSessionSnapshot>(StringComparer.OrdinalIgnoreCase);

            var legacyJson = GetString("orchestratorSession");
            if (!string.IsNullOrWhiteSpace(legacyJson))
            {
                var legacy = JsonSerializer.Deserialize<OrchestratorSessionSnapshot>(legacyJson);
                if (legacy != null)
                {
                    var key = (legacy.ConversationId ?? 0).ToString();
                    return new Dictionary<string, OrchestratorSessionSnapshot>(StringComparer.OrdinalIgnoreCase)
                    {
                        [key] = legacy
                    };
                }
            }

            return new Dictionary<string, OrchestratorSessionSnapshot>(StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            AppLogger.Error("AppState.LoadOrchestratorSessions", "Failed to read orchestrator sessions; defaulting to empty", ex);
            return new Dictionary<string, OrchestratorSessionSnapshot>(StringComparer.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Generates a random 256-bit token encoded as uppercase hexadecimal text.
    /// </summary>
    private static string GenerateToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
}

/// <summary>Persisted orchestrator settings shown when the dialog is reopened.</summary>
internal sealed record OrchestratorConfigSnapshot(
    int TokenBudget,
    List<string> Goals,
    List<string> SelectedCategories);

/// <summary>Persisted runtime state for the currently active or paused orchestrator session.</summary>
public sealed record OrchestratorSessionSnapshot(
    int? ConversationId,
    int TokenBudget,
    int TokensConsumed,
    int HeartbeatCount,
    List<string> Goals,
    List<string> SelectedCategories,
    Dictionary<string, List<string>> FailureVariants,
    string? StopReason,
    string? LastNarrative,
    DateTimeOffset SavedAt);
