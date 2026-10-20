using HtmlToPdfCore.Licensing;

namespace HtmlToPdfCore.Tests.Licensing;

/// <summary>
/// Fake implementation of the internal ILicenseValidator used so licensing logic
/// can be unit tested without a live MongoDB connection.
/// </summary>
internal class FakeLicenseValidator : ILicenseValidator
{
    private readonly Func<string, Task<MongoLicenseValidator.ValidationResult>>? _handler;
    private readonly MongoLicenseValidator.ValidationResult? _fixedResult;
    private readonly Exception? _exceptionToThrow;

    public FakeLicenseValidator(MongoLicenseValidator.ValidationResult fixedResult)
    {
        _fixedResult = fixedResult;
    }

    public FakeLicenseValidator(Exception exceptionToThrow)
    {
        _exceptionToThrow = exceptionToThrow;
    }

    public FakeLicenseValidator(Func<string, Task<MongoLicenseValidator.ValidationResult>> handler)
    {
        _handler = handler;
    }

    public Task<MongoLicenseValidator.ValidationResult> ValidateAsync(string licenseKey)
    {
        if (_exceptionToThrow != null)
        {
            throw _exceptionToThrow;
        }

        if (_handler != null)
        {
            return _handler(licenseKey);
        }

        return Task.FromResult(_fixedResult!);
    }
}
