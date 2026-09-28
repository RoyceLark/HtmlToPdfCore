using HtmlToPdfCore.Models;
using Xunit;

namespace HtmlToPdfCore.Tests;

public class HtmlToPdfBasicTests
{
    [Fact]
    public void PdfRenderOptions_DefaultValues_AreCorrect()
    {
        var options = new PdfRenderOptions();

        Assert.Equal(PageSize.A4, options.PageSize);
        Assert.Equal(PageOrientation.Portrait, options.Orientation);
        Assert.True(options.EnableJavaScript);
        Assert.Equal(30000, options.RenderTimeout);
        Assert.Equal(CssMediaType.Print, options.MediaType);
        Assert.True(options.PrintBackground);
        Assert.True(options.CompressImages);
        Assert.Equal(85, options.ImageQuality);
        Assert.Equal(96, options.Dpi);
        Assert.Equal(1, options.FirstPageNumber);
        Assert.False(options.Grayscale);
        Assert.Equal(MeasurementUnit.Millimeters, options.MarginUnit);
    }

    [Fact]
    public void PageSize_And_PaperSize_Alias_RoundTrip()
    {
        var options = new PdfRenderOptions();

        options.PaperSize = PageSize.Letter;
        Assert.Equal(PageSize.Letter, options.PageSize);

        options.PageSize = PageSize.Legal;
        Assert.Equal(PageSize.Legal, options.PaperSize);
    }

    [Fact]
    public void Timeout_Alias_ConvertsSecondsToMilliseconds()
    {
        var options = new PdfRenderOptions();

        options.Timeout = 5;
        Assert.Equal(5000, options.RenderTimeout);

        options.RenderTimeout = 12000;
        Assert.Equal(12, options.Timeout);
    }

    [Fact]
    public void GrayScale_Alias_MapsToGrayscale()
    {
        var options = new PdfRenderOptions();

        options.GrayScale = true;
        Assert.True(options.Grayscale);

        options.Grayscale = false;
        Assert.False(options.GrayScale);
    }

    [Fact]
    public void CssMedia_Alias_MapsToMediaType()
    {
        var options = new PdfRenderOptions();

        options.CssMedia = CssMediaType.Screen;
        Assert.Equal(CssMediaType.Screen, options.MediaType);

        options.MediaType = CssMediaType.Print;
        Assert.Equal(CssMediaType.Print, options.CssMedia);
    }

    [Fact]
    public void MeasurementUnit_Alias_MapsToMarginUnit()
    {
        var options = new PdfRenderOptions();

        options.MeasurementUnit = MeasurementUnit.Inches;
        Assert.Equal(MeasurementUnit.Inches, options.MarginUnit);

        options.MarginUnit = MeasurementUnit.Pixels;
        Assert.Equal(MeasurementUnit.Pixels, options.MeasurementUnit);
    }

    [Theory]
    [InlineData(10.0, 20.0, 30.0, 40.0)]
    [InlineData(0.0, 0.0, 0.0, 0.0)]
    public void MarginProperties_DelegateToMargins(double top, double right, double bottom, double left)
    {
        var options = new PdfRenderOptions
        {
            MarginTop = top,
            MarginRight = right,
            MarginBottom = bottom,
            MarginLeft = left
        };

        Assert.Equal(top, options.Margins.Top);
        Assert.Equal(right, options.Margins.Right);
        Assert.Equal(bottom, options.Margins.Bottom);
        Assert.Equal(left, options.Margins.Left);

        Assert.Equal(top, options.MarginTop);
        Assert.Equal(right, options.MarginRight);
        Assert.Equal(bottom, options.MarginBottom);
        Assert.Equal(left, options.MarginLeft);
    }

    [Fact]
    public void SetCustomPaperSize_SetsCustomDimensionsAndPageSize()
    {
        var options = new PdfRenderOptions();

        options.SetCustomPaperSize(150f, 200f);

        Assert.Equal(PageSize.Custom, options.PageSize);
        Assert.Equal(150f, options.CustomPageWidth);
        Assert.Equal(200f, options.CustomPageHeight);
        Assert.Equal(150, options.PaperWidth);
        Assert.Equal(200, options.PaperHeight);
    }

    [Fact]
    public void PaperWidthHeight_Alias_SetsCustomPageDimensions()
    {
        var options = new PdfRenderOptions();

        options.PaperWidth = 100;
        options.PaperHeight = 120;

        Assert.Equal(100f, options.CustomPageWidth);
        Assert.Equal(120f, options.CustomPageHeight);
    }

    [Fact]
    public void PageMargins_DefaultConstructor_UsesDefaultsOf25()
    {
        var margins = new PageMargins();

        Assert.Equal(25f, margins.Top);
        Assert.Equal(25f, margins.Right);
        Assert.Equal(25f, margins.Bottom);
        Assert.Equal(25f, margins.Left);
    }

    [Fact]
    public void PageMargins_CustomConstructor_SetsProvidedValues()
    {
        var margins = new PageMargins(1, 2, 3, 4);

        Assert.Equal(1f, margins.Top);
        Assert.Equal(2f, margins.Right);
        Assert.Equal(3f, margins.Bottom);
        Assert.Equal(4f, margins.Left);
    }

    [Fact]
    public void HeaderFooterOptions_DefaultValues_AreCorrect()
    {
        var header = new HeaderFooterOptions();

        Assert.Equal(25f, header.Height);
        Assert.False(header.ShowPageNumbers);
        Assert.Equal(TextAlignment.Center, header.Alignment);
        Assert.False(header.DividerLine);
        Assert.Equal("#000000", header.DividerColor);
    }

    [Fact]
    public void HeaderFooterOptions_DividerAndColorAliases_RoundTrip()
    {
        var header = new HeaderFooterOptions();

        header.DrawDividerLine = true;
        Assert.True(header.DividerLine);

        header.DividerLineColor = "#FF0000";
        Assert.Equal("#FF0000", header.DividerColor);

        header.TextColor = "#00FF00";
        Assert.Equal("#00FF00", header.Color);
    }

    [Fact]
    public void HtmlHeaderFooter_HtmlAlias_MapsToHtmlContent()
    {
        var htmlHeader = new HtmlHeaderFooter();

        htmlHeader.Html = "<div>Header</div>";

        Assert.Equal("<div>Header</div>", htmlHeader.HtmlContent);
        Assert.Equal("<div>Header</div>", htmlHeader.HtmlFragment);
    }

    [Fact]
    public void TextHeaderFooter_AllowsLeftCenterRightText()
    {
        var textFooter = new TextHeaderFooter
        {
            LeftText = "Left",
            CenterText = "Center",
            RightText = "Right"
        };

        Assert.Equal("Left", textFooter.LeftText);
        Assert.Equal("Center", textFooter.CenterText);
        Assert.Equal("Right", textFooter.RightText);
    }

    [Fact]
    public void HtmlHeaderFooter_Header_CanBeAssignedToPdfRenderOptions()
    {
        var options = new PdfRenderOptions
        {
            Header = new HtmlHeaderFooter { HtmlContent = "<b>Hi</b>", Height = 30 }
        };

        Assert.NotNull(options.HtmlHeader);
        Assert.Equal("<b>Hi</b>", options.HtmlHeader!.HtmlContent);
        Assert.Equal(30f, options.HtmlHeader.Height);
    }
}
