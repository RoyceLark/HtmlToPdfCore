using HtmlToPdfCore.Interfaces;
using HtmlToPdfCore.Models;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas;
using iText.Kernel.Pdf.Extgstate;
using iText.Kernel.Colors;
using iText.Kernel.Font;
using iText.Layout;
using iText.Layout.Element;
using iText.Layout.Properties;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Listener;
using Microsoft.Extensions.Logging;
using System.Text;

namespace HtmlToPdfCore.Services;

/// <summary>
/// PDF manipulation service for merging, splitting, watermarking, and encryption
/// </summary>
public class PdfManipulator : IPdfManipulator
{
    private readonly ILogger<PdfManipulator> _logger;

    public PdfManipulator(ILogger<PdfManipulator> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<byte[]> MergePdfsAsync(
        IEnumerable<byte[]> pdfs,
        CancellationToken cancellationToken = default)
    {
        if (pdfs == null || !pdfs.Any())
            throw new ArgumentException("PDF collection cannot be null or empty", nameof(pdfs));

        return await Task.Run(() =>
        {
            try
            {
                using var outputStream = new MemoryStream();
                using var mergedDoc = new PdfDocument(new PdfWriter(outputStream));

                foreach (var pdfBytes in pdfs)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    using var inputStream = new MemoryStream(pdfBytes);
                    using var sourceDoc = new PdfDocument(new PdfReader(inputStream));

                    var pageCount = sourceDoc.GetNumberOfPages();
                    sourceDoc.CopyPagesTo(1, pageCount, mergedDoc);
                }

                mergedDoc.Close();
                
                var result = outputStream.ToArray();
                _logger.LogInformation("Successfully merged {Count} PDFs. Result size: {Size} bytes",
                    pdfs.Count(), result.Length);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error merging PDFs");
                throw new PdfManipulationException("Failed to merge PDFs", ex);
            }
        }, cancellationToken);
    }

