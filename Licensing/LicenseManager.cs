using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace HtmlToPdfCore.Licensing;

/// <summary>
/// License key manager - validates directly with MongoDB
/// Set once at application startup via configuration
/// </summary>
public class LicenseManager
{
    // MongoDB Configuration - Change these to your values
    private static readonly string MongoConnectionString = "mongodb+srv://readonly_user:33gD6uR5VDXN5b97@cluster0.rxwxwjy.mongodb.net/";
    private static readonly string MongoDatabaseName = "HtmlToPdfLicenses";

    private static LicenseInfo? _cachedLicense;
    private static DateTime _lastValidation = DateTime.MinValue;
    private static readonly TimeSpan ValidationCacheTime = TimeSpan.FromHours(24);
    private static MongoLicenseValidator? _validator;
    private static bool _isInitialized = false;
    private static readonly object _lockObject = new object();
    private static string? _currentLicenseKey;
    private static ILogger? _logger;

    /// <summary>
    /// Set logger for license operations
    /// </summary>
    internal static void SetLogger(ILogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Set license key once at application startup
    /// This is called automatically from configuration
    /// </summary>
    public static async Task<bool> SetLicenseKeyAsync(string licenseKey)
    {
        try
        {
            _logger?.LogInformation("Validating license key with MongoDB...");

            // Initialize validator if needed
            _validator ??= MongoLicenseValidator.GetInstance(MongoConnectionString, MongoDatabaseName);

            // Validate with MongoDB
            var result = await _validator.ValidateAsync(licenseKey);

            if (!result.IsValid && !result.IsOfflineMode)
            {
                var errorMessage = GetUserFriendlyMessage(result.ErrorMessage);
                _logger?.LogError("License validation failed: {ErrorMessage}", errorMessage);
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("\n" + new string('=', 80));
                Console.WriteLine("  LICENSE VALIDATION FAILED");
                Console.WriteLine(new string('=', 80));
                Console.WriteLine(errorMessage);
                Console.WriteLine(new string('=', 80) + "\n");
                Console.ResetColor();
                return false;
            }

            if (result.LicenseInfo == null)
            {
                var errorMessage = GetUserFriendlyMessage("Invalid license information");
                _logger?.LogError("License validation failed: {ErrorMessage}", errorMessage);
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("\n" + new string('=', 80));
                Console.WriteLine("  LICENSE VALIDATION FAILED");
                Console.WriteLine(new string('=', 80));
                Console.WriteLine(errorMessage);
                Console.WriteLine(new string('=', 80) + "\n");
                Console.ResetColor();
                return false;
            }

            _cachedLicense = result.LicenseInfo;
            _lastValidation = DateTime.UtcNow;
            _isInitialized = true;
            _currentLicenseKey = licenseKey;

            _logger?.LogInformation(
                "License validated successfully for {CompanyName} ({Email})",
                result.LicenseInfo.CompanyName,
                result.LicenseInfo.Email
            );

            return true;
        }
        catch (LicenseException)
        {
            throw;
        }
        catch (Exception ex)
        {
            var errorMessage = GetUserFriendlyMessage($"Connection error: {ex.Message}");
            _logger?.LogError(ex, "License validation error");
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("\n" + new string('=', 80));
            Console.WriteLine("  LICENSE VALIDATION ERROR");
            Console.WriteLine(new string('=', 80));
            Console.WriteLine(errorMessage);
            Console.WriteLine(new string('=', 80) + "\n");
            Console.ResetColor();
            return false;
        }
    }

    /// <summary>
    /// Get user-friendly error message with contact information
    /// </summary>
    private static string GetUserFriendlyMessage(string? errorMessage)
    {
        var baseMessage = errorMessage switch
        {
            "License key not found" =>
                "❌ INVALID LICENSE KEY\n\n" +
                "The license key you provided was not found in our system.\n" +
                "This could mean:\n" +
                "  • The license key was entered incorrectly\n" +
                "  • The license key has not been activated yet\n" +
                "  • The license key is for a different product\n",

            "License has expired" =>
                "❌ LICENSE EXPIRED\n\n" +
                "Your license has expired and needs to be renewed.\n" +
                "To continue using HtmlToPdfCore, please renew your license.\n",

            "License has been deactivated" =>
                "❌ LICENSE DEACTIVATED\n\n" +
                "Your license has been deactivated.\n" +
                "This may happen due to:\n" +
                "  • Payment issues\n" +
                "  • Policy violations\n" +
                "  • Manual deactivation request\n",

            var msg when msg?.Contains("Maximum activations") == true =>
                "❌ ACTIVATION LIMIT REACHED\n\n" +
                "Your license has reached the maximum number of machine activations.\n" +
                "Please deactivate the license from an unused machine first.\n",

            var msg when msg?.Contains("Connection") == true || msg?.Contains("timeout") == true =>
                "❌ CONNECTION ERROR\n\n" +
                "Unable to connect to the license validation server.\n" +
                "Please check:\n" +
                "  • Your internet connection\n" +
                "  • Firewall settings\n" +
                "  • MongoDB connection string in the library\n",

            _ =>
                $"❌ LICENSE VALIDATION FAILED\n\n" +
                $"Error: {errorMessage}\n"
        };

        return baseMessage +
               "\n" +
               "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n" +
               "  📧 TO PURCHASE OR RENEW YOUR LICENSE:\n" +
               "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n" +
               "\n" +
               "  Email:    sales@roycelark.com\n" +
               "  Phone:    +91 (9008) 751-562\n" +
               "  Website:  https://roycelark.com/htmltopdf\n" +
               "  Support:  support@roycelark.com\n" +
               "\n" +
               "  Business Hours: Monday - Friday, 9 AM - 5 PM EST\n" +
               "\n";
    }

    /// <summary>
    /// Set license key (synchronous)
    /// Called automatically from configuration
    /// </summary>
    public static bool SetLicenseKey(string licenseKey)
    {
        return SetLicenseKeyAsync(licenseKey).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Internal validation - called automatically by the library
    /// Returns true if valid, false if invalid
    /// </summary>
    internal static bool ValidateLicense()
    {
        if (!_isInitialized || _cachedLicense == null)
        {
            _logger?.LogError("License not initialized");

            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("\n" + new string('=', 80));
            Console.WriteLine("  LICENSE NOT INITIALIZED");
            Console.WriteLine(new string('=', 80));
            Console.WriteLine("\n❌ HtmlToPdfCore license has not been configured.\n");
            Console.WriteLine("Please add license configuration to your Program.cs:\n");
            Console.WriteLine("  builder.Services.AddHtmlToPdfCoreLicense(\"YOUR-LICENSE-KEY\");\n");
            Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            Console.WriteLine("  📧 TO PURCHASE A LICENSE:");
            Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            Console.WriteLine("\n  Email:    sales@roycelark.com");
            Console.WriteLine("  Phone:    +91 (9008) 751-562");
            Console.WriteLine("  Website:  https://roycelark.com/htmltopdf");
            Console.WriteLine("  Support:  support@roycelark.com\n");
            Console.WriteLine(new string('=', 80) + "\n");
            Console.ResetColor();

            return false;
        }

        // Check if license is still valid
        if (_cachedLicense.ExpiryDate < DateTime.UtcNow)
        {
            _logger?.LogError(
                "License expired on {ExpiryDate} for {Email}",
                _cachedLicense.ExpiryDate,
                _cachedLicense.Email
            );

            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("\n" + new string('=', 80));
            Console.WriteLine("  LICENSE EXPIRED");
            Console.WriteLine(new string('=', 80));
            Console.WriteLine($"\n❌ Your license expired on {_cachedLicense.ExpiryDate:yyyy-MM-dd}\n");
            Console.WriteLine($"  Company: {_cachedLicense.CompanyName}");
            Console.WriteLine($"  Email:   {_cachedLicense.Email}\n");
            Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            Console.WriteLine("  📧 TO RENEW YOUR LICENSE:");
            Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            Console.WriteLine("\n  Email:    sales@roycelark.com");
            Console.WriteLine("  Phone:    +91 (9008) 751-562");
            Console.WriteLine("  Website:  https://roycelark.com/htmltopdf/renew");
            Console.WriteLine("  Support:  support@roycelark.com\n");
            Console.WriteLine(new string('=', 80) + "\n");
            Console.ResetColor();

            return false;
        }

        if (!_cachedLicense.IsActive)
        {
            _logger?.LogError(
                "License deactivated for {Email}",
                _cachedLicense.Email
            );

            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("\n" + new string('=', 80));
            Console.WriteLine("  LICENSE DEACTIVATED");
            Console.WriteLine(new string('=', 80));
            Console.WriteLine($"\n❌ Your license has been deactivated.\n");
            Console.WriteLine($"  Company: {_cachedLicense.CompanyName}");
            Console.WriteLine($"  Email:   {_cachedLicense.Email}\n");
            Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            Console.WriteLine("  📧 CONTACT SUPPORT:");
            Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            Console.WriteLine("\n  Email:    support@roycelark.com");
            Console.WriteLine("  Phone:    +91 (9008) 751-562");
            Console.WriteLine("  Website:  https://roycelark.com/support\n");
            Console.WriteLine(new string('=', 80) + "\n");
            Console.ResetColor();

            return false;
        }

        // Check if we need to revalidate (after 24 hours)
        if ((DateTime.UtcNow - _lastValidation) > ValidationCacheTime)
        {
            _logger?.LogInformation("Revalidating license (24h cache expired)...");

            // Revalidate in background (non-blocking)
            Task.Run(async () =>
            {
                try
                {
                    if (_validator != null && _currentLicenseKey != null)
                    {
                        var result = await _validator.ValidateAsync(_currentLicenseKey);
                        if (result.IsValid && result.LicenseInfo != null)
                        {
                            lock (_lockObject)
                            {
                                _cachedLicense = result.LicenseInfo;
                                _lastValidation = DateTime.UtcNow;
                            }
                            _logger?.LogInformation("License revalidated successfully");
                        }
                        else
                        {
                            _logger?.LogWarning("License revalidation failed: {ErrorMessage}", result.ErrorMessage);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "License revalidation error, using cached license");
                }
            });
        }

        return true; // License is valid
    }

    /// <summary>
    /// Get current license information
    /// </summary>
    public static LicenseInfo? GetLicenseInfo()
    {
        return _cachedLicense;
    }

    /// <summary>
    /// Check if license is initialized
    /// </summary>
    public static bool IsInitialized => _isInitialized;
}

/// <summary>
/// License information
/// </summary>
public class LicenseInfo
{
    public string LicenseId { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTime IssueDate { get; set; }
    public DateTime ExpiryDate { get; set; }
    public LicenseType LicenseType { get; set; }
    public bool IsActive { get; set; }
    public int MaxDocumentsPerMonth { get; set; }
}

/// <summary>
/// License types
/// </summary>
public enum LicenseType
{
    Trial,
    Standard,
    Professional,
    Enterprise
}

/// <summary>
/// License exception
/// </summary>
public class LicenseException : Exception
{
    public LicenseException(string message) : base(message) { }
    public LicenseException(string message, Exception innerException) : base(message, innerException) { }
}