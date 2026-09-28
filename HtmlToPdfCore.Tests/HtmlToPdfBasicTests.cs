using HtmlToPdfCore.Models;
using Xunit;

namespace HtmlToPdfCore.Tests;

public class HtmlToPdfBasicTests
{
    [Fact]
    public void Create_ReturnsInstance_WithDefaultRenderingOptions()
    {
        using var htmlToPdf = HtmlToPdf.Create();

        Assert.NotNull(htmlToPdf);
        Assert.NotNull(htmlToPdf.RenderingOptions);
        Assert.NotNull(htmlToPdf.PrintOptions);
        Assert.Same(htmlToPdf.RenderingOptions, htmlToPdf.PrintOptions);
    }

    [Fact]
    public void PdfRenderOptions_DefaultValues_MatchExpectedDefaults()
    {
        var options = new PdfRenderOptions();

        Assert.Equal(PageSize.A4, options.PageSize);
        Assert.Equal(PageOrientation.Portrait, options.Orientation);

        Assert.Equal(25, options.Margins.Top);
        Assert.Equal(25, options.Margins.Right);
        Assert.Equal(25, options.Margins.Bottom);
        Assert.Equal(25, options.Margins.Left);
    }
}
