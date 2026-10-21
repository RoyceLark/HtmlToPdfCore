using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using HtmlToPdfCore.Models;
using HtmlToPdfCore.Renderers;
using iText.Kernel.Pdf;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HtmlToPdfCore.Tests.Renderers;

public class HtmlToPdfRendererTests : IDisposable
{
    private readonly HtmlToPdfRenderer _renderer;

    public HtmlToPdfRendererTests()
    {
        _renderer = new HtmlToPdfRenderer(NullLogger<HtmlToPdfRenderer>.Instance);
    }

    public void Dispose() => _renderer.Dispose();

    private static (int PageCount, float Width, float Height) ReadPdf(byte[] pdfBytes)
    {
        using var ms = new MemoryStream(pdfBytes);
        using var reader = new PdfReader(ms);
        using var doc = new iText.Kernel.Pdf.PdfDocument(reader);
        var size = doc.GetPage(1).GetPageSize();
        return (doc.GetNumberOfPages(), size.GetWidth(), size.GetHeight());
    }

    [Fact]
    public async Task RenderHtmlToPdfAsync_NullHtml_ThrowsArgumentException()
    {
        // Arrange / Act
        Func<Task> act = () => _renderer.RenderHtmlToPdfAsync(null!);

        // Assert
        await Assert.ThrowsAsync<ArgumentException>(act);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RenderHtmlToPdfAsync_EmptyOrWhitespaceHtml_ThrowsArgumentException(string html)
    {
        // Arrange / Act
        Func<Task> act = () => _renderer.RenderHtmlToPdfAsync(html);

        // Assert
        await Assert.ThrowsAsync<ArgumentException>(act);
    }

    [Fact]
    public async Task RenderHtmlToPdfAsync_ValidHtml_ReturnsNonEmptyPdfWithSinglePage()
    {
        // Arrange
        var html = "<html><body><h1>Hello World</h1></body></html>";

        // Act
        var result = await _renderer.RenderHtmlToPdfAsync(html);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Length > 0);
        var (pageCount, _, _) = ReadPdf(result);
        Assert.Equal(1, pageCount);
    }

    [Fact]
    public async Task RenderHtmlToPdfAsync_DefaultOptions_ProducesA4PortraitPage()
    {
        // Arrange
        var html = "<html><body>Content</body></html>";
        var a4 = iText.Kernel.Geom.PageSize.A4;

        // Act
        var result = await _renderer.RenderHtmlToPdfAsync(html);

        // Assert
        var (_, width, height) = ReadPdf(result);
        Assert.Equal(a4.GetWidth(), width, 0);
        Assert.Equal(a4.GetHeight(), height, 0);
    }

    [Fact]
    public async Task RenderHtmlToPdfAsync_LandscapeOrientation_SwapsWidthAndHeight()
    {
        // Arrange
        var html = "<html><body>Content</body></html>";
        var options = new PdfRenderOptions { Orientation = PageOrientation.Landscape };
        var a4 = iText.Kernel.Geom.PageSize.A4;

        // Act
        var result = await _renderer.RenderHtmlToPdfAsync(html, options);

        // Assert
        var (_, width, height) = ReadPdf(result);
        Assert.Equal(a4.GetHeight(), width, 0);
        Assert.Equal(a4.GetWidth(), height, 0);
    }

    [Fact]
    public async Task RenderHtmlToPdfAsync_LetterPageSize_ProducesLetterDimensions()
    {
        // Arrange
        var html = "<html><body>Content</body></html>";
        var options = new PdfRenderOptions { PageSize = Models.PageSize.Letter };
        var letter = iText.Kernel.Geom.PageSize.LETTER;

        // Act
        var result = await _renderer.RenderHtmlToPdfAsync(html, options);

        // Assert
        var (_, width, height) = ReadPdf(result);
        Assert.Equal(letter.GetWidth(), width, 0);
        Assert.Equal(letter.GetHeight(), height, 0);
    }

    [Fact]
    public async Task RenderHtmlToPdfAsync_WithTitleMetadata_SetsDocumentTitle()
    {
        // Arrange
        var html = "<html><body>Content</body></html>";
        var options = new PdfRenderOptions { Metadata = new PdfMetadata { Title = "My Test Document" } };

        // Act
        var result = await _renderer.RenderHtmlToPdfAsync(html, options);

        // Assert
        using var ms = new MemoryStream(result);
        using var reader = new PdfReader(ms);
        using var doc = new iText.Kernel.Pdf.PdfDocument(reader);
        Assert.Equal("My Test Document", doc.GetDocumentInfo().GetTitle());
    }

    [Fact]
    public async Task RenderHtmlToPdfAsync_AlreadyCancelledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        Func<Task> act = () => _renderer.RenderHtmlToPdfAsync("<html><body>x</body></html>", null, cts.Token);

        // Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(act);
    }

    [Fact]
    public async Task RenderHtmlFileToPdfAsync_NullOrEmptyPath_ThrowsArgumentException()
    {
        // Arrange / Act
        Func<Task> act = () => _renderer.RenderHtmlFileToPdfAsync("");

        // Assert
        await Assert.ThrowsAsync<ArgumentException>(act);
    }

    [Fact]
    public async Task RenderHtmlFileToPdfAsync_FileDoesNotExist_ThrowsFileNotFoundException()
    {
        // Arrange
        var missingPath = Path.Combine(Path.GetTempPath(), $"does-not-exist-{Guid.NewGuid():N}.html");

        // Act
        Func<Task> act = () => _renderer.RenderHtmlFileToPdfAsync(missingPath);

        // Assert
        await Assert.ThrowsAsync<FileNotFoundException>(act);
    }

    [Fact]
    public async Task RenderHtmlFileToPdfAsync_ValidFile_ReturnsNonEmptyPdf()
    {
        // Arrange
        var tempFile = Path.Combine(Path.GetTempPath(), $"render-test-{Guid.NewGuid():N}.html");
        await File.WriteAllTextAsync(tempFile, "<html><body><p>From file</p></body></html>");

        try
        {
            // Act
            var result = await _renderer.RenderHtmlFileToPdfAsync(tempFile);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Length > 0);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task RenderUrlToPdfAsync_EmptyUrl_ThrowsArgumentException()
    {
        // Arrange / Act
        Func<Task> act = () => _renderer.RenderUrlToPdfAsync("");

        // Assert
        await Assert.ThrowsAsync<ArgumentException>(act);
    }

    [Fact]
    public async Task RenderUrlToPdfAsync_InvalidUrlFormat_ThrowsArgumentException()
    {
        // Arrange / Act
        Func<Task> act = () => _renderer.RenderUrlToPdfAsync("not a url");

        // Assert
        await Assert.ThrowsAsync<ArgumentException>(act);
    }

    [Fact]
    public async Task RenderUrlToPdfAsync_UnreachableHost_ThrowsPdfRenderException()
    {
        // Arrange: port 1 is reserved and should refuse the connection immediately on loopback
        var url = "http://127.0.0.1:1/";

        // Act
        Func<Task> act = () => _renderer.RenderUrlToPdfAsync(url);

        // Assert
        await Assert.ThrowsAsync<PdfRenderException>(act);
    }

    [Fact]
    public async Task RenderUrlToPdfAsync_ValidUrl_ReturnsNonEmptyPdf()
    {
        // Arrange: start a local loopback HTTP server so no external network access is required
        using var listener = new System.Net.HttpListener();
        var port = GetFreeTcpPort();
        var prefix = $"http://127.0.0.1:{port}/";
        listener.Prefixes.Add(prefix);
        listener.Start();
        var serverTask = Task.Run(async () =>
        {
            var context = await listener.GetContextAsync();
            var buffer = System.Text.Encoding.UTF8.GetBytes("<html><body><p>Served content</p></body></html>");
            context.Response.ContentType = "text/html";
            context.Response.ContentLength64 = buffer.Length;
            await context.Response.OutputStream.WriteAsync(buffer);
            context.Response.OutputStream.Close();
        });

        try
        {
            // Act
            var result = await _renderer.RenderUrlToPdfAsync(prefix);
            await serverTask;

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Length > 0);
        }
        finally
        {
            listener.Stop();
            listener.Close();
        }
    }

    [Fact]
    public async Task RenderHtmlToPdfFileAsync_NullOrEmptyOutputPath_ThrowsArgumentException()
    {
        // Arrange / Act
        Func<Task> act = () => _renderer.RenderHtmlToPdfFileAsync("<html></html>", "");

        // Assert
        await Assert.ThrowsAsync<ArgumentException>(act);
    }

    [Fact]
    public async Task RenderHtmlToPdfFileAsync_ValidInput_WritesPdfFileToDisk()
    {
        // Arrange
        var outputPath = Path.Combine(Path.GetTempPath(), $"render-output-{Guid.NewGuid():N}.pdf");

        try
        {
            // Act
            await _renderer.RenderHtmlToPdfFileAsync("<html><body>File output</body></html>", outputPath);

            // Assert
            Assert.True(File.Exists(outputPath));
            var bytes = await File.ReadAllBytesAsync(outputPath);
            Assert.True(bytes.Length > 0);
            Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
        }
        finally
        {
            if (File.Exists(outputPath)) File.Delete(outputPath);
        }
    }

    private static int GetFreeTcpPort()
    {
        var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
