using HtmlToPdfCore.Interfaces;
using HtmlToPdfCore.Models;
using HtmlToPdfCore.Renderers;
using HtmlToPdfCore.Services;
using HtmlToPdfCore.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Org.BouncyCastle.Utilities;

namespace HtmlToPdfCore;

/// <summary>
/// Main entry point for HtmlToPdfCore library
/// Provides a simple, fluent API for HTML to PDF conversion
/// </summary>
public class HtmlToPdf : IDisposable
{
    private readonly IHtmlToPdfRenderer _renderer;
    private readonly IPdfManipulator _manipulator;
    private bool _disposed;

    /// <summary>
    /// Creates a new instance of HtmlToPdf with default settings
    /// </summary>
    public HtmlToPdf() : this(NullLoggerFactory.Instance)
    {
    }

    /// <summary>
    /// Creates a new instance of HtmlToPdf with custom logger factory
    /// </summary>
    public HtmlToPdf(ILoggerFactory loggerFactory)
    {
        if (loggerFactory == null)
            throw new ArgumentNullException(nameof(loggerFactory));

        var rendererLogger = loggerFactory.CreateLogger<HtmlToPdfRenderer>();
        var manipulatorLogger = loggerFactory.CreateLogger<PdfManipulator>();
        var optimizerLogger = loggerFactory.CreateLogger<MemoryOptimizer>();

        var memoryOptimizer = new MemoryOptimizer(optimizerLogger);
        _renderer = new HtmlToPdfRenderer(rendererLogger, memoryOptimizer);
        _manipulator = new PdfManipulator(manipulatorLogger);
    }

    /// <summary>
    /// Creates a new instance of HtmlToPdf with custom services
    /// </summary>
    public HtmlToPdf(IHtmlToPdfRenderer renderer, IPdfManipulator manipulator)
    {
        _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
        _manipulator = manipulator ?? throw new ArgumentNullException(nameof(manipulator));
    }

    #region Static Factory Methods

    /// <summary>
    /// Creates a new HtmlToPdf instance
    /// </summary>
    public static HtmlToPdf Create() => new HtmlToPdf();

    /// <summary>
    /// Creates a new HtmlToPdf instance with logger factory
    /// </summary>
    public static HtmlToPdf Create(ILoggerFactory loggerFactory) => new HtmlToPdf(loggerFactory);

    #endregion

    #region Rendering Methods

    /// <summary>
    /// Renders HTML string to PDF
    /// </summary>
    public async Task<byte[]> RenderAsync(
        string html,
        Action<PdfRenderOptions>? configure = null,
        CancellationToken cancellationToken = default)
    {
        var options = new PdfRenderOptions();
        configure?.Invoke(options);
        return await _renderer.RenderHtmlToPdfAsync(html, options, cancellationToken);
    }

    /// <summary>
    /// Renders HTML file to PDF
    /// </summary>
    public async Task<byte[]> RenderFileAsync(
        string filePath,
        Action<PdfRenderOptions>? configure = null,
        CancellationToken cancellationToken = default)
    {
        var options = new PdfRenderOptions();
        configure?.Invoke(options);
        return await _renderer.RenderHtmlFileToPdfAsync(filePath, options, cancellationToken);
    }

    /// <summary>
    /// Renders URL to PDF
    /// </summary>
    public async Task<byte[]> RenderUrlAsync(
        string url,
        Action<PdfRenderOptions>? configure = null,
        CancellationToken cancellationToken = default)
    {
        var options = new PdfRenderOptions();
        configure?.Invoke(options);
        return await _renderer.RenderUrlToPdfAsync(url, options, cancellationToken);
    }

    /// <summary>
    /// Renders HTML to PDF and saves to file
    /// </summary>
    public async Task RenderToFileAsync(
        string html,
        string outputPath,
        Action<PdfRenderOptions>? configure = null,
        CancellationToken cancellationToken = default)
    {
        var options = new PdfRenderOptions();
        configure?.Invoke(options);
        await _renderer.RenderHtmlToPdfFileAsync(html, outputPath, options, cancellationToken);
    }

    #endregion

    #region Fluent API

    /// <summary>
    /// Starts a fluent configuration chain
    /// </summary>
    public FluentPdfBuilder FromHtml(string html) => new FluentPdfBuilder(this, html);

    /// <summary>
    /// Starts a fluent configuration chain from file
    /// </summary>
    public FluentPdfBuilder FromFile(string filePath) => new FluentPdfBuilder(this, filePath, isFile: true);

    /// <summary>
    /// Starts a fluent configuration chain from URL
    /// </summary>
    public FluentPdfBuilder FromUrl(string url) => new FluentPdfBuilder(this, url, isUrl: true);

    #endregion

    #region PDF Manipulation

    /// <summary>
    /// Merges multiple PDFs into one
    /// </summary>
    public async Task<byte[]> MergeAsync(
        IEnumerable<byte[]> pdfs,
        CancellationToken cancellationToken = default)
    {
        return await _manipulator.MergePdfsAsync(pdfs, cancellationToken);
    }

    /// <summary>
    /// Splits a PDF into separate documents
    /// </summary>
    public async Task<IEnumerable<byte[]>> SplitAsync(
        byte[] pdf,
        int[] pageNumbers,
        CancellationToken cancellationToken = default)
    {
        return await _manipulator.SplitPdfAsync(pdf, pageNumbers, cancellationToken);
    }

