using System;
using System.Collections.Generic;
using System.Linq;
using Aire.AppLayer.Mcp;
using Aire.Services.Mcp;
using Xunit;

namespace Aire.Tests.Services;

public sealed class McpCatalogApplicationServiceTests
{
    private readonly McpCatalogApplicationService _sut = new();

    [Fact]
    public void GetCatalog_ReturnsNonEmptyList()
    {
        var catalog = _sut.GetCatalog();
        Assert.NotEmpty(catalog);
    }

    [Fact]
    public void GetCatalog_ReturnsExpectedEntryCount()
    {
        var catalog = _sut.GetCatalog();
        Assert.Equal(15, catalog.Count);
    }

    [Fact]
    public void GetEntry_ValidKey_ReturnsCorrectEntry()
    {
        var entry = _sut.GetEntry("github");
        Assert.Equal("github", entry.Key);
        Assert.Equal("GitHub", entry.Name);
        Assert.Equal("npx", entry.Command);
        Assert.Equal("-y @modelcontextprotocol/server-github", entry.Arguments);
        Assert.Equal("Developer", entry.Category);
    }

    [Fact]
    public void GetEntry_CaseInsensitiveKey_ReturnsEntry()
    {
        var entry = _sut.GetEntry("GitHub");
        Assert.Equal("github", entry.Key);

        var entry2 = _sut.GetEntry("GITHUB");
        Assert.Equal("github", entry2.Key);
    }

    [Fact]
    public void GetEntry_UnknownKey_ThrowsInvalidOperationException()
    {
        Assert.Throws<InvalidOperationException>(() => _sut.GetEntry("nonexistent-key"));
    }

    [Fact]
    public void BuildConfig_ProducesCorrectConfig()
    {
        var config = _sut.BuildConfig("filesystem");

        Assert.Equal("Filesystem", config.Name);
        Assert.Equal("npx", config.Command);
        Assert.Equal("-y @modelcontextprotocol/server-filesystem", config.Arguments);
        Assert.True(config.IsEnabled);
        Assert.Empty(config.EnvVars);
    }

    [Fact]
    public void BuildConfig_WithEnvironmentHint_ParsesEnvVars()
    {
        var config = _sut.BuildConfig("postgres");

        Assert.Equal("PostgreSQL", config.Name);
        Assert.NotEmpty(config.EnvVars);
        Assert.True(config.EnvVars.ContainsKey("POSTGRES_CONNECTION_STRING"));
    }

    [Fact]
    public void BuildConfig_WithMultiLineEnvironmentHint_ParsesAllEnvVars()
    {
        var config = _sut.BuildConfig("gitlab");

        Assert.Equal("GitLab", config.Name);
        Assert.Equal(2, config.EnvVars.Count);
        Assert.True(config.EnvVars.ContainsKey("GITLAB_PERSONAL_ACCESS_TOKEN"));
        Assert.True(config.EnvVars.ContainsKey("GITLAB_API_URL"));
        Assert.Equal("https://gitlab.com", config.EnvVars["GITLAB_API_URL"]);
    }

    [Fact]
    public void FindInstalledConfig_WithMatchingConfig_ReturnsIt()
    {
        var installed = new List<McpServerConfig>
        {
            new()
            {
                Name = "GitHub",
                Command = "npx",
                Arguments = "-y @modelcontextprotocol/server-github"
            }
        };

        var result = _sut.FindInstalledConfig("github", installed);
        Assert.NotNull(result);
        Assert.Equal("GitHub", result!.Name);
    }

    [Fact]
    public void FindInstalledConfig_WithNoMatch_ReturnsNull()
    {
        var installed = new List<McpServerConfig>
        {
            new()
            {
                Name = "Something Else",
                Command = "other",
                Arguments = ""
            }
        };

        var result = _sut.FindInstalledConfig("github", installed);
        Assert.Null(result);
    }

    [Fact]
    public void FindInstalledConfig_EmptyInstalledList_ReturnsNull()
    {
        var result = _sut.FindInstalledConfig("github", []);
        Assert.Null(result);
    }

    [Fact]
    public void FindInstalledConfig_IsCaseInsensitive()
    {
        var installed = new List<McpServerConfig>
        {
            new()
            {
                Name = "github",
                Command = "NPX",
                Arguments = "-y @modelcontextprotocol/server-github"
            }
        };

        var result = _sut.FindInstalledConfig("GITHUB", installed);
        Assert.NotNull(result);
    }

    [Fact]
    public void FindInstalledConfig_PartialMatch_ReturnsNull()
    {
        var installed = new List<McpServerConfig>
        {
            new()
            {
                Name = "GitHub",
                Command = "npx",
                Arguments = "-y @different/server"
            }
        };

        var result = _sut.FindInstalledConfig("github", installed);
        Assert.Null(result);
    }

    [Fact]
    public void FindInstalledConfig_UnknownKey_ThrowsInvalidOperationException()
    {
        Assert.Throws<InvalidOperationException>(() =>
            _sut.FindInstalledConfig("nonexistent-key", []));
    }
}
