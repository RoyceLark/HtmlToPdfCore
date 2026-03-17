# ML.HtmlToPdf (RoyceLark Corporations)

A robust, high-performance HTML to PDF rendering library for **.NET 8, .NET 9, and .NET 10** with AI/ML-powered optimizations.

**Professional HTML to PDF conversion library for .NET**

A robust, high-performance HTML to PDF rendering library with AI/ML-powered optimizations, built on iText 9 and designed as a modern, high-performance solution for .NET applications.

## Features

### Core Features
- ✅ HTML to PDF conversion with full CSS support
- ✅ JavaScript execution support
- ✅ Multiple page sizes (A0-A6, Letter, Legal, Tabloid, Ledger, Custom)
- ✅ Portrait and Landscape orientations
- ✅ Custom margins and page settings
- ✅ Header and footer support with page numbers
- ✅ URL to PDF conversion
- ✅ File-based rendering
- ✅ Custom CSS injection
- ✅ PDF metadata support

### Advanced Features
- ✅ PDF merging and splitting
- ✅ Watermark support (text, position, opacity, rotation)
- ✅ PDF encryption with password protection
- ✅ Text extraction from PDFs
- ✅ Image compression and quality control
- ✅ Grayscale rendering
- ✅ Custom DPI settings

### AI/ML Features
- ✅ AI-powered memory usage prediction
- ✅ Intelligent resource optimization
- ✅ Automatic performance tuning
- ✅ Memory leak prevention
- ✅ Content complexity analysis

### IronPDF Parity Features
- ✅ **Full API Symmetry**: Drop-in replacement for `PdfMlRender` and `MLPdfRenderOptions`.
- ✅ **True Chromium Rendering**: `PdfMlRender` uses a headless Chromium engine — identical — supporting modern CSS, CSS Grid, WebGL, JS events, and viewport scaling.
- ✅ **Fluent Builder**: Native fluent API for all rendering settings.
- ✅ **Static Rendering**: Convenience methods like `PdfMlRender.StaticRenderHtmlAsPdf()`.
- ✅ **Extended Page Sizes**: A0-A9, B0-B10, Letter, Legal, Ledger, Tabloid, Custom.
- ✅ **FontScale via CSS zoom**: `FontScale` is injected as `html { zoom: X }`.
- ✅ **Viewport 1280×1024 default**: Matches  internal Chromium viewport baseline.
- ✅ **Media Type Emulation**: Always sets `print` or `screen` CSS media type.
- ✅ **Web Font Loading**: Waits for `document.fonts.ready` before rendering — no cut-off fonts.
- ✅ **BaseUrl injection**: `<base href>` is injected into `<head>` to resolve local CSS, images, and fonts.
- ✅ **Custom CSS injection**: `CustomCss` and `CustomCssUrl` injected after page load.
- ✅ **JavaScript support**: Custom JS evaluated post-load via `Javascript` option.
- ✅ **HTML Form Conversion**: AcroForms from HTML form elements.
- ✅ **Advanced Styling**: `DrawDividerLine`, `DividerColor`, `FontScale`, `Zoom`, `Grayscale`.
- ✅ **Complete Metadata**: `Title`, `Author`, `Subject`, `Keywords` as top-level properties.
- ✅ **Security/Encryption**: Password protection, copy/print restrictions.
- ✅ **Watermarks**: Text watermarks with opacity, rotation, position, and color.

### Performance & Reliability
- ✅ Object pooling for memory efficiency
- ✅ Concurrent rendering with semaphore control
- ✅ Async/await throughout
- ✅ CancellationToken support
- ✅ Comprehensive error handling
- ✅ Structured logging with Microsoft.Extensions.Logging
- ✅ Thread-safe operations

## Installation

```bash
dotnet add package Pdf.ML
```

Or via NuGet Package Manager:

```
Install-Package Pdf.ML
```

## Quick Start - Dual Engine Architecture

`Pdf.ML` features a dual-engine architecture giving you the choice between absolute Chromium parity (via `PuppeteerSharp`) or highly-optimized lightweight rendering (via `iText9`).

### 1. IronPDF Drop-In Replacement (True Chromium Engine)

Use `PdfMlRender` when you want a 1:1 drop-in replacement for IronPDF using a real headless Chromium engine. This guarantees perfect rendering for complex WebGL, JS events, and experimental CSS.

