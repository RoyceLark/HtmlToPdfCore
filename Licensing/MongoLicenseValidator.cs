using MongoDB.Driver;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace HtmlToPdfCore.Licensing;

/// <summary>
/// Direct MongoDB license validator
/// </summary>
internal class MongoLicenseValidator
{
    private readonly IMongoCollection<LicenseDocument> _licenses;
    private readonly IMongoCollection<ActivationDocument> _activations;
    private static MongoLicenseValidator? _instance;

    private MongoLicenseValidator(string connectionString, string databaseName)
    {
        var client = new MongoClient(connectionString);
        var database = client.GetDatabase(databaseName);

        _licenses = database.GetCollection<LicenseDocument>("licenses");
        _activations = database.GetCollection<ActivationDocument>("activations");
    }

    public static MongoLicenseValidator GetInstance(string connectionString, string databaseName)
    {
        _instance ??= new MongoLicenseValidator(connectionString, databaseName);
        return _instance;
    }

    public async Task<ValidationResult> ValidateAsync(string licenseKey)
    {
        try
        {
            // Find license in MongoDB
            var license = await _licenses.Find(l => l.LicenseKey == licenseKey).FirstOrDefaultAsync();

            if (license == null)
            {
                return new ValidationResult
                {
                    IsValid = false,
                    ErrorMessage = "License key not found"
                };
            }

            // Check expiry
            if (license.ExpiryDate < DateTime.UtcNow)
            {
                return new ValidationResult
                {
                    IsValid = false,
                    ErrorMessage = "License has expired"
                };
            }

            // Check active status
            if (!license.IsActive)
            {
                return new ValidationResult
                {
                    IsValid = false,
                    ErrorMessage = "License has been deactivated"
                };
            }

            return new ValidationResult
            {
                IsValid = true,
                LicenseInfo = new LicenseInfo
                {
                    LicenseId = license.Id,
                    CompanyName = license.CompanyName,
                    Email = license.Email,
                    IssueDate = license.IssueDate,
                    ExpiryDate = license.ExpiryDate,
                    LicenseType = Enum.Parse<LicenseType>(license.LicenseType, true),
                    IsActive = license.IsActive,
                    MaxDocumentsPerMonth = license.MaxDocumentsPerMonth
                }
            };
        }
        catch (Exception ex)
        {
            return new ValidationResult
            {
                IsValid = false,
                ErrorMessage = $"Validation error: {ex.Message}",
                IsOfflineMode = true
            };
        }
    }

    /// <summary>
    /// License document in MongoDB
    /// </summary>
    internal class LicenseDocument
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;

        [BsonElement("licenseKey")]
        public string LicenseKey { get; set; } = string.Empty;

        [BsonElement("companyName")]
        public string CompanyName { get; set; } = string.Empty;

        [BsonElement("email")]
        public string Email { get; set; } = string.Empty;

        [BsonElement("issueDate")]
        public DateTime IssueDate { get; set; }

        [BsonElement("expiryDate")]
        public DateTime ExpiryDate { get; set; }

        [BsonElement("licenseType")]
        public string LicenseType { get; set; } = "Standard";

        [BsonElement("isActive")]
        public bool IsActive { get; set; } = true;

        [BsonElement("maxActivations")]
        public int MaxActivations { get; set; } = 3;

        [BsonElement("maxDocumentsPerMonth")]
        public int MaxDocumentsPerMonth { get; set; } = 10000;
    }

    /// <summary>
    /// Activation document in MongoDB
    /// </summary>
    internal class ActivationDocument
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;

        [BsonElement("licenseKey")]
        public string LicenseKey { get; set; } = string.Empty;

        [BsonElement("machineId")]
        public string MachineId { get; set; } = string.Empty;

        [BsonElement("machineName")]
        public string MachineName { get; set; } = string.Empty;

        [BsonElement("activationTime")]
        public DateTime ActivationTime { get; set; }

        [BsonElement("lastValidation")]
        public DateTime LastValidation { get; set; }

        [BsonElement("validationCount")]
        public int ValidationCount { get; set; } = 0;
    }

    /// <summary>
    /// Validation result
    /// </summary>
    internal class ValidationResult
    {
        public bool IsValid { get; set; }
        public bool IsOfflineMode { get; set; }
        public string? ErrorMessage { get; set; }
        public LicenseInfo? LicenseInfo { get; set; }
    }
}