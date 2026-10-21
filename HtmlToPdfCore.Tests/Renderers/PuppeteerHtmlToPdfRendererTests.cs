using System;
using System.Reflection;
using HtmlToPdfCore.Models;
using HtmlToPdfCore.Renderers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HtmlToPdfCore.Tests.Renderers;

// PuppeteerHtmlToPdfRenderer launches a real Chromium browser for its public RenderHtmlToPdfAsync
// method, which requires downloading a browser binary and is not suitable for an isolated unit test.
// These tests exercise its deterministic, pure helper methods (invoked via reflection since they
// are private) which is where the renderer's actual mapping/formatting logic lives.
public class PuppeteerHtmlToPdfRendererTests
{
    private readonly PuppeteerHtmlToPdfRenderer _renderer =
        new PuppeteerHtmlToPdfRenderer(NullLogger<PuppeteerHtmlToPdfRenderer>.Instance);

    private static object? InvokePrivate(object target, string methodName, params object?[] args)
    {
        var method = target.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"Method {methodName} not found");
        return method.Invoke(target, args);
    }

    private static object? InvokePrivateStatic(Type type, string methodName, params object?[] args)
    {
        var method = type.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException($"Static method {methodName} not found");
        return method.Invoke(null, args);
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        // Arrange / Act
        Action act = () => new PuppeteerHtmlToPdfRenderer(null!);

        // Assert
        Assert.Throws<ArgumentNullException>(act);
    }

    [Theory]
    [InlineData(MeasurementUnit.Millimeters, "mm")]
    [InlineData(MeasurementUnit.Inches, "in")]
    [InlineData(MeasurementUnit.Pixels, "px")]
    public void GetMarginUnit_ReturnsExpectedSuffix(MeasurementUnit unit, string expected)
    {
        // Arrange
        var options = new PdfRenderOptions { MarginUnit = unit };

        // Act
        var result = (string)InvokePrivate(_renderer, "GetMarginUnit", options)!;

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void BuildFontScaleCss_DefaultScale_ReturnsEmptyString()
    {
        // Act
        var result = (string)InvokePrivate(_renderer, "BuildFontScaleCss", 1.0f)!;

        // Assert
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void BuildFontScaleCss_NonDefaultScale_ReturnsZoomCss()
    {
        // Act
        var result = (string)InvokePrivate(_renderer, "BuildFontScaleCss", 1.5f)!;

        // Assert
        Assert.Equal("html { zoom: 1.5; }", result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void InjectBaseUrl_NullOrEmptyBaseUrl_ReturnsOriginalHtml(string? baseUrl)
    {
        // Arrange
        var html = "<html><body>x</body></html>";

        // Act
        var result = (string)InvokePrivate(_renderer, "InjectBaseUrl", html, baseUrl)!;

        // Assert
        Assert.Equal(html, result);
    }

    [Fact]
    public void InjectBaseUrl_HtmlHasHeadTag_InsertsBaseTagInHead()
    {
        // Arrange
        var html = "<html><head></head><body>x</body></html>";

        // Act
        var result = (string)InvokePrivate(_renderer, "InjectBaseUrl", html, "file:///tmp/")!;

        // Assert
        Assert.Contains("<head><base href=\"file:///tmp/\">", result);
    }

    [Fact]
    public void InjectBaseUrl_HtmlHasOnlyHtmlTag_InsertsHeadAndBaseTag()
    {
        // Arrange
        var html = "<html><body>x</body></html>";

        // Act
        var result = (string)InvokePrivate(_renderer, "InjectBaseUrl", html, "https://example.com/")!;

        // Assert
        Assert.Contains("<head><base href=\"https://example.com/\"></head>", result);
    }

    [Fact]
    public void InjectBaseUrl_NoHeadOrHtmlTag_PrependsBaseTag()
    {
        // Arrange
        var html = "<body>x</body>";

        // Act
        var result = (string)InvokePrivate(_renderer, "InjectBaseUrl", html, "https://example.com/")!;

        // Assert
        Assert.StartsWith("<base href=\"https://example.com/\">", result);
    }

    [Fact]
    public void BuildTemplate_NullHtmlContent_ReturnsEmptySpan()
    {
        // Arrange
        var hf = new HeaderFooterOptions();

        // Act
        var result = (string)InvokePrivate(_renderer, "BuildTemplate", hf, true)!;

        // Assert
        Assert.Equal("<span></span>", result);
    }

    [Fact]
    public void BuildTemplate_WithPageAndTotalTokens_ReplacesTokens()
    {
        // Arrange
        var hf = new HeaderFooterOptions { HtmlContent = "Page {page} of {total}" };

        // Act
        var result = (string)InvokePrivate(_renderer, "BuildTemplate", hf, false)!;

        // Assert
        Assert.Contains("<span class='pageNumber'></span>", result);
        Assert.Contains("<span class='totalPages'></span>", result);
    }

    [Theory]
    [InlineData(PageSize.A4, PageOrientation.Portrait, 794)]
    [InlineData(PageSize.A4, PageOrientation.Landscape, 1123)]
    [InlineData(PageSize.Letter, PageOrientation.Portrait, 816)]
    public void GetPaperWidthPx_StandardSizes_ReturnsExpectedPixelWidth(PageSize size, PageOrientation orientation, int expected)
    {
        // Act
        var result = (int)InvokePrivateStatic(typeof(PuppeteerHtmlToPdfRenderer), "GetPaperWidthPx", size, orientation, null)!;

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void GetPaperWidthPx_CustomSizePortrait_UsesCustomWidth()
    {
        // Act: 100mm at 96 DPI => round(100 * 96 / 25.4) = 378
        var result = (int)InvokePrivateStatic(typeof(PuppeteerHtmlToPdfRenderer), "GetPaperWidthPx", PageSize.Custom, PageOrientation.Portrait, 100f)!;

        // Assert
        Assert.Equal(378, result);
    }
}
