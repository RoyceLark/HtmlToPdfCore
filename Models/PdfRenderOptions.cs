using System.Drawing;

namespace HtmlToPdfCore.Models;

/// <summary>
/// Comprehensive PDF rendering options
/// </summary>
public class PdfRenderOptions
{
    /// <summary>
    /// Page size settings
    /// </summary>
    public PageSize PageSize { get; set; } = PageSize.A4;

    /// <summary>
    /// Alias for PageSize
    /// </summary>
    public PageSize PaperSize 
    { 
        get => PageSize; 
        set => PageSize = value; 
    }

    /// <summary>
    /// Page orientation
    /// </summary>
    public PageOrientation Orientation { get; set; } = PageOrientation.Portrait;

    /// <summary>
    /// Alias for Orientation
    /// </summary>
    public PageOrientation PaperOrientation 
    { 
        get => Orientation; 
        set => Orientation = value; 
    }

    /// <summary>
    /// Page margins in millimeters
    /// </summary>
    private PageMargins _margins = new PageMargins(25, 25, 25, 25);
    public PageMargins Margins 
    { 
        get => _margins; 
        set => _margins = value ?? new PageMargins(); 
    }

    // Direct margin properties compatibility
    public double MarginTop { get => Margins.Top; set => Margins.Top = (float)value; }
    public double MarginBottom { get => Margins.Bottom; set => Margins.Bottom = (float)value; }
    public double MarginLeft { get => Margins.Left; set => Margins.Left = (float)value; }
    public double MarginRight { get => Margins.Right; set => Margins.Right = (float)value; }

    /// <summary>
    /// Enable JavaScript execution
    /// </summary>
    public bool EnableJavaScript { get; set; } = true;

    /// <summary>
    /// Timeout for rendering in milliseconds
    /// </summary>
    public int RenderTimeout { get; set; } = 30000;

    /// <summary>
    /// Alias for RenderTimeout in seconds 
    /// </summary>
    public int Timeout 
    { 
        get => RenderTimeout / 1000; 
        set => RenderTimeout = value * 1000; 
    }

    /// <summary>
    /// Delay before rendering in milliseconds to allow JS execution
    /// </summary>
    public int RenderDelay { get; set; } = 0;

    /// <summary>
    /// CSS media type
    /// </summary>
    public CssMediaType MediaType { get; set; } = CssMediaType.Print;

    /// <summary>
    /// Custom CSS to inject
    /// </summary>
    public string? CustomCss { get; set; }

    /// <summary>
    /// Base URL for relative paths
    /// </summary>
    public string? BaseUrl { get; set; }

    /// <summary>
    /// Header configuration
    /// </summary>
    public HeaderFooterOptions? Header { get; set; }

    /// <summary>
    /// Alias for Header 
    /// </summary>
    public HtmlHeaderFooter? HtmlHeader 
    { 
        get => Header as HtmlHeaderFooter ?? (Header != null ? new HtmlHeaderFooter { HtmlContent = Header.HtmlContent, Height = Header.Height, ShowPageNumbers = Header.ShowPageNumbers } : null); 
        set => Header = value; 
    }

    /// <summary>
    /// Footer configuration
    /// </summary>
    public HeaderFooterOptions? Footer { get; set; }

    /// <summary>
    /// Alias for Footer 
    /// </summary>
    public HtmlHeaderFooter? HtmlFooter 
    { 
        get => Footer as HtmlHeaderFooter ?? (Footer != null ? new HtmlHeaderFooter { HtmlContent = Footer.HtmlContent, Height = Footer.Height, ShowPageNumbers = Footer.ShowPageNumbers } : null); 
        set => Footer = value; 
    }

    /// <summary>
    /// Alias for Header as TextHeader
    /// </summary>
    public TextHeaderFooter? TextHeader 
    { 
        get => Header as TextHeaderFooter ?? (Header != null ? new TextHeaderFooter { LeftText = (Header as TextHeaderFooter)?.LeftText, CenterText = (Header as TextHeaderFooter)?.CenterText, RightText = (Header as TextHeaderFooter)?.RightText, Height = Header.Height, ShowPageNumbers = Header.ShowPageNumbers } : null); 
        set => Header = value; 
    }

