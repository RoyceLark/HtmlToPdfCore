# HtmlToPdfCore (RoyceLark Corporations)

A robust, high-performance HTML to PDF rendering library for .NET 9 with AI/ML-powered and Soon .NET 10 with AI/ML-powered optimizations.

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
dotnet add package RoyceLark.HtmlToPdfCore
```

Or via NuGet Package Manager:

```
Install-Package RoyceLark.HtmlToPdfCore
```

## Quick Start

### Simple Usage

```csharp
using RoyceLark.HtmlToPdfCore;

// Create instance
var converter = HtmlToPdf.Create();

// Render HTML to PDF
var html = "<h1>Hello World!</h1><p>This is a PDF document.</p>";
var pdfBytes = await converter.RenderAsync(html);

// Save to file
await File.WriteAllBytesAsync("output.pdf", pdfBytes);
```

### Fluent API

```csharp
using RoyceLark.HtmlToPdfCore;
using RoyceLark.HtmlToPdfCore.Models;

var converter = HtmlToPdf.Create();

await converter
    .FromHtml("<h1>My Document</h1><p>Content here...</p>")
    .WithPageSize(PageSize.A4)
    .WithOrientation(PageOrientation.Portrait)
    .WithMargins(20, 20, 20, 20)
    .WithMetadata(m =>
    {
        m.Title = "My Document";
        m.Author = "Your Name";
    })
    .SaveAsync("output.pdf");
```

### With Configuration

```csharp
var html = "<h1>Configured Document</h1>";

var pdfBytes = await converter.RenderAsync(html, options =>
{
    options.PageSize = PageSize.Letter;
    options.Orientation = PageOrientation.Landscape;
    options.Margins = new PageMargins(15, 15, 15, 15);
    options.EnableJavaScript = true;
    options.PrintBackground = true;
    options.ImageQuality = 90;
    options.Dpi = 300;
    
    // Header with page numbers
    options.Footer = new HeaderFooterOptions
    {
        ShowPageNumbers = true,
        PageNumberFormat = "Page {page} of {total}",
        Alignment = TextAlignment.Center
    };
    
    // Metadata
    options.Metadata = new PdfMetadata
    {
        Title = "My Report",
        Author = "John Doe",
        Subject = "Monthly Report",
        Keywords = "report, monthly, analytics"
    };
});
```

## Advanced Usage

### Render from URL

```csharp
var pdfBytes = await converter.RenderUrlAsync("https://example.com");
```

### Render from File

```csharp
var pdfBytes = await converter.RenderFileAsync("template.html");
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
using RoyceLark.HtmlToPdfCore.Extensions;

// In Program.cs or Startup.cs
builder.Services.AddHtmlToPdfCore(options =>
{
    options.EnableAIOptimization = true;
    options.MaxConcurrentRenders = Environment.ProcessorCount;
    options.DefaultRenderTimeout = 30000;
});

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
        var html = "<h1>Monthly Report</h1><p>Content...</p>";
        var pdf = await _renderer.RenderHtmlToPdfAsync(html);
        
        return File(pdf, "application/pdf", "report.pdf");
    }
}
```

### Certificate Generation

```csharp
using RoyceLark.HtmlToPdfCore.Extensions;


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
using RoyceLark.HtmlToPdfCore;
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

- .NET 9.0 soon .Net 10
- Windows, Linux, or macOS

## Dependencies

- iText7 (9.0.4)
- iText7.pdfhtml (6.3.0)
- HtmlAgilityPack (1.11.61)
- Microsoft.ML (3.0.1)
- AngleSharp (1.1.2)
- SixLabors.ImageSharp (3.1.5)

## License

MIT License

## Support

For issues, questions, or contributions, please visit:
https://github.com/roycelark/RoyceLark.HtmlToPdfCore

## Changelog

### Version 1.0.0
- Initial release
- Full HTML to PDF rendering
- AI/ML-powered optimizations
- Comprehensive PDF manipulation
- .NET 9 and soon .NET 10 support

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## Acknowledgments

Built with:
- iText7 for PDF generation
- Microsoft.ML for AI capabilities
- AngleSharp for HTML parsing
