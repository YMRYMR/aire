using System;

namespace Aire.Services;

/// <summary>
/// Default <see cref="IAppState"/> implementation that delegates every call to the
/// <see cref="AppState"/> static class. This adapter allows consumers to depend on the
/// interface for testability while the underlying persistence layer stays unchanged.
/// </summary>
internal sealed class AppStateImpl : IAppState
{
    // Singleton — callers that don't use DI can reference this directly.
    public static readonly AppStateImpl Instance = new();

    public bool IsShuttingDown
    {
        get => AppState.IsShuttingDown;
        set => AppState.IsShuttingDown = value;
    }

    public event Action? ApiAccessChanged
    {
        add    => AppState.ApiAccessChanged += value;
        remove => AppState.ApiAccessChanged -= value;
    }

    public void SetBrowserOpen(bool open)           => AppState.SetBrowserOpen(open);
    public void SetSettingsOpen(bool open)          => AppState.SetSettingsOpen(open);
    public void SetHasCompletedOnboarding(bool val) => AppState.SetHasCompletedOnboarding(val);
    public void SetApiAccessEnabled(bool enabled)   => AppState.SetApiAccessEnabled(enabled);
    public void OpenSidebar()                       => AppState.OpenSidebar();
    public void CloseSidebar()                      => AppState.CloseSidebar();

    public bool GetBrowserOpen()            => AppState.GetBrowserOpen();
    public bool GetSettingsOpen()           => AppState.GetSettingsOpen();
    public bool GetHasCompletedOnboarding() => AppState.GetHasCompletedOnboarding();
    public bool GetApiAccessEnabled()       => AppState.GetApiAccessEnabled();
    public bool GetSidebarOpen()            => AppState.GetSidebarOpen();

    public void SetLanguage(string code)         => AppState.SetLanguage(code);
    public string GetLanguage()                  => AppState.GetLanguage();
    public void SetPreferredCurrency(string code) => AppState.SetPreferredCurrency(code);
    public string GetPreferredCurrency()         => AppState.GetPreferredCurrency();
    public void SetApiAccessToken(string token)  => AppState.SetApiAccessToken(token);
    public string GetApiAccessToken()            => AppState.GetApiAccessToken();
    public string EnsureApiAccessToken()         => AppState.EnsureApiAccessToken();
    public string RegenerateApiAccessToken()     => AppState.RegenerateApiAccessToken();
}