    /// <summary>
    /// Alias for Footer as TextFooter
    /// </summary>
    public TextHeaderFooter? TextFooter 
    { 
        get => Footer as TextHeaderFooter ?? (Footer != null ? new TextHeaderFooter { LeftText = (Footer as TextHeaderFooter)?.LeftText, CenterText = (Footer as TextHeaderFooter)?.CenterText, RightText = (Footer as TextHeaderFooter)?.RightText, Height = Footer.Height, ShowPageNumbers = Footer.ShowPageNumbers } : null); 
        set => Footer = value; 
    }

    /// <summary>
    /// Print background graphics
    /// </summary>
    public bool PrintBackground { get; set; } = true;

    /// <summary>
    /// Alias for PrintBackground 
    /// </summary>
    public bool PrintHtmlBackgrounds 
    { 
        get => PrintBackground; 
        set => PrintBackground = value; 
    }

    /// <summary>
    /// Enable image compression
    /// </summary>
    public bool CompressImages { get; set; } = true;

    /// <summary>
    /// Image quality (0-100)
    /// </summary>
    public int ImageQuality { get; set; } = 85;

    /// <summary>
    /// Enable AI-powered memory optimization
    /// </summary>
    public bool EnableAIOptimization { get; set; } = true;

    /// <summary>
    /// DPI for rendering
    /// </summary>
    public int Dpi { get; set; } = 96;

    /// <summary>
    /// Starting page number
    /// </summary>
    public int FirstPageNumber { get; set; } = 1;

    /// <summary>
    /// Custom JavaScript to execute (Note: Limited support in current engine)
    /// </summary>
    public string? Javascript { get; set; }

    /// <summary>
    /// URL to a custom CSS file
    /// </summary>
    public string? CustomCssUrl { get; set; }

    /// <summary>
    /// Enable generation of Table of Contents
    /// </summary>
    public bool TableOfContents { get; set; } = false;

    /// <summary>
    /// Alias for TableOfContents
    /// </summary>
    public bool GenerateTableOfContents 
    { 
        get => TableOfContents; 
        set => TableOfContents = value; 
    }

    /// <summary>
    /// Apply document margins to headers and footers
    /// </summary>
    public bool UseMarginsOnHeaderAndFooter { get; set; } = true;

    /// <summary>
    /// Alias for UseMarginsOnHeaderAndFooter
    /// </summary>
    public bool ApplyMarginToHeaderAndFooter 
    { 
        get => UseMarginsOnHeaderAndFooter; 
        set => UseMarginsOnHeaderAndFooter = value; 
    }

    /// <summary>
    /// Font scale factor (1.0 = 100%, 0.8 = 80%, etc)
    /// </summary>
    public float FontScale { get; set; } = 1.0f;

    /// <summary>
    /// Full content scale factor (0.1 to 10.0)
    /// </summary>
    public double Scale { get; set; } = 1.0;

    /// <summary>
    /// Zoom level (100 = 100%)
    /// </summary>
    public int Zoom { get; set; } = 100;

    /// <summary>
    /// Enable images in PDF
    /// </summary>
    public bool EnableImages { get; set; } = true;

    /// <summary>
    /// Fit content to paper width
    /// </summary>
    public bool FitToPaperWidth { get; set; } = false;

    /// <summary>
    /// Force paper size to exact dimensions
    /// </summary>
    public bool ForcePaperSize { get; set; } = false;

    /// <summary>
    /// Override settings with CSS @page rules
    /// </summary>
    public bool OverrideWithCssProperties { get; set; } = false;

    /// <summary>
    /// Enable grayscale rendering
    /// </summary>
    public bool Grayscale { get; set; } = false;

    /// <summary>
    /// Alias for Grayscale
    /// </summary>
    public bool GrayScale 
    { 
        get => Grayscale; 
        set => Grayscale = value; 
    }

    /// <summary>
    /// PDF metadata
    /// </summary>
    public PdfMetadata Metadata { get; set; } = new PdfMetadata();

    // Metadata aliases for top-level access
    public string? Title { get => Metadata.Title; set => Metadata.Title = value; }
    public string? Author { get => Metadata.Author; set => Metadata.Author = value; }
    public string? Subject { get => Metadata.Subject; set => Metadata.Subject = value; }
    public string? Keywords { get => Metadata.Keywords; set => Metadata.Keywords = value; }