```csharp
using HtmlToPdfCore;
using HtmlToPdfCore.Models;

var renderer = new PdfMlRender();

// Build BaseUrl so local CSS, images, web fonts resolve correctly
var projectRoot = Directory.GetCurrentDirectory();
var baseUrl = new Uri(projectRoot + Path.DirectorySeparatorChar).AbsoluteUri;

var options = new MLPdfRenderOptions
{
    // Page layout
    PageSize        = PageSize.A4,
    Orientation     = PageOrientation.Portrait,   // aliases: PaperOrientation
    Margins         = new PageMargins(20, 20, 20, 20),
    FitToPaperWidth = true,

    // Styling — exactly matching defaults
    MediaType       = CssMediaType.Screen,    // CSS @media screen rules apply
    PrintBackground = true,                   // render background colors & images
    FontScale       = 0.8f,                   // 80% font scale injected as CSS zoom
    Dpi             = 96,                     // match default DPI
    Zoom            = 100,                    // page-level zoom (100 = default)

    // Asset resolution
    BaseUrl         = baseUrl,               // resolves relative CSS, images, fonts

    // Optional: header/footer
    HtmlHeader = new HtmlHeaderFooter
    {
        HtmlFragment   = "<h1 style='font-size:10px'>My Report</h1>",
        Height         = 15,
        DrawDividerLine = true
    },

    // PDF metadata
    Title  = "My Document",
    Author = "John Doe",
};

var pdf = await renderer.RenderHtmlAsPdfAsync("<h1>MLPDF Parity</h1>", options);
pdf.SaveAs("output.pdf");
```

**Static Quick-Start**
```csharp
using HtmlToPdfCore;

var pdf = PdfMlRender.StaticRenderHtmlAsPdf("<h1>Instant PDF</h1>");
pdf.SaveAs("instant.pdf");
```

### 2. High-Performance API (Optimized iText9 Engine)

Use `HtmlToPdf.Create()` when you want maximum server performance, the lowest memory footprint, and AI-powered memory optimizations using the native iText9 engine.

```csharp
using HtmlToPdfCore;
using HtmlToPdfCore.Models;

// Uses the highly-optimized native engine
var pdf = await HtmlToPdf.Create()
    .FromHtml("<h1>High Performance Document</h1>")
    .WithPageSize(PageSize.Letter)
    .AsLandscape()
    .WithMargins(25) // Standard 1-inch margins
    .WithZoom(110)
    .WithFontScale(0.9f)
    .WithHtmlFooter("<footer>Page {page} of {total}</footer>", height: 10)
    .WithMetadata(m => m.Title = "Analytics Report")
    .WithWatermark("CONFIDENTIAL", w => w.Opacity = 0.2f)
    .GenerateAsync();

pdf.SaveAs("optimized_report.pdf");
```

## Advanced Usage

### Render from URL

```csharp
var pdf = await renderer.RenderUrlAsync("https://example.com");
```

### Render from File

```csharp
var pdf = await renderer.RenderFileAsync("template.html");
```

### Custom CSS Injection

```csharp
var html = "<h1>Styled Document</h1>";

var pdfBytes = await converter.RenderAsync(html, options =>
{
    options.CustomCss = @"
        body { font-family: 'Arial', sans-serif; }
        h1 { color: #333; border-bottom: 2px solid #007bff; }
        p { line-height: 1.6; }
    ";
});
```

### PDF Manipulation

#### Merge PDFs

```csharp
var pdf1 = await converter.RenderAsync("<h1>Document 1</h1>");
var pdf2 = await converter.RenderAsync("<h1>Document 2</h1>");
var pdf3 = await converter.RenderAsync("<h1>Document 3</h1>");

var mergedPdf = await converter.MergeAsync(new[] { pdf1, pdf2, pdf3 });
```

#### Split PDF

```csharp
var pdf = await converter.RenderAsync("<h1>Page 1</h1><div style='page-break-after: always;'></div><h1>Page 2</h1>");

// Split into individual pages
var pages = await converter.SplitAsync(pdf, new[] { 1, 2 });
```

#### Add Watermark

```csharp
var pdf = await converter.RenderAsync("<h1>Confidential Document</h1>");

var watermarkedPdf = await converter.AddWatermarkAsync(pdf, "CONFIDENTIAL", options =>
{
    options.Opacity = 0.3f;
    options.FontSize = 72;
    options.Rotation = 45;
    options.Position = WatermarkPosition.Center;
    options.Color = "#FF0000";
});
```

#### Encrypt PDF

```csharp
var pdf = await converter.RenderAsync("<h1>Secure Document</h1>");

var encryptedPdf = await converter.EncryptAsync(
    pdf,
    userPassword: "user123",
    ownerPassword: "admin456"
);
```

#### Extract Text

```csharp
var pdf = await converter.RenderAsync("<h1>Hello World</h1><p>This is sample text.</p>");

var text = await converter.ExtractTextAsync(pdf);
Console.WriteLine(text);
```

## Dependency Injection

