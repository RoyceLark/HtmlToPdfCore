using System.Reflection;
using HtmlToPdfCore.Licensing;
using MongoDB.Driver;
using Moq;
using Xunit;

namespace HtmlToPdfCore.Tests.Licensing;

/// <summary>
/// Tests for MongoLicenseValidator.ValidateAsync's decision logic. MongoLicenseValidator
/// has no injectable abstraction over the MongoDB driver, so these tests obtain the
/// singleton via its public GetInstance factory (MongoClient construction is lazy and
/// does not touch the network) and then replace the private readonly '_licenses'
/// collection with a Moq mock via reflection, so each test controls exactly what
/// document (if any) is 'found' without hitting a real database.
/// </summary>
[Collection("LicensingSharedState")]
public class MongoLicenseValidatorTests
{
    public MongoLicenseValidatorTests()
    {
        ReflectionHelper.SetStaticField(typeof(MongoLicenseValidator), "_instance", null);
    }

    private static MongoLicenseValidator.LicenseDocument MakeDocument(
        string licenseKey, DateTime expiryDate, bool isActive, string licenseType = "Professional") => new()
    {
        Id = "000000000000000000000001",
        LicenseKey = licenseKey,
        CompanyName = "Acme Corp",
        Email = "ops@acme.example",
        IssueDate = DateTime.UtcNow.AddYears(-1),
        ExpiryDate = expiryDate,
        LicenseType = licenseType,
        IsActive = isActive,
        MaxActivations = 3,
        MaxDocumentsPerMonth = 5000
    };

