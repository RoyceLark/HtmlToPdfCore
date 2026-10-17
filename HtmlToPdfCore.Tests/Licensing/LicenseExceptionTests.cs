using HtmlToPdfCore.Licensing;
using Xunit;

namespace HtmlToPdfCore.Tests.Licensing;

public class LicenseExceptionTests
{
    [Fact]
    public void Constructor_WithMessage_SetsMessage()
    {
        var ex = new LicenseException("license key missing");

        Assert.Equal("license key missing", ex.Message);
        Assert.Null(ex.InnerException);
    }

    [Fact]
    public void Constructor_WithMessageAndInnerException_SetsBoth()
    {
        var inner = new InvalidOperationException("root cause");
        var ex = new LicenseException("license validation failed", inner);

        Assert.Equal("license validation failed", ex.Message);
        Assert.Same(inner, ex.InnerException);
    }
}
