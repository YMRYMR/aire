using System;
using Aire.Services.Email;
using Xunit;

namespace Aire.Tests.Services;

public class GoogleOAuthServiceCoverageTests
{
    [Fact]
    public void TokenResultAndEmailSummary_Defaults_AreStable()
    {
        var token   = new OAuthTokenResult();
        var summary = new EmailSummary();

        Assert.Equal(string.Empty,    token.AccessToken);
        Assert.Equal(string.Empty,    token.RefreshToken);
        Assert.Equal(default(DateTime), token.ExpiresAt);
        Assert.Equal(string.Empty,    summary.Id);
        Assert.Equal(string.Empty,    summary.Subject);
        Assert.False(summary.IsRead);
    }

    [Fact]
    public void OAuthHelpers_GenerateExpectedShapes()
    {
        string verifier   = GoogleOAuthService.GenerateCodeVerifier();
        string challenge  = GoogleOAuthService.GenerateCodeChallenge(verifier);
        int    port       = GoogleOAuthService.GetFreePort();
        string encoded    = GoogleOAuthService.Base64UrlEncode(new byte[] { 0xFB, 0xFF, 0x00 });

        Assert.NotEmpty(verifier);
        Assert.DoesNotContain("=", verifier, StringComparison.Ordinal);
        Assert.DoesNotContain("+", verifier, StringComparison.Ordinal);
        Assert.DoesNotContain("/", verifier, StringComparison.Ordinal);
        Assert.NotEmpty(challenge);
        Assert.DoesNotContain("=", challenge, StringComparison.Ordinal);
        Assert.InRange(port, 1, 65535);
        Assert.Equal("-_8A", encoded);
    }

    [Fact]
    public void GenerateCodeChallenge_IsDeterministicForSameVerifier()
    {
        string first  = GoogleOAuthService.GenerateCodeChallenge("fixed-verifier-value");
        string second = GoogleOAuthService.GenerateCodeChallenge("fixed-verifier-value");

        Assert.Equal(first, second);
    }
}
