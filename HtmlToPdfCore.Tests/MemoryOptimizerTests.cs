using HtmlToPdfCore.AI;
using HtmlToPdfCore.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HtmlToPdfCore.Tests;

public class MemoryOptimizerTests
{
    private static MemoryOptimizer CreateOptimizer() => new(NullLogger<MemoryOptimizer>.Instance);

    /// <summary>
    /// Builds html containing an exact, regex-matchable number of '&lt;img src=a&gt; ' (12 chars each,
    /// matches &lt;img\s) and '&lt;table &gt;&lt;/table&gt;' (16 chars each, matches &lt;table\s) tags,
    /// padded with 'x' characters so the resulting string length equals totalLength exactly.
    /// </summary>
    private static string BuildHtml(int imageCount, int tableCount, int totalLength)
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < imageCount; i++)
            sb.Append("<img src=a> ");
        for (int i = 0; i < tableCount; i++)
            sb.Append("<table ></table>");

        var padding = totalLength - sb.Length;
        if (padding > 0)
            sb.Append('x', padding);

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
        var optimizer = CreateOptimizer();
        await Assert.ThrowsAsync<ArgumentException>(() => optimizer.PredictMemoryUsageAsync(html!));
    }

    [Fact]
    public async Task PredictMemoryUsageAsync_SmallPlainHtml_ReturnsLowPoolSizeNoStreaming()
    {
        var optimizer = CreateOptimizer();
        var html = BuildHtml(imageCount: 0, tableCount: 0, totalLength: 20);

        MemoryPrediction result = await optimizer.PredictMemoryUsageAsync(html);

        Assert.Equal(10, result.EstimatedMemoryMB);
        Assert.Equal(10, result.RecommendedPoolSize);
        Assert.False(result.ShouldUseStreaming);
        Assert.Equal(0.7f, result.ConfidenceScore);
    }

    [Fact]
    public async Task PredictMemoryUsageAsync_ManyImages_IncreasesEstimateAndLowersPoolSize()
    {
        var optimizer = CreateOptimizer();
        // 10 images * 12 chars = 120 chars total, well under 1024 so memoryPerKb contributes 0.
        var html = BuildHtml(imageCount: 10, tableCount: 0, totalLength: 120);

        MemoryPrediction result = await optimizer.PredictMemoryUsageAsync(html);

        Assert.Equal(30, result.EstimatedMemoryMB);
        Assert.Equal(5, result.RecommendedPoolSize);
        Assert.False(result.ShouldUseStreaming);
    }

    [Fact]
    public async Task PredictMemoryUsageAsync_ManyTablesAndLargeHtml_EnablesStreamingAndSmallestPool()
    {
        var optimizer = CreateOptimizer();
        // 20 tables (table memory = 20*5 = 100) + 51200 total chars (memoryPerKb = 51200/1024 = 50).
        var html = BuildHtml(imageCount: 0, tableCount: 20, totalLength: 51200);

        MemoryPrediction result = await optimizer.PredictMemoryUsageAsync(html);

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
        var optimizer = CreateOptimizer();
        await Assert.ThrowsAsync<ArgumentException>(() => optimizer.AnalyzeContentAsync(html!));
    }

    [Fact]
    public async Task AnalyzeContentAsync_ManyImages_SetsParallelAndHighCompression()
    {
        var optimizer = CreateOptimizer();
        var html = BuildHtml(imageCount: 21, tableCount: 0, totalLength: 300);

        OptimizationStrategy result = await optimizer.AnalyzeContentAsync(html);

        Assert.True(result.UseParallelProcessing);
        Assert.Equal(CompressionLevel.High, result.CompressionLevel);
    }

    [Fact]
    public async Task AnalyzeContentAsync_ManyTables_SetsParallelTrue()
    {
        var optimizer = CreateOptimizer();
        var html = BuildHtml(imageCount: 0, tableCount: 6, totalLength: 300);

        OptimizationStrategy result = await optimizer.AnalyzeContentAsync(html);

        Assert.True(result.UseParallelProcessing);
        Assert.Equal(CompressionLevel.Low, result.CompressionLevel);
    }

    [Theory]
    [InlineData(9999, 4096)]
    [InlineData(49999, 8192)]
    [InlineData(99999, 16384)]
    [InlineData(100001, 32768)]
    public async Task AnalyzeContentAsync_ChunkSizeByLength(int length, int expectedChunkSize)
    {
        var optimizer = CreateOptimizer();
        var html = BuildHtml(imageCount: 0, tableCount: 0, totalLength: length);

        OptimizationStrategy result = await optimizer.AnalyzeContentAsync(html);

        Assert.Equal(expectedChunkSize, result.OptimalChunkSize);
    }

    [Fact]
    public async Task AnalyzeContentAsync_LargeHtml_EnablesCachingAndMediumCompression()
    {
        var optimizer = CreateOptimizer();
        var html = BuildHtml(imageCount: 0, tableCount: 0, totalLength: 150000);

        OptimizationStrategy result = await optimizer.AnalyzeContentAsync(html);

        Assert.True(result.EnableCaching);
        Assert.Equal(CompressionLevel.Medium, result.CompressionLevel);
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
