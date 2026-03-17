using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HtmlToPdfCore.Licensing;

/// <summary>
/// Configuration extensions for license setup
/// </summary>
public static class LicenseConfiguration
{
    /// <summary>
    /// Configure HtmlToPdfCore with license key from configuration
    /// Add this in Program.cs: builder.Services.AddHtmlToPdfCoreLicense(configuration)
    /// </summary>
    public static IServiceCollection AddHtmlToPdfCoreLicense(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var licenseKey = configuration["HtmlToPdfCore:LicenseKey"];

        if (string.IsNullOrEmpty(licenseKey))
        {
            throw new LicenseException(
                "License key not found in configuration. " +
                "Please add 'HtmlToPdfCore:LicenseKey' to appsettings.json"
            );
        }

        // Build service provider to get logger
        var serviceProvider = services.BuildServiceProvider();
        var logger = serviceProvider.GetService<ILogger<LicenseManager>>();

        // Initialize license at startup
        try
        {
            logger?.LogInformation("Initializing HtmlToPdfCore license...");

            LicenseManager.SetLicenseKey(licenseKey);

            var licenseInfo = LicenseManager.GetLicenseInfo();
            if (licenseInfo != null)
            {
                logger?.LogInformation(
                    "✓ License validated successfully\n" +
                    "  Company: {CompanyName}\n" +
                    "  Email: {Email}\n" +
                    "  Type: {LicenseType}\n" +
                    "  Expires: {ExpiryDate:yyyy-MM-dd}\n" +
                    "  Max Documents/Month: {MaxDocs}",
                    licenseInfo.CompanyName,
                    licenseInfo.Email,
                    licenseInfo.LicenseType,
                    licenseInfo.ExpiryDate,
                    licenseInfo.MaxDocumentsPerMonth
                );
            }
        }
        catch (LicenseException ex)
        {
            logger?.LogError(
                "✗ License validation failed: {ErrorMessage}",
                ex.Message
            );
            throw;
        }

        return services;
    }

    /// <summary>
    /// Configure HtmlToPdfCore with license key directly
    /// Add this in Program.cs: builder.Services.AddHtmlToPdfCoreLicense("YOUR-KEY")
    /// </summary>
    public static IServiceCollection AddHtmlToPdfCoreLicense(
        this IServiceCollection services,
        string licenseKey)
    {
        if (string.IsNullOrEmpty(licenseKey))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("\n" + new string('=', 80));
            Console.WriteLine("  LICENSE KEY MISSING");
            Console.WriteLine(new string('=', 80));
            Console.WriteLine("\n❌ License key is required to use HtmlToPdfCore.\n");
            Console.WriteLine("Please provide a valid license key in your configuration.\n");
            Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            Console.WriteLine("  📧 TO PURCHASE A LICENSE:");
            Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            Console.WriteLine("\n  Email:    web.html123@gmail.com");
            Console.WriteLine("  Phone:    +91 (9008) 751-562");
            Console.WriteLine("  Website:  https://RoyceLark.com/htmltopdf");
            Console.WriteLine("  Support:  support@RoyceLark.com\n");
            Console.WriteLine(new string('=', 80) + "\n");
            Console.ResetColor();
            return services;
        }

        // Build service provider to get logger
        var serviceProvider = services.BuildServiceProvider();
        var logger = serviceProvider.GetService<ILogger<LicenseManager>>();

        // Initialize license at startup
        try
        {
            logger?.LogInformation("Initializing HtmlToPdfCore license...");

            var isValid = LicenseManager.SetLicenseKey(licenseKey);

            if (!isValid)
            {
                logger?.LogWarning("License validation failed. Application will start but PDF generation will be disabled.");
                return services;
            }

            var licenseInfo = LicenseManager.GetLicenseInfo();
            if (licenseInfo != null)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("\n" + new string('=', 80));
                Console.WriteLine("  ✓ LICENSE VALIDATED SUCCESSFULLY");
                Console.WriteLine(new string('=', 80));
                Console.WriteLine($"\n  Company:          {licenseInfo.CompanyName}");
                Console.WriteLine($"  Email:            {licenseInfo.Email}");
                Console.WriteLine($"  License Type:     {licenseInfo.LicenseType}");
                Console.WriteLine($"  Expires:          {licenseInfo.ExpiryDate:yyyy-MM-dd}");
                Console.WriteLine($"  Max Docs/Month:   {licenseInfo.MaxDocumentsPerMonth:N0}");
                Console.WriteLine("\n" + new string('=', 80) + "\n");
                Console.ResetColor();

                logger?.LogInformation(
                    "✓ License validated successfully\n" +
                    "  Company: {CompanyName}\n" +
                    "  Email: {Email}\n" +
                    "  Type: {LicenseType}\n" +
                    "  Expires: {ExpiryDate:yyyy-MM-dd}\n" +
                    "  Max Documents/Month: {MaxDocs}",
                    licenseInfo.CompanyName,
                    licenseInfo.Email,
                    licenseInfo.LicenseType,
                    licenseInfo.ExpiryDate,
                    licenseInfo.MaxDocumentsPerMonth
                );
            }
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Error during license initialization");
        }

        return services;
    }
}