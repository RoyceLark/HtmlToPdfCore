using HtmlToPdfCore.Licensing;

namespace HtmlToPdfCore.Tests;

/// <summary>
/// Test double for ILicenseValidator so LicenseManager behaviour can be
/// unit tested without contacting MongoDB.
/// </summary>
internal class FakeLicenseValidator : ILicenseValidator
{
    private readonly Func<string, MongoLicenseValidator.ValidationResult>? _resultFactory;
    private readonly Exception? _exceptionToThrow;

    public string? LastLicenseKeyRequested { get; private set; }

    private FakeLicenseValidator(
        Func<string, MongoLicenseValidator.ValidationResult>? resultFactory,
        Exception? exceptionToThrow)
    {
        _resultFactory = resultFactory;
        _exceptionToThrow = exceptionToThrow;
    }

    public static FakeLicenseValidator ReturningResult(MongoLicenseValidator.ValidationResult result)
        => new FakeLicenseValidator(_ => result, null);

    public static FakeLicenseValidator Throwing(Exception exception)
        => new FakeLicenseValidator(null, exception);

    public Task<MongoLicenseValidator.ValidationResult> ValidateAsync(string licenseKey)
    {
        LastLicenseKeyRequested = licenseKey;

        if (_exceptionToThrow != null)
        {
            throw _exceptionToThrow;
        }

        return Task.FromResult(_resultFactory!(licenseKey));
    }
}
