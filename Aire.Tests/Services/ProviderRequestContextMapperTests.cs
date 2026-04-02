using System.Linq;
using Aire.AppLayer.Providers;
using Aire.Domain.Providers;
using Aire.Providers;
using Xunit;

namespace Aire.Tests.Services;

public class ProviderRequestContextMapperTests
{
    [Fact]
    public void FromLegacyMessages_MapsAllFieldsInOrder()
    {
        ChatMessage[] messages =
        [
            new()
            {
                Role = "system",
                Content = "You are helpful.",
            },
            new()
            {
                Role = "user",
                Content = "Describe this image.",
                ImagePath = "C:/Temp/example.png",
                ImageBytes = [1, 2, 3],
                ImageMimeType = "image/png"
            }
        ];

        var requestMessages = ProviderRequestContextMapper.FromLegacyMessages(messages);

        Assert.Equal(2, requestMessages.Count);
        Assert.Equal("system", requestMessages[0].Role);
        Assert.Equal("You are helpful.", requestMessages[0].Content);
        Assert.Equal("user", requestMessages[1].Role);
        Assert.Equal("Describe this image.", requestMessages[1].Content);
        Assert.Equal("C:/Temp/example.png", requestMessages[1].ImagePath);
        Assert.Equal([1, 2, 3], requestMessages[1].ImageBytes);
        Assert.Equal("image/png", requestMessages[1].ImageMimeType);
    }

    [Fact]
    public void FromLegacyMessages_ReturnsEmptyList_ForNullInput()
    {
        var requestMessages = ProviderRequestContextMapper.FromLegacyMessages(null!);

        Assert.NotNull(requestMessages);
        Assert.Empty(requestMessages);
    }

    [Fact]
    public void ToLegacyMessages_MapsAllFieldsInOrder()
    {
        ProviderRequestMessage[] messages =
        [
            new()
            {
                Role = "system",
                Content = "You are helpful.",
            },
            new()
            {
                Role = "user",
                Content = "Describe this image.",
                ImagePath = "C:/Temp/example.png",
                ImageBytes = [1, 2, 3],
                ImageMimeType = "image/png"
            }
        ];

        var legacy = ProviderRequestContextMapper.ToLegacyMessages(messages);

        Assert.Equal(2, legacy.Count);
        Assert.Equal("system", legacy[0].Role);
        Assert.Equal("You are helpful.", legacy[0].Content);
        Assert.Equal("user", legacy[1].Role);
        Assert.Equal("Describe this image.", legacy[1].Content);
        Assert.Equal("C:/Temp/example.png", legacy[1].ImagePath);
        Assert.Equal([1, 2, 3], legacy[1].ImageBytes);
        Assert.Equal("image/png", legacy[1].ImageMimeType);
    }

    [Fact]
    public void ToLegacyMessages_ReturnsEmptyList_ForNullInput()
    {
        var legacy = ProviderRequestContextMapper.ToLegacyMessages(null!);

        Assert.NotNull(legacy);
        Assert.Empty(legacy);
    }

    [Fact]
    public void ToLegacyMessages_CreatesNewInstances()
    {
        ProviderRequestMessage[] messages =
        [
            new()
            {
                Role = "user",
                Content = "Hello"
            }
        ];

        var legacy = ProviderRequestContextMapper.ToLegacyMessages(messages);

        Assert.Single(legacy);
        Assert.NotSame(messages[0], legacy.Single());
    }
}
