using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Aire.Services;
using Xunit;

namespace Aire.Tests.Services;

public sealed class GitHubReleaseUpdateServiceTests
{
    [Fact]
    public async Task CheckLatestReleaseAsync_ReturnsNull_WhenReleaseVersionMatchesCurrent()
    {
        var client = new HttpClient(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
            {
              "tag_name": "v1.0.0",
              "name": "Aire 1.0.0",
              "html_url": "https://github.com/YMRYMR/aire/releases/tag/v1.0.0",
              "assets": [
                {
                  "name": "Aire.msi",
                  "browser_download_url": "https://example.com/Aire.msi",
                  "size": 1234
                }
              ]
            }
            """, Encoding.UTF8, "application/json")
        }));

        using var service = new GitHubReleaseUpdateService("YMRYMR", "aire", client);
        var update = await service.CheckLatestReleaseAsync();

        Assert.Null(update);
    }

    [Fact]
    public async Task CheckLatestReleaseAsync_ReturnsInstaller_WhenNewerReleaseExists()
    {
        var client = new HttpClient(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
            {
              "tag_name": "v1.0.1",
              "name": "Aire 1.0.1",
              "html_url": "https://github.com/YMRYMR/aire/releases/tag/v1.0.1",
              "body": "Bug fixes",
              "assets": [
                {
                  "name": "Aire.msi",
                  "browser_download_url": "https://example.com/Aire.msi",
                  "size": 1234
                },
                {
                  "name": "Aire.Portable-win-x64.zip",
                  "browser_download_url": "https://example.com/Aire.zip",
                  "size": 5678
                }
              ]
            }
            """, Encoding.UTF8, "application/json")
        }));

        using var service = new GitHubReleaseUpdateService("YMRYMR", "aire", client);
        var update = await service.CheckLatestReleaseAsync();

        Assert.NotNull(update);
        Assert.Equal(new Version(1, 0, 1), update!.LatestVersion);
        Assert.Equal("Aire.msi", update.InstallerAsset.Name);
        Assert.Equal("Bug fixes", update.ReleaseNotes);
    }

    [Fact]
    public async Task DownloadInstallerAsync_WritesInstallerBytes_ToLocalUpdateFolder()
    {
        var bytes = Encoding.UTF8.GetBytes("msi-bytes");
        var client = new HttpClient(new StubHandler(request =>
        {
            if (request.RequestUri?.AbsoluteUri == "https://example.com/Aire.msi")
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(bytes)
                };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }));

        using var service = new GitHubReleaseUpdateService("YMRYMR", "aire", client);
        var update = new GitHubReleaseUpdateInfo(
            new Version(1, 0, 0),
            new Version(1, 0, 1),
            "v1.0.1",
            "Aire 1.0.1",
            new Uri("https://github.com/YMRYMR/aire/releases/tag/v1.0.1"),
            new GitHubReleaseAsset("Aire.msi", new Uri("https://example.com/Aire.msi"), 1234),
            null);

        var path = await service.DownloadInstallerAsync(update);

        try
        {
            Assert.EndsWith("Aire.msi", path, StringComparison.OrdinalIgnoreCase);
            Assert.True(File.Exists(path));
            Assert.Equal(bytes, await File.ReadAllBytesAsync(path));
        }
        finally
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_handler(request));
    }
}
