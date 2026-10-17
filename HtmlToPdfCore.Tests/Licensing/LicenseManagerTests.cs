using System.Reflection;
using HtmlToPdfCore.Licensing;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using Moq;
using Xunit;

namespace HtmlToPdfCore.Tests.Licensing;

/// <summary>
/// Tests for LicenseManager's validation decision logic. LicenseManager keeps its
/// state in private static fields, so every test resets that state in its
/// constructor to stay independent of test order. To exercise SetLicenseKeyAsync's
/// branches precisely (success / explicit invalid / offline-caught-error), the
/// shared MongoLicenseValidator singleton's private '_licenses' collection is
/// replaced with a Moq mock via reflection, and the resulting validator instance is
/// assigned to LicenseManager's private '_validator' field so no real MongoDB is used.
/// </summary>
[Collection("LicensingSharedState")]
public class LicenseManagerTests
{
    public LicenseManagerTests()
    {
        ResetState();
    }

    private static void ResetState()
    {
        var type = typeof(LicenseManager);
        ReflectionHelper.SetStaticField(type, "_cachedLicense", null);
        ReflectionHelper.SetStaticField(type, "_lastValidation", DateTime.MinValue);
        ReflectionHelper.SetStaticField(type, "_isInitialized", false);
        ReflectionHelper.SetStaticField(type, "_validator", null);
        ReflectionHelper.SetStaticField(type, "_currentLicenseKey", null);
        ReflectionHelper.SetStaticField(type, "_logger", null);
        ReflectionHelper.SetStaticField(typeof(MongoLicenseValidator), "_instance", null);
    }

    /// <summary>
    /// Builds a Mock&lt;ILogger&gt; whose Log(...) call throws the given exception, so
    /// installing it via LicenseManager.SetLogger makes the very first statement in
    /// SetLicenseKeyAsync's try block (_logger?.LogInformation(...), LicenseManager.cs:43)
    /// throw deterministically - without needing any real MongoDB interaction - to drive
    /// the method's own catch blocks (lines 92-95 and 96-108).
    /// </summary>
    private static void InstallThrowingLogger(Exception exceptionToThrow)
    {
        // Only the first Log(...) call (LicenseManager.cs:43, 'Validating license key...')
        // throws; later LogError calls made from inside the catch blocks themselves must
        // succeed, otherwise the catch-block logging call would itself throw unhandled.
        var callCount = 0;
        var loggerMock = new Mock<ILogger>();
        loggerMock
            .Setup(l => l.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()))
            .Callback(() =>
            {
                callCount++;
                if (callCount == 1)
                {
                    throw exceptionToThrow;
                }
            });

