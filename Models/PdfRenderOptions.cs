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
    /// Page orientation
    /// </summary>
    public PageOrientation Orientation { get; set; } = PageOrientation.Portrait;

    /// <summary>
    /// Page margins in millimeters
    /// </summary>
    public PageMargins Margins { get; set; } = new PageMargins(10, 10, 10, 10);

    /// <summary>
    /// Enable JavaScript execution
    /// </summary>
    public bool EnableJavaScript { get; set; } = true;

    /// <summary>
    /// Timeout for rendering in milliseconds
    /// </summary>
    public int RenderTimeout { get; set; } = 30000;

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
    /// Footer configuration
    /// </summary>
    public HeaderFooterOptions? Footer { get; set; }

    /// <summary>
    /// Print background graphics
    /// </summary>
    public bool PrintBackground { get; set; } = true;

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
    /// Font scale factor (1.0 = 100%, 0.8 = 80%, etc)
    /// </summary>
    public float FontScale { get; set; } = 1.0f;

    /// <summary>
    /// Fit content to paper width
    /// </summary>
    public bool FitToPaperWidth { get; set; } = true;

    /// <summary>
    /// Enable grayscale rendering
    /// </summary>
    public bool Grayscale { get; set; } = false;

    /// <summary>
    /// PDF metadata
    /// </summary>
    public PdfMetadata Metadata { get; set; } = new PdfMetadata();
}

/// <summary>
/// Page size enumeration
/// </summary>
public enum PageSize
{
    A0, A1, A2, A3, A4, A5, A6,
    Letter, Legal, Tabloid, Ledger,
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
/// Page margins
/// </summary>
public class PageMargins
{
    public float Top { get; set; }
    public float Right { get; set; }
    public float Bottom { get; set; }
    public float Left { get; set; }

    public PageMargins(float top, float right, float bottom, float left)
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
    public float Height { get; set; } = 20;
    public bool ShowPageNumbers { get; set; } = false;
    public string? PageNumberFormat { get; set; } = "Page {page} of {total}";
    public TextAlignment Alignment { get; set; } = TextAlignment.Center;
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