    public async Task<IEnumerable<byte[]>> SplitPdfAsync(
        byte[] pdf,
        int[] pageNumbers,
        CancellationToken cancellationToken = default)
    {
        if (pdf == null || pdf.Length == 0)
            throw new ArgumentException("PDF cannot be null or empty", nameof(pdf));

        if (pageNumbers == null || pageNumbers.Length == 0)
            throw new ArgumentException("Page numbers cannot be null or empty", nameof(pageNumbers));

        return await Task.Run(() =>
        {
            try
            {
                var results = new List<byte[]>();

                using var inputStream = new MemoryStream(pdf);
                using var sourceDoc = new PdfDocument(new PdfReader(inputStream));

                var totalPages = sourceDoc.GetNumberOfPages();

                foreach (var pageNumber in pageNumbers)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (pageNumber < 1 || pageNumber > totalPages)
                    {
                        _logger.LogWarning("Page number {PageNumber} is out of range (1-{TotalPages})",
                            pageNumber, totalPages);
                        continue;
                    }

                    using var outputStream = new MemoryStream();
                    using var splitDoc = new PdfDocument(new PdfWriter(outputStream));

                    sourceDoc.CopyPagesTo(pageNumber, pageNumber, splitDoc);
                    splitDoc.Close();

                    results.Add(outputStream.ToArray());
                }

                _logger.LogInformation("Successfully split PDF into {Count} documents", results.Count);
                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error splitting PDF");
                throw new PdfManipulationException("Failed to split PDF", ex);
            }
        }, cancellationToken);
    }

    public async Task<byte[]> AddWatermarkAsync(
        byte[] pdf,
        WatermarkOptions options,
        CancellationToken cancellationToken = default)
    {
        if (pdf == null || pdf.Length == 0)
            throw new ArgumentException("PDF cannot be null or empty", nameof(pdf));

        if (options == null)
            throw new ArgumentNullException(nameof(options));

        if (string.IsNullOrWhiteSpace(options.Text))
            throw new ArgumentException("Watermark text cannot be null or empty", nameof(options));

        return await Task.Run(() =>
        {
            try
            {
                using var inputStream = new MemoryStream(pdf);
                using var outputStream = new MemoryStream();

                using var pdfDoc = new PdfDocument(new PdfReader(inputStream), new PdfWriter(outputStream));
                var font = PdfFontFactory.CreateFont(iText.IO.Font.Constants.StandardFonts.HELVETICA_BOLD);

                var pageCount = pdfDoc.GetNumberOfPages();

                for (int i = 1; i <= pageCount; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var page = pdfDoc.GetPage(i);
                    var pageSize = page.GetPageSize();
                    var canvas = new PdfCanvas(page.NewContentStreamBefore(), page.GetResources(), pdfDoc);

                    // Set transparency
                    var gs = new PdfExtGState();
                    gs.SetFillOpacity(options.Opacity);
                    canvas.SetExtGState(gs);

                    // Parse color
                    var color = ParseColor(options.Color ?? "#808080");
                    canvas.SetColor(color, true);

                    // Calculate position
                    var (x, y) = CalculateWatermarkPosition(
                        pageSize.GetWidth(),
                        pageSize.GetHeight(),
                        options.Position);

                    // Save state and apply transformations
                    canvas.SaveState();
                    canvas.ConcatMatrix(
                        Math.Cos(options.Rotation * Math.PI / 180),
                        Math.Sin(options.Rotation * Math.PI / 180),
                        -Math.Sin(options.Rotation * Math.PI / 180),
                        Math.Cos(options.Rotation * Math.PI / 180),
                        x, y);

                    // Draw watermark text
                    canvas.BeginText()
                        .SetFontAndSize(font, options.FontSize)
                        .ShowText(options.Text)
                        .EndText();

                    canvas.RestoreState();
                }

                pdfDoc.Close();

                var result = outputStream.ToArray();
                _logger.LogInformation("Successfully added watermark to PDF. Size: {Size} bytes", result.Length);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding watermark to PDF");
                throw new PdfManipulationException("Failed to add watermark to PDF", ex);
            }
        }, cancellationToken);
    }

    public async Task<byte[]> EncryptPdfAsync(
        byte[] pdf,
        string userPassword,
        string? ownerPassword = null,
        CancellationToken cancellationToken = default)
    {
        if (pdf == null || pdf.Length == 0)
            throw new ArgumentException("PDF cannot be null or empty", nameof(pdf));

        if (string.IsNullOrWhiteSpace(userPassword))
            throw new ArgumentException("User password cannot be null or empty", nameof(userPassword));

        return await Task.Run(() =>
        {
            try
            {
                using var inputStream = new MemoryStream(pdf);
                using var outputStream = new MemoryStream();

                var owner = string.IsNullOrWhiteSpace(ownerPassword) ? userPassword : ownerPassword;

                var writerProperties = new WriterProperties()
                    .SetStandardEncryption(
                        Encoding.UTF8.GetBytes(userPassword),
                        Encoding.UTF8.GetBytes(owner),
                        EncryptionConstants.ALLOW_PRINTING | EncryptionConstants.ALLOW_COPY,
                        EncryptionConstants.ENCRYPTION_AES_256);

                using var pdfDoc = new PdfDocument(
                    new PdfReader(inputStream),
                    new PdfWriter(outputStream, writerProperties));

                pdfDoc.Close();

                var result = outputStream.ToArray();
                _logger.LogInformation("Successfully encrypted PDF. Size: {Size} bytes", result.Length);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error encrypting PDF");
                throw new PdfManipulationException("Failed to encrypt PDF", ex);
            }
        }, cancellationToken);
    }

    public async Task<string> ExtractTextAsync(
        byte[] pdf,
        CancellationToken cancellationToken = default)
    {
        if (pdf == null || pdf.Length == 0)
            throw new ArgumentException("PDF cannot be null or empty", nameof(pdf));

        return await Task.Run(() =>
        {
            try
            {
                using var inputStream = new MemoryStream(pdf);
                using var pdfDoc = new PdfDocument(new PdfReader(inputStream));

                var text = new StringBuilder();
                var pageCount = pdfDoc.GetNumberOfPages();

                for (int i = 1; i <= pageCount; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var page = pdfDoc.GetPage(i);
                    var strategy = new LocationTextExtractionStrategy();
                    var pageText = PdfTextExtractor.GetTextFromPage(page, strategy);

                    text.AppendLine(pageText);
                    text.AppendLine(); // Add separator between pages
                }

                var result = text.ToString();
                _logger.LogInformation("Successfully extracted text from PDF. Length: {Length} characters", result.Length);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting text from PDF");
                throw new PdfManipulationException("Failed to extract text from PDF", ex);
            }
        }, cancellationToken);
    }

    private Color ParseColor(string colorString)
    {
        try
        {
            if (colorString.StartsWith("#"))
            {
                colorString = colorString.Substring(1);
                
                if (colorString.Length == 6)
                {
                    var r = Convert.ToInt32(colorString.Substring(0, 2), 16);
                    var g = Convert.ToInt32(colorString.Substring(2, 2), 16);
                    var b = Convert.ToInt32(colorString.Substring(4, 2), 16);
                    
                    return new DeviceRgb(r, g, b);
                }
            }

            // Default to gray
            return new DeviceRgb(128, 128, 128);
        }
        catch
        {
            return new DeviceRgb(128, 128, 128);
        }
    }

    private (float x, float y) CalculateWatermarkPosition(
        float pageWidth,
        float pageHeight,
        WatermarkPosition position)
    {
        return position switch
        {
            WatermarkPosition.TopLeft => (50, pageHeight - 50),
            WatermarkPosition.TopCenter => (pageWidth / 2, pageHeight - 50),
            WatermarkPosition.TopRight => (pageWidth - 50, pageHeight - 50),
            WatermarkPosition.BottomLeft => (50, 50),
            WatermarkPosition.BottomCenter => (pageWidth / 2, 50),
            WatermarkPosition.BottomRight => (pageWidth - 50, 50),
            _ => (pageWidth / 2, pageHeight / 2) // Center
        };
    }
}

/// <summary>
/// Custom exception for PDF manipulation errors
/// </summary>
public class PdfManipulationException : Exception
{
    public PdfManipulationException(string message) : base(message) { }
    public PdfManipulationException(string message, Exception innerException) : base(message, innerException) { }
}
