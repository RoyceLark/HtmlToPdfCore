using HtmlToPdfCore.Licensing;
using Xunit;

namespace HtmlToPdfCore.Tests;

/// <summary>
/// Exercises MongoLicenseValidator.ValidateAsync's real decision logic
/// (lookup-miss, expiry check, active check, mapping, exception handling)
/// by overriding only the MongoDB fetch step - everything else in
/// ValidateAsync runs exactly as in production.
/// </summary>
public class MongoLicenseValidatorTests
{
    private static MongoLicenseValidator.LicenseDocument MakeDocument(
        DateTime expiryDate,
        bool isActive = true,
        string licenseType = "Professional") => new MongoLicenseValidator.LicenseDocument
    {
        Id = "507f1f77bcf86cd799439011",
        LicenseKey = "KEY-1",
        CompanyName = "Acme Corp",
        Email = "acme@example.com",
        IssueDate = DateTime.UtcNow.AddDays(-30),
        ExpiryDate = expiryDate,
        LicenseType = licenseType,
        IsActive = isActive,
        MaxDocumentsPerMonth = 10000
    };

    [Fact]
    public async Task ValidateAsync_LicenseFoundActiveAndNotExpired_ReturnsValidWithMappedInfo()
    {
        var document = MakeDocument(DateTime.UtcNow.AddDays(30));
        var validator = TestableMongoLicenseValidator.ForDocument(document);

        var result = await validator.ValidateAsync("KEY-1");

        Assert.True(result.IsValid);
        Assert.False(result.IsOfflineMode);
        Assert.NotNull(result.LicenseInfo);
        Assert.Equal("Acme Corp", result.LicenseInfo!.CompanyName);
        Assert.Equal("acme@example.com", result.LicenseInfo.Email);
        Assert.Equal(LicenseType.Professional, result.LicenseInfo.LicenseType);
        Assert.True(result.LicenseInfo.IsActive);
    }

    [Fact]
    public async Task ValidateAsync_LicenseKeyNotFound_ReturnsInvalidWithExpectedMessage()
    {
        var validator = TestableMongoLicenseValidator.ForDocument(null);

        var result = await validator.ValidateAsync("UNKNOWN-KEY");

        Assert.False(result.IsValid);
        Assert.Equal("License key not found", result.ErrorMessage);
        Assert.Null(result.LicenseInfo);
    }

    [Fact]
    public async Task ValidateAsync_LicenseExpired_ReturnsInvalidWithExpectedMessage()
    {
        var document = MakeDocument(DateTime.UtcNow.AddDays(-1));
        var validator = TestableMongoLicenseValidator.ForDocument(document);

        var result = await validator.ValidateAsync("KEY-1");

        Assert.False(result.IsValid);
        Assert.Equal("License has expired", result.ErrorMessage);
        Assert.Null(result.LicenseInfo);
    }

    [Fact]
    public async Task ValidateAsync_LicenseDeactivated_ReturnsInvalidWithExpectedMessage()
    {
        var document = MakeDocument(DateTime.UtcNow.AddDays(30), isActive: false);
        var validator = TestableMongoLicenseValidator.ForDocument(document);

        var result = await validator.ValidateAsync("KEY-1");

        Assert.False(result.IsValid);
        Assert.Equal("License has been deactivated", result.ErrorMessage);
        Assert.Null(result.LicenseInfo);
    }

    [Fact]
    public async Task ValidateAsync_LookupThrows_ReturnsOfflineModeWithErrorMessage_DoesNotThrow()
    {
        var validator = TestableMongoLicenseValidator.ForException(new TimeoutException("connection timed out"));

        var result = await validator.ValidateAsync("KEY-1");

        Assert.False(result.IsValid);
        Assert.True(result.IsOfflineMode);
        Assert.Contains("connection timed out", result.ErrorMessage);
        Assert.Null(result.LicenseInfo);
    }

    private class TestableMongoLicenseValidator : MongoLicenseValidator
    {
        private readonly LicenseDocument? _document;
        private readonly Exception? _exceptionToThrow;

        private TestableMongoLicenseValidator(LicenseDocument? document, Exception? exceptionToThrow)
        {
            _document = document;
            _exceptionToThrow = exceptionToThrow;
        }

        public static TestableMongoLicenseValidator ForDocument(LicenseDocument? document) =>
            new TestableMongoLicenseValidator(document, null);

        public static TestableMongoLicenseValidator ForException(Exception exceptionToThrow) =>
            new TestableMongoLicenseValidator(null, exceptionToThrow);

        protected override Task<LicenseDocument?> FindLicenseAsync(string licenseKey)
        {
            if (_exceptionToThrow != null)
            {
                throw _exceptionToThrow;
            }

            return Task.FromResult(_document);
        }
    }
}
