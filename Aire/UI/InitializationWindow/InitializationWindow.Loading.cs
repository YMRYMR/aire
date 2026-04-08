using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Aire.Services;

namespace Aire.UI
{
    public partial class InitializationWindow
    {
        private async Task LoadAsync(IProgress<string> progress)
        {
            progress.Report("Reading system profile…");
            var profile = await Task.Run(() => OllamaService.GetLocalSystemProfile());

            progress.Report("Checking Ollama…");
            bool inPath = await Task.Run(() => OllamaService.IsOllamaInPath());

            if (!inPath)
            {
                progress.Report("Loading Ollama model catalog…");
                var available = await FetchAvailableModelsAsync();
                AppStartupCache.Set(
                    profile,
                    OllamaStartupStatus.NotInstalled,
                    Array.Empty<OllamaService.OllamaModel>(),
                    available);
                return;
            }

            progress.Report("Checking if Ollama is running…");
            using var svc = new OllamaService();
            bool running = await svc.IsOllamaReachableAsync(cancellationToken: CancellationToken.None);

            if (!running)
            {
                progress.Report("Loading Ollama model catalog…");
                var available = await FetchAvailableModelsAsync();
                AppStartupCache.Set(
                    profile,
                    OllamaStartupStatus.NotRunning,
                    Array.Empty<OllamaService.OllamaModel>(),
                    available);
                return;
            }

            progress.Report("Loading Ollama models…");
            var installedTask = svc.GetInstalledModelsAsync(cancellationToken: CancellationToken.None);
            var availableTask = FetchAvailableModelsAsync();
            await Task.WhenAll(installedTask, availableTask);

            List<OllamaService.OllamaModel> installed;
            try
            {
                installed = await installedTask;
            }
            catch
            {
                installed = [];
            }

            var availableModels = await availableTask;
            AppStartupCache.Set(profile, OllamaStartupStatus.Ready, installed, availableModels);
        }

        private static async Task<IReadOnlyList<OllamaService.OllamaModel>> FetchAvailableModelsAsync()
        {
            try
            {
                using var svc = new OllamaService();
                return await svc.GetAvailableModelsAsync(CancellationToken.None);
            }
            catch
            {
                return Array.Empty<OllamaService.OllamaModel>();
            }
        }
    }
}
