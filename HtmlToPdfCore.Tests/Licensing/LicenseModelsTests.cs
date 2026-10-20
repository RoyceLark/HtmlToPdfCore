using HtmlToPdfCore.Licensing;
using Xunit;

namespace HtmlToPdfCore.Tests.Licensing;

public class LicenseModelsTests
{
    [Fact]
    public void LicenseException_WithMessage_SetsMessage()
    {
        var ex = new LicenseException("bad license");

        Assert.Equal("bad license", ex.Message);
    }

    [Fact]
    public void LicenseException_WithInnerException_SetsInnerException()
    {
        var inner = new InvalidOperationException("root cause");
        var ex = new LicenseException("bad license", inner);

        Assert.Equal("bad license", ex.Message);
        Assert.Same(inner, ex.InnerException);
    }

    [Theory]
    [InlineData(LicenseType.Trial)]
    [InlineData(LicenseType.Standard)]
    [InlineData(LicenseType.Professional)]
    [InlineData(LicenseType.Enterprise)]
    public void LicenseType_AllValues_AreDefined(LicenseType type)
    {
        Assert.True(Enum.IsDefined(typeof(LicenseType), type));
    }
}
