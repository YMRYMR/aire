using System;

namespace Aire.Services;

/// <summary>
/// Abstracts application-wide persistent state so that consumers can be tested without
/// touching the filesystem. The default implementation is <see cref="AppStateImpl"/>
/// which delegates to the <see cref="AppState"/> static class.
/// </summary>
public interface IAppState
{
    // ── Lifecycle ─────────────────────────────────────────────────────────────

    /// <summary>Set to true by App just before Shutdown(). See <see cref="AppState.IsShuttingDown"/>.</summary>
    bool IsShuttingDown { get; set; }

    /// <summary>Raised when local API access is enabled or disabled.</summary>
    event Action? ApiAccessChanged;

    // ── Bool flags ────────────────────────────────────────────────────────────

    void SetBrowserOpen(bool open);
    void SetSettingsOpen(bool open);
    void SetHasCompletedOnboarding(bool val);

    /// <summary>Persists local API enablement and raises <see cref="ApiAccessChanged"/>.</summary>
    void SetApiAccessEnabled(bool enabled);

    void OpenSidebar();
    void CloseSidebar();

    bool GetBrowserOpen();
    bool GetSettingsOpen();
    bool GetHasCompletedOnboarding();
    bool GetApiAccessEnabled();
    bool GetSidebarOpen();

    // ── String / secure values ────────────────────────────────────────────────

    void SetLanguage(string code);
    string GetLanguage();

    void SetApiAccessToken(string token);
    string GetApiAccessToken();

    /// <summary>Returns the current API token, generating one if none exists yet.</summary>
    string EnsureApiAccessToken();

    /// <summary>Replaces the API token with a fresh random value and returns it.</summary>
    string RegenerateApiAccessToken();
}
