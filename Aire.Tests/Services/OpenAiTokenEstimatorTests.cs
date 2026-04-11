using Aire.Data;
using Aire.Services;
using Xunit;

namespace Aire.Tests.Services;

public sealed class OpenAiTokenEstimatorTests
{
    private readonly OpenAiTokenEstimator _estimator = new();

    [Fact]
    public void EstimateTokens_EmptyString_ReturnsZero()
    {
        var result = _estimator.EstimateTokens("");
        Assert.Equal(0, result);
    }

    [Fact]
    public void EstimateTokens_Text_UsesFallbackHeuristic()
    {
        // Microsoft.ML.Tokenizers tokenizes "12345678" into 3 tokens (cl100k_base).
        var result = _estimator.EstimateTokens("12345678");
        Assert.Equal(3, result);
    }

    [Fact]
    public void EstimateTokensForImage_Null_ReturnsZero()
    {
        var result = _estimator.EstimateTokensForImage(null);
        Assert.Equal(0, result);
    }

    [Fact]
    public void EstimateTokensForImage_LowDetail_Returns85()
    {
        var image = new ImageMetadata { DetailLevel = "low", Width = 800, Height = 600 };
        var result = _estimator.EstimateTokensForImage(image);
        Assert.Equal(85, result);
    }

    [Fact]
    public void EstimateTokensForImage_HighDetail_SmallImage_Returns170()
    {
        // 512×512 tile count = 1
        var image = new ImageMetadata { DetailLevel = "high", Width = 500, Height = 500 };
        var result = _estimator.EstimateTokensForImage(image);
        Assert.Equal(170, result);
    }

    [Fact]
    public void EstimateTokensForImage_HighDetail_LargeImage_CalculatesTiles()
    {
        // 1200×900 → tiles wide = ceil(1200/512)=3, tiles high = ceil(900/512)=2, total = 6
        var image = new ImageMetadata { DetailLevel = "high", Width = 1200, Height = 900 };
        var result = _estimator.EstimateTokensForImage(image);
        Assert.Equal(6 * 170, result);
    }

    [Fact]
    public void EstimateTokensForImage_NoDetailLevelButDimensions_AssumesHighDetail()
    {
        var image = new ImageMetadata { Width = 600, Height = 400 };
        var result = _estimator.EstimateTokensForImage(image);
        // Tiles: ceil(600/512)=2, ceil(400/512)=1 → 2 tiles → 340 tokens
        Assert.Equal(340, result);
    }

    [Fact]
    public void EstimateTokensForAttachments_Null_ReturnsZero()
    {
        var result = _estimator.EstimateTokensForAttachments(null);
        Assert.Equal(0, result);
    }

    [Fact]
    public void EstimateTokensForAttachments_ImageAttachment_UsesLowDetailDefault()
    {
        var attachment = new MessageAttachment { IsImage = true };
        var result = _estimator.EstimateTokensForAttachments(new[] { attachment });
        Assert.Equal(85, result);
    }

    [Fact]
    public void EstimateTokensForAttachments_NonImageAttachment_EstimatesMetadata()
    {
        var attachment = new MessageAttachment
        {
            FileName = "readme.txt",
            MimeType = "text/plain",
            FilePath = @"C:\docs\readme.txt"
        };
        var result = _estimator.EstimateTokensForAttachments(new[] { attachment });
        // Character heuristic applied to each metadata part.
        Assert.True(result > 0);
    }

    [Fact]
    public void EstimateTokensForAttachments_Mixed_Sum()
    {
        var attachments = new[]
        {
            new MessageAttachment { IsImage = true },
            new MessageAttachment { FileName = "file.txt" }
        };
        var result = _estimator.EstimateTokensForAttachments(attachments);
        // At least 85 for image + something for file name.
        Assert.True(result >= 85);
    }
}