using System.Text;
using HtmlToPdfCore.AI;
using HtmlToPdfCore.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HtmlToPdfCore.Tests;

public class MemoryOptimizerTests
{
    private static MemoryOptimizer CreateOptimizer() => new MemoryOptimizer(NullLogger<MemoryOptimizer>.Instance);

    /// <summary>
    /// Builds an html string containing the given number of &lt;img&gt; and &lt;table&gt; tags
    /// (each matching the production regexes "&lt;img\s" / "&lt;table\s"), padded with filler
    /// characters so the resulting string has exactly <paramref name="totalLength"/> characters.
    /// </summary>
    private static string BuildHtml(int imageCount, int tableCount, int totalLength)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < imageCount; i++)
            sb.Append("<img src=a> ");
        for (int i = 0; i < tableCount; i++)
            sb.Append("<table border=1></table>");

        if (sb.Length > totalLength)
            throw new ArgumentException("totalLength too small to fit requested tags");

        while (sb.Length < totalLength)
            sb.Append('x');

        return sb.ToString();
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
        using var optimizer = CreateOptimizer();

        await Assert.ThrowsAsync<ArgumentException>(() => optimizer.PredictMemoryUsageAsync(html!));
    }

    [Fact]
    public async Task PredictMemoryUsageAsync_SmallPlainHtml_ReturnsLowPoolSizeNoStreaming()
    {
        using var optimizer = CreateOptimizer();

        // length 19, 0 images, 0 tables -> estimated = 10 + 0 + 0 + 0 = 10
        var result = await optimizer.PredictMemoryUsageAsync("<p>Hello World</p>");

        Assert.Equal(10, result.EstimatedMemoryMB);
        Assert.Equal(10, result.RecommendedPoolSize);
        Assert.False(result.ShouldUseStreaming);
        Assert.Equal(0.7f, result.ConfidenceScore);
    }

    [Fact]
    public async Task PredictMemoryUsageAsync_ManyImages_IncreasesEstimateAndLowersPoolSize()
    {
        using var optimizer = CreateOptimizer();

        // 10 images (12 chars each = 120 chars total, no padding) -> memoryPerKb = 120/1024 = 0
        // estimated = 10 + 0 + (10 * 2) + 0 = 30
        var html = BuildHtml(imageCount: 10, tableCount: 0, totalLength: 120);

        var result = await optimizer.PredictMemoryUsageAsync(html);

        Assert.Equal(30, result.EstimatedMemoryMB);
        Assert.Equal(5, result.RecommendedPoolSize);
        Assert.False(result.ShouldUseStreaming);
    }

    [Fact]
    public async Task PredictMemoryUsageAsync_ManyTablesAndLargeHtml_EnablesStreamingAndSmallestPool()
    {
        using var optimizer = CreateOptimizer();

        // 20 tables, padded to 51200 chars -> memoryPerKb = 51200/1024 = 50
        // estimated = 10 + 50 + 0 + (20 * 5) = 160
        var html = BuildHtml(imageCount: 0, tableCount: 20, totalLength: 51200);

        var result = await optimizer.PredictMemoryUsageAsync(html);

        Assert.Equal(160, result.EstimatedMemoryMB);
        Assert.Equal(2, result.RecommendedPoolSize);
        Assert.True(result.ShouldUseStreaming);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AnalyzeContentAsync_InvalidHtml_ThrowsArgumentException(string? html)
    {
        using var optimizer = CreateOptimizer();

        await Assert.ThrowsAsync<ArgumentException>(() => optimizer.AnalyzeContentAsync(html!));
    }

    [Fact]
    public async Task AnalyzeContentAsync_ManyImages_SetsParallelAndHighCompression()
    {
        using var optimizer = CreateOptimizer();

        // 21 images (> 20) -> CompressionLevel.High; (> 10) -> UseParallelProcessing = true
        var html = BuildHtml(imageCount: 21, tableCount: 0, totalLength: 21 * 12);

        var strategy = await optimizer.AnalyzeContentAsync(html);

        Assert.True(strategy.UseParallelProcessing);
        Assert.Equal(CompressionLevel.High, strategy.CompressionLevel);
        Assert.Equal(4096, strategy.OptimalChunkSize);
        Assert.False(strategy.EnableCaching);
    }

    [Fact]
    public async Task AnalyzeContentAsync_ManyTables_SetsParallelTrue()
    {
        using var optimizer = CreateOptimizer();

        // 6 tables (> 5) -> UseParallelProcessing = true; 0 images, short html -> CompressionLevel.Low
        var html = BuildHtml(imageCount: 0, tableCount: 6, totalLength: 6 * 24);

        var strategy = await optimizer.AnalyzeContentAsync(html);

        Assert.True(strategy.UseParallelProcessing);
        Assert.Equal(CompressionLevel.Low, strategy.CompressionLevel);
    }

    [Theory]
    [InlineData(5000, 4096)]
    [InlineData(20000, 8192)]
    [InlineData(80000, 16384)]
    [InlineData(150000, 32768)]
    public async Task AnalyzeContentAsync_ChunkSizeByLength(int htmlLength, int expectedChunkSize)
    {
        using var optimizer = CreateOptimizer();

        var html = BuildHtml(imageCount: 0, tableCount: 0, totalLength: htmlLength);

        var strategy = await optimizer.AnalyzeContentAsync(html);

        Assert.Equal(expectedChunkSize, strategy.OptimalChunkSize);
    }

    [Fact]
    public async Task AnalyzeContentAsync_LargeHtml_EnablesCachingAndMediumCompression()
    {
        using var optimizer = CreateOptimizer();

        // length 150000 (> 100000), 0 images/tables -> EnableCaching = true, CompressionLevel.Medium
        var html = BuildHtml(imageCount: 0, tableCount: 0, totalLength: 150000);

        var strategy = await optimizer.AnalyzeContentAsync(html);

        Assert.True(strategy.EnableCaching);
        Assert.Equal(CompressionLevel.Medium, strategy.CompressionLevel);
    }

    [Fact]
    public void Dispose_CalledMultipleTimes_DoesNotThrow()
    {
        var optimizer = CreateOptimizer();

        optimizer.Dispose();
        optimizer.Dispose();
    }
}