### ASP.NET Core Integration

```csharp
using HtmlToPdfCore.Extensions;

// In Program.cs or Startup.cs
builder.Services.AddHtmlToPdfCore(options =>
{
    options.EnableAIOptimization = true;
    options.MaxConcurrentRenders = Environment.ProcessorCount;
    options.DefaultRenderTimeout = 30000;
});

// If you plan to use the True Chromium Engine (PdfMlRender) via DI:
builder.Services.AddSingleton<PdfMlRender>();
// Note: PdfMlRender manages its own headless Chromium instances under the hood.

// In your controller or service
public class ReportController : ControllerBase
{
    private readonly IHtmlToPdfRenderer _renderer;
    
    public ReportController(IHtmlToPdfRenderer renderer)
    {
        _renderer = renderer;
    }
    
    [HttpGet("generate")]
    public async Task<IActionResult> GenerateReport([FromServices] PdfMlRender chromiumRenderer)
    {
        // Example 1: Using the Chromium Engine for IronPDF drop-in parity
        var pdf1 = await chromiumRenderer.RenderHtmlAsPdfAsync("<h1>True Chromium PDF</h1>");
        
        // Example 2: Using the high-speed iText engine
        var html = "<h1>Monthly Report</h1><p>Content...</p>";
        var pdf2 = await _renderer.RenderHtmlToPdfAsync(html);
        
        return File(pdf, "application/pdf", "report.pdf");
    }
}
```

### Certificate Generation

```csharp
using HtmlToPdfCore.Extensions;


// In your controller or service
public class ReportController : ControllerBase
{
    private readonly IHtmlToPdfRenderer _renderer;
    
    public ReportController(IHtmlToPdfRenderer renderer)
    {
        _renderer = renderer;
    }
    
    [HttpGet("generate")]
    public async Task<IActionResult> GenerateReport()
    {
        var projectRoot = Directory.GetCurrentDirectory();
            var baseUrl = new Uri(projectRoot + Path.DirectorySeparatorChar).AbsoluteUri;

            var converter = HtmlToPdf.Create();
            var watermark = "";
            var pdf = await converter.RenderAsync(certificateHtml, options =>
            {
                // FitToPaperWidth equivalent - handled automatically by page size
                options.PageSize = PageSize.Legal;
                options.Orientation = PageOrientation.Portrait;// PaperOrientation
                options.MediaType = CssMediaType.Screen;
                //for css rendering
                options.BaseUrl = baseUrl;
                // Reduce font size by 20%
                options.FontScale = 0.8f;  // 80% of original

                // Increase margins for portrait mode
                options.Margins = new PageMargins(
                    top: 20,      // Increased
                    right: 30,    // Increased  
                    bottom: 20,   // Increased
                    left: 30      // Increased
                );

                options.FitToPaperWidth = true;
                options.PrintBackground = true;


                // Additional useful options
               // options.EnableJavaScript = true;
                options.Dpi = 96;
                options.ImageQuality = 100;
            });
            pdf = await converter.AddWatermarkAsync(pdf, "Draft", options =>
            {
                options.Position = WatermarkPosition.Center;  // MiddleCenter
                options.Opacity = 0.3f;  // 30% opacity
                options.FontSize = 72;
                options.Rotation = 45;
                options.Color = "#808080";
            });
            //Then add watermark
           pdf = await converter.AddWatermarkAsync(pdf, watermark, options =>
           {
               options.Position = WatermarkPosition.Center;
               options.Opacity = 0.3f;
           });

            return File(pdf, "application/pdf", "certificate.pdf");
    }
}
```


## AI/ML Optimization

The library includes built-in AI/ML capabilities for performance optimization:

```csharp
using HtmlToPdfCore;
using Microsoft.Extensions.Logging;

var loggerFactory = LoggerFactory.Create(builder => 
    builder.AddConsole().SetMinimumLevel(LogLevel.Information));

var converter = HtmlToPdf.Create(loggerFactory);

// AI optimization is enabled by default
var html = "<h1>Complex Document</h1>" + string.Join("", 
    Enumerable.Range(1, 1000).Select(i => $"<p>Paragraph {i}</p>"));

var pdfBytes = await converter.RenderAsync(html, options =>
{
    options.EnableAIOptimization = true; // Enabled by default
});

// The AI will:
// - Predict memory usage
// - Optimize rendering strategy
// - Prevent memory leaks
// - Adjust compression levels
// - Determine optimal chunk sizes
```

## Performance Best Practices

