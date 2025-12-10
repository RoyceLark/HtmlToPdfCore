using HtmlToPdfCore.Models;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace HtmlToPdfCore.Interfaces;

/// <summary>
/// Core interface for HTML to PDF rendering
/// </summary>
public interface IHtmlToPdfRenderer
{
    /// <summary>
    /// Renders HTML content to PDF
    /// </summary>
    Task<byte[]> RenderHtmlToPdfAsync(string html, PdfRenderOptions? options = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Renders HTML file to PDF
    /// </summary>
    Task<byte[]> RenderHtmlFileToPdfAsync(string filePath, PdfRenderOptions? options = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Renders URL to PDF
    /// </summary>
    Task<byte[]> RenderUrlToPdfAsync(string url, PdfRenderOptions? options = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Renders HTML to PDF and saves to file
    /// </summary>
    Task RenderHtmlToPdfFileAsync(string html, string outputPath, PdfRenderOptions? options = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// Interface for PDF manipulation operations
/// </summary>
public interface IPdfManipulator
{
    /// <summary>
    /// Merges multiple PDF documents
    /// </summary>
    Task<byte[]> MergePdfsAsync(IEnumerable<byte[]> pdfs, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Splits PDF into separate documents
    /// </summary>
    Task<IEnumerable<byte[]>> SplitPdfAsync(byte[] pdf, int[] pageNumbers, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Adds watermark to PDF
    /// </summary>
    Task<byte[]> AddWatermarkAsync(byte[] pdf, WatermarkOptions options, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Encrypts PDF with password
    /// </summary>
    Task<byte[]> EncryptPdfAsync(byte[] pdf, string userPassword, string? ownerPassword = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Extracts text from PDF
    /// </summary>
    Task<string> ExtractTextAsync(byte[] pdf, CancellationToken cancellationToken = default);
}

/// <summary>
/// Interface for memory optimization using ML
/// </summary>
public interface IMemoryOptimizer
{
    /// <summary>
    /// Predicts optimal memory settings for rendering
    /// </summary>
    Task<MemoryPrediction> PredictMemoryUsageAsync(string html, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Optimizes rendering based on content analysis
    /// </summary>
    Task<OptimizationStrategy> AnalyzeContentAsync(string html, CancellationToken cancellationToken = default);
}
