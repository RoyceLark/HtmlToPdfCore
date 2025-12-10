# Getting Started with HtmlToPdfCore

This guide will help you get up and running with HtmlToPdfCore in minutes.

## Quick Installation

### Prerequisites

- .NET 9.0 or .NET 10.0 SDK installed
- Any IDE: Visual Studio 2022, VS Code, or Rider

### Install via NuGet

```bash
# Using .NET CLI
dotnet add package HtmlToPdfCore

# Using Package Manager Console (Visual Studio)
Install-Package HtmlToPdfCore

# Using PackageReference (add to .csproj)
<PackageReference Include="HtmlToPdfCore" Version="1.0.0" />
```

## Your First PDF

### 1. Create a New Console Application

```bash
dotnet new console -n MyPdfApp
cd MyPdfApp
dotnet add package HtmlToPdfCore
```

### 2. Write Your First Code

Edit `Program.cs`:

```csharp
using HtmlToPdfCore;

// Create converter
var converter = HtmlToPdf.Create();

// Simple HTML
var html = @"
<!DOCTYPE html>
<html>
<head>
    <style>
        body { font-family: Arial, sans-serif; padding: 20px; }
        h1 { color: #2c3e50; }
    </style>
</head>
<body>
    <h1>My First PDF!</h1>
    <p>This was created with HtmlToPdfCore.</p>
</body>
</html>";

// Convert to PDF
var pdfBytes = await converter.RenderAsync(html);

// Save to file
await File.WriteAllBytesAsync("my-first.pdf", pdfBytes);

Console.WriteLine("PDF created successfully!");
```

### 3. Run Your Application

```bash
dotnet run
```

You'll find `my-first.pdf` in your project directory!

## Common Use Cases

### Invoice Generation

```csharp
var html = @"
<html>
<body>
    <h1>INVOICE #12345</h1>
    <table border='1' style='width:100%; border-collapse: collapse;'>
        <tr>
            <th>Item</th>
            <th>Quantity</th>
            <th>Price</th>
        </tr>
        <tr>
            <td>Product A</td>
            <td>2</td>
            <td>$100.00</td>
        </tr>
    </table>
</body>
</html>";

await converter
    .FromHtml(html)
    .WithPageSize(PageSize.A4)
    .WithMetadata(m => {
        m.Title = "Invoice #12345";
        m.Author = "My Company";
    })
    .SaveAsync("invoice.pdf");
```

### Report with Header and Footer

```csharp
var html = "<h1>Monthly Report</h1><p>Report content here...</p>";

var pdf = await converter.RenderAsync(html, options =>
{
    options.PageSize = PageSize.Letter;
    
    // Add header
    options.Header = new HeaderFooterOptions
    {
        HtmlContent = "<div style='text-align:center;'>Company Name</div>",
        Height = 30
    };
    
    // Add footer with page numbers
    options.Footer = new HeaderFooterOptions
    {
        ShowPageNumbers = true,
        PageNumberFormat = "Page {page} of {total}",
        Alignment = TextAlignment.Center
    };
});
```

### URL to PDF

```csharp
// Convert any website to PDF
var pdf = await converter.RenderUrlAsync("https://example.com");
await File.WriteAllBytesAsync("website.pdf", pdf);
```

### Watermark a PDF

```csharp
var originalPdf = await converter.RenderAsync("<h1>Confidential Report</h1>");

var watermarked = await converter.AddWatermarkAsync(
    originalPdf,
    "CONFIDENTIAL",
    options =>
    {
        options.Opacity = 0.3f;
        options.FontSize = 72;
        options.Rotation = 45;
        options.Color = "#FF0000";
    });
```

### Merge Multiple PDFs

```csharp
var pdf1 = await converter.RenderAsync("<h1>Document 1</h1>");
var pdf2 = await converter.RenderAsync("<h1>Document 2</h1>");
var pdf3 = await converter.RenderAsync("<h1>Document 3</h1>");

var merged = await converter.MergeAsync(new[] { pdf1, pdf2, pdf3 });
await File.WriteAllBytesAsync("merged.pdf", merged);
```

## ASP.NET Core Integration

### 1. Install Package

```bash
dotnet add package HtmlToPdfCore
```

### 2. Register Services

In `Program.cs`:

```csharp
using HtmlToPdfCore.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add HtmlToPdfCore services
builder.Services.AddHtmlToPdfCore(options =>
{
    options.EnableAIOptimization = true;
    options.MaxConcurrentRenders = Environment.ProcessorCount;
});

builder.Services.AddControllers();
var app = builder.Build();

app.MapControllers();
app.Run();
```

### 3. Use in Controller

```csharp
using HtmlToPdfCore.Interfaces;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/[controller]")]
public class PdfController : ControllerBase
{
    private readonly IHtmlToPdfRenderer _renderer;
    
    public PdfController(IHtmlToPdfRenderer renderer)
    {
        _renderer = renderer;
    }
    
    [HttpPost("generate")]
    public async Task<IActionResult> GeneratePdf([FromBody] PdfRequest request)
    {
        var pdf = await _renderer.RenderHtmlToPdfAsync(request.Html);
        return File(pdf, "application/pdf", "document.pdf");
    }
    
    [HttpGet("invoice/{id}")]
    public async Task<IActionResult> GenerateInvoice(int id)
    {
        // Get invoice data from database
        var invoice = await GetInvoiceData(id);
        
        // Generate HTML
        var html = GenerateInvoiceHtml(invoice);
        
        // Convert to PDF
        var pdf = await _renderer.RenderHtmlToPdfAsync(html);
        
        return File(pdf, "application/pdf", $"invoice-{id}.pdf");
    }
}

public class PdfRequest
{
    public string Html { get; set; } = string.Empty;
}
```

