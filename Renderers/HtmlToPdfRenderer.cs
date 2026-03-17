
using HtmlToPdfCore.Interfaces;
using HtmlToPdfCore.Models;
using iText.Html2pdf;
using iText.Kernel.Pdf;
using iText.Kernel.Geom;
using iText.Layout;
using iText.Layout.Element;
using iText.Kernel.Pdf.Canvas;
using iText.Kernel.Colors;
using iText.Layout.Borders;
using iText.Layout.Properties;
using iText.Layout.Font;
using iText.StyledXmlParser.Css.Media;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;
using System.Text;
using AngleSharp;
using AngleSharp.Html.Parser;
using iText.Kernel.Pdf.Event;
using iText.Commons.Actions;
using iText.Bouncycastle;


namespace HtmlToPdfCore.Renderers;

public class HtmlToPdfRenderer : IHtmlToPdfRenderer, IDisposable
{
    private PdfRenderOptions? _currentOptions;

    static HtmlToPdfRenderer()
    {
        try
        {
            // Use reflection-like fully qualified names to avoid namespace issues with iText 9
            iText.Bouncycastleconnector.BouncyCastleFactoryCreator.SetFactory(new BouncyCastleFactory());
        }
        catch
        {
            // Ignore if already set or fails
        }
    }

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
      //  Automatic license validation - BLOCKS if invalid
        //if (!LicenseManager.ValidateLicense())
        //    {
        //        throw new LicenseException(
        //            "PDF generation blocked: Invalid or missing license. " +
        //            "Please contact sales@roycelark.com / +91 (900) 875-1562 to purchase a license."
        //        );
        //    }

        if (string.IsNullOrWhiteSpace(html))
            throw new ArgumentException("HTML content cannot be null or empty", nameof(html));

        options ??= new PdfRenderOptions();
        _currentOptions = options;

