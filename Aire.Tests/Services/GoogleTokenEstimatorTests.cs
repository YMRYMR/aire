using Aire.Data;
using Aire.Services;
using Xunit;

namespace Aire.Tests.Services;

public sealed class GoogleTokenEstimatorTests
{
    private readonly GoogleTokenEstimator _estimator = new();

    [Fact]
    public void EstimateTokens_EmptyString_ReturnsZero()
    {
        var result = _estimator.EstimateTokens("");
        Assert.Equal(0, result);
    }

    [Fact]
    public void EstimateTokens_Text_UsesCharacterHeuristic()
    {
        var result = _estimator.EstimateTokens("hello world"); // 11 chars → ceil(11/4) = 3
        Assert.Equal(3, result);
    }

    [Fact]
    public void EstimateTokensForImage_Null_ReturnsZero()
    {
        var result = _estimator.EstimateTokensForImage(null);
        Assert.Equal(0, result);
    }

    [Fact]
    public void EstimateTokensForImage_AnyImage_Returns85()
    {
        var image = new ImageMetadata { Width = 800, Height = 600 };
        var result = _estimator.EstimateTokensForImage(image);
        Assert.Equal(85, result);
    }

    [Fact]
    public void EstimateTokensForAttachments_Null_ReturnsZero()
    {
        var result = _estimator.EstimateTokensForAttachments(null);
        Assert.Equal(0, result);
    }

    [Fact]
    public void EstimateTokensForAttachments_SingleAttachment_ComputesFromMetadata()
    {
        var attachment = new MessageAttachment
        {
            FileName = "image.png",
            MimeType = "image/png",
            FilePath = @"C:\images\image.png"
        };
        var result = _estimator.EstimateTokensForAttachments(new[] { attachment });
        // Character count ~37, tokens = ceil(37/4) = 10
        Assert.Equal(10, result);
    }

    [Fact]
    public void EstimateTokensForAttachments_MultipleAttachments_Sum()
    {
        var attachments = new[]
        {
            new MessageAttachment { FileName = "a.txt" },
            new MessageAttachment { FileName = "b.txt" }
        };
        var result = _estimator.EstimateTokensForAttachments(attachments);
        Assert.True(result > 0);
    }
}