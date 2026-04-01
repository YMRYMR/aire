namespace Aire.Platform;

/// <summary>
/// Cross-platform abstraction for reading and writing plain text to the system clipboard.
/// </summary>
public interface IClipboardService
{
    Task SetTextAsync(string text, CancellationToken cancellationToken = default);
    Task<string?> GetTextAsync(CancellationToken cancellationToken = default);
}
