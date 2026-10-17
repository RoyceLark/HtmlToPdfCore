using HtmlToPdfCore.Licensing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HtmlToPdfCore.Tests.Licensing;

/// <summary>
/// Tests for the missing/malformed license key paths in LicenseConfiguration.
/// The 'success' paths go through LicenseManager.SetLicenseKey -> MongoDB and are
/// covered indirectly via LicenseManagerTests/MongoLicenseValidatorTests instead of
/// here, to avoid real network calls.
/// </summary>
public class LicenseConfigurationTests
{
    [Fact]
    public void AddHtmlToPdfCoreLicense_WithConfiguration_MissingKey_ThrowsLicenseException()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build(); // no 'HtmlToPdfCore:LicenseKey'

        Assert.Throws<LicenseException>(() => services.AddHtmlToPdfCoreLicense(configuration));
    }

    [Fact]
    public void AddHtmlToPdfCoreLicense_WithEmptyStringKey_ReturnsServicesWithoutThrowing()
    {
        var services = new ServiceCollection();

        var result = services.AddHtmlToPdfCoreLicense(string.Empty);

        Assert.Same(services, result);
    }

    [Fact]
    public void AddHtmlToPdfCoreLicense_WithNullStringKey_ReturnsServicesWithoutThrowing()
    {
        var services = new ServiceCollection();

        var result = services.AddHtmlToPdfCoreLicense((string)null!);

        Assert.Same(services, result);
    }
}
