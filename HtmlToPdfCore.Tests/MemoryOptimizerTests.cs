using HtmlToPdfCore.AI;
using HtmlToPdfCore.Models;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text;
using Xunit;

namespace HtmlToPdfCore.Tests;

public class MemoryOptimizerTests
{
    private static MemoryOptimizer CreateOptimizer() =>
        new MemoryOptimizer(NullLogger<MemoryOptimizer>.Instance);

    /// <summary>
    /// Builds an html string containing exactly <paramref name="imageCount"/> regex-matchable
    /// "&lt;img " tags and <paramref name="tableCount"/> regex-matchable "&lt;table " tags,
    /// padded with filler characters so the whole string is exactly <paramref name="totalLength"/>
    /// characters long. This lets each test assert literal, hand-computed expected values against
    /// MemoryOptimizer's heuristics instead of re-deriving the math.
    /// </summary>
    private static string BuildHtml(int imageCount, int tableCount, int totalLength)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < imageCount; i++)
            sb.Append("<img src=a> ");
        for (var i = 0; i < tableCount; i++)
            sb.Append("<table x></table> ");

        var tags = sb.ToString();
        if (tags.Length > totalLength)
            throw new ArgumentException($"totalLength ({totalLength}) must be >= tag length ({tags.Length})");

        return tags + new string('x', totalLength - tags.Length);
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new MemoryOptimizer(null!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task PredictMemoryUsageAsync_InvalidHtml_ThrowsArgumentException(string? html)
    {
        var optimizer = CreateOptimizer();

        await Assert.ThrowsAsync<ArgumentException>(() => optimizer.PredictMemoryUsageAsync(html!));
    }

    [Fact]
    public async Task PredictMemoryUsageAsync_SmallPlainHtml_ReturnsLowPoolSizeNoStreaming()
    {
        var optimizer = CreateOptimizer();
        const string html = "<p>Hello world</p>"; // length 19, no img/table tags

        var prediction = await optimizer.PredictMemoryUsageAsync(html);

        // baseMemory(10) + length/1024(0) + imageMemory(0) + tableMemory(0) = 10
        Assert.Equal(10, prediction.EstimatedMemoryMB);
        Assert.Equal(10, prediction.RecommendedPoolSize);
        Assert.False(prediction.ShouldUseStreaming);
        Assert.Equal(0.7f, prediction.ConfidenceScore);
    }

    [Fact]
    public async Task PredictMemoryUsageAsync_ManyImages_IncreasesEstimateAndLowersPoolSize()
    {
        var optimizer = CreateOptimizer();
        var html = BuildHtml(imageCount: 10, tableCount: 0, totalLength: 120);

        var prediction = await optimizer.PredictMemoryUsageAsync(html);

        // baseMemory(10) + length/1024(0) + imageMemory(10*2=20) + tableMemory(0) = 30
        Assert.Equal(30, prediction.EstimatedMemoryMB);
        Assert.Equal(5, prediction.RecommendedPoolSize);
        Assert.False(prediction.ShouldUseStreaming);
    }

    [Fact]
    public async Task PredictMemoryUsageAsync_ManyTablesAndLargeHtml_EnablesStreamingAndSmallestPool()
    {
        var optimizer = CreateOptimizer();
        var html = BuildHtml(imageCount: 0, tableCount: 20, totalLength: 51200);

        var prediction = await optimizer.PredictMemoryUsageAsync(html);

        // baseMemory(10) + length/1024(51200/1024=50) + imageMemory(0) + tableMemory(20*5=100) = 160
        Assert.Equal(160, prediction.EstimatedMemoryMB);
        Assert.Equal(2, prediction.RecommendedPoolSize);
        Assert.True(prediction.ShouldUseStreaming);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AnalyzeContentAsync_InvalidHtml_ThrowsArgumentException(string? html)
    {
        var optimizer = CreateOptimizer();

        await Assert.ThrowsAsync<ArgumentException>(() => optimizer.AnalyzeContentAsync(html!));
    }

    [Fact]
    public async Task AnalyzeContentAsync_ManyImages_SetsParallelAndHighCompression()
    {
        var optimizer = CreateOptimizer();
        var html = BuildHtml(imageCount: 21, tableCount: 0, totalLength: 21 * 12);

        var strategy = await optimizer.AnalyzeContentAsync(html);

        Assert.True(strategy.UseParallelProcessing); // ImageCount(21) > 10
        Assert.Equal(CompressionLevel.High, strategy.CompressionLevel); // ImageCount(21) > 20
    }

    [Fact]
    public async Task AnalyzeContentAsync_ManyTables_SetsParallelTrue()
    {
        var optimizer = CreateOptimizer();
        var html = BuildHtml(imageCount: 0, tableCount: 6, totalLength: 6 * 18);

        var strategy = await optimizer.AnalyzeContentAsync(html);

        Assert.True(strategy.UseParallelProcessing); // TableCount(6) > 5
        Assert.Equal(CompressionLevel.Low, strategy.CompressionLevel); // default: no image/length trigger
    }

    [Theory]
    [InlineData(9999, 4096)]
    [InlineData(49999, 8192)]
    [InlineData(99999, 16384)]
    [InlineData(100001, 32768)]
    public async Task AnalyzeContentAsync_ChunkSizeByLength(int htmlLength, int expectedChunkSize)
    {
        var optimizer = CreateOptimizer();
        var html = BuildHtml(imageCount: 0, tableCount: 0, totalLength: htmlLength);

        var strategy = await optimizer.AnalyzeContentAsync(html);

        Assert.Equal(expectedChunkSize, strategy.OptimalChunkSize);
    }

    [Fact]
    public async Task AnalyzeContentAsync_LargeHtml_EnablesCachingAndMediumCompression()
    {
        var optimizer = CreateOptimizer();
        var html = BuildHtml(imageCount: 0, tableCount: 0, totalLength: 150000);

        var strategy = await optimizer.AnalyzeContentAsync(html);

        Assert.True(strategy.EnableCaching); // length(150000) > 50000
        Assert.Equal(CompressionLevel.Medium, strategy.CompressionLevel); // length > 100000
    }

    [Fact]
    public void Dispose_CalledMultipleTimes_DoesNotThrow()
    {
        var optimizer = CreateOptimizer();
        optimizer.Dispose();

        var exception = Record.Exception(() => optimizer.Dispose());

        Assert.Null(exception);
    }
}
