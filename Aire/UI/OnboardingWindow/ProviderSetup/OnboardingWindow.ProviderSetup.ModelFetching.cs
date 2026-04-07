using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Aire.Data;
using Aire.AppLayer.Providers;
using Aire.Providers;
using Aire.Services;
using WpfComboBoxItem = System.Windows.Controls.ComboBoxItem;

namespace Aire.UI
{
    public partial class OnboardingWindow
    {
        private async Task FetchModelsWithDebounceAsync(CancellationToken ct)
        {
            try
            {
                await Task.Delay(900, ct);
                if (ct.IsCancellationRequested)
                    return;

                if (ProviderTypeCombo.SelectedItem is not WpfComboBoxItem typeItem)
                    return;

                var type = typeItem.Tag as string ?? "OpenAI";
                var apiKey = ApiKeyBox.Password;

                if (type == "Ollama" || type == "ClaudeWeb" || apiKey.Length < 8)
                    return;

                ModelFetchStatus.Text = LocalizationService.S("onboard.fetchingModels", "Fetching latest models\u2026");
                ModelFetchStatus.Visibility = Visibility.Visible;

                var baseUrl = BaseUrlBox.Text.Trim();
                var metadata = ProviderFactory.GetMetadata(type);
                var catalog = await new ProviderModelCatalogApplicationService().LoadModelsAsync(
                    metadata,
                    apiKey,
                    string.IsNullOrEmpty(baseUrl) ? null : baseUrl,
                    ct);

                if (ct.IsCancellationRequested)
                    return;

                var previous = ModelCombo.SelectedValue as string
                    ?? (ModelCombo.SelectedItem as ModelDefinition)?.Id;
                BindStandardModels(catalog.EffectiveModels, previous);
                ModelFetchStatus.Text = catalog.StatusMessage ?? string.Empty;
            }
            catch (OperationCanceledException)
            {
            }
            catch
            {
                if (!ct.IsCancellationRequested)
                    ModelFetchStatus.Text = LocalizationService.S("onboard.fetchFailed", "Could not fetch models \u2014 showing built-in list");
            }
        }
    }
}
