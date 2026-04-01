using Aire.Services;
using Xunit;

namespace Aire.Tests.Core;

public class SecureStorageTests
{
    [Fact]
    public void Protect_And_Unprotect_RoundTripValue()
    {
        string text = SecureStorage.Protect("secret-value");
        Assert.NotNull(text);
        Assert.NotEqual("secret-value", text);
        Assert.True(SecureStorage.IsProtected(text));
        Assert.Equal("secret-value", SecureStorage.Unprotect(text));
    }

    [Fact]
    public void Protect_LeavesNullEmptyAndAlreadyProtectedValuesAlone()
    {
        Assert.Null(SecureStorage.Protect(null));
        Assert.Equal(string.Empty, SecureStorage.Protect(string.Empty));
        string text = SecureStorage.Protect("abc");
        string actual = SecureStorage.Protect(text);
        Assert.Equal(text, actual);
    }

    [Fact]
    public void Unprotect_ReturnsPlaintextAndInvalidCipherGracefully()
    {
        Assert.Equal("plain", SecureStorage.Unprotect("plain"));
        Assert.Equal("dpapi:not-valid-base64", SecureStorage.Unprotect("dpapi:not-valid-base64"));
    }
}