        try
        {
            await _renderSemaphore.WaitAsync(cancellationToken);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            if (options.RenderTimeout > 0)
            {
                cts.CancelAfter(options.RenderTimeout);
            }

            if (options.EnableAIOptimization && _memoryOptimizer != null)
            {
                var prediction = await _memoryOptimizer.PredictMemoryUsageAsync(html, cts.Token);
                _logger.LogInformation("Memory prediction: {EstimatedMB}MB, Confidence: {Confidence}",
                    prediction.EstimatedMemoryMB, prediction.ConfidenceScore);
            }

            html = await PreProcessHtmlAsync(html, options, cts.Token);

            using var outputStream = _streamPool.Get();
            outputStream.SetLength(0);

            var writerProperties = new WriterProperties();
            if (options.CompressImages)
            {
                writerProperties.SetCompressionLevel(iText.Kernel.Pdf.CompressionConstants.BEST_COMPRESSION);
            }

            if (options.Security != null)
            {
                int permissions = 0;
                permissions |= options.Security.AllowPrinting ? iText.Kernel.Pdf.EncryptionConstants.ALLOW_PRINTING : 0;
                permissions |= options.Security.AllowCopyContent ? iText.Kernel.Pdf.EncryptionConstants.ALLOW_COPY : 0;
                permissions |= options.Security.AllowEditContent ? iText.Kernel.Pdf.EncryptionConstants.ALLOW_MODIFY_CONTENTS : 0;
                permissions |= options.Security.AllowEditAnnotations ? iText.Kernel.Pdf.EncryptionConstants.ALLOW_MODIFY_ANNOTATIONS : 0;

                writerProperties.SetStandardEncryption(
                    options.Security.UserPassword != null ? Encoding.UTF8.GetBytes(options.Security.UserPassword) : null,
                    options.Security.OwnerPassword != null ? Encoding.UTF8.GetBytes(options.Security.OwnerPassword) : null,
                    permissions,
                    iText.Kernel.Pdf.EncryptionConstants.ENCRYPTION_AES_256);
            }
            
            var pdfWriter = new PdfWriter(outputStream, writerProperties);
            var pdfDoc = new iText.Kernel.Pdf.PdfDocument(pdfWriter);

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

            if (options.RenderDelay > 0)
            {
                await Task.Delay(options.RenderDelay, cancellationToken);
            }

            var converterProperties = new ConverterProperties();
            
            var fontProvider = new FontProvider();
            fontProvider.AddStandardPdfFonts();
            fontProvider.AddSystemFonts();
            
            // To mimic Chrome defaults, we set Times-Roman as the default if nothing else is specified.
            fontProvider.GetDefaultFontFamily();
            converterProperties.SetFontProvider(fontProvider);

            if (string.IsNullOrEmpty(options.BaseUrl))
            {
                var currentDir = System.IO.Path.GetFullPath(Environment.CurrentDirectory);
                if (!currentDir.EndsWith(System.IO.Path.DirectorySeparatorChar.ToString()) && !currentDir.EndsWith(System.IO.Path.AltDirectorySeparatorChar.ToString()))
                {
                    currentDir += System.IO.Path.DirectorySeparatorChar;
                }
                options.BaseUrl = new Uri(currentDir).AbsoluteUri;
            }

            if (!string.IsNullOrEmpty(options.BaseUrl))
            {
                converterProperties.SetBaseUri(options.BaseUrl);
            }

            // Set media type to behavior (Print by default)
            var cssMediaType = options.MediaType == Models.CssMediaType.Screen ? iText.StyledXmlParser.Css.Media.MediaType.SCREEN : iText.StyledXmlParser.Css.Media.MediaType.PRINT;
            var mediaDevice = new MediaDeviceDescription(cssMediaType);
            
            // Mimic Chrome's virtual viewport for vw/vh units. defaults to a desktop viewport (e.g., 1024px width) 
            // when FitToPaperWidth is enabled or when Screen media is requested.
            float viewportWidth = options.ViewPortWidth ?? (options.FitToPaperWidth || options.MediaType == Models.CssMediaType.Screen ? 1024f : pageSize.GetWidth());
            float viewportHeight = options.ViewPortHeight ?? pageSize.GetHeight();
            mediaDevice.SetWidth(viewportWidth);
            mediaDevice.SetHeight(viewportHeight);
            converterProperties.SetMediaDeviceDescription(mediaDevice);

            // Enable HTML to PDF form conversion feature
            converterProperties.SetCreateAcroForm(options.CreatePdfFormsFromHtml);

            // Register header and footer handler
            if (options.Header != null || options.Footer != null)
            {
                pdfDoc.AddEventHandler(PdfDocumentEvent.END_PAGE, new HeaderFooterEventHandler(options, converterProperties));
            }

            // Register watermarking handler
            if (options.Watermark != null && !string.IsNullOrEmpty(options.Watermark.Text))
            {
                pdfDoc.AddEventHandler(PdfDocumentEvent.END_PAGE, new WatermarkEventHandler(options.Watermark));
            }

            if (!string.IsNullOrEmpty(options.CustomCssUrl))
            {
                try
                {
                    var cssContent = await _httpClient.GetStringAsync(options.CustomCssUrl, cancellationToken);
                    html = InjectCustomCss(html, cssContent);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to load CustomCssUrl: {Url}", options.CustomCssUrl);
                }
            }

            if (!string.IsNullOrEmpty(options.CustomCss))
            {
                html = InjectCustomCss(html, options.CustomCss);
            }

            string fitToPaperCss = options.FitToPaperWidth ? "max-width: 100%; overflow: hidden; box-sizing: border-box;" : "";
            string backgroundCss = options.PrintBackground ? "-webkit-print-color-adjust: exact; color-adjust: exact; print-color-adjust: exact;" : "background-color: transparent !important; background-image: none !important;";
            string fontScaleCss = options.FontScale != 1.0f ? $"font-size: {options.FontScale * 100}%;" : "";
            string zoomCss = options.Zoom != 100 ? $"zoom: {options.Zoom}%;" : "";
            string contentScaleCss = options.Scale != 1.0 ? $"transform: scale({options.Scale}); transform-origin: top left; width: {100 / options.Scale}%;" : "";
            string grayscaleCss = options.Grayscale ? "filter: grayscale(100%); -webkit-filter: grayscale(100%);" : "";
            
            string marginUnit = options.MarginUnit switch {
                MeasurementUnit.Inches => "in",
                MeasurementUnit.Pixels => "px",
                MeasurementUnit.Points => "pt",
                _ => "mm"
            };

            // Add CSS for better rendering with margins
            var cssForRendering = $@"
                @page {{
                    size: {GetCssPageSize(options.PageSize, options.Orientation)};
                    margin-top: {options.Margins.Top}{marginUnit};
                    margin-right: {options.Margins.Right}{marginUnit};
                    margin-bottom: {options.Margins.Bottom}{marginUnit};
                    margin-left: {options.Margins.Left}{marginUnit};
                }}
                html {{
                    {backgroundCss}
                    {grayscaleCss}
                }}
                body {{
                    margin: 0;
                    padding: 0;
                    font-family: 'Times New Roman', Times, serif;
                    {fitToPaperCss}
                    {backgroundCss}
                    {fontScaleCss}
                    {zoomCss}
                    {contentScaleCss}
                    {grayscaleCss}
                }}
                * {{
                    {backgroundCss}
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
        catch (OperationCanceledException)
        {
            throw;
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
            options ??= new PdfRenderOptions();
            var encoding = Encoding.GetEncoding(options.InputEncoding ?? "UTF-8");
            var html = await File.ReadAllTextAsync(filePath, encoding, cancellationToken);

            if (string.IsNullOrEmpty(options.BaseUrl))
            {
                var directory = System.IO.Path.GetDirectoryName(filePath);
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
            
            options ??= new PdfRenderOptions();
            var request = new HttpRequestMessage(HttpMethod.Get, uri);
            
            foreach (var header in options.CustomHttpHeaders)
            {
                request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            if (options.CustomCookies.Count > 0)
            {
                var cookieHeader = string.Join("; ", options.CustomCookies.Select(c => $"{c.Key}={c.Value}"));
                request.Headers.TryAddWithoutValidation("Cookie", cookieHeader);
            }

            var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
            
            var html = await response.Content.ReadAsStringAsync(cancellationToken);

            if (string.IsNullOrEmpty(options.BaseUrl))
            {
                options.BaseUrl = url;
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

            var directory = System.IO.Path.GetDirectoryName(outputPath);
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
            Models.PageSize.A0 => "841mm 1189mm",
            Models.PageSize.A1 => "594mm 841mm",
            Models.PageSize.A2 => "420mm 594mm",
            Models.PageSize.A3 => "297mm 420mm",
            Models.PageSize.A4 => "210mm 297mm",
            Models.PageSize.A5 => "148mm 210mm",
            Models.PageSize.A6 => "105mm 148mm",
            Models.PageSize.Letter => "8.5in 11in",
            Models.PageSize.Legal => "8.5in 14in",
            Models.PageSize.Tabloid => "11in 17in",
            Models.PageSize.Ledger => "17in 11in",
            Models.PageSize.Executive => "7.25in 10.5in",
            Models.PageSize.Folio => "8.5in 13in",
            Models.PageSize.Statement => "5.5in 8.5in",
            Models.PageSize.Personal => "3.625in 6.75in",
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
            Models.PageSize.A7 => iText.Kernel.Geom.PageSize.A7,
            Models.PageSize.A8 => iText.Kernel.Geom.PageSize.A8,
            Models.PageSize.A9 => iText.Kernel.Geom.PageSize.A9,
            Models.PageSize.B0 => iText.Kernel.Geom.PageSize.B0,
            Models.PageSize.B1 => iText.Kernel.Geom.PageSize.B1,
            Models.PageSize.B2 => iText.Kernel.Geom.PageSize.B2,
            Models.PageSize.B3 => iText.Kernel.Geom.PageSize.B3,
            Models.PageSize.B4 => iText.Kernel.Geom.PageSize.B4,
            Models.PageSize.B5 => iText.Kernel.Geom.PageSize.B5,
            Models.PageSize.B6 => iText.Kernel.Geom.PageSize.B6,
            Models.PageSize.B7 => iText.Kernel.Geom.PageSize.B7,
            Models.PageSize.B8 => iText.Kernel.Geom.PageSize.B8,
            Models.PageSize.B9 => iText.Kernel.Geom.PageSize.B9,
            Models.PageSize.B10 => iText.Kernel.Geom.PageSize.B10,
            Models.PageSize.Letter => iText.Kernel.Geom.PageSize.LETTER,
            Models.PageSize.Legal => iText.Kernel.Geom.PageSize.LEGAL,
            Models.PageSize.Tabloid => iText.Kernel.Geom.PageSize.TABLOID,
            Models.PageSize.Ledger => iText.Kernel.Geom.PageSize.LEDGER,
            Models.PageSize.Executive => iText.Kernel.Geom.PageSize.EXECUTIVE,
            Models.PageSize.Folio => new iText.Kernel.Geom.PageSize(MillimetersToPoints(215.9f), MillimetersToPoints(330.2f)),
            Models.PageSize.Statement => new iText.Kernel.Geom.PageSize(MillimetersToPoints(139.7f), MillimetersToPoints(215.9f)),
            Models.PageSize.Personal => new iText.Kernel.Geom.PageSize(MillimetersToPoints(92.1f), MillimetersToPoints(171.5f)),
            _ => (size == Models.PageSize.Custom && _currentOptions?.CustomPageWidth > 0 && _currentOptions?.CustomPageHeight > 0)
                 ? new iText.Kernel.Geom.PageSize(MillimetersToPoints(_currentOptions.CustomPageWidth.Value), MillimetersToPoints(_currentOptions.CustomPageHeight.Value))
                 : iText.Kernel.Geom.PageSize.A4
        };

        return orientation == PageOrientation.Landscape ? pageSize.Rotate() : pageSize;
    }

    private float MillimetersToPoints(float mm)
    {
        return mm * 72f / 25.4f;
    }

    private class HeaderFooterEventHandler : AbstractPdfDocumentEventHandler
    {
        private readonly PdfRenderOptions _options;
        private readonly ConverterProperties _converterProperties;

        public HeaderFooterEventHandler(PdfRenderOptions options, ConverterProperties converterProperties)
        {
            _options = options;
            _converterProperties = converterProperties;
        }

        private string GetHeaderFooterHtml(HeaderFooterOptions? options)
        {
            if (options == null) return "";
            
            if (options is TextHeaderFooter textOptions)
            {
                var sb = new StringBuilder();
                sb.Append("<div style='width: 100%; display: flex; justify-content: space-between;'>");
                sb.Append($"<div style='flex: 1; text-align: left;'>{textOptions.LeftText}</div>");
                sb.Append($"<div style='flex: 1; text-align: center;'>{textOptions.CenterText}</div>");
                sb.Append($"<div style='flex: 1; text-align: right;'>{textOptions.RightText}</div>");
                sb.Append("</div>");
                return sb.ToString();
            }

            if (options is HtmlHeaderFooter htmlOptions && !string.IsNullOrEmpty(htmlOptions.LoadStyleSheet))
            {
                return $"<link rel='stylesheet' href='{htmlOptions.LoadStyleSheet}'>{options.HtmlContent}";
            }

            return options.HtmlContent ?? "";
        }

        protected override void OnAcceptedEvent(AbstractPdfDocumentEvent @event)
        {
            if (@event is PdfDocumentEvent docEvent)
            {
                var pdf = docEvent.GetDocument();
                var page = docEvent.GetPage();
                var pageSize = page.GetPageSize();
                
                // Tokens for replacement
                var pageNumber = pdf.GetPageNumber(page) + (_options.FirstPageNumber - 1);
                var totalPages = pdf.GetNumberOfPages();
                var now = DateTime.Now;
                
                Func<string, string> replaceTokens = (text) => {
                    if (string.IsNullOrEmpty(text)) return text;
                    return text.Replace("{page}", pageNumber.ToString())
                               .Replace("{total}", totalPages.ToString()) // Legacy support
                               .Replace("{total-pages}", totalPages.ToString())
                               .Replace("{date}", now.ToShortDateString())
                               .Replace("{time}", now.ToShortTimeString())
                               .Replace("{pdf-title}", _options.Metadata?.Title ?? "")
                               .Replace("{html-title}", pdf.GetDocumentInfo()?.GetTitle() ?? "");
                };

                var canvas = new iText.Kernel.Pdf.Canvas.PdfCanvas(page.NewContentStreamAfter(), page.GetResources(), pdf);
                var canvasLayout = new iText.Layout.Canvas(canvas, pageSize);

                if (_options.Header != null)
                {
                    try
                    {
                        var html = GetHeaderFooterHtml(_options.Header);
                        if (string.IsNullOrEmpty(html)) goto ProcessFooter;

                        var content = replaceTokens(html);
                        var elements = HtmlConverter.ConvertToElements(content, _converterProperties);
                        
                        float left = _options.UseMarginsOnHeaderAndFooter ? (float)(_options.Margins.Left * 72 / 25.4) : pageSize.GetLeft();
                        float right = _options.UseMarginsOnHeaderAndFooter ? (float)(_options.Margins.Right * 72 / 25.4) : 0;
                        float width = pageSize.GetWidth() - (left + right);

                        var headerDiv = new Div().SetFixedPosition(left, pageSize.GetTop() - _options.Header.Height, width);
                        headerDiv.SetHeight(_options.Header.Height);
                        headerDiv.SetVerticalAlignment(VerticalAlignment.TOP);
                        headerDiv.SetPadding(0);
                        headerDiv.SetMargin(0);

                        if (_options.Header.FontSize.HasValue)
                            headerDiv.SetFontSize(_options.Header.FontSize.Value);
                        
                        if (!string.IsNullOrEmpty(_options.Header.FontFamily))
                        {
                            try {
                                var font = iText.Kernel.Font.PdfFontFactory.CreateFont(_options.Header.FontFamily);
                                headerDiv.SetFont(font);
                            } catch { /* Suppress font load errors */ }
                        }

                        if (!string.IsNullOrEmpty(_options.Header.Color))
                        {
                            try {
                                headerDiv.SetFontColor(iText.Kernel.Colors.WebColors.GetRGBColor(_options.Header.Color));
                            } catch { }
                        }

                        foreach (var element in elements)
                        {
                            if (element is IBlockElement block)
                                headerDiv.Add(block);
                        }
                        
                        if (_options.Header.DividerLine)
                        {
                            headerDiv.SetBorderBottom(new iText.Layout.Borders.SolidBorder(
                                iText.Kernel.Colors.WebColors.GetRGBColor(_options.Header.DividerColor ?? "#000000"), 0.5f));
                        }
                        
                        canvasLayout.Add(headerDiv);
                    }
                    catch (Exception)
                    {
                        // Log or handle error if needed
                    }
                }
            ProcessFooter:
                if (_options.Footer != null)
                {
                    try
                    {
                        var html = GetHeaderFooterHtml(_options.Footer);
                        if (string.IsNullOrEmpty(html)) return;

                        var footerContent = replaceTokens(html);
                        var elements = HtmlConverter.ConvertToElements(footerContent, _converterProperties);
                        
                        float left = _options.UseMarginsOnHeaderAndFooter ? (float)(_options.Margins.Left * 72 / 25.4) : pageSize.GetLeft();
                        float right = _options.UseMarginsOnHeaderAndFooter ? (float)(_options.Margins.Right * 72 / 25.4) : 0;
                        float width = pageSize.GetWidth() - (left + right);

                        var footerDiv = new Div().SetFixedPosition(left, pageSize.GetBottom(), width);
                        footerDiv.SetHeight(_options.Footer.Height);
                        footerDiv.SetVerticalAlignment(VerticalAlignment.BOTTOM);
                        footerDiv.SetPadding(0);
                        footerDiv.SetMargin(0);

                        if (_options.Footer.FontSize.HasValue)
                            footerDiv.SetFontSize(_options.Footer.FontSize.Value);

                        if (!string.IsNullOrEmpty(_options.Footer.FontFamily))
                        {
                            try {
                                var font = iText.Kernel.Font.PdfFontFactory.CreateFont(_options.Footer.FontFamily);
                                footerDiv.SetFont(font);
                            } catch { /* Suppress font load errors */ }
                        }

                        if (!string.IsNullOrEmpty(_options.Footer.Color))
                        {
                            try {
                                footerDiv.SetFontColor(iText.Kernel.Colors.WebColors.GetRGBColor(_options.Footer.Color));
                            } catch { }
                        }

                        foreach (var element in elements)
                        {
                            if (element is IBlockElement block)
                                footerDiv.Add(block);
                        }
                        
                        if (_options.Footer.DividerLine)
                        {
                            footerDiv.SetBorderTop(new iText.Layout.Borders.SolidBorder(
                                iText.Kernel.Colors.WebColors.GetRGBColor(_options.Footer.DividerColor ?? "#000000"), 0.5f));
                        }
                        
                        canvasLayout.Add(footerDiv);
                    }
                    catch (Exception)
                    {
                        // Log or handle error if needed
                    }
                }

                canvasLayout.Close();
            }
        }
    }

    private class WatermarkEventHandler : AbstractPdfDocumentEventHandler
    {
        private readonly WatermarkOptions _options;

        public WatermarkEventHandler(WatermarkOptions options)
        {
            _options = options;
        }

        protected override void OnAcceptedEvent(AbstractPdfDocumentEvent @event)
        {
            if (@event is PdfDocumentEvent docEvent)
            {
                var pdf = docEvent.GetDocument();
                var page = docEvent.GetPage();
                var pageSize = page.GetPageSize();
                var canvas = new PdfCanvas(page.NewContentStreamAfter(), page.GetResources(), pdf);

                float x = pageSize.GetWidth() / 2;
                float y = pageSize.GetHeight() / 2;

                switch (_options.Position)
                {
                    case WatermarkPosition.TopLeft: x = 50; y = pageSize.GetHeight() - 50; break;
                    case WatermarkPosition.TopCenter: y = pageSize.GetHeight() - 50; break;
                    case WatermarkPosition.TopRight: x = pageSize.GetWidth() - 50; y = pageSize.GetHeight() - 50; break;
                    case WatermarkPosition.BottomLeft: x = 50; y = 50; break;
                    case WatermarkPosition.BottomCenter: y = 50; break;
                    case WatermarkPosition.BottomRight: x = pageSize.GetWidth() - 50; y = 50; break;
                }

                var canvasLayout = new iText.Layout.Canvas(canvas, pageSize);
                
                var p = new iText.Layout.Element.Paragraph(_options.Text)
                    .SetFontSize(_options.FontSize)
                    .SetOpacity(_options.Opacity)
                    .SetRotationAngle(Math.PI * _options.Rotation / 180)
                    .SetFixedPosition(x, y, 500);

                if (!string.IsNullOrEmpty(_options.Color))
                {
                    try {
                        // Simple hex conversion
                        var color = iText.Kernel.Colors.ColorConstants.GRAY;
                        p.SetFontColor(color);
                    } catch { }
                }

                canvasLayout.ShowTextAligned(p, x, y, pdf.GetNumberOfPages(), iText.Layout.Properties.TextAlignment.CENTER, iText.Layout.Properties.VerticalAlignment.MIDDLE, (float)(Math.PI * _options.Rotation / 180));
                canvasLayout.Close();
            }
        }
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