using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Aire.Services;

public sealed class GitHubReleaseUpdateService : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly bool _ownsClient;
    private readonly string _owner;
    private readonly string _repo;

    public GitHubReleaseUpdateService(string owner, string repo, HttpClient? httpClient = null)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        _repo = repo ?? throw new ArgumentNullException(nameof(repo));
        _httpClient = httpClient ?? new HttpClient();
        _ownsClient = httpClient == null;
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Aire/1.0 (+https://github.com/YMRYMR/aire)");
        _httpClient.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
    }

    public Version CurrentVersion => GetCurrentVersion();

    public async Task<GitHubReleaseUpdateInfo?> CheckLatestReleaseAsync(CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync(GetLatestReleaseUri(), cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        return ParseLatestRelease(document.RootElement);
    }

    public async Task<string> DownloadInstallerAsync(GitHubReleaseUpdateInfo update, CancellationToken cancellationToken = default)
    {
        if (update.InstallerAsset == null)
            throw new InvalidOperationException("The release does not include an installer asset.");

        var updateFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Aire",
            "Updates",
            update.LatestVersion.ToString());

        Directory.CreateDirectory(updateFolder);
        var targetPath = Path.Combine(updateFolder, update.InstallerAsset.Name);

        using var response = await _httpClient.GetAsync(update.InstallerAsset.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using (var file = File.Create(targetPath))
        await using (var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
        {
            await source.CopyToAsync(file, cancellationToken).ConfigureAwait(false);
        }

        return targetPath;
    }

    public void LaunchInstaller(string installerPath)
    {
        if (string.IsNullOrWhiteSpace(installerPath))
            throw new ArgumentException("Installer path is required.", nameof(installerPath));

        Process.Start(new ProcessStartInfo
        {
            FileName = installerPath,
            UseShellExecute = true,
        });
    }

    internal GitHubReleaseUpdateInfo? ParseLatestRelease(JsonElement root)
    {
        var tag = root.TryGetProperty("tag_name", out var tagValue) ? tagValue.GetString() : null;
        var latestVersion = ParseVersion(tag);
        if (latestVersion == null)
            return null;

        var currentVersion = GetCurrentVersion();
        if (latestVersion <= currentVersion)
            return null;

        var name = root.TryGetProperty("name", out var nameValue) ? nameValue.GetString() : null;
        var htmlUrl = root.TryGetProperty("html_url", out var htmlValue) ? htmlValue.GetString() : null;
        var body = root.TryGetProperty("body", out var bodyValue) ? bodyValue.GetString() : null;

        var installerAsset = FindInstallerAsset(root);
        if (installerAsset == null)
            return null;

        return new GitHubReleaseUpdateInfo(
            currentVersion,
            latestVersion,
            tag ?? latestVersion.ToString(),
            name ?? $"Aire {latestVersion}",
            new Uri(htmlUrl ?? $"https://github.com/{_owner}/{_repo}/releases/latest", UriKind.Absolute),
            installerAsset,
            body);
    }

    internal static Version? ParseVersion(string? tagName)
    {
        if (string.IsNullOrWhiteSpace(tagName))
            return null;

        var normalized = tagName.Trim().TrimStart('v', 'V');
        return Version.TryParse(normalized, out var parsed) ? parsed : null;
    }

    private static GitHubReleaseAsset? FindInstallerAsset(JsonElement root)
    {
        if (!root.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array)
            return null;

        foreach (var asset in assets.EnumerateArray())
        {
            var name = asset.TryGetProperty("name", out var nameValue) ? nameValue.GetString() : null;
            var url = asset.TryGetProperty("browser_download_url", out var urlValue) ? urlValue.GetString() : null;
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(url))
                continue;

            if (!name.EndsWith(".msi", StringComparison.OrdinalIgnoreCase))
                continue;

            long? size = asset.TryGetProperty("size", out var sizeValue) && sizeValue.TryGetInt64(out var parsedSize)
                ? parsedSize
                : null;

            return new GitHubReleaseAsset(name, new Uri(url, UriKind.Absolute), size);
        }

        return null;
    }

    private static Version GetCurrentVersion()
    {
        var assembly = typeof(GitHubReleaseUpdateService).Assembly;
        var info = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (Version.TryParse(info?.Split('+', StringSplitOptions.RemoveEmptyEntries)[0], out var parsed))
            return parsed;

        return assembly.GetName().Version ?? new Version(0, 0, 0, 0);
    }

    private Uri GetLatestReleaseUri()
        => new($"https://api.github.com/repos/{_owner}/{_repo}/releases/latest", UriKind.Absolute);

    public void Dispose()
    {
        if (_ownsClient)
            _httpClient.Dispose();
    }
}

public sealed record GitHubReleaseUpdateInfo(
    Version CurrentVersion,
    Version LatestVersion,
    string TagName,
    string ReleaseName,
    Uri ReleasePageUrl,
    GitHubReleaseAsset InstallerAsset,
    string? ReleaseNotes);

public sealed record GitHubReleaseAsset(
    string Name,
    Uri DownloadUrl,
    long? Size);
