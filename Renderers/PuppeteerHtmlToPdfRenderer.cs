using HtmlToPdfCore.Interfaces;
using HtmlToPdfCore.Models;
using Microsoft.Extensions.Logging;
using PuppeteerSharp;
using PuppeteerSharp.Media;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace HtmlToPdfCore.Renderers;

/// <summary>
/// Chromium-based renderer using PuppeteerSharp to provide true feature 
/// </summary>
public class PuppeteerHtmlToPdfRenderer : IHtmlToPdfRenderer, IDisposable
{
    private readonly ILogger<PuppeteerHtmlToPdfRenderer> _logger;
    private static bool _browserDownloaded = false;
    private static readonly SemaphoreSlim _downloadSemaphore = new SemaphoreSlim(1, 1);

    //  1280px viewport width as its internal baseline
    private const int DefaultViewportWidth = 1280;
    private const int DefaultViewportHeight = 1024;

    public PuppeteerHtmlToPdfRenderer(ILogger<PuppeteerHtmlToPdfRenderer> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    private async Task EnsureBrowserDownloadedAsync()
    {
        if (_browserDownloaded) return;
        await _downloadSemaphore.WaitAsync();
        try
        {
            if (!_browserDownloaded)
            {
                var browserFetcher = new BrowserFetcher();
                 await browserFetcher.DownloadAsync();
                _browserDownloaded = true;
                _logger.LogInformation("Chromium engine downloaded successfully.");
            }
        }
        finally
        {
            _downloadSemaphore.Release();
        }
    }

    /// <summary>
    /// Chromium launch args matching internal Chromium config
    /// </summary>
    private LaunchOptions BuildLaunchOptions() => new LaunchOptions
    {
        Headless = true,
        Args = new[]
        {
            "--no-sandbox",
            "--disable-setuid-sandbox",
            "--disable-web-security",
            "--allow-file-access-from-files",
            "--enable-local-file-accesses",
            "--disable-dev-shm-usage",
            "--font-render-hinting=none",              // Pixel-identical font rendering
            "--force-color-profile=srgb",              // Consistent color output
            "--disable-background-timer-throttling",
            "--disable-backgrounding-occluded-windows",
            "--run-all-compositor-stages-before-draw",
            "--disable-gpu",                           // Fixes solid black pages on Windows/Servers
            "--disable-software-rasterizer"            // Fixes solid black pages on Windows/Servers
        }
    };

    /// <summary>
    /// Map our PdfRenderOptions to Puppeteer PdfOptions.
    /// NOTE: FontScale is NOT applied here — it is injected as CSS zoom (see BuildFontScaleCss).
    /// </summary>
    private PdfOptions MapToPuppeteerOptions(PdfRenderOptions options)
    {
        // PDF-level scale: combines Scale and Zoom only. FontScale is handled separately via CSS.
        decimal pdfScale = (decimal)Math.Clamp(options.Scale * (options.Zoom / 100.0), 0.1, 2.0);

        var pdfOptions = new PdfOptions
        {
            PrintBackground = options.PrintBackground,
            Landscape = options.Orientation == PageOrientation.Landscape,
            Scale = pdfScale,
            MarginOptions = new MarginOptions
            {
                Top    = $"{options.Margins.Top}{GetMarginUnit(options)}",
                Right  = $"{options.Margins.Right}{GetMarginUnit(options)}",
                Bottom = $"{options.Margins.Bottom}{GetMarginUnit(options)}",
                Left   = $"{options.Margins.Left}{GetMarginUnit(options)}"
            },
            PreferCSSPageSize = options.OverrideWithCssProperties,
            DisplayHeaderFooter = options.Header != null || options.Footer != null,
            OmitBackground = !options.PrintBackground
        };

        if (options.PageSize != PageSize.Custom)
        {
            try { pdfOptions.Format = GetPaperFormat(options.PageSize); }
            catch { /* Ignore, let Chromium use default */ }
        }
        else
        {
            pdfOptions.Width  = $"{options.CustomPageWidth}{GetMarginUnit(options)}";
            pdfOptions.Height = $"{options.CustomPageHeight}{GetMarginUnit(options)}";
        }

        if (options.Header != null) pdfOptions.HeaderTemplate = BuildTemplate(options.Header, true);
        if (options.Footer != null) pdfOptions.FooterTemplate = BuildTemplate(options.Footer, false);

        return pdfOptions;
    }

    /// <summary>
    /// FontScale is applied as CSS `html { zoom: X }` 
    /// does NOT use Puppeteer's PDF Scale for FontScale.
    /// </summary>
    private string BuildFontScaleCss(float fontScale)
    {
        if (Math.Abs(fontScale - 1.0f) < 0.001f) return string.Empty;
        return $"html {{ zoom: {fontScale.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}; }}";
    }

    /// <summary>
    /// Inject a BaseUrl into the HTML by inserting a &lt;base&gt; tag in the &lt;head&gt;.
    /// </summary>
    private string InjectBaseUrl(string html, string? baseUrl)
    {
        if (string.IsNullOrEmpty(baseUrl)) return html;
        string baseTag = $"<base href=\"{baseUrl}\">";
        if (html.Contains("<head>", StringComparison.OrdinalIgnoreCase))
            return html.Replace("<head>", $"<head>{baseTag}", StringComparison.OrdinalIgnoreCase);
        if (html.Contains("<html>", StringComparison.OrdinalIgnoreCase))
            return html.Replace("<html>", $"<html><head>{baseTag}</head>", StringComparison.OrdinalIgnoreCase);
        return $"{baseTag}{html}";
    }

    /// <summary>
    /// Apply page-level configuration before loading content.
    /// Always sets viewport and media type 
    /// </summary>
    private async Task ConfigurePageAsync(IPage page, PdfRenderOptions options)
    {
        if (!options.EnableJavaScript)
            await page.SetJavaScriptEnabledAsync(false);

        // always emulates Print media unless Screen is specified
        var mediaType = options.MediaType == CssMediaType.Screen
            ? PuppeteerSharp.Media.MediaType.Screen
            : PuppeteerSharp.Media.MediaType.Print;
        await page.EmulateMediaTypeAsync(mediaType);

        //  always sets viewport. FitToPaperWidth uses paper-size pixels.
        // Paper widths in px at 96 DPI: A4=794, Legal=816, Letter=816, etc.
        int vpWidth;
        if (options.FitToPaperWidth)
        {
            // Calculate paper width in pixels at 96 DPI to FitToPaperWidth
            vpWidth = GetPaperWidthPx(options.PageSize, options.Orientation, options.CustomPageWidth);
        }
        else
        {
            vpWidth = options.ViewPortWidth ?? DefaultViewportWidth;
        }
        int vpHeight = options.ViewPortHeight ?? DefaultViewportHeight;
        await page.SetViewportAsync(new ViewPortOptions
        {
            Width = vpWidth,
            Height = vpHeight,
            DeviceScaleFactor = 1   // always uses 1
        });

        // Apply custom HTTP headers via request interception
        if (options.CustomHttpHeaders?.Count > 0)
        {
            await page.SetRequestInterceptionAsync(true);
            page.Request += async (sender, e) =>
            {
                var headers = new System.Collections.Generic.Dictionary<string, string>(e.Request.Headers);
                foreach (var kv in options.CustomHttpHeaders)
                    headers[kv.Key] = kv.Value;
                await e.Request.ContinueAsync(new Payload { Headers = headers });
            };
        }

        // Apply cookies
        if (options.CustomCookies?.Count > 0)
        {
            var cookies = options.CustomCookies
                .Select(kv => new CookieParam { Name = kv.Key, Value = kv.Value, Domain = "localhost" })
                .ToArray();
            await page.SetCookieAsync(cookies);
        }
    }

    /// <summary>
    /// Get paper width in pixels at 96 DPI for FitToPaperWidth mode.
    /// Matches internal paper-sizing logic.
    /// </summary>
    private static int GetPaperWidthPx(PageSize pageSize, PageOrientation orientation, float? customWidthMm)
    {
        // Standard paper widths in mm
        double widthMm = pageSize switch
        {
            PageSize.A3      => 297,
            PageSize.A4      => 210,
            PageSize.A5      => 148,
            PageSize.Letter  => 215.9,
            PageSize.Legal   => 215.9,
            PageSize.Tabloid => 279.4,
            PageSize.Ledger  => 431.8,
            PageSize.Custom  => customWidthMm ?? 210,
            _                => 210  // default A4
        };

        // For landscape, swap width/height
        if (orientation == PageOrientation.Landscape)
        {
            widthMm = pageSize switch
            {
                PageSize.A3      => 420,
                PageSize.A4      => 297,
                PageSize.A5      => 210,
                PageSize.Letter  => 279.4,
                PageSize.Legal   => 355.6,
                PageSize.Tabloid => 431.8,
                PageSize.Ledger  => 279.4,
                _                => 297
            };
        }

        // Convert mm to px at 96 DPI: px = mm * 96 / 25.4
        return (int)Math.Round(widthMm * 96.0 / 25.4);
    }

    /// <summary>
    /// Post-load: inject FontScale CSS, custom CSS, run JS, wait for fonts.
    /// </summary>
    private async Task ApplyPostLoadOptionsAsync(IPage page, PdfRenderOptions options)
    {
        // FontScale via CSS zoom.
        // To fix blank first pages caused by `min-height: 100vh` combining with `padding`,
        // we enforce border-box sizing on html/body so padding doesn't overflow the physical page dimensions,
        // without destroying the user's custom flexbox and height styles.
        var baseFixCss = "html, body { box-sizing: border-box !important; }";
        var fontScaleCss = BuildFontScaleCss(options.FontScale);
        await page.AddStyleTagAsync(new AddTagOptions { Content = $"{baseFixCss} {fontScaleCss}" });

        // Grayscale via CSS filter — applies this post-render as a CSS filter
        if (options.Grayscale)
            await page.AddStyleTagAsync(new AddTagOptions
            {
                Content = "html { filter: grayscale(100%); -webkit-filter: grayscale(100%); }"
            });

        // Custom CSS injection
        if (!string.IsNullOrEmpty(options.CustomCss))
            await page.AddStyleTagAsync(new AddTagOptions { Content = options.CustomCss });

        // External CSS URL
        if (!string.IsNullOrEmpty(options.CustomCssUrl))
            await page.AddStyleTagAsync(new AddTagOptions { Url = options.CustomCssUrl });

        // Custom JavaScript
        if (!string.IsNullOrEmpty(options.Javascript))
            await page.EvaluateExpressionAsync(options.Javascript);

        // Wait for all fonts to finish loading (web fonts, @font-face, etc.)
        try { await page.EvaluateFunctionAsync("async () => { await document.fonts.ready; }"); }
        catch { /* Ignore timeout — fonts may already be loaded */ }

        // A small base delay to allow WebKit layout rendering to settle after font/CSS application,
        // mirroring IronPDF's internal minimum settling timeout if no RenderDelay is provided
        int renderDelayMs = options.RenderDelay > 0 ? options.RenderDelay : 150;
        await Task.Delay(renderDelayMs);
        if (options.Watermark != null && !string.IsNullOrEmpty(options.Watermark.Text))
        {
            await InjectWatermarkAsync(page, options.Watermark);
        }

        // Additional render delay if specified
        if (options.RenderDelay > 0)
            await Task.Delay(options.RenderDelay);
    }

    private async Task InjectWatermarkAsync(IPage page, WatermarkOptions watermark)
    {
        string justify = watermark.Position switch
        {
            WatermarkPosition.TopLeft => "top: 0; left: 0;",
            WatermarkPosition.TopCenter => "top: 0; left: 50%; transform: translateX(-50%);",
            WatermarkPosition.TopRight => "top: 0; right: 0;",
            WatermarkPosition.BottomLeft => "bottom: 0; left: 0;",
            WatermarkPosition.BottomCenter => "bottom: 0; left: 50%; transform: translateX(-50%);",
            WatermarkPosition.BottomRight => "bottom: 0; right: 0;",
            _ => "top: 50%; left: 50%; transform: translate(-50%, -50%) " + (watermark.Rotation != 0 ? $"rotate({watermark.Rotation}deg);" : ";")
        };

        if (watermark.Position != WatermarkPosition.Center && watermark.Rotation != 0)
            justify += $" transform: rotate({watermark.Rotation}deg); transform-origin: center;";

        var script = $$"""
        const wm = document.createElement('div');
        wm.innerHTML = '{{watermark.Text.Replace("'", "\\'")}}';
        wm.style.cssText = `position: fixed; {{justify}} font-size: {{watermark.FontSize}}px; font-family: '{{watermark.FontFamily}}'; color: {{watermark.Color}}; opacity: {{watermark.Opacity.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}}; z-index: 2147483647; pointer-events: none; margin: 20px; text-align: center; white-space: pre-wrap;`;
        document.body.appendChild(wm);
        """;
        await page.EvaluateExpressionAsync(script);
    }

    private string BuildTemplate(HeaderFooterOptions hf, bool isHeader)
    {
        if (hf == null || string.IsNullOrEmpty(hf.HtmlContent)) return "<span></span>";
        string content = hf.HtmlContent
            .Replace("{page}", "<span class='pageNumber'></span>")
            .Replace("{total}", "<span class='totalPages'></span>");

        string dividerStyle = "";
        if (hf.DividerLine)
        {
            string border = $"1px solid {hf.DividerColor ?? "black"}";
            dividerStyle = isHeader ? $"border-bottom: {border};" : $"border-top: {border};";
        }

        string style = $@"<style>
            #header, #footer {{ padding: 0 !important; margin: 0 !important; width: 100% !important; }}
            .custom-hf {{ width: 100%; font-size: {(hf.FontSize ?? 10)}px; font-family: {(hf.FontFamily ?? "sans-serif")}; color: {(hf.Color ?? "black")}; padding: 10px; {dividerStyle} box-sizing: border-box; }}
        </style>";
        return $"{style}<div class='custom-hf'>{content}</div>";
    }

    private string GetMarginUnit(PdfRenderOptions options) => options.MarginUnit switch
    {
        MeasurementUnit.Inches => "in",
        MeasurementUnit.Pixels => "px",
        _ => "mm"
    };

    private PaperFormat GetPaperFormat(PageSize size) => size switch
    {
        PageSize.A4      => PaperFormat.A4,
        PageSize.A3      => PaperFormat.A3,
        PageSize.A5      => PaperFormat.A5,
        PageSize.Letter  => PaperFormat.Letter,
        PageSize.Legal   => PaperFormat.Legal,
        PageSize.Ledger  => PaperFormat.Ledger,
        PageSize.Tabloid => PaperFormat.Tabloid,
        _                => PaperFormat.A4
    };

    // ─── Public API ────────────────────────────────────────────────────────────

    public async Task<byte[]> RenderHtmlToPdfAsync(string html, PdfRenderOptions? options = null, CancellationToken cancellationToken = default)
    {
        await EnsureBrowserDownloadedAsync();
        options ??= new PdfRenderOptions();

        await using var browser = await Puppeteer.LaunchAsync(BuildLaunchOptions());
        await using var page = await browser.NewPageAsync();

        await ConfigurePageAsync(page, options);

        var navOptions = new NavigationOptions
        {
            WaitUntil = new[] { WaitUntilNavigation.Load, WaitUntilNavigation.Networkidle0 },
            Timeout = options.RenderTimeout > 0 ? options.RenderTimeout : 30000
        };

        // ── Key parity fix ────────────────────────────────────────────
        // SetContentAsync renders from about:blank which blocks ALL local file://
        // assets (CSS, images, fonts) even with a <base> tag, due to Chromium's
        // same-origin security. writes to a temp file and opens it with
        // file:// URL so Chromium sees a real file origin and loads all assets.
        // We do the same here.
        string? tempFile = null;
        try
        {
            // For ALL local file:// BaseUrls, we write to the OS Temp directory
            // We DO NOT write to the BaseUrl directory because it triggers ASP.NET 'hot reload' file watchers and crashes the app!
            // Instead, we inject <base href> and rely on Chromium's --allow-file-access-from-files flag to load across local directories.
            if (!string.IsNullOrEmpty(options.BaseUrl) && options.BaseUrl.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
            {
                html = InjectBaseUrl(html, options.BaseUrl);
                tempFile = Path.Combine(Path.GetTempPath(), $"_pdf_render_{Guid.NewGuid():N}.html");
                await File.WriteAllTextAsync(tempFile, html, cancellationToken);
                var fileUrl = new Uri(tempFile).AbsoluteUri;
                await page.GoToAsync(fileUrl, navOptions);
            }
            else
            {
                // For http/https BaseUrl or no BaseUrl, inject <base> and use SetContentAsync
                html = InjectBaseUrl(html, options.BaseUrl);
                await page.SetContentAsync(html, navOptions);
            }

            await ApplyPostLoadOptionsAsync(page, options);
            return await page.PdfDataAsync(MapToPuppeteerOptions(options));
        }
        finally
        {
            // Always clean up temp file
            if (tempFile != null && File.Exists(tempFile))
                try { File.Delete(tempFile); } catch { }
        }
    }

    public async Task<byte[]> RenderHtmlFileToPdfAsync(string filePath, PdfRenderOptions? options = null, CancellationToken cancellationToken = default)
    {
        var html = await File.ReadAllTextAsync(filePath, cancellationToken);
        options ??= new PdfRenderOptions();

        // If no BaseUrl set, auto-set it to the file's directory — exactly handles file rendering
        if (string.IsNullOrEmpty(options.BaseUrl))
        {
            var dir = Path.GetDirectoryName(Path.GetFullPath(filePath)) ?? Directory.GetCurrentDirectory();
            options.BaseUrl = new Uri(dir + Path.DirectorySeparatorChar).AbsoluteUri;
        }

        return await RenderHtmlToPdfAsync(html, options, cancellationToken);
    }

    public async Task<byte[]> RenderUrlToPdfAsync(string url, PdfRenderOptions? options = null, CancellationToken cancellationToken = default)
    {
        await EnsureBrowserDownloadedAsync();
        options ??= new PdfRenderOptions();

        await using var browser = await Puppeteer.LaunchAsync(BuildLaunchOptions());
        await using var page = await browser.NewPageAsync();

        await ConfigurePageAsync(page, options);

        var navOptions = new NavigationOptions
        {
            WaitUntil = new[] { WaitUntilNavigation.Load, WaitUntilNavigation.Networkidle0 },
            Timeout = options.RenderTimeout > 0 ? options.RenderTimeout : 30000
        };

        await page.GoToAsync(url, navOptions);
        await ApplyPostLoadOptionsAsync(page, options);
        return await page.PdfDataAsync(MapToPuppeteerOptions(options));
    }

    public async Task RenderHtmlToPdfFileAsync(string html, string outputPath, PdfRenderOptions? options = null, CancellationToken cancellationToken = default)
    {
        var data = await RenderHtmlToPdfAsync(html, options, cancellationToken);
        await File.WriteAllBytesAsync(outputPath, data, cancellationToken);
    }

    public void Dispose() { }
}
