namespace Aire.Domain.Providers
{
    /// <summary>
    /// Describes whether Aire can launch a working local Codex CLI.
    /// </summary>
    public sealed record CodexCliStatus(
        bool IsInstalled,
        string? CliPath,
        bool SawStorePackageOnly,
        string UserMessage);

    /// <summary>
    /// Result of an install action for the local Codex CLI bridge.
    /// </summary>
    public sealed record CodexActionResult(
        bool Succeeded,
        string UserMessage);
}
