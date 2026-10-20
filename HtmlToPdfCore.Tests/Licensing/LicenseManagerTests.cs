using HtmlToPdfCore.Licensing;
using Xunit;
using ValidationResult = HtmlToPdfCore.Licensing.MongoLicenseValidator.ValidationResult;

namespace HtmlToPdfCore.Tests.Licensing;

/// <summary>
/// Unit tests for <see cref="LicenseManager"/>, exercised independently of PDF rendering
/// by injecting a <see cref="FakeLicenseValidator"/> instead of the real MongoDB validator.
/// Tests run sequentially (single test class/collection) because LicenseManager holds
/// static state that is reset before every test.
/// </summary>
public class LicenseManagerTests
{
    public LicenseManagerTests()
    {
        LicenseManager.ResetForTesting();
    }

    private static LicenseInfo SampleLicense(DateTime expiryDate, bool isActive = true) => new LicenseInfo
    {
        LicenseId = "abc123",
        CompanyName = "Acme Corp",
        Email = "ops@acme.test",
        IssueDate = DateTime.UtcNow.AddYears(-1),
        ExpiryDate = expiryDate,
        LicenseType = LicenseType.Professional,
        IsActive = isActive,
        MaxDocumentsPerMonth = 10000
    };

    [Fact]
    public async Task SetLicenseKeyAsync_ValidLicense_ReturnsTrueAndCachesLicenseInfo()
    {
        var info = SampleLicense(DateTime.UtcNow.AddYears(1));
        LicenseManager.SetValidatorForTesting(FakeLicenseValidator.ReturningResult(FakeLicenseValidator.Valid(info)));

        var result = await LicenseManager.SetLicenseKeyAsync("GOOD-KEY");

        Assert.True(result);
        Assert.True(LicenseManager.IsInitialized);
        Assert.NotNull(LicenseManager.GetLicenseInfo());
        Assert.Equal("Acme Corp", LicenseManager.GetLicenseInfo()!.CompanyName);
    }

    [Fact]
    public async Task SetLicenseKeyAsync_UnknownLicenseKey_ReturnsFalseAndDoesNotInitialize()
    {
        LicenseManager.SetValidatorForTesting(FakeLicenseValidator.ReturningResult(FakeLicenseValidator.NotFound()));

        var result = await LicenseManager.SetLicenseKeyAsync("NOT-FOUND");

        Assert.False(result);
        Assert.False(LicenseManager.IsInitialized);
        Assert.Null(LicenseManager.GetLicenseInfo());
    }

    [Fact]
    public async Task SetLicenseKeyAsync_ExpiredLicense_ReturnsFalse()
    {
        LicenseManager.SetValidatorForTesting(FakeLicenseValidator.ReturningResult(FakeLicenseValidator.Expired()));

        var result = await LicenseManager.SetLicenseKeyAsync("EXPIRED-KEY");

        Assert.False(result);
        Assert.False(LicenseManager.IsInitialized);
    }

    [Fact]
    public async Task SetLicenseKeyAsync_DeactivatedLicense_ReturnsFalse()
    {
        LicenseManager.SetValidatorForTesting(FakeLicenseValidator.ReturningResult(FakeLicenseValidator.Deactivated()));

        var result = await LicenseManager.SetLicenseKeyAsync("DEACTIVATED-KEY");

        Assert.False(result);
        Assert.False(LicenseManager.IsInitialized);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SetLicenseKeyAsync_MissingOrMalformedKey_ReturnsFalseWithoutThrowing(string licenseKey)
    {
        LicenseManager.SetValidatorForTesting(FakeLicenseValidator.ReturningResult(FakeLicenseValidator.NotFound()));

        var result = await LicenseManager.SetLicenseKeyAsync(licenseKey);

        Assert.False(result);
        Assert.False(LicenseManager.IsInitialized);
    }

    [Fact]
    public async Task SetLicenseKeyAsync_ValidatorResultHasNoLicenseInfo_ReturnsFalse()
    {
        var malformedResult = new ValidationResult { IsValid = true, LicenseInfo = null };
        LicenseManager.SetValidatorForTesting(FakeLicenseValidator.ReturningResult(malformedResult));

        var result = await LicenseManager.SetLicenseKeyAsync("MALFORMED");

        Assert.False(result);
        Assert.False(LicenseManager.IsInitialized);
    }

    [Fact]
    public async Task SetLicenseKeyAsync_ValidatorThrows_ReturnsFalseInsteadOfPropagating()
    {
        LicenseManager.SetValidatorForTesting(FakeLicenseValidator.Throwing(new InvalidOperationException("Mongo connection refused")));

        var result = await LicenseManager.SetLicenseKeyAsync("ANY-KEY");

        Assert.False(result);
        Assert.False(LicenseManager.IsInitialized);
    }

    [Fact]
    public async Task SetLicenseKeyAsync_OfflineModeWithLicenseInfo_ReturnsTrue()
    {
        var info = SampleLicense(DateTime.UtcNow.AddMonths(1));
        var offlineButCachedResult = new ValidationResult { IsValid = false, IsOfflineMode = true, LicenseInfo = info };
        LicenseManager.SetValidatorForTesting(FakeLicenseValidator.ReturningResult(offlineButCachedResult));

        var result = await LicenseManager.SetLicenseKeyAsync("OFFLINE-KEY");

        Assert.True(result);
        Assert.True(LicenseManager.IsInitialized);
    }

    [Fact]
    public void ValidateLicense_NotInitialized_ReturnsFalse()
    {
        var result = LicenseManager.ValidateLicense();

        Assert.False(result);
    }

    [Fact]
    public void ValidateLicense_ValidCachedLicense_ReturnsTrue()
    {
        LicenseManager.SetCachedLicenseForTesting(SampleLicense(DateTime.UtcNow.AddYears(1)));

        var result = LicenseManager.ValidateLicense();

        Assert.True(result);
    }

    [Fact]
    public void ValidateLicense_ExpiredCachedLicense_ReturnsFalse()
    {
        LicenseManager.SetCachedLicenseForTesting(SampleLicense(DateTime.UtcNow.AddDays(-1)));

        var result = LicenseManager.ValidateLicense();

        Assert.False(result);
    }

    [Fact]
    public void ValidateLicense_DeactivatedCachedLicense_ReturnsFalse()
    {
        LicenseManager.SetCachedLicenseForTesting(SampleLicense(DateTime.UtcNow.AddYears(1), isActive: false));

        var result = LicenseManager.ValidateLicense();

        Assert.False(result);
    }
}