    /// <summary>
    /// Adds a watermark to PDF
    /// </summary>
    public async Task<byte[]> AddWatermarkAsync(
        byte[] pdf,
        string text,
        Action<WatermarkOptions>? configure = null,
        CancellationToken cancellationToken = default)
    {
        var options = new WatermarkOptions { Text = text };
        configure?.Invoke(options);
        return await _manipulator.AddWatermarkAsync(pdf, options, cancellationToken);
    }

    /// <summary>
    /// Encrypts PDF with password
    /// </summary>
    public async Task<byte[]> EncryptAsync(
        byte[] pdf,
        string userPassword,
        string? ownerPassword = null,
        CancellationToken cancellationToken = default)
    {
        return await _manipulator.EncryptPdfAsync(pdf, userPassword, ownerPassword, cancellationToken);
    }

    /// <summary>
    /// Extracts text from PDF
    /// </summary>
    public async Task<string> ExtractTextAsync(
        byte[] pdf,
        CancellationToken cancellationToken = default)
    {
        return await _manipulator.ExtractTextAsync(pdf, cancellationToken);
    }

    #endregion

    public void Dispose()
    {
        if (_disposed)
            return;

        if (_renderer is IDisposable rendererDisposable)
            rendererDisposable.Dispose();

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Fluent builder for PDF rendering
/// </summary>
public class FluentPdfBuilder
{
    private readonly HtmlToPdf _htmlToPdf;
    private readonly string _source;
    private readonly bool _isFile;
    private readonly bool _isUrl;
    private readonly PdfRenderOptions _options;

    internal FluentPdfBuilder(HtmlToPdf htmlToPdf, string source, bool isFile = false, bool isUrl = false)
    {
        _htmlToPdf = htmlToPdf;
        _source = source;
        _isFile = isFile;
        _isUrl = isUrl;
        _options = new PdfRenderOptions();
    }

    public FluentPdfBuilder WithPageSize(PageSize pageSize)
    {
        _options.PageSize = pageSize;
        return this;
    }

    public FluentPdfBuilder WithOrientation(PageOrientation orientation)
    {
        _options.Orientation = orientation;
        return this;
    }

    public FluentPdfBuilder WithMargins(float top, float right, float bottom, float left)
    {
        _options.Margins = new PageMargins(top, right, bottom, left);
        return this;
    }

    public FluentPdfBuilder WithHeader(string html)
    {
        _options.Header = new HeaderFooterOptions { HtmlContent = html };
        return this;
    }

    public FluentPdfBuilder WithFooter(string html, bool showPageNumbers = false)
    {
        _options.Footer = new HeaderFooterOptions
        {
            HtmlContent = html,
            ShowPageNumbers = showPageNumbers
        };
        return this;
    }

    public FluentPdfBuilder WithMetadata(Action<PdfMetadata> configure)
    {
        configure(_options.Metadata);
        return this;
    }

    public FluentPdfBuilder EnableJavaScript(bool enable = true)
    {
        _options.EnableJavaScript = enable;
        return this;
    }

    public FluentPdfBuilder WithCustomCss(string css)
    {
        _options.CustomCss = css;
        return this;
    }

    public FluentPdfBuilder WithDpi(int dpi)
    {
        _options.Dpi = dpi;
        return this;
    }

    public FluentPdfBuilder AsGrayscale(bool grayscale = true)
    {
        _options.Grayscale = grayscale;
        return this;
    }

    public FluentPdfBuilder WithImageQuality(int quality)
    {
        _options.ImageQuality = quality;
        return this;
    }

    public async Task<byte[]> GenerateAsync(CancellationToken cancellationToken = default)
    {
        if (_isFile)
            return await _htmlToPdf.RenderFileAsync(_source, opts => CopyOptions(opts, _options), cancellationToken);
        else if (_isUrl)
            return await _htmlToPdf.RenderUrlAsync(_source, opts => CopyOptions(opts, _options), cancellationToken);
        else
            return await _htmlToPdf.RenderAsync(_source, opts => CopyOptions(opts, _options), cancellationToken);
    }

    public async Task<byte[]> SaveAsync(string outputPath, CancellationToken cancellationToken = default)
    {
        var pdfBytes = await GenerateAsync(cancellationToken);
        // await File.WriteAllBytesAsync(outputPath, pdfBytes, cancellationToken);
        return pdfBytes;
    }

    private void CopyOptions(PdfRenderOptions target, PdfRenderOptions source)
    {
        target.PageSize = source.PageSize;
        target.Orientation = source.Orientation;
        target.Margins = source.Margins;
        target.EnableJavaScript = source.EnableJavaScript;
        target.RenderTimeout = source.RenderTimeout;
        target.MediaType = source.MediaType;
        target.CustomCss = source.CustomCss;
        target.BaseUrl = source.BaseUrl;
        target.Header = source.Header;
        target.Footer = source.Footer;
        target.PrintBackground = source.PrintBackground;
        target.CompressImages = source.CompressImages;
        target.ImageQuality = source.ImageQuality;
        target.EnableAIOptimization = source.EnableAIOptimization;
        target.Dpi = source.Dpi;
        target.Grayscale = source.Grayscale;
        target.Metadata = source.Metadata;
    }
}
