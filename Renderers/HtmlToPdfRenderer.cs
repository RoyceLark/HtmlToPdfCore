
//public async Task<byte[]> RenderHtmlToPdfAsync(
//    string html,
//    PdfRenderOptions? options = null,
//    CancellationToken cancellationToken = default)
//{
//    // Automatic license validation
//    LicenseManager.ValidateLicense();


using HtmlToPdfCore.Interfaces;
using HtmlToPdfCore.Models;
using iText.Html2pdf;
using iText.Kernel.Pdf;
using iText.Kernel.Geom;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;
using System.Text;
using AngleSharp;
using AngleSharp.Html.Parser;
using Path = System.IO.Path;
using HtmlToPdfCore.Licensing;

namespace HtmlToPdfCore.Renderers;

public class HtmlToPdfRenderer : IHtmlToPdfRenderer, IDisposable
{
    private readonly ILogger<HtmlToPdfRenderer> _logger;
    private readonly IMemoryOptimizer? _memoryOptimizer;
    private readonly ObjectPool<MemoryStream> _streamPool;
    private readonly HttpClient _httpClient;
    private readonly SemaphoreSlim _renderSemaphore;
    private bool _disposed;

    public HtmlToPdfRenderer(
        ILogger<HtmlToPdfRenderer> logger,
        IMemoryOptimizer? memoryOptimizer = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _memoryOptimizer = memoryOptimizer;

        var poolPolicy = new DefaultPooledObjectPolicy<MemoryStream>();
        _streamPool = new DefaultObjectPool<MemoryStream>(poolPolicy, 10);

        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "HtmlToPdfCore/1.0");

