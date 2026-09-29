using System;
using System.Threading.Tasks;
using HtmlToPdfCore.Renderers;
using Xunit;

namespace HtmlToPdfCore.Tests;

/// <summary>
/// Tests covering the URL-to-PDF conversion capability exposed by
/// HtmlToPdf.RenderUrlAsync / RenderUrlAsPdf, backed by
/// HtmlToPdfRenderer.RenderUrlToPdfAsync.
/// </summary>
public class HtmlToPdfUrlRenderingTests
{
    [Fact]
    public async Task RenderUrlAsync_WithValidUrl_ReturnsNonEmptyPdfBytes()
    {
        using var pdf = HtmlToPdf.Create();

        var result = await pdf.RenderUrlAsync("https://example.com");

        Assert.NotNull(result);
        Assert.NotNull(result.BinaryData);
        Assert.True(result.BinaryData.Length > 0);

        var header = System.Text.Encoding.ASCII.GetString(result.BinaryData, 0, 4);
        Assert.Equal("%PDF", header);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RenderUrlAsync_WithNullOrWhitespaceUrl_ThrowsArgumentException(string? url)
    {
        using var pdf = HtmlToPdf.Create();

        await Assert.ThrowsAsync<ArgumentException>(() => pdf.RenderUrlAsync(url!));
    }

    [Fact]
    public async Task RenderUrlAsync_WithMalformedUrl_ThrowsArgumentException()
    {
        using var pdf = HtmlToPdf.Create();

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => pdf.RenderUrlAsync("not a url"));

        Assert.Contains("Invalid URL format", ex.Message);
    }

    [Fact]
    public async Task RenderUrlAsync_WithUnreachableHost_ThrowsPdfRenderException()
    {
        using var pdf = HtmlToPdf.Create();

        var ex = await Assert.ThrowsAsync<PdfRenderException>(
            () => pdf.RenderUrlAsync("https://this-host-should-not-exist-sprintflow.invalid"));

        Assert.IsType<HttpRequestException>(ex.InnerException);
    }
}
