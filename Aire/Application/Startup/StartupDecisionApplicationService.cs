using Aire.AppLayer.Abstractions;
using Aire.Services;

namespace Aire.AppLayer.Startup;

/// <summary>
/// Decides whether the startup flow should open onboarding.
/// Keeps the onboarding gate out of App.xaml.cs so the UI shell stays thinner.
/// </summary>
public sealed class StartupDecisionApplicationService
{
    /// <summary>
    /// Returns true when Aire should show onboarding before opening the main shell.
    /// </summary>
    /// <param name="providerRepository">Initialized provider repository used to inspect configured providers.</param>
    /// <param name="onboardingCompleted">Whether the user has already completed onboarding.</param>
    public async Task<bool> ShouldShowOnboardingAsync(IProviderRepository providerRepository, bool onboardingCompleted)
    {
        if (!onboardingCompleted)
        {
            return true;
        }

        try
        {
            var providers = await providerRepository.GetProvidersAsync().ConfigureAwait(false);
            return providers.Count == 0;
        }
        catch (Exception ex)
        {
            AppLogger.Warn(
                $"{nameof(StartupDecisionApplicationService)}.{nameof(ShouldShowOnboardingAsync)}",
                "Failed to inspect providers; skipping onboarding.",
                ex);
            return false;
        }
    }
}
