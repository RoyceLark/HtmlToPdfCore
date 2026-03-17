using HtmlToPdfCore.Interfaces;
using HtmlToPdfCore.Models;
using HtmlToPdfCore.Renderers;
using HtmlToPdfCore.Services;
using HtmlToPdfCore.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text;
using System.Linq;
using iText.Kernel.Pdf;
using iText.Kernel.Colors;
using iText.Layout;
using iText.Layout.Element;
using iText.Layout.Properties;
using iText.Kernel.Font;
using iText.Html2pdf;
using iText.IO.Font.Constants;
using iText.IO.Image;
using System.Collections;
using System.Collections.Generic;

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
    /// Rendering options to be used for all render calls.
    /// </summary>
    public MLPdfRenderOptions RenderingOptions { get; set; } = new MLPdfRenderOptions();

    /// <summary>
    /// Alias for RenderingOptions to match "PrintOptions" terminology
    /// </summary>
    public MLPdfRenderOptions PrintOptions 
    { 
        get => RenderingOptions; 
        set => RenderingOptions = value; 
    }

    /// <summary>
    /// License key for the library
    /// </summary>
    public static string? LicenseKey { get; set; }

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
    /// Protected constructor for derived classes to inject their own renderer
    /// </summary>
    protected HtmlToPdf(IHtmlToPdfRenderer renderer, ILoggerFactory loggerFactory)
    {
        _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
        var manipulatorLogger = loggerFactory.CreateLogger<PdfManipulator>();
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
    public async Task<PdfDocument> RenderAsync(
        string html,
        MLPdfRenderOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var finalOptions = options ?? RenderingOptions;
        var data = await _renderer.RenderHtmlToPdfAsync(html, finalOptions, cancellationToken);
        return new PdfDocument(data);
    }

    /// <summary>
    /// Alias for RenderAsync
    /// </summary>
    public async Task<PdfDocument> RenderHtmlAsPdfAsync(string html, MLPdfRenderOptions? options = null) => await RenderAsync(html, options);

    /// <summary>
    /// Synchronous version of RenderHtmlAsPdfAsync
    /// </summary>
    public PdfDocument RenderHtmlAsPdf(string html, MLPdfRenderOptions? options = null) => RenderHtmlAsPdfAsync(html, options).GetAwaiter().GetResult();

    /// <summary>
    /// Renders HTML string to PDF with a configuration action
    /// </summary>
    public async Task<PdfDocument> RenderAsync(
        string html,
        Action<MLPdfRenderOptions> configure,
        CancellationToken cancellationToken = default)
    {
        var options = new MLPdfRenderOptions();
        // Copy current instance defaults
        CopyOptions(options, RenderingOptions);
        configure?.Invoke(options);
        var data = await _renderer.RenderHtmlToPdfAsync(html, options, cancellationToken);
        return new PdfDocument(data);
    }

    /// <summary>
    /// Renders HTML file to PDF
    /// </summary>
    public async Task<PdfDocument> RenderFileAsync(
        string filePath,
        MLPdfRenderOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var finalOptions = options ?? RenderingOptions;
        var data = await _renderer.RenderHtmlFileToPdfAsync(filePath, finalOptions, cancellationToken);
        return new PdfDocument(data);
    }

    /// <summary>
    /// Alias for RenderFileAsync 
    /// </summary>
    public async Task<PdfDocument> RenderHtmlFileAsPdfAsync(string filePath, MLPdfRenderOptions? options = null) => await RenderFileAsync(filePath, options);

    /// <summary>
    /// Synchronous version of RenderHtmlFileAsPdfAsync
    /// </summary>
    public PdfDocument RenderHtmlFileAsPdf(string filePath, MLPdfRenderOptions? options = null) => RenderHtmlFileAsPdfAsync(filePath, options).GetAwaiter().GetResult();

    /// <summary>
    /// Another alias for RenderFileAsync
    /// </summary>
    public async Task<PdfDocument> RenderFileAsPdfAsync(string filePath, MLPdfRenderOptions? options = null) => await RenderFileAsync(filePath, options);

    /// <summary>
    /// Renders URL to PDF
    /// </summary>
    public async Task<PdfDocument> RenderUrlAsync(
        string url,
        MLPdfRenderOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var finalOptions = options ?? RenderingOptions;
        var data = await _renderer.RenderUrlToPdfAsync(url, finalOptions, cancellationToken);
        return new PdfDocument(data);
    }

    /// <summary>
    /// Alias for RenderUrlAsync
    /// </summary>
    public async Task<PdfDocument> RenderUrlAsPdfAsync(string url, MLPdfRenderOptions? options = null) => await RenderUrlAsync(url, options);

    /// <summary>
    /// Synchronous version of RenderUrlAsPdfAsync
    /// </summary>
    public PdfDocument RenderUrlAsPdf(string url, MLPdfRenderOptions? options = null) => RenderUrlAsPdfAsync(url, options).GetAwaiter().GetResult();

    /// <summary>
    /// Renders HTML to PDF and saves to file
    /// </summary>
    public async Task RenderToFileAsync(
        string html,
        string outputPath,
        MLPdfRenderOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var finalOptions = options ?? RenderingOptions;
        await _renderer.RenderHtmlToPdfFileAsync(html, outputPath, finalOptions, cancellationToken);
    }

    /// <summary>
    /// Converts an image file to a PDF
    /// </summary>
    public async Task<PdfDocument> ImageToPdfAsync(string imagePath, MLPdfRenderOptions? options = null)
    {
        var fullPath = Path.GetFullPath(imagePath);
        var html = $@"<html><body style='margin:0;padding:0;background-color:white;'><img src='{new Uri(fullPath).AbsoluteUri}' style='width:100%;height:auto;display:block;'></body></html>";
        return await RenderAsync(html, options);
    }

    /// <summary>
    /// Synchronous version of ImageToPdfAsync
    /// </summary>
    public PdfDocument ImageToPdf(string imagePath, MLPdfRenderOptions? options = null) => ImageToPdfAsync(imagePath, options).GetAwaiter().GetResult();

    /// <summary>
    /// Converts multiple image files to a single PDF
    /// </summary>
    public async Task<PdfDocument> ImagesToPdfAsync(IEnumerable<string> imagePaths, MLPdfRenderOptions? options = null)
    {
        var sb = new StringBuilder();
        sb.Append("<html><body style='margin:0;padding:0;background-color:white;'>");
        foreach (var path in imagePaths)
        {
            var fullPath = Path.GetFullPath(path);
            sb.Append($"<img src='{new Uri(fullPath).AbsoluteUri}' style='width:100%;height:auto;display:block;page-break-after:always;'>");
        }
        sb.Append("</body></html>");
        return await RenderAsync(sb.ToString(), options);
    }

    /// <summary>
    /// Synchronous version of ImagesToPdfAsync
    /// </summary>
    public PdfDocument ImagesToPdf(IEnumerable<string> imagePaths, MLPdfRenderOptions? options = null) => ImagesToPdfAsync(imagePaths, options).GetAwaiter().GetResult();

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
    public async Task<PdfDocument> MergeAsync(
        IEnumerable<byte[]> pdfs,
        CancellationToken cancellationToken = default)
    {
        var data = await _manipulator.MergePdfsAsync(pdfs, cancellationToken);
        return new PdfDocument(data);
    }

    public async Task<PdfDocument> MergeAsync(
        IEnumerable<PdfDocument> pdfs,
        CancellationToken cancellationToken = default)
    {
        return await MergeAsync(pdfs.Select(p => p.BinaryData), cancellationToken);
    }

    /// <summary>
    /// Splits a PDF into separate documents
    /// </summary>
    public async Task<IEnumerable<PdfDocument>> SplitAsync(
        byte[] pdf,
        int[] pageNumbers,
        CancellationToken cancellationToken = default)
    {
        var results = await _manipulator.SplitPdfAsync(pdf, pageNumbers, cancellationToken);
        return results.Select(r => new PdfDocument(r));
    }

    /// <summary>
    /// Adds a watermark to PDF
    /// </summary>
    public async Task<PdfDocument> AddWatermarkAsync(
        byte[] pdf,
        string text,
        Action<WatermarkOptions>? configure = null,
        CancellationToken cancellationToken = default)
    {
        var options = new WatermarkOptions { Text = text };
        configure?.Invoke(options);
        var data = await _manipulator.AddWatermarkAsync(pdf, options, cancellationToken);
        return new PdfDocument(data);
    }

    /// <summary>
    /// Encrypts PDF with password
    /// </summary>
    public async Task<PdfDocument> EncryptAsync(
        byte[] pdf,
        string userPassword,
        string? ownerPassword = null,
        CancellationToken cancellationToken = default)
    {
        var data = await _manipulator.EncryptPdfAsync(pdf, userPassword, ownerPassword, cancellationToken);
        return new PdfDocument(data);
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

    public async Task<PdfDocument> AddWatermarkAsync(PdfDocument pdf, string text, Action<WatermarkOptions>? configure = null, CancellationToken cancellationToken = default)
        => await AddWatermarkAsync(pdf.BinaryData, text, configure, cancellationToken);

    public async Task<PdfDocument> EncryptAsync(PdfDocument pdf, string userPassword, string? ownerPassword = null, CancellationToken cancellationToken = default)
        => await EncryptAsync(pdf.BinaryData, userPassword, ownerPassword, cancellationToken);

    public async Task<string> ExtractTextAsync(PdfDocument pdf, CancellationToken cancellationToken = default)
        => await ExtractTextAsync(pdf.BinaryData, cancellationToken);

    public async Task<IEnumerable<PdfDocument>> SplitAsync(PdfDocument pdf, int[] pageNumbers, CancellationToken cancellationToken = default)
        => await SplitAsync(pdf.BinaryData, pageNumbers, cancellationToken);

    #endregion

    public void CopyOptions(PdfRenderOptions target, PdfRenderOptions source)
    {
        if (target == null || source == null) return;
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
        target.CreatePdfFormsFromHtml = source.CreatePdfFormsFromHtml;
        target.Watermark = source.Watermark;
        target.Security = source.Security;
        target.ViewPortWidth = source.ViewPortWidth;
        target.RenderDelay = source.RenderDelay;
        target.CustomPageWidth = source.CustomPageWidth;
        target.CustomPageHeight = source.CustomPageHeight;
        target.FirstPageNumber = source.FirstPageNumber;
        target.Javascript = source.Javascript;
        target.CustomCssUrl = source.CustomCssUrl;
        target.TableOfContents = source.TableOfContents;
        target.UseMarginsOnHeaderAndFooter = source.UseMarginsOnHeaderAndFooter;
        target.FitToPaperWidth = source.FitToPaperWidth;
        target.FontScale = source.FontScale;
        target.Zoom = source.Zoom;
        target.EnableImages = source.EnableImages;
        target.ForcePaperSize = source.ForcePaperSize;
        target.OverrideWithCssProperties = source.OverrideWithCssProperties;
        target.Scale = source.Scale;
        target.ViewPortHeight = source.ViewPortHeight;
        target.MarginUnit = source.MarginUnit;
        target.EnableCookies = source.EnableCookies;
        target.InputEncoding = source.InputEncoding;
        target.Proxy = source.Proxy;
        foreach (var header in source.CustomHttpHeaders) target.CustomHttpHeaders[header.Key] = header.Value;
        foreach (var cookie in source.CustomCookies) target.CustomCookies[cookie.Key] = cookie.Value;
    }

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
/// Alias for compatibility
/// </summary>
public class PdfMlRender : HtmlToPdf 
{
    public PdfMlRender() : this(NullLoggerFactory.Instance) { }
    public PdfMlRender(ILoggerFactory loggerFactory) 
        : base(new PuppeteerHtmlToPdfRenderer(loggerFactory.CreateLogger<PuppeteerHtmlToPdfRenderer>()), loggerFactory) { }

    public static PdfDocument StaticRenderHtmlAsPdf(string html, MLPdfRenderOptions? options = null) => new PdfMlRender().RenderHtmlAsPdf(html, options);
    public static async Task<PdfDocument> StaticRenderHtmlAsPdfAsync(string html, MLPdfRenderOptions? options = null) => await new PdfMlRender().RenderHtmlAsPdfAsync(html, options);

    public static PdfDocument StaticRenderUrlAsPdf(string url, MLPdfRenderOptions? options = null) => new PdfMlRender().RenderUrlAsPdf(url, options);
    public static async Task<PdfDocument> StaticRenderUrlAsPdfAsync(string url, MLPdfRenderOptions? options = null) => await new PdfMlRender().RenderUrlAsPdfAsync(url, options);

    public static PdfDocument StaticRenderHtmlFileAsPdf(string path, MLPdfRenderOptions? options = null) => new PdfMlRender().RenderHtmlFileAsPdf(path, options);
    public static async Task<PdfDocument> StaticRenderHtmlFileAsPdfAsync(string path, MLPdfRenderOptions? options = null) => await new PdfMlRender().RenderHtmlFileAsPdfAsync(path, options);
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
    private readonly MLPdfRenderOptions _options;

    internal FluentPdfBuilder(HtmlToPdf htmlToPdf, string source, bool isFile = false, bool isUrl = false)
    {
        _htmlToPdf = htmlToPdf;
        _source = source;
        _isFile = isFile;
        _isUrl = isUrl;
        _options = new MLPdfRenderOptions();
        // Seed with instance defaults
        _htmlToPdf.CopyOptions(_options, _htmlToPdf.RenderingOptions);
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

    public FluentPdfBuilder AsLandscape()
    {
        _options.Orientation = PageOrientation.Landscape;
        return this;
    }

    public FluentPdfBuilder AsPortrait()
    {
        _options.Orientation = PageOrientation.Portrait;
        return this;
    }

    public FluentPdfBuilder WithMargins(float top, float right, float bottom, float left)
    {
        _options.Margins = new PageMargins(top, right, bottom, left);
        return this;
    }

    public FluentPdfBuilder WithMargins(float margin)
    {
        _options.Margins = new PageMargins(margin, margin, margin, margin);
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

    public FluentPdfBuilder WithHtmlHeader(string html, float height = 20)
    {
        _options.Header = new HtmlHeaderFooter { HtmlContent = html, Height = height };
        return this;
    }

    public FluentPdfBuilder WithHtmlFooter(string html, float height = 20)
    {
        _options.Footer = new HtmlHeaderFooter { HtmlContent = html, Height = height };
        return this;
    }

    public FluentPdfBuilder WithFitToPaperWidth(bool fit = true)
    {
        _options.FitToPaperWidth = fit;
        return this;
    }

    public FluentPdfBuilder WithZoom(int zoom)
    {
        _options.Zoom = zoom;
        return this;
    }

    public FluentPdfBuilder WithFontScale(float scale)
    {
        _options.FontScale = scale;
        return this;
    }

    public FluentPdfBuilder EnableImages(bool enable = true)
    {
        _options.EnableImages = enable;
        return this;
    }

    public FluentPdfBuilder WithOverrideWithCssProperties(bool enable = true)
    {
        _options.OverrideWithCssProperties = enable;
        return this;
    }

    public FluentPdfBuilder WithForcePaperSize(bool enable = true)
    {
        _options.ForcePaperSize = enable;
        return this;
    }

    public FluentPdfBuilder WithCustomHttpHeaders(System.Collections.Generic.Dictionary<string, string> headers)
    {
        foreach (var header in headers) _options.CustomHttpHeaders[header.Key] = header.Value;
        return this;
    }

    public FluentPdfBuilder WithCustomHttpHeader(string name, string value)
    {
        _options.CustomHttpHeaders[name] = value;
        return this;
    }

    public FluentPdfBuilder WithCustomCookies(System.Collections.Generic.Dictionary<string, string> cookies)
    {
        foreach (var cookie in cookies) _options.CustomCookies[cookie.Key] = cookie.Value;
        return this;
    }

    public FluentPdfBuilder WithInputEncoding(string encoding)
    {
        _options.InputEncoding = encoding;
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

    public FluentPdfBuilder WithRenderDelay(int milliseconds)
    {
        _options.RenderDelay = milliseconds;
        return this;
    }

    public FluentPdfBuilder WithViewPortWidth(int width)
    {
        _options.ViewPortWidth = width;
        return this;
    }

    public FluentPdfBuilder WithTimeout(int seconds)
    {
        _options.Timeout = seconds;
        return this;
    }

    public FluentPdfBuilder AsPrint()
    {
        _options.MediaType = CssMediaType.Print;
        return this;
    }

    public FluentPdfBuilder AsScreen()
    {
        _options.MediaType = CssMediaType.Screen;
        return this;
    }

    public FluentPdfBuilder PrintHtmlBackgrounds(bool enable = true)
    {
        _options.PrintHtmlBackgrounds = enable;
        return this;
    }

    public FluentPdfBuilder WithPrintBackground(bool enable = true)
    {
        _options.PrintBackground = enable;
        return this;
    }

    public FluentPdfBuilder WithBaseUrl(string url)
    {
        _options.BaseUrl = url;
        return this;
    }

    public FluentPdfBuilder WithTitle(string title)
    {
        _options.Title = title;
        return this;
    }

    public FluentPdfBuilder WithAuthor(string author)
    {
        _options.Author = author;
        return this;
    }

    public FluentPdfBuilder WithSubject(string subject)
    {
        _options.Subject = subject;
        return this;
    }

    public FluentPdfBuilder WithKeywords(string keywords)
    {
        _options.Keywords = keywords;
        return this;
    }

    public FluentPdfBuilder WithFirstPageNumber(int pageNumber)
    {
        _options.FirstPageNumber = pageNumber;
        return this;
    }

    public FluentPdfBuilder WithCustomCssUrl(string url)
    {
        _options.CustomCssUrl = url;
        return this;
    }

    public FluentPdfBuilder UseMarginsOnHeaderAndFooter(bool use = true)
    {
        _options.UseMarginsOnHeaderAndFooter = use;
        return this;
    }

    public FluentPdfBuilder WithSecurity(Action<SecurityOptions> configure)
    {
        _options.Security ??= new SecurityOptions();
        configure(_options.Security);
        return this;
    }

    public FluentPdfBuilder WithWatermark(string text, Action<WatermarkOptions>? configure = null)
    {
        _options.Watermark = new WatermarkOptions { Text = text };
        configure?.Invoke(_options.Watermark);
        return this;
    }

    public async Task<PdfDocument> GenerateAsync(CancellationToken cancellationToken = default)
    {
        if (_isFile)
            return await _htmlToPdf.RenderFileAsync(_source, _options, cancellationToken);
        else if (_isUrl)
            return await _htmlToPdf.RenderUrlAsync(_source, _options, cancellationToken);
        else
            return await _htmlToPdf.RenderAsync(_source, _options, cancellationToken);
    }

    public async Task<PdfDocument> SaveAsync(string outputPath, CancellationToken cancellationToken = default)
    {
        var doc = await GenerateAsync(cancellationToken);
        doc.SaveAs(outputPath);
        return doc;
    }
}

/// <summary>
/// Wrapper for PDF results to match the API shape of other libraries
/// </summary>
public class PdfDocument : IEnumerable<byte>
{
    private readonly byte[] _data;
    private int? _pageCount;

    public PdfDocument(byte[] data)
    {
        _data = data;
    }

    /// <summary>
    /// Binary data of the PDF
    /// </summary>
    public byte[] BinaryData => _data;

    /// <summary>
    /// Length of the PDF data
    /// </summary>
    public long Length => _data.Length;

    /// <summary>
    /// Implicit conversion to byte[]
    /// </summary>
    public static implicit operator byte[](PdfDocument doc) => doc?.BinaryData ?? Array.Empty<byte>();

    public IEnumerator<byte> GetEnumerator() => ((IEnumerable<byte>)_data).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => _data.GetEnumerator();

    /// <summary>
    /// Read-only stream of the PDF data
    /// </summary>
    public Stream Stream => new MemoryStream(_data);

    /// <summary>
    /// Number of pages in the PDF
    /// </summary>
    public int PageCount
    {
        get
        {
            if (_pageCount.HasValue) return _pageCount.Value;

            using var ms = new MemoryStream(_data);
            using var reader = new iText.Kernel.Pdf.PdfReader(ms);
            using var doc = new iText.Kernel.Pdf.PdfDocument(reader);
            _pageCount = doc.GetNumberOfPages();
            return _pageCount.Value;
        }
    }

    /// <summary>
    /// Saves the PDF to a file path
    /// </summary>
    public void SaveAs(string path) => File.WriteAllBytes(path, _data);

    /// <summary>
    /// Saves the PDF to a file path asynchronously
    /// </summary>
    public async Task SaveAsAsync(string path) => await File.WriteAllBytesAsync(path, _data);

    /// <summary>
    /// Static method to merge multiple PDFs 
    /// </summary>
    public static PdfDocument? Merge(params PdfDocument[] documents)
    {
        if (documents == null || !documents.Any()) return null;
        
        using var ms = new MemoryStream();
        using (var mergedDoc = new iText.Kernel.Pdf.PdfDocument(new iText.Kernel.Pdf.PdfWriter(ms)))
        {
            foreach (var doc in documents)
            {
                using var sourceMs = new MemoryStream(doc.BinaryData);
                using var sourceDoc = new iText.Kernel.Pdf.PdfDocument(new iText.Kernel.Pdf.PdfReader(sourceMs));
                sourceDoc.CopyPagesTo(1, sourceDoc.GetNumberOfPages(), mergedDoc);
            }
        }
        return new PdfDocument(ms.ToArray());
    }

    public static PdfDocument FromFile(string path) => new PdfDocument(File.ReadAllBytes(path));
    public static PdfDocument FromBinaryData(byte[] data) => new PdfDocument(data);
    public static PdfDocument FromStream(Stream stream)
    {
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return new PdfDocument(ms.ToArray());
    }

    /// <summary>
    /// Inserts a page from another document
    /// </summary>
    public PdfDocument InsertPage(PdfDocument otherDocument, int pageIndex, int insertAt = 1)
    {
        using var inputStream = new MemoryStream(_data);
        using var otherStream = new MemoryStream(otherDocument.BinaryData);
        using var outputStream = new MemoryStream();
        
        using (var pdfDoc = new iText.Kernel.Pdf.PdfDocument(new iText.Kernel.Pdf.PdfReader(inputStream), new iText.Kernel.Pdf.PdfWriter(outputStream)))
        using (var otherDoc = new iText.Kernel.Pdf.PdfDocument(new iText.Kernel.Pdf.PdfReader(otherStream)))
        {
            otherDoc.CopyPagesTo(pageIndex, pageIndex, pdfDoc, insertAt);
        }
        return new PdfDocument(outputStream.ToArray());
    }

    /// <summary>
    /// Removes a page by index
    /// </summary>
    public PdfDocument RemovePage(int pageIndex)
    {
        using var inputStream = new MemoryStream(_data);
        using var outputStream = new MemoryStream();
        using (var pdfDoc = new iText.Kernel.Pdf.PdfDocument(new iText.Kernel.Pdf.PdfReader(inputStream), new iText.Kernel.Pdf.PdfWriter(outputStream)))
        {
            pdfDoc.RemovePage(pageIndex);
        }
        return new PdfDocument(outputStream.ToArray());
    }

    /// <summary>
    /// Applies a watermark to this document
    /// </summary>
    public PdfDocument ApplyWatermark(string text, float opacity = 0.5f, int rotation = 45, int fontSize = 50)
    {
        using var inputStream = new MemoryStream(_data);
        using var outputStream = new MemoryStream();
        using (var pdfDoc = new iText.Kernel.Pdf.PdfDocument(new iText.Kernel.Pdf.PdfReader(inputStream), new iText.Kernel.Pdf.PdfWriter(outputStream)))
        {
            var font = iText.Kernel.Font.PdfFontFactory.CreateFont(iText.IO.Font.Constants.StandardFonts.HELVETICA_BOLD);
            var pageCount = pdfDoc.GetNumberOfPages();
            var color = iText.Kernel.Colors.WebColors.GetRGBColor("#808080");

            for (int i = 1; i <= pageCount; i++)
            {
                var page = pdfDoc.GetPage(i);
                var pageSize = page.GetPageSize();
                var canvas = new iText.Kernel.Pdf.Canvas.PdfCanvas(page.NewContentStreamAfter(), page.GetResources(), pdfDoc);

                new iText.Layout.Canvas(canvas, pageSize)
                    .ShowTextAligned(new iText.Layout.Element.Paragraph(text)
                        .SetFont(font)
                        .SetFontSize(fontSize)
                        .SetFontColor(color, opacity),
                        pageSize.GetWidth() / 2, pageSize.GetHeight() / 2, i,
                        iText.Layout.Properties.TextAlignment.CENTER, iText.Layout.Properties.VerticalAlignment.MIDDLE, (float)(rotation * Math.PI / 180));
            }
        }
        return new PdfDocument(outputStream.ToArray());
    }

    /// <summary>
    /// Extracts all text from the PDF
    /// </summary>
    public string ExtractAllText()
    {
        using var inputStream = new MemoryStream(_data);
        using var pdfDoc = new iText.Kernel.Pdf.PdfDocument(new iText.Kernel.Pdf.PdfReader(inputStream));
        var text = new StringBuilder();
        for (int i = 1; i <= pdfDoc.GetNumberOfPages(); i++)
        {
            text.Append(iText.Kernel.Pdf.Canvas.Parser.PdfTextExtractor.GetTextFromPage(pdfDoc.GetPage(i)));
        }
        return text.ToString();
    }

    /// <summary>
    /// Encrypts the document with a password
    /// </summary>
    public PdfDocument Encrypt(string userPassword, string? ownerPassword = null)
    {
        using var inputStream = new MemoryStream(_data);
        using var outputStream = new MemoryStream();
        var writerProperties = new iText.Kernel.Pdf.WriterProperties();
        
        writerProperties.SetStandardEncryption(
            Encoding.UTF8.GetBytes(userPassword),
            ownerPassword != null ? Encoding.UTF8.GetBytes(ownerPassword) : null,
            iText.Kernel.Pdf.EncryptionConstants.ALLOW_PRINTING | iText.Kernel.Pdf.EncryptionConstants.ALLOW_COPY,
            iText.Kernel.Pdf.EncryptionConstants.ENCRYPTION_AES_256);

        using (var pdfDoc = new iText.Kernel.Pdf.PdfDocument(new iText.Kernel.Pdf.PdfReader(inputStream), new iText.Kernel.Pdf.PdfWriter(outputStream, writerProperties)))
        {
            // Just opening and closing with writer properties applies encryption
        }
        return new PdfDocument(outputStream.ToArray());
    }

    /// <summary>
    /// Stamps HTML onto every page of the document
    /// </summary>
    public PdfDocument StampHTML(string html, float opacity = 1.0f)
    {
        using var inputStream = new MemoryStream(_data);
        using var outputStream = new MemoryStream();
        using (var pdfDoc = new iText.Kernel.Pdf.PdfDocument(new iText.Kernel.Pdf.PdfReader(inputStream), new iText.Kernel.Pdf.PdfWriter(outputStream)))
        {
            var converterProperties = new iText.Html2pdf.ConverterProperties();
            for (int i = 1; i <= pdfDoc.GetNumberOfPages(); i++)
            {
                var page = pdfDoc.GetPage(i);
                var pageSize = page.GetPageSize();
                var canvas = new iText.Kernel.Pdf.Canvas.PdfCanvas(page.NewContentStreamAfter(), page.GetResources(), pdfDoc);
                var layout = new iText.Layout.Canvas(canvas, pageSize);
                
                var elements = iText.Html2pdf.HtmlConverter.ConvertToElements(html, converterProperties);
                foreach (var element in elements)
                {
                    if (element is iText.Layout.Element.IBlockElement block)
                    {
                        block.SetProperty(iText.Layout.Properties.Property.OPACITY, opacity);
                        layout.Add(block);
                    }
                }
            }
        }
        return new PdfDocument(outputStream.ToArray());
    }

    /// <summary>
    /// Stamps an image onto every page of the document
    /// </summary>
    public PdfDocument StampImage(string imagePath, float x = 0, float y = 0, float width = 100, float opacity = 1.0f)
    {
        using var inputStream = new MemoryStream(_data);
        using var outputStream = new MemoryStream();
        using (var pdfDoc = new iText.Kernel.Pdf.PdfDocument(new iText.Kernel.Pdf.PdfReader(inputStream), new iText.Kernel.Pdf.PdfWriter(outputStream)))
        {
            var imgData = iText.IO.Image.ImageDataFactory.Create(imagePath);
            var img = new iText.Layout.Element.Image(imgData);
            img.SetFixedPosition(x, y);
            img.SetWidth(width);
            img.SetOpacity(opacity);

            for (int i = 1; i <= pdfDoc.GetNumberOfPages(); i++)
            {
                var page = pdfDoc.GetPage(i);
                var pageSize = page.GetPageSize();
                var canvas = new iText.Kernel.Pdf.Canvas.PdfCanvas(page.NewContentStreamAfter(), page.GetResources(), pdfDoc);
                new iText.Layout.Canvas(canvas, pageSize).Add(img);
            }
        }
        return new PdfDocument(outputStream.ToArray());
    }

}
