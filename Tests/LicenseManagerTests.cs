using HtmlToPdfCore.Licensing;
using Xunit;

namespace HtmlToPdfCore.Tests;

/// <summary>
/// Unit tests for LicenseManager covering all license validation paths,
/// independent of PDF rendering functionality.
/// </summary>
public class LicenseManagerTests
{
    public LicenseManagerTests()
    {
        // LicenseManager keeps static state; reset before every test so tests
        // are independent of execution order.
        LicenseManager.ResetForTesting();
    }

    private static MongoLicenseValidator.ValidationResult ValidResult(DateTime? expiry = null, bool isActive = true) =>
        new MongoLicenseValidator.ValidationResult
        {
            IsValid = true,
            LicenseInfo = new LicenseInfo
            {
                LicenseId = "lic-1",
                CompanyName = "Acme Corp",
                Email = "acme@example.com",
                IssueDate = DateTime.UtcNow.AddDays(-30),
                ExpiryDate = expiry ?? DateTime.UtcNow.AddDays(30),
                LicenseType = LicenseType.Professional,
                IsActive = isActive,
                MaxDocumentsPerMonth = 10000
            }
        };

    [Fact]
    public async Task SetLicenseKeyAsync_ValidLicense_ReturnsTrueAndCachesInfo()
    {
        LicenseManager.SetValidatorForTesting(FakeLicenseValidator.ReturningResult(ValidResult()));

        var result = await LicenseManager.SetLicenseKeyAsync("GOOD-KEY");

        Assert.True(result);
        var info = LicenseManager.GetLicenseInfo();
        Assert.NotNull(info);
        Assert.Equal("Acme Corp", info!.CompanyName);
        Assert.Equal("acme@example.com", info.Email);
        Assert.True(LicenseManager.IsInitialized);
    }

    [Fact]
    public async Task SetLicenseKeyAsync_LicenseKeyNotFound_ReturnsFalse()
    {
        LicenseManager.SetValidatorForTesting(FakeLicenseValidator.ReturningResult(new MongoLicenseValidator.ValidationResult
        {
            IsValid = false,
            ErrorMessage = "License key not found"
        }));

        var result = await LicenseManager.SetLicenseKeyAsync("UNKNOWN-KEY");

        Assert.False(result);
        Assert.Null(LicenseManager.GetLicenseInfo());
        Assert.False(LicenseManager.IsInitialized);
    }

    [Fact]
    public async Task SetLicenseKeyAsync_ExpiredLicense_ReturnsFalse()
    {
        LicenseManager.SetValidatorForTesting(FakeLicenseValidator.ReturningResult(new MongoLicenseValidator.ValidationResult
        {
            IsValid = false,
            ErrorMessage = "License has expired"
        }));

        var result = await LicenseManager.SetLicenseKeyAsync("EXPIRED-KEY");

        Assert.False(result);
        Assert.False(LicenseManager.IsInitialized);
    }

    [Fact]
    public async Task SetLicenseKeyAsync_DeactivatedLicense_ReturnsFalse()
    {
        LicenseManager.SetValidatorForTesting(FakeLicenseValidator.ReturningResult(new MongoLicenseValidator.ValidationResult
        {
            IsValid = false,
            ErrorMessage = "License has been deactivated"
        }));

        var result = await LicenseManager.SetLicenseKeyAsync("DEACTIVATED-KEY");

        Assert.False(result);
        Assert.False(LicenseManager.IsInitialized);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task SetLicenseKeyAsync_MissingOrMalformedKey_ReturnsFalse(string? licenseKey)
    {
        var fake = FakeLicenseValidator.ReturningResult(new MongoLicenseValidator.ValidationResult
        {
            IsValid = false,
            ErrorMessage = "License key not found"
        });
        LicenseManager.SetValidatorForTesting(fake);

        var result = await LicenseManager.SetLicenseKeyAsync(licenseKey!);

        Assert.False(result);
        Assert.Equal(licenseKey, fake.LastLicenseKeyRequested);
    }

    [Fact]
    public async Task SetLicenseKeyAsync_ValidFlagButNoLicenseInfo_ReturnsFalse()
    {
        // Malformed validator response: marked valid but no license payload.
        LicenseManager.SetValidatorForTesting(FakeLicenseValidator.ReturningResult(new MongoLicenseValidator.ValidationResult
        {
            IsValid = true,
            LicenseInfo = null
        }));

        var result = await LicenseManager.SetLicenseKeyAsync("ANY-KEY");

        Assert.False(result);
        Assert.False(LicenseManager.IsInitialized);
    }

    [Fact]
    public async Task SetLicenseKeyAsync_OfflineModeWithoutLicenseInfo_ReturnsFalse()
    {
        // Simulates a connection/DB error surfaced as offline mode by the validator.
        LicenseManager.SetValidatorForTesting(FakeLicenseValidator.ReturningResult(new MongoLicenseValidator.ValidationResult
        {
            IsValid = false,
            IsOfflineMode = true,
            ErrorMessage = "Validation error: timeout"
        }));

        var result = await LicenseManager.SetLicenseKeyAsync("ANY-KEY");

        Assert.False(result);
    }

    [Fact]
    public async Task SetLicenseKeyAsync_ValidatorThrowsGenericException_ReturnsFalse()
    {
        LicenseManager.SetValidatorForTesting(FakeLicenseValidator.Throwing(new InvalidOperationException("connection refused")));

        var result = await LicenseManager.SetLicenseKeyAsync("ANY-KEY");

        Assert.False(result);
        Assert.False(LicenseManager.IsInitialized);
    }

    [Fact]
    public async Task SetLicenseKeyAsync_ValidatorThrowsLicenseException_Rethrows()
    {
        LicenseManager.SetValidatorForTesting(FakeLicenseValidator.Throwing(new LicenseException("bad license")));

        await Assert.ThrowsAsync<LicenseException>(() => LicenseManager.SetLicenseKeyAsync("ANY-KEY"));
    }

    [Fact]
    public void ValidateLicense_NotInitialized_ReturnsFalse()
    {
        Assert.False(LicenseManager.ValidateLicense());
    }

    [Fact]
    public async Task ValidateLicense_CachedLicenseExpired_ReturnsFalse()
    {
        LicenseManager.SetValidatorForTesting(FakeLicenseValidator.ReturningResult(ValidResult(expiry: DateTime.UtcNow.AddDays(-1))));
        await LicenseManager.SetLicenseKeyAsync("EXPIRED-CACHE-KEY");

        Assert.False(LicenseManager.ValidateLicense());
    }

    [Fact]
    public async Task ValidateLicense_CachedLicenseDeactivated_ReturnsFalse()
    {
        LicenseManager.SetValidatorForTesting(FakeLicenseValidator.ReturningResult(ValidResult(isActive: false)));
        await LicenseManager.SetLicenseKeyAsync("DEACTIVATED-CACHE-KEY");

        Assert.False(LicenseManager.ValidateLicense());
    }

    [Fact]
    public async Task ValidateLicense_ValidCachedLicense_ReturnsTrue()
    {
        LicenseManager.SetValidatorForTesting(FakeLicenseValidator.ReturningResult(ValidResult()));
        await LicenseManager.SetLicenseKeyAsync("GOOD-KEY");

        Assert.True(LicenseManager.ValidateLicense());
    }
}
