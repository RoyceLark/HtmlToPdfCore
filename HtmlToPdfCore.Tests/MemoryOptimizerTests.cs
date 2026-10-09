using System.Text;
using HtmlToPdfCore.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HtmlToPdfCore.Tests;

public class MemoryOptimizerTests
{
    private static MemoryOptimizer CreateOptimizer() =>
        new MemoryOptimizer(NullLogger<MemoryOptimizer>.Instance);

    /// <summary>
    /// Builds html containing exactly <paramref name="imageCount"/> regex-matchable "&lt;img " tags
    /// and <paramref name="tableCount"/> regex-matchable "&lt;table " tags, then pads with 'x'
    /// characters so the resulting string has exactly <paramref name="totalLength"/> characters.
    /// </summary>
    private static string BuildHtml(int imageCount, int tableCount, int totalLength)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < imageCount; i++)
            sb.Append("<img src=a> ");
        for (int i = 0; i < tableCount; i++)
            sb.Append("<table ></table>");

        if (sb.Length > totalLength)
            throw new InvalidOperationException("totalLength too small for requested tag counts");

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
        var optimizer = CreateOptimizer();
        await Assert.ThrowsAsync<ArgumentException>(() => optimizer.PredictMemoryUsageAsync(html!));
    }

    [Fact]
    public async Task PredictMemoryUsageAsync_SmallPlainHtml_ReturnsLowPoolSizeNoStreaming()
    {
        var optimizer = CreateOptimizer();
        var html = BuildHtml(imageCount: 0, tableCount: 0, totalLength: 20);

        var result = await optimizer.PredictMemoryUsageAsync(html);

        Assert.Equal(10L, result.EstimatedMemoryMB);
        Assert.Equal(10, result.RecommendedPoolSize);
        Assert.False(result.ShouldUseStreaming);
        Assert.Equal(0.7f, result.ConfidenceScore);
    }

    [Fact]
    public async Task PredictMemoryUsageAsync_ManyImages_IncreasesEstimateAndLowersPoolSize()
    {
        var optimizer = CreateOptimizer();
        // 10 images of 12 chars each = 120 chars total, no padding needed (length/1024 == 0).
        var html = BuildHtml(imageCount: 10, tableCount: 0, totalLength: 120);

        var result = await optimizer.PredictMemoryUsageAsync(html);

        Assert.Equal(30L, result.EstimatedMemoryMB);
        Assert.Equal(5, result.RecommendedPoolSize);
        Assert.False(result.ShouldUseStreaming);
    }

    [Fact]
    public async Task PredictMemoryUsageAsync_ManyTablesAndLargeHtml_EnablesStreamingAndSmallestPool()
    {
        var optimizer = CreateOptimizer();
        // 20 tables, padded to 51200 chars => memoryPerKb=50, tableMemory=100, estimate=160.
        var html = BuildHtml(imageCount: 0, tableCount: 20, totalLength: 51200);

        var result = await optimizer.PredictMemoryUsageAsync(html);

        Assert.Equal(160L, result.EstimatedMemoryMB);
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
        var html = BuildHtml(imageCount: 21, tableCount: 0, totalLength: 21 * 12);

        var result = await optimizer.AnalyzeContentAsync(html);

        Assert.True(result.UseParallelProcessing);
        Assert.Equal(Models.CompressionLevel.High, result.CompressionLevel);
    }

    [Fact]
    public async Task AnalyzeContentAsync_ManyTables_SetsParallelTrue()
    {
        var optimizer = CreateOptimizer();
        var html = BuildHtml(imageCount: 0, tableCount: 6, totalLength: 6 * 16);

        var result = await optimizer.AnalyzeContentAsync(html);

        Assert.True(result.UseParallelProcessing);
        Assert.Equal(Models.CompressionLevel.Low, result.CompressionLevel);
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

        var result = await optimizer.AnalyzeContentAsync(html);

        Assert.Equal(expectedChunkSize, result.OptimalChunkSize);
    }

    [Fact]
    public async Task AnalyzeContentAsync_LargeHtml_EnablesCachingAndMediumCompression()
    {
        var optimizer = CreateOptimizer();
        var html = BuildHtml(imageCount: 0, tableCount: 0, totalLength: 150000);

        var result = await optimizer.AnalyzeContentAsync(html);

        Assert.True(result.EnableCaching);
        Assert.Equal(Models.CompressionLevel.Medium, result.CompressionLevel);
    }

    [Fact]
    public void Dispose_CalledMultipleTimes_DoesNotThrow()
    {
        var optimizer = CreateOptimizer();

        optimizer.Dispose();
        optimizer.Dispose();
    }
}
