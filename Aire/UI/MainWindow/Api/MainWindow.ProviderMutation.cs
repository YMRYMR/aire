using System.Linq;
using System.Threading.Tasks;
using Aire.AppLayer.Api;
using Aire.AppLayer.Providers;
using Aire.Data;
using Aire.Providers;
using Aire.Services.Providers;
using Aire.Services;

namespace Aire;

public partial class MainWindow
{
    public async Task<ApiProviderSnapshot> ApiCreateProviderAsync(
        string? name,
        string type,
        string? apiKey,
        string? baseUrl,
        string model,
        bool isEnabled = true,
        string? color = null,
        bool selectAfterCreate = false,
        int? inheritCredentialsFromProviderId = null)
        => await DispatchAsync(async () =>
        {
            await AppStartupState.WaitUntilReadyAsync();

            var apiService = _localApiApplicationService ?? new LocalApiApplicationService();
            var mutationService = new LocalApiProviderMutationApplicationService();
            var normalizedType = apiService.NormalizeProviderType(type);
            var creation = await mutationService.CreateProviderAsync(
                _databaseService,
                new ProviderCreationApplicationService.ProviderCreationRequest(
                    name,
                    normalizedType,
                    apiKey,
                    baseUrl,
                    model,
                    isEnabled,
                    color,
                    inheritCredentialsFromProviderId),
                selectAfterCreate);
            if (creation.IsDuplicate)
                throw new InvalidOperationException("A matching provider already exists.");

            await ApplyProviderMutationEffectsAsync(
                creation.RefreshProviderCatalog,
                creation.RefreshSettingsProviderList,
                creation.ReselectProviderId,
                creation.SelectProviderId);

            return apiService.BuildProviderSnapshots(new[] { creation.Provider }).Single();
        });

    public async Task<bool> ApiSetProviderModelAsync(int providerId, string model)
        => await DispatchAsync(async () =>
        {
            await AppStartupState.WaitUntilReadyAsync();
            var provider = ProviderComboBox.Items.OfType<Provider>()
                .FirstOrDefault(p => p.Id == providerId);
            if (provider == null) return false;

            var normalizedModel = (_localApiApplicationService ?? new LocalApiApplicationService())
                .NormalizeProviderModel(model);
            if (normalizedModel == null)
                return false;

            var mutationService = new LocalApiProviderMutationApplicationService();
            var update = await mutationService.UpdateProviderModelAsync(
                _databaseService,
                providerId,
                normalizedModel,
                _currentProviderId);
            if (!update.Updated || update.Provider == null)
                return false;

            if (update.RefreshActiveProvider)
                await _providerFactory.UpdateProviderAsync(update.Provider);

            await ApplyProviderMutationEffectsAsync(
                update.RefreshProviderCatalog,
                update.RefreshSettingsProviderList,
                update.Provider.Id,
                update.RefreshActiveProvider ? update.Provider.Id : null);

            return true;
        });

    private async Task ApplyProviderMutationEffectsAsync(
        bool refreshProviderCatalog,
        bool refreshSettingsProviderList,
        int? reselectProviderId,
        int? selectProviderId)
    {
        if (refreshProviderCatalog)
            await RefreshProvidersAsync();

        if (refreshSettingsProviderList && _settingsWindow != null)
            await _settingsWindow.RefreshProvidersForExternalChangeAsync(reselectProviderId);

        if (selectProviderId.HasValue)
            await ApiSetProviderAsync(selectProviderId.Value);
    }
}