        LicenseManager.SetLogger(loggerMock.Object);
    }

    private static LicenseInfo MakeLicenseInfo(DateTime expiryDate, bool isActive) => new LicenseInfo
    {
        LicenseId = "abc123",
        CompanyName = "Acme Corp",
        Email = "ops@acme.example",
        IssueDate = DateTime.UtcNow.AddYears(-1),
        ExpiryDate = expiryDate,
        LicenseType = LicenseType.Professional,
        IsActive = isActive,
        MaxDocumentsPerMonth = 5000
    };

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

    /// <summary>
    /// Builds a MongoLicenseValidator backed by a mocked IMongoCollection so
    /// LicenseManager.SetLicenseKeyAsync can be driven through specific ValidateAsync
    /// outcomes without a real MongoDB connection.
    /// </summary>
    private static void InstallMockedValidator(MongoLicenseValidator.LicenseDocument? document)
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

        ReflectionHelper.SetStaticField(typeof(LicenseManager), "_validator", validator);
    }

    [Fact]
    public void ValidateLicense_NotInitialized_ReturnsFalse()
    {
        // _isInitialized and _cachedLicense are left at defaults (missing license).
        var result = LicenseManager.ValidateLicense();

        Assert.False(result);
    }

    [Fact]
    public void ValidateLicense_ExpiredLicense_ReturnsFalse()
    {
        ReflectionHelper.SetStaticField(typeof(LicenseManager), "_isInitialized", true);
        ReflectionHelper.SetStaticField(typeof(LicenseManager), "_cachedLicense", MakeLicenseInfo(DateTime.UtcNow.AddDays(-1), isActive: true));
        ReflectionHelper.SetStaticField(typeof(LicenseManager), "_lastValidation", DateTime.UtcNow);

        var result = LicenseManager.ValidateLicense();

        Assert.False(result);
    }

    [Fact]
    public void ValidateLicense_DeactivatedLicense_ReturnsFalse()
    {
        ReflectionHelper.SetStaticField(typeof(LicenseManager), "_isInitialized", true);
        ReflectionHelper.SetStaticField(typeof(LicenseManager), "_cachedLicense", MakeLicenseInfo(DateTime.UtcNow.AddYears(1), isActive: false));
        ReflectionHelper.SetStaticField(typeof(LicenseManager), "_lastValidation", DateTime.UtcNow);

        var result = LicenseManager.ValidateLicense();

        Assert.False(result);
    }

    [Fact]
    public void ValidateLicense_ValidLicenseWithinCacheWindow_ReturnsTrue()
    {
        ReflectionHelper.SetStaticField(typeof(LicenseManager), "_isInitialized", true);
        ReflectionHelper.SetStaticField(typeof(LicenseManager), "_cachedLicense", MakeLicenseInfo(DateTime.UtcNow.AddYears(1), isActive: true));
        // Within the 24h validation cache window, so no background revalidation is triggered.
        ReflectionHelper.SetStaticField(typeof(LicenseManager), "_lastValidation", DateTime.UtcNow);

        var result = LicenseManager.ValidateLicense();

        Assert.True(result);
    }

    [Fact]
    public void GetLicenseInfo_ReturnsCachedLicense()
    {
        var license = MakeLicenseInfo(DateTime.UtcNow.AddYears(1), isActive: true);
        ReflectionHelper.SetStaticField(typeof(LicenseManager), "_cachedLicense", license);

        var result = LicenseManager.GetLicenseInfo();

        Assert.Same(license, result);
    }

    [Fact]
    public void GetLicenseInfo_WhenNotSet_ReturnsNull()
    {
        var result = LicenseManager.GetLicenseInfo();

        Assert.Null(result);
    }

    [Fact]
    public async Task SetLicenseKeyAsync_ValidLicenseFound_ReturnsTrueAndCachesLicenseInfo()
    {
        var doc = MakeDocument("VALID-KEY", DateTime.UtcNow.AddYears(1), isActive: true);
        InstallMockedValidator(doc);

        var result = await LicenseManager.SetLicenseKeyAsync(doc.LicenseKey);

        Assert.True(result);
        Assert.True(LicenseManager.IsInitialized);
        var info = LicenseManager.GetLicenseInfo();
        Assert.NotNull(info);
        Assert.Equal(doc.CompanyName, info!.CompanyName);
        Assert.Equal(doc.Email, info.Email);
        Assert.Equal(LicenseType.Professional, info.LicenseType);
    }

    [Fact]
    public async Task SetLicenseKeyAsync_LicenseKeyNotFound_ReturnsFalseAndDoesNotInitialize()
    {
        // Exercises the explicit '!result.IsValid && !result.IsOfflineMode' branch
        // (LicenseManager.cs:51-63) since the validator returns a non-offline failure.
        InstallMockedValidator(document: null);

        var result = await LicenseManager.SetLicenseKeyAsync("UNKNOWN-KEY");

        Assert.False(result);
        Assert.False(LicenseManager.IsInitialized);
        Assert.Null(LicenseManager.GetLicenseInfo());
    }

    [Fact]
    public async Task SetLicenseKeyAsync_ValidatorOfflineError_HitsLicenseInfoNullBranchAndReturnsFalse()
    {
        // A caught connection/validation exception inside MongoLicenseValidator.ValidateAsync
        // returns IsOfflineMode=true with no LicenseInfo, so SetLicenseKeyAsync's
        // '!result.IsValid && !result.IsOfflineMode' check is false (skipped) and it
        // falls through to the 'result.LicenseInfo == null' branch (LicenseManager.cs:65-77).
        var validator = MongoLicenseValidator.GetInstance(
            "mongodb://127.0.0.1:1/?connectTimeoutMS=2000&serverSelectionTimeoutMS=2000",
            "HtmlToPdfLicensesTest");
        ReflectionHelper.SetStaticField(typeof(LicenseManager), "_validator", validator);

        var result = await LicenseManager.SetLicenseKeyAsync("ANY-KEY");

        Assert.False(result);
        Assert.False(LicenseManager.IsInitialized);
        Assert.Null(LicenseManager.GetLicenseInfo());
    }

    [Fact]
    public async Task SetLicenseKeyAsync_EmptyLicenseKey_ReturnsFalseWithoutThrowing()
    {
        InstallMockedValidator(document: null);

        var result = await LicenseManager.SetLicenseKeyAsync(string.Empty);

        Assert.False(result);
    }

    [Fact]
    public async Task SetLicenseKeyAsync_UnexpectedNonLicenseException_IsCaughtAndReturnsFalse()
    {
        // Drives LicenseManager.cs:96-108 (the generic catch(Exception) branch) via a
        // non-LicenseException thrown before any MongoDB interaction happens.
        InstallThrowingLogger(new InvalidOperationException("mock logger failure"));

        var result = await LicenseManager.SetLicenseKeyAsync("ANY-KEY");

        Assert.False(result);
        Assert.False(LicenseManager.IsInitialized);
    }

    [Fact]
    public async Task SetLicenseKeyAsync_LicenseException_IsRethrownWithoutBeingSwallowed()
    {
        // Drives LicenseManager.cs:92-95 (catch (LicenseException) { throw; }): a
        // LicenseException raised inside the try block must propagate out of
        // SetLicenseKeyAsync unchanged, not be converted into a 'return false'.
        var expected = new LicenseException("mock license failure");
        InstallThrowingLogger(expected);

        var actual = await Assert.ThrowsAsync<LicenseException>(() => LicenseManager.SetLicenseKeyAsync("ANY-KEY"));

        Assert.Same(expected, actual);
        Assert.False(LicenseManager.IsInitialized);
    }

    [Theory]
    [InlineData("License key not found", "INVALID LICENSE KEY")]
    [InlineData("License has expired", "LICENSE EXPIRED")]
    [InlineData("License has been deactivated", "LICENSE DEACTIVATED")]
    [InlineData("Maximum activations reached", "ACTIVATION LIMIT REACHED")]
    [InlineData("Connection timeout to server", "CONNECTION ERROR")]
    [InlineData("Something unexpected", "LICENSE VALIDATION FAILED")]
    [InlineData(null, "LICENSE VALIDATION FAILED")]
    public void GetUserFriendlyMessage_MapsErrorsToExpectedMessage(string? errorMessage, string expectedFragment)
    {
        var message = (string)ReflectionHelper.InvokeStaticMethod(
            typeof(LicenseManager), "GetUserFriendlyMessage", new object?[] { errorMessage })!;

        Assert.Contains(expectedFragment, message);
    }
}
