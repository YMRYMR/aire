namespace Aire.Platform;

/// <summary>
/// Cross-platform abstraction for opening external URLs using the host platform.
/// </summary>
public interface IExternalNavigationService
{
    Task OpenUrlAsync(string url, CancellationToken cancellationToken = default);
}
