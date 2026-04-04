using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aire.AppLayer.Abstractions;
using Aire.Data;
using Aire.Providers;

namespace Aire.Services;

/// <summary>
/// Background service that keeps local provider model catalogs warm by periodically
/// fetching live model lists in the background.
/// </summary>
public sealed class ProviderModelRefreshService : IDisposable
{
    private static readonly TimeSpan DefaultRefreshInterval = TimeSpan.FromHours(4);
    private readonly Func<Task<IReadOnlyList<Provider>>> _providerLoader;
    private readonly Func<string, IProviderMetadata> _metadataResolver;
    private readonly Action<string, string>? _notificationSink;
    private readonly TimeSpan _refreshInterval;
    private readonly SemaphoreSlim _refreshGate = new(1, 1);

    private CancellationTokenSource? _cts;
    private Task? _loopTask;
    private bool _disposed;

    /// <summary>
    /// Creates the refresher using the default provider repository and provider metadata resolver.
    /// </summary>
    public ProviderModelRefreshService(
        Func<Task<IReadOnlyList<Provider>>>? providerLoader = null,
        Func<string, IProviderMetadata>? metadataResolver = null,
        Action<string, string>? notificationSink = null,
        TimeSpan? refreshInterval = null)
    {
        _providerLoader = providerLoader ?? LoadProvidersFromDatabaseAsync;
        _metadataResolver = metadataResolver ?? ProviderRegistry.GetMetadata;
        _notificationSink = notificationSink;
        _refreshInterval = refreshInterval ?? DefaultRefreshInterval;
    }

    /// <summary>
    /// Returns whether the periodic refresh loop is currently running.
    /// </summary>
    public bool IsRunning => _loopTask != null && !_loopTask.IsCompleted;

    /// <summary>
    /// Starts the periodic background refresh loop if it is not already running.
    /// </summary>
    public void Start()
    {
        if (_disposed || IsRunning)
            return;

        _cts = new CancellationTokenSource();
        _loopTask = Task.Run(() => RunAsync(_cts.Token));
    }

    /// <summary>
    /// Stops the refresh loop and waits for any in-flight refresh to complete.
    /// </summary>
    public async Task StopAsync()
    {
        if (_disposed)
            return;

        var cts = _cts;
        var task = _loopTask;
        _cts = null;
        _loopTask = null;

        if (cts != null)
            cts.Cancel();

        if (task != null)
        {
            try
            {
                await task.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch
            {
                // Shutdown should be best-effort only.
            }
        }

        cts?.Dispose();
    }

    /// <summary>
    /// Runs one refresh pass immediately.
    /// </summary>
    public async Task RefreshNowAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
            return;

        await _refreshGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await RefreshAllProvidersAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            await AppStartupState.WaitUntilReadyAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            return;
        }

        try
        {
            await RefreshNowAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WARN] [ProviderModelRefreshService] Initial refresh failed: {ex.GetType().Name}");
        }

        using var timer = new PeriodicTimer(_refreshInterval);
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (!await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
                    break;

                await RefreshNowAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WARN] [ProviderModelRefreshService] Periodic refresh failed: {ex.GetType().Name}");
            }
        }
    }

    private async Task RefreshAllProvidersAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<Provider> providers;
        try
        {
            providers = await _providerLoader().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WARN] [ProviderModelRefreshService] Could not load providers: {ex.GetType().Name}");
            return;
        }

        foreach (var provider in providers)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            await RefreshProviderAsync(provider, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task RefreshProviderAsync(Provider provider, CancellationToken cancellationToken)
    {
        try
        {
            var metadata = _metadataResolver(provider.Type);
            var liveModels = await metadata.FetchLiveModelsAsync(provider.ApiKey, provider.BaseUrl, cancellationToken).ConfigureAwait(false);
            if (liveModels == null || liveModels.Count == 0)
                return;

            var syncResult = ModelCatalog.SyncLiveModels(provider.Type, liveModels);
            if (syncResult.CreatedCatalog || syncResult.AddedModelIds.Count == 0)
                return;

            var addedNames = liveModels
                .Where(model => syncResult.AddedModelIds.Contains(model.Id, StringComparer.OrdinalIgnoreCase))
                .Select(model => model.DisplayName?.Trim())
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name!)
                .Take(3)
                .ToArray();

            var providerLabel = provider.DisplayType;
            var title = $"{providerLabel} models updated";
            var body = BuildNotificationBody(providerLabel, syncResult.AddedModelIds.Count, addedNames);
            _notificationSink?.Invoke(title, body);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WARN] [ProviderModelRefreshService] Refresh failed for {provider.Type}: {ex.GetType().Name}");
        }
    }

    private static string BuildNotificationBody(string providerLabel, int addedCount, IReadOnlyList<string> addedNames)
    {
        var prefix = addedCount == 1
            ? $"New model available for {providerLabel}"
            : $"New models available for {providerLabel}";

        if (addedNames.Count == 0)
            return $"{prefix}.";

        var list = string.Join(", ", addedNames);
        if (addedCount > addedNames.Count)
        {
            return $"{prefix}: {list} and {addedCount - addedNames.Count} more.";
        }

        return $"{prefix}: {list}.";
    }

    private static async Task<IReadOnlyList<Provider>> LoadProvidersFromDatabaseAsync()
    {
        using var database = new DatabaseService();
        await database.InitializeAsync().ConfigureAwait(false);
        return await database.GetProvidersAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Stops the refresher and releases background resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        try
        {
            StopAsync().GetAwaiter().GetResult();
        }
        catch
        {
            // Dispose should never throw during process shutdown.
        }
        _refreshGate.Dispose();
        GC.SuppressFinalize(this);
    }
}