1. **Reuse Instances**: Create one `HtmlToPdf` instance and reuse it
2. **Enable AI Optimization**: Keep `EnableAIOptimization` enabled for best performance
3. **Use Async Methods**: Always use async/await for better scalability
4. **Set Appropriate Timeouts**: Configure timeouts based on document complexity
5. **Compress Images**: Enable image compression for smaller file sizes
6. **Use Object Pooling**: The library automatically uses object pooling for streams

## Examples

### Invoice Generation

```csharp
var invoiceHtml = @"
<!DOCTYPE html>
<html>
<head>
    <style>
        body { font-family: Arial, sans-serif; }
        .header { text-align: center; color: #333; }
        .invoice-details { margin: 20px 0; }
        table { width: 100%; border-collapse: collapse; }
        th, td { border: 1px solid #ddd; padding: 8px; text-align: left; }
        th { background-color: #f2f2f2; }
        .total { font-weight: bold; font-size: 1.2em; }
    </style>
</head>
<body>
    <div class='header'>
        <h1>INVOICE</h1>
        <p>Invoice #12345</p>
    </div>
    <div class='invoice-details'>
        <p><strong>Date:</strong> 2024-12-08</p>
        <p><strong>Customer:</strong> John Doe</p>
    </div>
    <table>
        <thead>
            <tr>
                <th>Item</th>
                <th>Quantity</th>
                <th>Price</th>
                <th>Total</th>
            </tr>
        </thead>
        <tbody>
            <tr>
                <td>Product A</td>
                <td>2</td>
                <td>$50.00</td>
                <td>$100.00</td>
            </tr>
            <tr>
                <td>Product B</td>
                <td>1</td>
                <td>$75.00</td>
                <td>$75.00</td>
            </tr>
        </tbody>
        <tfoot>
            <tr class='total'>
                <td colspan='3'>Total</td>
                <td>$175.00</td>
            </tr>
        </tfoot>
    </table>
</body>
</html>";

await converter
    .FromHtml(invoiceHtml)
    .WithPageSize(PageSize.A4)
    .WithFooter("", showPageNumbers: true)
    .WithMetadata(m =>
    {
        m.Title = "Invoice #12345";
        m.Author = "Company Name";
    })
    .SaveAsync("invoice.pdf");
```

### Certificate Generation

```csharp
var certificateHtml = @"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {
            font-family: 'Georgia', serif;
            text-align: center;
            padding: 50px;
        }
        h1 {
            font-size: 48px;
            color: #2c3e50;
            margin-bottom: 30px;
        }
        .recipient {
            font-size: 36px;
            color: #3498db;
            margin: 30px 0;
        }
        .description {
            font-size: 18px;
            margin: 20px 0;
        }
    </style>
</head>
<body>
    <h1>Certificate of Achievement</h1>
    <p class='description'>This is to certify that</p>
    <p class='recipient'>John Doe</p>
    <p class='description'>has successfully completed the course</p>
    <p class='recipient'>Advanced .NET Development</p>
    <p class='description'>Date: December 8, 2024</p>
</body>
</html>";

await converter
    .FromHtml(certificateHtml)
    .WithPageSize(PageSize.A4)
    .WithOrientation(PageOrientation.Landscape)
    .SaveAsync("certificate.pdf");
```

## Error Handling

```csharp
try
{
    var pdfBytes = await converter.RenderAsync(html);
}
catch (PdfRenderException ex)
{
    // Handle rendering errors
    Console.WriteLine($"Rendering failed: {ex.Message}");
    
    if (ex.InnerException != null)
    {
        Console.WriteLine($"Inner error: {ex.InnerException.Message}");
    }
}
catch (OperationCanceledException)
{
    // Handle cancellation
    Console.WriteLine("Operation was cancelled");
}
```

## Requirements

### Supported .NET Versions

| Version | Support |
|---|---|
| .NET 8 | ✅ Supported |
| .NET 9 | ✅ Supported |
| .NET 10 | ✅ Supported |

- Windows, Linux, or macOS

## Dependencies

- iText (9.4.0)
- iText.pdfhtml (6.3.0)
- HtmlAgilityPack (1.12.4)
- Microsoft.ML (5.0.0)
- AngleSharp (1.4.0)
- SixLabors.ImageSharp (3.1.12)

## License

MIT License

## Support

For issues, questions, or contributions, please visit:
https://github.com/roycelark/ML.HtmlToPdf

## Changelog

### Version 9.0.0
- Added multi-targeting support for **.NET 8, .NET 9, and .NET 10**
- Full HTML to PDF rendering
- AI/ML-powered optimizations
- Comprehensive PDF manipulation

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## Acknowledgments

Built with:
- iText7 for PDF generation
- Microsoft.ML for AI capabilities
- AngleSharp for HTML parsing


© 2025 Royce Lark Corporations. All rights reserved.