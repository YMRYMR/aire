using Aire.Data;
using Xunit;

namespace Aire.Tests.UI;

public class ProviderDisplayTests
{
    [Theory]
    [InlineData(new object[] { "Anthropic", "Anthropic API" })]
    [InlineData(new object[] { "ClaudeWeb", "Claude.ai" })]
    [InlineData(new object[] { "OpenAI", "OpenAI" })]
    [InlineData(new object[] { "GoogleAI", "Google AI" })]
    [InlineData(new object[] { "UnknownType", "UnknownType" })]
    public void DisplayType_ReturnsFriendlyProviderName(string type, string expected)
    {
        Provider provider = new Provider
        {
            Type = type
        };
        Assert.Equal(expected, provider.DisplayType);
    }
}