## Configuration Options

### All Available Options

```csharp
var pdf = await converter.RenderAsync(html, options =>
{
    // Page Settings
    options.PageSize = PageSize.A4;
    options.Orientation = PageOrientation.Portrait;
    options.Margins = new PageMargins(20, 20, 20, 20);
    
    // Quality Settings
    options.Dpi = 300;
    options.ImageQuality = 90;
    options.CompressImages = true;
    
    // Rendering Options
    options.EnableJavaScript = true;
    options.PrintBackground = true;
    options.Grayscale = false;
    options.MediaType = "print";
    
    // Custom Styling
    options.CustomCss = "body { font-family: Arial; }";
    options.BaseUrl = "https://example.com";
    
    // Header/Footer
    options.Header = new HeaderFooterOptions
    {
        HtmlContent = "<div>Header Content</div>",
        Height = 30
    };
    
    options.Footer = new HeaderFooterOptions
    {
        ShowPageNumbers = true,
        PageNumberFormat = "Page {page} of {total}"
    };
    
    // Metadata
    options.Metadata = new PdfMetadata
    {
        Title = "My Document",
        Author = "John Doe",
        Subject = "Report",
        Keywords = "pdf, report",
        Creator = "My Application"
    };
    
    // Performance
    options.EnableAIOptimization = true;
    options.RenderTimeout = 30000;
});
```

## Fluent API Examples

The fluent API provides a clean, chainable syntax:

```csharp
// Basic example
await converter
    .FromHtml(html)
    .WithPageSize(PageSize.Letter)
    .WithOrientation(PageOrientation.Landscape)
    .SaveAsync("output.pdf");

// Complex example
await converter
    .FromHtml(html)
    .WithPageSize(PageSize.A4)
    .WithOrientation(PageOrientation.Portrait)
    .WithMargins(25, 25, 25, 25)
    .WithHeader("<div style='text-align:center;'>Company Name</div>")
    .WithFooter("", showPageNumbers: true)
    .WithMetadata(m =>
    {
        m.Title = "Report";
        m.Author = "Jane Doe";
    })
    .EnableJavaScript(true)
    .WithCustomCss("body { font-family: Arial; }")
    .WithDpi(300)
    .WithImageQuality(95)
    .SaveAsync("report.pdf");

// From file
await converter
    .FromFile("template.html")
    .WithPageSize(PageSize.Letter)
    .SaveAsync("output.pdf");

// From URL
await converter
    .FromUrl("https://example.com")
    .WithPageSize(PageSize.A4)
    .SaveAsync("website.pdf");
```

## Error Handling

Always wrap PDF operations in try-catch:

```csharp
using HtmlToPdfCore.Renderers;

try
{
    var pdf = await converter.RenderAsync(html);
    await File.WriteAllBytesAsync("output.pdf", pdf);
    Console.WriteLine("Success!");
}
catch (PdfRenderException ex)
{
    Console.WriteLine($"Rendering failed: {ex.Message}");
    
    if (ex.InnerException != null)
    {
        Console.WriteLine($"Details: {ex.InnerException.Message}");
    }
}
catch (OperationCanceledException)
{
    Console.WriteLine("Operation was cancelled");
}
catch (Exception ex)
{
    Console.WriteLine($"Unexpected error: {ex.Message}");
}
```

## Performance Tips

1. **Reuse the converter instance**:
   ```csharp
   // Good - reuse
   var converter = HtmlToPdf.Create();
   for (int i = 0; i < 100; i++)
   {
       await converter.RenderAsync(html);
   }
   
   // Bad - creates new instance each time
   for (int i = 0; i < 100; i++)
   {
       var converter = HtmlToPdf.Create();
       await converter.RenderAsync(html);
   }
   ```

2. **Enable AI optimization** (enabled by default):
   ```csharp
   options.EnableAIOptimization = true;
   ```

3. **Use appropriate image quality**:
   ```csharp
   options.ImageQuality = 85; // Balance quality and size
   options.CompressImages = true;
   ```

4. **Set reasonable timeouts**:
   ```csharp
   options.RenderTimeout = 30000; // 30 seconds
   ```

## Memory Management

The library automatically manages memory, but you can help:

```csharp
// Use 'using' statement for automatic disposal
using var converter = HtmlToPdf.Create();
await converter.RenderAsync(html);
// Automatically disposed here

// Or manually dispose
var converter = HtmlToPdf.Create();
try
{
    await converter.RenderAsync(html);
}
finally
{
    converter.Dispose();
}
```

## Troubleshooting

### Common Issues

**Issue**: PDF is blank
- **Solution**: Ensure HTML is valid and complete
- Check for JavaScript errors if enabled
- Verify CSS is correctly applied

**Issue**: Images not showing
- **Solution**: Use absolute URLs for images
- Or set `BaseUrl` option:
  ```csharp
  options.BaseUrl = "https://yoursite.com";
  ```

**Issue**: Fonts look wrong
- **Solution**: Specify fonts in CSS:
  ```csharp
  options.CustomCss = @"
      body { font-family: 'Arial', 'Helvetica', sans-serif; }
  ";
  ```

**Issue**: Out of memory
- **Solution**: Enable AI optimization
- Process documents in batches
- Reduce image quality

## Next Steps

- Read the full [README.md](README.md) for all features
- Check [Examples](Examples/) for more code samples
- Review [BUILD.md](BUILD.md) for building from source
- See [CHANGELOG.md](CHANGELOG.md) for version history

## Support

- 📖 [Documentation](README.md)
- 💬 [GitHub Issues](https://github.com/yourusername/HtmlToPdfCore/issues)
- 📧 Email: support@example.com

## License

MIT License - feel free to use in commercial projects!