    /// <summary>
    /// Wait for various conditions before rendering 
    /// Currently supports a direct delay mapping.
    /// </summary>
    public int WaitFor { get => RenderDelay; set => RenderDelay = value; }

    /// <summary>
    /// Convert HTML form fields to interactive PDF (AcroForm) fields
    /// </summary>
    public bool CreatePdfFormsFromHtml { get; set; } = false;

    /// <summary>
    /// Watermark configuration
    /// </summary>
    public WatermarkOptions? Watermark { get; set; }

    /// <summary>
    /// Security and encryption settings
    /// </summary>
    public SecurityOptions? Security { get; set; }

    /// <summary>
    /// Explicit ViewPortWidth to match fit mode logic
    /// </summary>
    public int? ViewPortWidth { get; set; }

    /// <summary>
    /// Explicit ViewPortHeight
    /// </summary>
    public int? ViewPortHeight { get; set; }

    /// <summary>
    /// Unit used for measurements (default: Millimeters)
    /// </summary>
    public MeasurementUnit MarginUnit { get; set; } = MeasurementUnit.Millimeters;

    /// <summary>
    /// Alias for MarginUnit
    /// </summary>
    public MeasurementUnit MeasurementUnit { get => MarginUnit; set => MarginUnit = value; }

    /// <summary>
    /// Enable Cookies
    /// </summary>
    public bool EnableCookies { get; set; } = true;

    /// <summary>
    /// Proxy configuration string
    /// </summary>
    public string? Proxy { get; set; }

    /// <summary>
    /// Custom HTTP headers for the request
    /// </summary>
    public System.Collections.Generic.Dictionary<string, string> CustomHttpHeaders { get; set; } = new();

    /// <summary>
    /// Custom cookies for the request
    /// </summary>
    public System.Collections.Generic.Dictionary<string, string> CustomCookies { get; set; } = new();

    /// <summary>
    /// Input text encoding (default: UTF-8)
    /// </summary>
    public string InputEncoding { get; set; } = "UTF-8";

    /// <summary>
    /// Alias for MediaType 
    /// </summary>
    public CssMediaType CssMedia { get => MediaType; set => MediaType = value; }


    /// <summary>
    /// Custom page width in millimeters (used if PageSize is Custom)
    /// </summary>
    public float? CustomPageWidth { get; set; }

    /// <summary>
    /// Alias for CustomPageWidth
    /// </summary>
    public double PaperWidth { get => CustomPageWidth ?? 0; set => CustomPageWidth = (float)value; }

    /// <summary>
    /// Custom page height in millimeters (used if PageSize is Custom)
    /// </summary>
    public float? CustomPageHeight { get; set; }

    /// <summary>
    /// Alias for CustomPageHeight
    /// </summary>
    public double PaperHeight { get => CustomPageHeight ?? 0; set => CustomPageHeight = (float)value; }

    /// <summary>
    /// Sets a custom paper size in millimeters
    /// </summary>
    public void SetCustomPaperSize(float width, float height)
    {
        PageSize = PageSize.Custom;
        CustomPageWidth = width;
        CustomPageHeight = height;
    }
}

/// <summary>
/// Alias for compatibility
/// </summary>
public class HtmlHeaderFooter : HeaderFooterOptions 
{ 
    public string? Html { get => HtmlContent; set => HtmlContent = value; }
    public string? LoadStyleSheet { get; set; }
}

/// <summary>
/// Alias for compatibility
/// </summary>
public class TextHeaderFooter : HeaderFooterOptions
{
    public string? LeftText { get; set; }
    public string? CenterText { get; set; }
    public string? RightText { get; set; }
}

/// <summary>
/// Alias for compatibility
/// </summary>
public class MLPdfRenderOptions : PdfRenderOptions { }

/// <summary>
/// Page size enumeration
/// </summary>
public enum PageSize
{
    A0, A1, A2, A3, A4, A5, A6, A7, A8, A9,
    B0, B1, B2, B3, B4, B5, B6, B7, B8, B9, B10,
    Letter, Legal, Tabloid, Ledger, Personal, 
    Executive, Folio, Statement,
    Custom
}

