using HtmlToPdfCore.Licensing;
using ValidationResult = HtmlToPdfCore.Licensing.MongoLicenseValidator.ValidationResult;

namespace HtmlToPdfCore.Tests.Licensing;

/// <summary>
/// Test double for <see cref="ILicenseValidator"/> that lets tests return a canned
/// <see cref="ValidationResult"/> (or throw) without talking to MongoDB.
/// </summary>
internal class FakeLicenseValidator : ILicenseValidator
{
    private readonly Func<string, Task<ValidationResult>> _handler;
    public string? LastRequestedKey { get; private set; }

    public FakeLicenseValidator(Func<string, Task<ValidationResult>> handler)
    {
        _handler = handler;
    }

    public static FakeLicenseValidator ReturningResult(ValidationResult result) =>
        new FakeLicenseValidator(_ => Task.FromResult(result));

    public static FakeLicenseValidator Throwing(Exception exception) =>
        new FakeLicenseValidator(_ => throw exception);

    public Task<ValidationResult> ValidateAsync(string licenseKey)
    {
        LastRequestedKey = licenseKey;
        return _handler(licenseKey);
    }

    public static ValidationResult Valid(LicenseInfo info) => new ValidationResult
    {
        IsValid = true,
        LicenseInfo = info
    };

    public static ValidationResult NotFound() => new ValidationResult
    {
        IsValid = false,
        ErrorMessage = "License key not found"
    };

    public static ValidationResult Expired() => new ValidationResult
    {
        IsValid = false,
        ErrorMessage = "License has expired"
    };

    public static ValidationResult Deactivated() => new ValidationResult
    {
        IsValid = false,
        ErrorMessage = "License has been deactivated"
    };

    public static ValidationResult Offline(string errorMessage) => new ValidationResult
    {
        IsValid = false,
        ErrorMessage = errorMessage,
        IsOfflineMode = true
    };
}
