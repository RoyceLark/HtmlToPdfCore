using HtmlToPdfCore.Licensing;
using Xunit;

namespace HtmlToPdfCore.Tests.Licensing;

/// <summary>
/// Unit tests for license validation logic (LicenseManager), independent of
/// rendering functionality and of any live MongoDB connection.
/// </summary>
[Collection("LicenseManager")]
public class LicenseManagerTests : IDisposable
{
    public LicenseManagerTests()
    {
        LicenseManager.ResetForTesting();
    }

    public void Dispose()
    {
        LicenseManager.ResetForTesting();
    }

    private static LicenseInfo ValidLicenseInfo(DateTime? expiry = null, bool isActive = true) => new LicenseInfo
    {
        LicenseId = "id-1",
        CompanyName = "Acme Corp",
        Email = "user@acme.test",
        IssueDate = DateTime.UtcNow.AddYears(-1),
        ExpiryDate = expiry ?? DateTime.UtcNow.AddYears(1),
        LicenseType = LicenseType.Professional,
        IsActive = isActive,
        MaxDocumentsPerMonth = 10000
    };

    // ---- SetLicenseKeyAsync scenarios (go through the validator) ----

    [Fact]
    public async Task SetLicenseKeyAsync_ValidLicense_ReturnsTrueAndCachesLicenseInfo()
    {
        var info = ValidLicenseInfo();
        LicenseManager.SetValidatorForTesting(new FakeLicenseValidator(
            new MongoLicenseValidator.ValidationResult { IsValid = true, LicenseInfo = info }));

        var result = await LicenseManager.SetLicenseKeyAsync("VALID-KEY");

        Assert.True(result);
        var cached = LicenseManager.GetLicenseInfo();
        Assert.NotNull(cached);
        Assert.Equal("Acme Corp", cached!.CompanyName);
        Assert.True(LicenseManager.IsInitialized);
    }

    [Fact]
    public async Task SetLicenseKeyAsync_ExpiredLicense_ReturnsFalse()
    {
        LicenseManager.SetValidatorForTesting(new FakeLicenseValidator(
            new MongoLicenseValidator.ValidationResult { IsValid = false, ErrorMessage = "License has expired" }));

        var result = await LicenseManager.SetLicenseKeyAsync("EXPIRED-KEY");

        Assert.False(result);
        Assert.Null(LicenseManager.GetLicenseInfo());
    }

    [Fact]
    public async Task SetLicenseKeyAsync_DeactivatedLicense_ReturnsFalse()
    {
        LicenseManager.SetValidatorForTesting(new FakeLicenseValidator(
            new MongoLicenseValidator.ValidationResult { IsValid = false, ErrorMessage = "License has been deactivated" }));

        var result = await LicenseManager.SetLicenseKeyAsync("DEACTIVATED-KEY");

        Assert.False(result);
    }

    [Fact]
    public async Task SetLicenseKeyAsync_UnknownLicenseKey_ReturnsFalse()
    {
        LicenseManager.SetValidatorForTesting(new FakeLicenseValidator(
            new MongoLicenseValidator.ValidationResult { IsValid = false, ErrorMessage = "License key not found" }));

        var result = await LicenseManager.SetLicenseKeyAsync("DOES-NOT-EXIST");

        Assert.False(result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SetLicenseKeyAsync_MalformedLicenseKey_ReturnsFalseWithoutThrowing(string malformedKey)
    {
        LicenseManager.SetValidatorForTesting(new FakeLicenseValidator(
            new MongoLicenseValidator.ValidationResult { IsValid = false, ErrorMessage = "License key not found" }));

        var result = await LicenseManager.SetLicenseKeyAsync(malformedKey);

        Assert.False(result);
    }

    [Fact]
    public async Task SetLicenseKeyAsync_ValidatorReportsOfflineModeWithNoLicenseInfo_ReturnsFalse()
    {
        // Simulates a validator that caught a connection error internally and
        // returned an offline-mode result instead of throwing.
        LicenseManager.SetValidatorForTesting(new FakeLicenseValidator(
            new MongoLicenseValidator.ValidationResult { IsValid = false, IsOfflineMode = true, ErrorMessage = "Validation error: timeout" }));

        var result = await LicenseManager.SetLicenseKeyAsync("ANY-KEY");

        Assert.False(result);
    }

    [Fact]
    public async Task SetLicenseKeyAsync_ValidatorThrows_ReturnsFalseInsteadOfThrowing()
    {
        LicenseManager.SetValidatorForTesting(new FakeLicenseValidator(new InvalidOperationException("connection refused")));

        var result = await LicenseManager.SetLicenseKeyAsync("ANY-KEY");

        Assert.False(result);
    }

    [Fact]
    public void SetLicenseKey_Synchronous_DelegatesToAsyncValidation()
    {
        var info = ValidLicenseInfo();
        LicenseManager.SetValidatorForTesting(new FakeLicenseValidator(
            new MongoLicenseValidator.ValidationResult { IsValid = true, LicenseInfo = info }));

        var result = LicenseManager.SetLicenseKey("VALID-KEY");

        Assert.True(result);
    }

    // ---- ValidateLicense() scenarios (seeded cache state) ----

    [Fact]
    public void ValidateLicense_NotInitialized_ReturnsFalse()
    {
        Assert.False(LicenseManager.ValidateLicense());
    }

    [Fact]
    public void ValidateLicense_CachedLicenseIsCurrentAndActive_ReturnsTrue()
    {
        LicenseManager.SeedCachedLicenseForTesting(ValidLicenseInfo(), DateTime.UtcNow);

        Assert.True(LicenseManager.ValidateLicense());
    }

    [Fact]
    public void ValidateLicense_CachedLicenseExpired_ReturnsFalse()
    {
        var expired = ValidLicenseInfo(expiry: DateTime.UtcNow.AddDays(-1));
        LicenseManager.SeedCachedLicenseForTesting(expired, DateTime.UtcNow);

        Assert.False(LicenseManager.ValidateLicense());
    }

    [Fact]
    public void ValidateLicense_CachedLicenseDeactivated_ReturnsFalse()
    {
        var deactivated = ValidLicenseInfo(isActive: false);
        LicenseManager.SeedCachedLicenseForTesting(deactivated, DateTime.UtcNow);

        Assert.False(LicenseManager.ValidateLicense());
    }

    [Fact]
    public void GetLicenseInfo_WhenNotInitialized_ReturnsNull()
    {
        Assert.Null(LicenseManager.GetLicenseInfo());
    }
}

/// <summary>
/// LicenseManager holds process-wide static state; these tests must not run in
/// parallel with each other.
/// </summary>
[CollectionDefinition("LicenseManager", DisableParallelization = true)]
public class LicenseManagerTestCollection
{
}