    private static MongoLicenseValidator CreateValidatorWithDocument(MongoLicenseValidator.LicenseDocument? document)
    {
        var cursorMock = new Mock<IAsyncCursor<MongoLicenseValidator.LicenseDocument>>();
        var batch = document != null
            ? new List<MongoLicenseValidator.LicenseDocument> { document }
            : new List<MongoLicenseValidator.LicenseDocument>();

        cursorMock.SetupSequence(c => c.MoveNextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)
            .ReturnsAsync(false);
        cursorMock.Setup(c => c.Current).Returns(batch);

        // IMongoCollection<T> in this driver version exposes no 'Find' instance method
        // returning IFindFluent (that is purely an extension method wrapping FindAsync),
        // so the mock targets the real interface method, FindAsync, directly.
        var collectionMock = new Mock<IMongoCollection<MongoLicenseValidator.LicenseDocument>>();
        collectionMock
            .Setup(c => c.FindAsync(
                It.IsAny<FilterDefinition<MongoLicenseValidator.LicenseDocument>>(),
                It.IsAny<FindOptions<MongoLicenseValidator.LicenseDocument, MongoLicenseValidator.LicenseDocument>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(cursorMock.Object);

        var validator = MongoLicenseValidator.GetInstance("mongodb://localhost/", "TestDb");
        var licensesField = typeof(MongoLicenseValidator).GetField("_licenses", BindingFlags.NonPublic | BindingFlags.Instance)!;
        licensesField.SetValue(validator, collectionMock.Object);
        return validator;
    }

    [Fact]
    public async Task ValidateAsync_LicenseNotFound_ReturnsInvalidWithNotFoundMessage()
    {
        var validator = CreateValidatorWithDocument(document: null);

        var result = await validator.ValidateAsync("UNKNOWN-KEY");

        Assert.False(result.IsValid);
        Assert.False(result.IsOfflineMode);
        Assert.Equal("License key not found", result.ErrorMessage);
        Assert.Null(result.LicenseInfo);
    }

    [Fact]
    public async Task ValidateAsync_ExpiredLicense_ReturnsInvalidWithExpiredMessage()
    {
        var doc = MakeDocument("EXP-KEY", DateTime.UtcNow.AddDays(-1), isActive: true);
        var validator = CreateValidatorWithDocument(doc);

        var result = await validator.ValidateAsync(doc.LicenseKey);

        Assert.False(result.IsValid);
        Assert.False(result.IsOfflineMode);
        Assert.Equal("License has expired", result.ErrorMessage);
        Assert.Null(result.LicenseInfo);
    }

    [Fact]
    public async Task ValidateAsync_DeactivatedLicense_ReturnsInvalidWithDeactivatedMessage()
    {
        var doc = MakeDocument("DEACT-KEY", DateTime.UtcNow.AddYears(1), isActive: false);
        var validator = CreateValidatorWithDocument(doc);

        var result = await validator.ValidateAsync(doc.LicenseKey);

        Assert.False(result.IsValid);
        Assert.False(result.IsOfflineMode);
        Assert.Equal("License has been deactivated", result.ErrorMessage);
        Assert.Null(result.LicenseInfo);
    }

    [Fact]
    public async Task ValidateAsync_ValidActiveLicense_ReturnsLicenseInfoMappedFromDocument()
    {
        var doc = MakeDocument("VALID-KEY", DateTime.UtcNow.AddYears(1), isActive: true, licenseType: "Professional");
        var validator = CreateValidatorWithDocument(doc);

        var result = await validator.ValidateAsync(doc.LicenseKey);

        Assert.True(result.IsValid);
        Assert.False(result.IsOfflineMode);
        Assert.NotNull(result.LicenseInfo);
        Assert.Equal(doc.Id, result.LicenseInfo!.LicenseId);
        Assert.Equal(doc.CompanyName, result.LicenseInfo.CompanyName);
        Assert.Equal(doc.Email, result.LicenseInfo.Email);
        Assert.Equal(doc.ExpiryDate, result.LicenseInfo.ExpiryDate);
        Assert.Equal(LicenseType.Professional, result.LicenseInfo.LicenseType);
        Assert.True(result.LicenseInfo.IsActive);
        Assert.Equal(doc.MaxDocumentsPerMonth, result.LicenseInfo.MaxDocumentsPerMonth);
    }

    [Fact]
    public async Task ValidateAsync_UnparsableLicenseType_IsCaughtAndReturnsOfflineError()
    {
        // A malformed LicenseType string makes Enum.Parse<LicenseType> throw (line 77);
        // the outer catch(Exception) must handle it rather than let it propagate.
        var doc = MakeDocument("BAD-TYPE-KEY", DateTime.UtcNow.AddYears(1), isActive: true, licenseType: "NotARealLicenseType");
        var validator = CreateValidatorWithDocument(doc);

        var result = await validator.ValidateAsync(doc.LicenseKey);

        Assert.False(result.IsValid);
        Assert.True(result.IsOfflineMode);
        Assert.NotNull(result.ErrorMessage);
        Assert.StartsWith("Validation error:", result.ErrorMessage);
        Assert.Null(result.LicenseInfo);
    }

    [Fact]
    public async Task ValidateAsync_WhenConnectionFails_ReturnsOfflineModeWithError()
    {
        // No mocking here: a real (unreachable) MongoClient is used to confirm that a
        // genuine driver/connection exception is caught and surfaced as an offline result.
        var validator = MongoLicenseValidator.GetInstance(
            "mongodb://127.0.0.1:1/?connectTimeoutMS=2000&serverSelectionTimeoutMS=2000",
            "HtmlToPdfLicensesTest");

        var result = await validator.ValidateAsync("ANY-LICENSE-KEY");

        Assert.False(result.IsValid);
        Assert.True(result.IsOfflineMode);
        Assert.NotNull(result.ErrorMessage);
        Assert.StartsWith("Validation error:", result.ErrorMessage);
        Assert.Null(result.LicenseInfo);
    }

    [Fact]
    public async Task ValidateAsync_EmptyLicenseKey_DoesNotThrowAndReturnsInvalid()
    {
        var validator = CreateValidatorWithDocument(document: null);

        var result = await validator.ValidateAsync(string.Empty);

        Assert.False(result.IsValid);
        Assert.Equal("License key not found", result.ErrorMessage);
    }

    [Fact]
    public void GetInstance_ReturnsSameSingletonOnSubsequentCalls()
    {
        var first = MongoLicenseValidator.GetInstance("mongodb://127.0.0.1:1/", "db1");
        var second = MongoLicenseValidator.GetInstance("mongodb://127.0.0.1:2/", "db2");

        Assert.Same(first, second);
    }
}
