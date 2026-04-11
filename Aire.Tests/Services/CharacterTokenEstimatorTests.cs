using System.Collections.Generic;
using Aire.Data;
using Aire.Services;
using Xunit;

namespace Aire.Tests.Services;

public sealed class CharacterTokenEstimatorTests
{
    private readonly CharacterTokenEstimator _estimator = new();

    [Fact]
    public void EstimateTokens_EmptyString_ReturnsZero()
    {
        var result = _estimator.EstimateTokens("");
        Assert.Equal(0, result);
    }

    [Fact]
    public void EstimateTokens_NullString_ReturnsZero()
    {
        var result = _estimator.EstimateTokens(null);
        Assert.Equal(0, result);
    }

    [Fact]
    public void EstimateTokens_SimpleText_UsesCharacterHeuristic()
    {
        // 20 characters, default chars per token = 4 → 5 tokens (ceil).
        var result = _estimator.EstimateTokens("12345678901234567890");
        Assert.Equal(5, result);
    }

    [Fact]
    public void EstimateTokensForImage_ReturnsZero()
    {
        var image = new ImageMetadata { Width = 100, Height = 100 };
        var result = _estimator.EstimateTokensForImage(image);
        Assert.Equal(0, result);
    }

    [Fact]
    public void EstimateTokensForAttachments_Null_ReturnsZero()
    {
        var result = _estimator.EstimateTokensForAttachments(null);
        Assert.Equal(0, result);
    }

    [Fact]
    public void EstimateTokensForAttachments_Empty_ReturnsZero()
    {
        var result = _estimator.EstimateTokensForAttachments(new List<MessageAttachment>());
        Assert.Equal(0, result);
    }

    [Fact]
    public void EstimateTokensForAttachments_SingleAttachment_ComputesFromMetadata()
    {
        var attachment = new MessageAttachment
        {
            FileName = "test.txt",
            MimeType = "text/plain",
            FilePath = @"C:\temp\test.txt"
        };
        var result = _estimator.EstimateTokensForAttachments(new[] { attachment });
        // Total characters ≈ 34 (filename 8 + mime 10 + path 16) / 4 = 8.5 → ceil 9
        Assert.Equal(9, result);
    }

    [Fact]
    public void Constructor_CustomCharsPerToken_Respected()
    {
        var custom = new CharacterTokenEstimator(2.0); // 2 chars per token
        var result = custom.EstimateTokens("12345678"); // 8 chars → 4 tokens
        Assert.Equal(4, result);
    }

    [Fact]
    public void Constructor_ZeroCharsPerToken_Throws()
    {
        Assert.Throws<System.ArgumentOutOfRangeException>(() => new CharacterTokenEstimator(0));
    }

    [Fact]
    public void Constructor_NegativeCharsPerToken_Throws()
    {
        Assert.Throws<System.ArgumentOutOfRangeException>(() => new CharacterTokenEstimator(-1));
    }
}