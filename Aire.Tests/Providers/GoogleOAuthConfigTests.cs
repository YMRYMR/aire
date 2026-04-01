using System;
using Aire.Services.Email;
using Xunit;

namespace Aire.Tests.Providers;

public class GoogleOAuthConfigTests
{
    [Fact]
    public void ClientId_ReadsFromEnvironmentVariable()
    {
        Environment.SetEnvironmentVariable("AIRE_GOOGLE_CLIENT_ID", "test-client-id.apps.googleusercontent.com");
        try
        {
            Assert.Equal("test-client-id.apps.googleusercontent.com", GoogleOAuthConfig.ClientId);
        }
        finally
        {
            Environment.SetEnvironmentVariable("AIRE_GOOGLE_CLIENT_ID", null);
        }
    }

    [Fact]
    public void ClientSecret_ReadsFromEnvironmentVariable()
    {
        Environment.SetEnvironmentVariable("AIRE_GOOGLE_CLIENT_SECRET", "test-secret");
        try
        {
            Assert.Equal("test-secret", GoogleOAuthConfig.ClientSecret);
        }
        finally
        {
            Environment.SetEnvironmentVariable("AIRE_GOOGLE_CLIENT_SECRET", null);
        }
    }

    [Fact]
    public void ClientId_WhenEnvVarNotSet_ReturnsEmpty()
    {
        Environment.SetEnvironmentVariable("AIRE_GOOGLE_CLIENT_ID", null);
        Assert.Equal(string.Empty, GoogleOAuthConfig.ClientId);
    }

    [Fact]
    public void ClientSecret_WhenEnvVarNotSet_ReturnsEmpty()
    {
        Environment.SetEnvironmentVariable("AIRE_GOOGLE_CLIENT_SECRET", null);
        Assert.Equal(string.Empty, GoogleOAuthConfig.ClientSecret);
    }
}