        _renderSemaphore = new SemaphoreSlim(Environment.ProcessorCount);
    }

    public async Task<byte[]> RenderHtmlToPdfAsync(
        string html,
        PdfRenderOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        // Automatic license validation - BLOCKS if invalid
        //if (!LicenseManager.ValidateLicense())
        //{
        //    throw new LicenseException(
        //        "PDF generation blocked: Invalid or missing license. " +
        //        "Please contact sales@roycelark.com / +91 (900) 875-1562 to purchase a license."
        //    );
        //}

        if (string.IsNullOrWhiteSpace(html))
            throw new ArgumentException("HTML content cannot be null or empty", nameof(html));

        options ??= new PdfRenderOptions();

        try
        {
            await _renderSemaphore.WaitAsync(cancellationToken);

            if (options.EnableAIOptimization && _memoryOptimizer != null)
            {
                var prediction = await _memoryOptimizer.PredictMemoryUsageAsync(html, cancellationToken);
                _logger.LogInformation("Memory prediction: {EstimatedMB}MB, Confidence: {Confidence}",
                    prediction.EstimatedMemoryMB, prediction.ConfidenceScore);
            }

            html = await PreProcessHtmlAsync(html, options, cancellationToken);

            using var outputStream = _streamPool.Get();
            outputStream.SetLength(0);

            var writerProperties = new WriterProperties();
            var pdfWriter = new PdfWriter(outputStream, writerProperties);
            var pdfDoc = new PdfDocument(pdfWriter);

            if (options.Metadata != null)
            {
                var docInfo = pdfDoc.GetDocumentInfo();

                if (!string.IsNullOrEmpty(options.Metadata.Title))
                    docInfo.SetTitle(options.Metadata.Title);
                if (!string.IsNullOrEmpty(options.Metadata.Author))
                    docInfo.SetAuthor(options.Metadata.Author);
                if (!string.IsNullOrEmpty(options.Metadata.Subject))
                    docInfo.SetSubject(options.Metadata.Subject);
                if (!string.IsNullOrEmpty(options.Metadata.Keywords))
                    docInfo.SetKeywords(options.Metadata.Keywords);
                if (!string.IsNullOrEmpty(options.Metadata.Creator))
                    docInfo.SetCreator(options.Metadata.Creator);
            }

            var pageSize = GetPageSize(options.PageSize, options.Orientation);
            pdfDoc.SetDefaultPageSize(pageSize);

            var converterProperties = new ConverterProperties();

            if (!string.IsNullOrEmpty(options.BaseUrl))
            {
                converterProperties.SetBaseUri(options.BaseUrl);
            }

            if (!string.IsNullOrEmpty(options.CustomCss))
            {
                html = InjectCustomCss(html, options.CustomCss);
            }

            // Add CSS for better rendering with margins
            var cssForRendering = $@"
                @page {{
                    size: {GetCssPageSize(options.PageSize, options.Orientation)};
                    margin-top: {options.Margins.Top}mm;
                    margin-right: {options.Margins.Right}mm;
                    margin-bottom: {options.Margins.Bottom}mm;
                    margin-left: {options.Margins.Left}mm;
                }}
                body {{
                    margin: 0;
                    padding: 0;
                }}
            ";
            html = InjectCustomCss(html, cssForRendering);

            // Convert HTML to PDF directly
            HtmlConverter.ConvertToPdf(html, pdfDoc, converterProperties);

            pdfDoc.Close();

            var result = outputStream.ToArray();

            _logger.LogInformation("Successfully rendered HTML to PDF. Size: {Size} bytes", result.Length);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rendering HTML to PDF");
            throw new PdfRenderException("Failed to render HTML to PDF", ex);
        }
        finally
        {
            _renderSemaphore.Release();
        }
    }

    public async Task<byte[]> RenderHtmlFileToPdfAsync(
        string filePath,
        PdfRenderOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path cannot be null or empty", nameof(filePath));

        if (!File.Exists(filePath))
            throw new FileNotFoundException("HTML file not found", filePath);

        try
        {
            var html = await File.ReadAllTextAsync(filePath, cancellationToken);

            options ??= new PdfRenderOptions();
            if (string.IsNullOrEmpty(options.BaseUrl))
            {
                var directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    options.BaseUrl = new Uri(directory).AbsoluteUri;
                }
            }

            return await RenderHtmlToPdfAsync(html, options, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rendering HTML file to PDF: {FilePath}", filePath);
            throw new PdfRenderException($"Failed to render HTML file to PDF: {filePath}", ex);
        }
    }

    public async Task<byte[]> RenderUrlToPdfAsync(
        string url,
        PdfRenderOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("URL cannot be null or empty", nameof(url));

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            throw new ArgumentException("Invalid URL format", nameof(url));

        try
        {
            _logger.LogInformation("Fetching content from URL: {Url}", url);

            var html = await _httpClient.GetStringAsync(uri, cancellationToken);

            options ??= new PdfRenderOptions();
            if (string.IsNullOrEmpty(options.BaseUrl))
            {
                options.BaseUrl = $"{uri.Scheme}://{uri.Host}";
            }

            return await RenderHtmlToPdfAsync(html, options, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error fetching URL: {Url}", url);
            throw new PdfRenderException($"Failed to fetch URL: {url}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rendering URL to PDF: {Url}", url);
            throw new PdfRenderException($"Failed to render URL to PDF: {url}", ex);
        }
    }

    public async Task RenderHtmlToPdfFileAsync(
        string html,
        string outputPath,
        PdfRenderOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
            throw new ArgumentException("Output path cannot be null or empty", nameof(outputPath));

        try
        {
            var pdfBytes = await RenderHtmlToPdfAsync(html, options, cancellationToken);

            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllBytesAsync(outputPath, pdfBytes, cancellationToken);

            _logger.LogInformation("PDF saved to: {OutputPath}", outputPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving PDF to file: {OutputPath}", outputPath);
            throw new PdfRenderException($"Failed to save PDF to file: {outputPath}", ex);
        }
    }

    private async Task<string> PreProcessHtmlAsync(
         string html,
         PdfRenderOptions options,
         CancellationToken cancellationToken)
    {
        try
        {
            var config = Configuration.Default;
            var context = BrowsingContext.New(config);
            var parser = context.GetService<IHtmlParser>();

            if (parser == null)
                return html;

            var document = await parser.ParseDocumentAsync(html, cancellationToken);

            if (document.Head == null)
            {
                var head = document.CreateElement("head");
                document.DocumentElement?.InsertBefore(head, document.Body);
            }

            if (document.Head?.QuerySelector("meta[name='viewport']") == null)
            {
                var viewport = document.CreateElement("meta");
                viewport.SetAttribute("name", "viewport");
                viewport.SetAttribute("content", "width=device-width, initial-scale=1.0");
                document.Head?.AppendChild(viewport);
            }

            var mediaType = options.MediaType == CssMediaType.Screen ? "screen" : "print";
            var style = document.CreateElement("style");
            style.SetAttribute("media", mediaType);
            document.Head?.AppendChild(style);

            return document.DocumentElement?.OuterHtml ?? html;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error pre-processing HTML, using original");
            return html;
        }
    }


    private string InjectCustomCss(string html, string css)
    {
        var styleTag = $"<style>{css}</style>";

        if (html.Contains("</head>", StringComparison.OrdinalIgnoreCase))
        {
            return html.Replace("</head>", $"{styleTag}</head>", StringComparison.OrdinalIgnoreCase);
        }
        else if (html.Contains("<body>", StringComparison.OrdinalIgnoreCase))
        {
            return html.Replace("<body>", $"<head>{styleTag}</head><body>", StringComparison.OrdinalIgnoreCase);
        }
        else
        {
            return $"<html><head>{styleTag}</head><body>{html}</body></html>";
        }
    }

    private string GetCssPageSize(Models.PageSize size, PageOrientation orientation)
    {
        var dimensions = size switch
        {
            Models.PageSize.A4 => "210mm 297mm",
            Models.PageSize.A3 => "297mm 420mm",
            Models.PageSize.Letter => "8.5in 11in",
            Models.PageSize.Legal => "8.5in 14in",
            Models.PageSize.Tabloid => "11in 17in",
            _ => "210mm 297mm"
        };

        if (orientation == PageOrientation.Landscape)
        {
            var parts = dimensions.Split(' ');
            return $"{parts[1]} {parts[0]}";
        }

        return dimensions;
    }

    private iText.Kernel.Geom.PageSize GetPageSize(Models.PageSize size, PageOrientation orientation)
    {
        iText.Kernel.Geom.PageSize pageSize = size switch
        {
            Models.PageSize.A0 => iText.Kernel.Geom.PageSize.A0,
            Models.PageSize.A1 => iText.Kernel.Geom.PageSize.A1,
            Models.PageSize.A2 => iText.Kernel.Geom.PageSize.A2,
            Models.PageSize.A3 => iText.Kernel.Geom.PageSize.A3,
            Models.PageSize.A4 => iText.Kernel.Geom.PageSize.A4,
            Models.PageSize.A5 => iText.Kernel.Geom.PageSize.A5,
            Models.PageSize.A6 => iText.Kernel.Geom.PageSize.A6,
            Models.PageSize.Letter => iText.Kernel.Geom.PageSize.LETTER,
            Models.PageSize.Legal => iText.Kernel.Geom.PageSize.LEGAL,
            Models.PageSize.Tabloid => iText.Kernel.Geom.PageSize.TABLOID,
            Models.PageSize.Ledger => iText.Kernel.Geom.PageSize.LEDGER,
            _ => iText.Kernel.Geom.PageSize.A4
        };

        return orientation == PageOrientation.Landscape ? pageSize.Rotate() : pageSize;
    }

    private float MillimetersToPoints(float mm)
    {
        return mm * 72f / 25.4f;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _httpClient?.Dispose();
        _renderSemaphore?.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}

public class PdfRenderException : Exception
{
    public PdfRenderException(string message) : base(message) { }
    public PdfRenderException(string message, Exception innerException) : base(message, innerException) { }
}