/// <summary>
/// Page orientation
/// </summary>
public enum PageOrientation
{
    Portrait,
    Landscape
}

/// <summary>
/// CSS media type for PDF rendering
/// </summary>
public enum CssMediaType
{
    Print,
    Screen
}

/// <summary>
/// Units for measurements
/// </summary>
public enum MeasurementUnit
{
    Millimeters,
    Inches,
    Pixels,
    Points
}

/// <summary>
/// Page margins
/// </summary>
public class PageMargins
{
    public float Top { get; set; }
    public float Right { get; set; }
    public float Bottom { get; set; }
    public float Left { get; set; }

    public PageMargins(float top = 25, float right = 25, float bottom = 25, float left = 25)
    {
        Top = top;
        Right = right;
        Bottom = bottom;
        Left = left;
    }
}

/// <summary>
/// Header and footer options
/// </summary>
public class HeaderFooterOptions
{
    public string? HtmlContent { get; set; }
    public string? HtmlFragment { get => HtmlContent; set => HtmlContent = value; }
    public float Height { get; set; } = 25;
    public bool ShowPageNumbers { get; set; } = false;
    public string? PageNumberFormat { get; set; } = "Page {page} of {total}";
    public TextAlignment Alignment { get; set; } = TextAlignment.Center;
    
    // Parity properties
    public bool DividerLine { get; set; } = false;
    public bool DrawDividerLine { get => DividerLine; set => DividerLine = value; }
    public string? DividerColor { get; set; } = "#000000";
    public string? DividerLineColor { get => DividerColor; set => DividerColor = value; }
    public string? FontFamily { get; set; }
    public float? FontSize { get; set; }
    public string? Color { get; set; }
    public string? TextColor { get => Color; set => Color = value; }
}

/// <summary>
/// Text alignment
/// </summary>
public enum TextAlignment
{
    Left,
    Center,
    Right
}

/// <summary>
/// PDF metadata
/// </summary>
public class PdfMetadata
{
    public string? Title { get; set; }
    public string? Author { get; set; }
    public string? Subject { get; set; }
    public string? Keywords { get; set; }
    public string? Creator { get; set; } = "HtmlToPdfCore";
    public DateTime? CreationDate { get; set; }
}

/// <summary>
/// Watermark options
/// </summary>
public class WatermarkOptions
{
    public string Text { get; set; } = string.Empty;
    public float Opacity { get; set; } = 0.3f;
    public int FontSize { get; set; } = 48;
    public string FontFamily { get; set; } = "Arial";
    public float Rotation { get; set; } = 45;
    public WatermarkPosition Position { get; set; } = WatermarkPosition.Center;
    public string? Color { get; set; } = "#808080";
}

/// <summary>
/// Watermark position
/// </summary>
public enum WatermarkPosition
{
    Center,
    TopLeft,
    TopCenter,
    TopRight,
    BottomLeft,
    BottomCenter,
    BottomRight
}

/// <summary>
/// Memory prediction result
/// </summary>
public class MemoryPrediction
{
    public long EstimatedMemoryMB { get; set; }
    public int RecommendedPoolSize { get; set; }
    public bool ShouldUseStreaming { get; set; }
    public float ConfidenceScore { get; set; }
}

/// <summary>
/// Optimization strategy
/// </summary>
public class OptimizationStrategy
{
    public bool UseParallelProcessing { get; set; }
    public int OptimalChunkSize { get; set; }
    public bool EnableCaching { get; set; }
    public CompressionLevel CompressionLevel { get; set; }
}

/// <summary>
/// Security and encryption options
/// </summary>
public class SecurityOptions
{
    public string? UserPassword { get; set; }
    public string? OwnerPassword { get; set; }
    public bool AllowPrinting { get; set; } = true;
    public bool AllowCopyContent { get; set; } = true;
    public bool AllowEditContent { get; set; } = true;
    public bool AllowEditAnnotations { get; set; } = true;
}

/// <summary>
/// Compression level
/// </summary>
public enum CompressionLevel
{
    None,
    Low,
    Medium,
    High,
    Maximum
}