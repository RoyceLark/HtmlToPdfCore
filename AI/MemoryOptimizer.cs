using HtmlToPdfCore.Interfaces;
using HtmlToPdfCore.Models;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace HtmlToPdfCore.AI;

public class MemoryOptimizer : IMemoryOptimizer, IDisposable
{
    private readonly ILogger<MemoryOptimizer> _logger;
    private bool _disposed;

    public MemoryOptimizer(ILogger<MemoryOptimizer> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<MemoryPrediction> PredictMemoryUsageAsync(
        string html,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(html))
            throw new ArgumentException("HTML cannot be null or empty", nameof(html));

        return await Task.Run(() =>
        {
            try
            {
                var features = ExtractFeatures(html);
                return PredictWithHeuristics(features);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error predicting memory usage, using fallback");
                return new MemoryPrediction
                {
                    EstimatedMemoryMB = 50,
                    RecommendedPoolSize = 5,
                    ShouldUseStreaming = false,
                    ConfidenceScore = 0.5f
                };
            }
        }, cancellationToken);
    }

    public async Task<OptimizationStrategy> AnalyzeContentAsync(
        string html,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(html))
            throw new ArgumentException("HTML cannot be null or empty", nameof(html));

        return await Task.Run(() =>
        {
            try
            {
                var features = ExtractFeatures(html);

                var strategy = new OptimizationStrategy
                {
                    UseParallelProcessing = features.ImageCount > 10 || features.TableCount > 5,
                    OptimalChunkSize = CalculateChunkSize(features),
                    EnableCaching = features.HtmlLength > 50000,
                    CompressionLevel = DetermineCompressionLevel(features)
                };

                _logger.LogInformation(
                    "Optimization strategy: Parallel={Parallel}, ChunkSize={ChunkSize}, Cache={Cache}, Compression={Compression}",
                    strategy.UseParallelProcessing,
                    strategy.OptimalChunkSize,
                    strategy.EnableCaching,
                    strategy.CompressionLevel);

                return strategy;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error analyzing content, using default strategy");
                return new OptimizationStrategy
                {
                    UseParallelProcessing = false,
                    OptimalChunkSize = 1024,
                    EnableCaching = true,
                    CompressionLevel = CompressionLevel.Medium
                };
            }
        }, cancellationToken);
    }

    private HtmlFeatures ExtractFeatures(string html)
    {
        return new HtmlFeatures
        {
            HtmlLength = html.Length,
            ImageCount = CountOccurrences(html, @"<img\s"),
            TableCount = CountOccurrences(html, @"<table\s"),
            DivCount = CountOccurrences(html, @"<div\s"),
            CssLength = ExtractCssLength(html),
            ScriptCount = CountOccurrences(html, @"<script\s")
        };
    }

    private int CountOccurrences(string text, string pattern)
    {
        try
        {
            return Regex.Matches(text, pattern, RegexOptions.IgnoreCase).Count;
        }
        catch
        {
            return 0;
        }
    }

    private int ExtractCssLength(string html)
    {
        try
        {
            var styleMatches = Regex.Matches(html, @"<style[^>]*>(.*?)</style>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            return styleMatches.Cast<Match>().Sum(m => m.Groups[1].Value.Length);
        }
        catch
        {
            return 0;
        }
    }

    private MemoryPrediction PredictWithHeuristics(HtmlFeatures features)
    {
        long baseMemory = 10;
        long memoryPerKb = features.HtmlLength / 1024;
        long imageMemory = features.ImageCount * 2;
        long tableMemory = features.TableCount * 5;

        long estimatedMemory = baseMemory + memoryPerKb + imageMemory + tableMemory;

        return new MemoryPrediction
        {
            EstimatedMemoryMB = estimatedMemory,
            RecommendedPoolSize = CalculatePoolSize((float)estimatedMemory),
            ShouldUseStreaming = estimatedMemory > 100,
            ConfidenceScore = 0.7f
        };
    }

    private int CalculatePoolSize(float estimatedMemoryMB)
    {
        if (estimatedMemoryMB < 20)
            return 10;
        else if (estimatedMemoryMB < 50)
            return 5;
        else if (estimatedMemoryMB < 100)
            return 3;
        else
            return 2;
    }

    private int CalculateChunkSize(HtmlFeatures features)
    {
        if (features.HtmlLength < 10000)
            return 4096;
        else if (features.HtmlLength < 50000)
            return 8192;
        else if (features.HtmlLength < 100000)
            return 16384;
        else
            return 32768;
    }

    private CompressionLevel DetermineCompressionLevel(HtmlFeatures features)
    {
        if (features.ImageCount > 20)
            return CompressionLevel.High;
        else if (features.ImageCount > 10)
            return CompressionLevel.Medium;
        else if (features.HtmlLength > 100000)
            return CompressionLevel.Medium;
        else
            return CompressionLevel.Low;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}

public class HtmlFeatures
{
    public int HtmlLength { get; set; }
    public int ImageCount { get; set; }
    public int TableCount { get; set; }
    public int DivCount { get; set; }
    public int CssLength { get; set; }
    public int ScriptCount { get; set; }
}