using System;
using System.Text;
using System.Threading.Tasks;
using HtmlToPdfCore.AI;
using HtmlToPdfCore.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HtmlToPdfCore.Tests;

public class MemoryOptimizerTests
{
    /// <summary>
    /// Builds an HTML string with an exact number of "&lt;img ...&gt;" and
    /// "&lt;table ...&gt;" matches for MemoryOptimizer's regexes ("&lt;img\s" / "&lt;table\s"),
    /// padded with filler characters so the overall length is exactly <paramref name="totalLength"/>
    /// (or the natural tag content length, if that is already longer).
    /// </summary>
    private static string BuildHtml(int imageCount, int tableCount, int totalLength)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < imageCount; i++)
        {
            sb.Append("<img src=\"a\"> ");
        }
        for (var i = 0; i < tableCount; i++)
        {
            sb.Append("<table border=\"1\"></table>");
        }
        if (sb.Length < totalLength)
        {
            sb.Append('x', totalLength - sb.Length);
        }
        return sb.ToString();
    }

    private static MemoryOptimizer CreateOptimizer() =>
        new MemoryOptimizer(NullLogger<MemoryOptimizer>.Instance);

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
        const string html = "<p>Hello World</p><p>This is a simple paragraph with no images or tables.</p>";

        var prediction = await optimizer.PredictMemoryUsageAsync(html);

        Assert.Equal(10, prediction.EstimatedMemoryMB);
        Assert.Equal(10, prediction.RecommendedPoolSize);
        Assert.False(prediction.ShouldUseStreaming);
        Assert.Equal(0.7f, prediction.ConfidenceScore);
    }

    [Fact]
    public async Task PredictMemoryUsageAsync_ManyImages_IncreasesEstimateAndLowersPoolSize()
    {
        var optimizer = CreateOptimizer();
        var html = BuildHtml(imageCount: 10, tableCount: 0, totalLength: 200);

        var prediction = await optimizer.PredictMemoryUsageAsync(html);

        // baseMemory(10) + length/1024(0, length=200) + images(10*2=20) + tables(0) = 30
        Assert.Equal(30, prediction.EstimatedMemoryMB);
        // 20 <= 30 < 50 -> pool size 5
        Assert.Equal(5, prediction.RecommendedPoolSize);
    }

    [Fact]
    public async Task PredictMemoryUsageAsync_ManyTablesAndLargeHtml_EnablesStreamingAndSmallestPool()
    {
        var optimizer = CreateOptimizer();
        var html = BuildHtml(imageCount: 0, tableCount: 20, totalLength: 51200);

        var prediction = await optimizer.PredictMemoryUsageAsync(html);

        // baseMemory(10) + length/1024(51200/1024=50) + images(0) + tables(20*5=100) = 160
        Assert.Equal(160, prediction.EstimatedMemoryMB);
        Assert.True(prediction.ShouldUseStreaming);
        Assert.Equal(2, prediction.RecommendedPoolSize);
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
        var html = BuildHtml(imageCount: 21, tableCount: 0, totalLength: 200);

        var strategy = await optimizer.AnalyzeContentAsync(html);

        Assert.True(strategy.UseParallelProcessing);
        Assert.Equal(CompressionLevel.High, strategy.CompressionLevel);
    }

    [Fact]
    public async Task AnalyzeContentAsync_ManyTables_SetsParallelTrue()
    {
        var optimizer = CreateOptimizer();
        var html = BuildHtml(imageCount: 0, tableCount: 6, totalLength: 200);

        var strategy = await optimizer.AnalyzeContentAsync(html);

        Assert.True(strategy.UseParallelProcessing);
        Assert.Equal(CompressionLevel.Low, strategy.CompressionLevel);
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
        var html = BuildHtml(imageCount: 0, tableCount: 0, totalLength: 100001);

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
