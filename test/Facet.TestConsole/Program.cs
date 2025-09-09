using Facet.Extensions;
using Facet.Mapping;
using Facet.TestConsole.Data;
using Facet.TestConsole.Services;
using Facet.TestConsole.Tests;
using Facet.TestConsole.GenerateDtosTests;
using Facet.TestConsole.TestInfrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Facet.TestConsole;

public class TestResult
{
    public string TestName { get; set; } = string.Empty;
    public bool Passed { get; set; }
    public string? ErrorMessage { get; set; }
    public TimeSpan Duration { get; set; }
    public int TestCount { get; set; }
    public int PassedCount { get; set; }
    public int FailedCount { get; set; }
}

public static class TestLogger
{
    private static readonly List<TestResult> _testResults = new();
    
    public static void LogTestStart(string testName)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"\n[RUNNING] {testName}");
        Console.ResetColor();
    }
    
    public static void LogTestResult(string testName, bool passed, string? errorMessage = null, TimeSpan? duration = null, int testCount = 0, int passedCount = 0, int failedCount = 0)
    {
        var result = new TestResult
        {
            TestName = testName,
            Passed = passed,
            ErrorMessage = errorMessage,
            Duration = duration ?? TimeSpan.Zero,
            TestCount = testCount,
            PassedCount = passedCount,
            FailedCount = failedCount
        };
        
        _testResults.Add(result);
        
        if (passed)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            var details = testCount > 0 ? $" ({passedCount}/{testCount} passed)" : "";
            Console.WriteLine($"[PASS] {testName}{details}");
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[FAIL] {testName}: {errorMessage}");
        }
        Console.ResetColor();
    }
    
    public static void PrintSummary()
    {
        Console.WriteLine();
        Console.WriteLine("=".PadRight(80, '='));
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("TEST EXECUTION SUMMARY");
        Console.ResetColor();
        Console.WriteLine("=".PadRight(80, '='));
        
        var totalTests = _testResults.Count;
        var passedTests = _testResults.Count(r => r.Passed);
        var failedTests = totalTests - passedTests;
        var totalDuration = _testResults.Sum(r => r.Duration.TotalMilliseconds);
        var totalSubTests = _testResults.Sum(r => r.TestCount);
        var totalSubPassed = _testResults.Sum(r => r.PassedCount);
        var totalSubFailed = _testResults.Sum(r => r.FailedCount);
        
        // Overall status
        if (failedTests == 0)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"ALL TESTS PASSED!");
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"SOME TESTS FAILED!");
        }
        Console.ResetColor();
        
        Console.WriteLine();
        Console.WriteLine($"Test Suites: {passedTests}/{totalTests} passed");
        if (totalSubTests > 0)
        {
            Console.WriteLine($"Individual Tests: {totalSubPassed}/{totalSubTests} passed");
        }
        Console.WriteLine($"Total Duration: {totalDuration:F0}ms");
        
        // Detailed results
        Console.WriteLine();
        Console.WriteLine("DETAILED RESULTS:");
        Console.WriteLine("-".PadRight(40, '-'));
        
        foreach (var result in _testResults)
        {
            var status = result.Passed ? "[PASS]" : "[FAIL]";
            var details = result.TestCount > 0 ? $" ({result.PassedCount}/{result.TestCount})" : "";
            var duration = result.Duration.TotalMilliseconds > 0 ? $" [{result.Duration.TotalMilliseconds:F0}ms]" : "";
            
            Console.WriteLine($"{status} {result.TestName}{details}{duration}");
            
            if (!result.Passed && !string.IsNullOrEmpty(result.ErrorMessage))
            {
                Console.ForegroundColor = ConsoleColor.DarkRed;
                Console.WriteLine($"   Error: {result.ErrorMessage}");
                Console.ResetColor();
            }
        }
        
        Console.WriteLine();
        Console.WriteLine("=".PadRight(80, '='));
        
        // Exit code for CI/CD
        if (failedTests > 0)
        {
            Environment.ExitCode = 1;
        }
    }
    
    public static void Reset()
    {
        _testResults.Clear();
    }
}

public abstract class BaseEntity
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
}

public class Person : BaseEntity
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public virtual string DisplayName => $"{FirstName} {LastName}";
}

public class Employee : Person
{
    public string EmployeeId { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public decimal Salary { get; set; }
    public DateTime HireDate { get; set; }
    
    // Override the base property
    public override string DisplayName => $"{FirstName} {LastName} ({EmployeeId})";
}

public class Manager : Employee
{
    public string TeamName { get; set; } = string.Empty;
    public int TeamSize { get; set; }
    public decimal Budget { get; set; }
    
    public override string DisplayName => $"Manager {FirstName} {LastName} - {TeamName}";
}

/// <summary>
/// Represents a user account in the system with authentication and profile information.
/// This entity contains both public and sensitive data that should be carefully managed.
/// Users are the primary actors in the system and have roles, permissions, and profile data.
/// </summary>
/// <remarks>
/// Users are created through the registration process and can be activated/deactivated by administrators.
/// The Password field contains sensitive information and should never be exposed in DTOs or API responses.
/// This class follows domain-driven design principles and includes comprehensive validation.
/// </remarks>
/// <example>
/// Creating a new user:
/// <code>
/// var user = new User
/// {
///     FirstName = "John",
///     LastName = "Doe", 
///     Email = "john.doe@example.com",
///     IsActive = true
/// };
/// </code>
/// </example>
public class User
{
    /// <summary>
    /// Gets or sets the unique identifier for the user account.
    /// This is the primary key used for database operations and foreign key relationships.
    /// </summary>
    /// <value>A positive integer that uniquely identifies the user in the system.</value>
    /// <example>12345</example>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the user's first name or given name.
    /// Used for display purposes and personalization throughout the application.
    /// </summary>
    /// <value>The first name of the user, typically 1-50 characters in length.</value>
    /// <remarks>
    /// This field is required and will be validated for length and appropriate content.
    /// Special characters and numbers are generally not allowed in names.
    /// </remarks>
    /// <example>John</example>
    public string FirstName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the user's last name, surname, or family name.
    /// Combined with FirstName to create the user's full display name for the UI.
    /// </summary>
    /// <value>The last name of the user, typically 1-50 characters in length.</value>
    /// <remarks>
    /// This field is required and follows the same validation rules as FirstName.
    /// In some cultures, this might include multiple family names separated by spaces.
    /// </remarks>
    /// <example>Doe</example>
    public string LastName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the user's email address used for authentication and communication.
    /// This serves as the primary means of user identification and must be unique across the entire system.
    /// </summary>
    /// <value>A valid email address in standard RFC 5322 format (user@domain.com).</value>
    /// <remarks>
    /// Email addresses must be unique across the system and are used for login authentication.
    /// The system will send verification emails to this address during registration and password resets.
    /// This field is required and must pass email validation.
    /// </remarks>
    /// <example>john.doe@example.com</example>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the user's date of birth for age verification and personalization features.
    /// Used to calculate age, ensure compliance with age-related restrictions, and birthday notifications.
    /// </summary>
    /// <value>A DateTime representing the user's birth date in UTC.</value>
    /// <remarks>
    /// This information is optional but helps with age verification for certain features.
    /// The date should be in the past and reasonable (not more than 150 years ago).
    /// Used for analytics, age-gated content, and compliance with regulations like COPPA.
    /// </remarks>
    /// <example>1990-05-15</example>
    public DateTime DateOfBirth { get; set; }

    /// <summary>
    /// Gets or sets the hashed password for user authentication.
    /// WARNING: This contains sensitive security information and should NEVER be exposed in public APIs, DTOs, or logs.
    /// </summary>
    /// <value>A securely hashed password string using industry-standard cryptographic algorithms.</value>
    /// <remarks>
    /// Passwords are hashed using bcrypt, scrypt, or similar algorithms before storage.
    /// This field should always be excluded when creating DTOs for API responses.
    /// Raw passwords should never be stored in the database - only secure hashes.
    /// Access to this field should be strictly controlled and audited.
    /// </remarks>
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the user account is active and can authenticate.
    /// Inactive users cannot log in but their data is preserved for potential reactivation and audit trails.
    /// </summary>
    /// <value><c>true</c> if the user can log in and access the system; otherwise, <c>false</c>.</value>
    /// <remarks>
    /// Administrators can deactivate users instead of deleting them to preserve audit trails and data integrity.
    /// Inactive users will receive appropriate error messages when attempting to authenticate.
    /// This field is used for soft deletion and account suspension functionality.
    /// </remarks>
    /// <example>true</example>
    public bool IsActive { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the user account was created in the system.
    /// Used for audit purposes, analytics, and tracking user registration patterns over time.
    /// </summary>
    /// <value>A DateTime representing when the user was first registered, stored in UTC.</value>
    /// <remarks>
    /// This field is automatically set during user creation and should not be modified afterward.
    /// Used for compliance reporting, analytics dashboards, and data retention policies.
    /// Essential for audit trails and regulatory compliance requirements.
    /// </remarks>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the timestamp of the user's last successful authentication session.
    /// Used for security monitoring, user engagement analytics, and detecting inactive accounts.
    /// </summary>
    /// <value>A nullable DateTime representing the last login time in UTC, or null if the user has never logged in.</value>
    /// <remarks>
    /// This field is updated automatically during the authentication process on successful login.
    /// A null value indicates the user has never successfully authenticated since account creation.
    /// Used for security analysis, detecting dormant accounts, and user engagement metrics.
    /// Important for compliance with data retention and account activity policies.
    /// </remarks>
    /// <example>2023-12-01T10:30:00Z</example>
    public DateTime? LastLoginAt { get; set; }
}

public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int CategoryId { get; set; }
    public bool IsAvailable { get; set; }
    public DateTime CreatedAt { get; set; }
    public string InternalNotes { get; set; } = string.Empty;
}

public class UserDtoWithMappingMapper : IFacetMapConfiguration<User, UserDtoWithMapping>
{
    public static void Map(User source, UserDtoWithMapping target)
    {
        target.FullName = $"{source.FirstName} {source.LastName}";
        target.Age = CalculateAge(source.DateOfBirth);
    }

    private static int CalculateAge(DateTime birthDate)
    {
        var today = DateTime.Today;
        var age = today.Year - birthDate.Year;
        if (birthDate.Date > today.AddYears(-age)) age--;
        return age;
    }
}

[Facet(typeof(Employee), "Salary", "CreatedBy")]
public partial class EmployeeDto;

[Facet(typeof(Manager), "Salary", "Budget", "CreatedBy")]
public partial class ManagerDto;

[Facet(typeof(User), "Password", "CreatedAt")]
public partial class UserDto 
{
    public string FullName { get; set; } = string.Empty;
    public int Age { get; set; }
}

[Facet(typeof(User), "Password", "CreatedAt", Configuration = typeof(UserDtoWithMappingMapper))]
public partial class UserDtoWithMapping 
{
    public string FullName { get; set; } = string.Empty;
    public int Age { get; set; }
}

// Add test facets with parameterless constructor enabled by default
[Facet(typeof(User), "Password", "CreatedAt")]
public partial class UserDtoWithParameterlessConstructor 
{
    public string FullName { get; set; } = string.Empty;
    public int Age { get; set; }
}

[Facet(typeof(Product), "InternalNotes", Kind = FacetKind.Record)]
public partial record ProductDtoWithParameterlessConstructor;

[Facet(typeof(Product), "InternalNotes", Kind = FacetKind.Record)]
public partial record ProductDto;

[Facet(typeof(Product), "InternalNotes", "CreatedAt", Kind = FacetKind.Struct)]
public partial struct ProductSummary;

[Facet(typeof(User), "Password", "CreatedAt", "LastLoginAt", Kind = FacetKind.RecordStruct)]
public partial record struct UserSummary;

public record ModernUser
{
    public required string Id { get; init; }
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public string? Email { get; set; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public string? Bio { get; set; }
    public string? PasswordHash { get; init; }
}

public record struct CompactUser(string Id, string Name, DateTime CreatedAt);

[Facet(typeof(ModernUser), "PasswordHash", "Bio")]
public partial record ModernUserDto
{
    public string FullName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
}

[Facet(typeof(CompactUser))]
public partial record struct CompactUserDto;

[Facet(typeof(ModernUser), "PasswordHash", "Bio", "Email")]
public partial class ModernUserClass
{
    public string? AdditionalInfo { get; set; }
}

// Test models for nested partials feature
public partial class OuterContainer
{
    [Facet(typeof(User), "Password", "CreatedAt")]
    public partial class NestedUserDto
    {
        public string FullName { get; set; } = string.Empty;
        public int Age { get; set; }
    }

    [Facet(typeof(Product), "InternalNotes", Kind = FacetKind.Record)]
    public partial record NestedProductDto;

    public partial class InnerContainer
    {
        [Facet(typeof(Employee), "Salary", "CreatedBy")]
        public partial class DeeplyNestedEmployeeDto;

        [Facet(typeof(Manager), "Salary", "Budget", "CreatedBy", Kind = FacetKind.RecordStruct)]
        public partial record struct DeeplyNestedManagerSummary;
    }
}

public class Program
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("=== Facet Generator Test Console ===\n");
        var host = CreateHostBuilder(args).Build();
        
        await TestRunner.RunAllTestsAsync(host);
        
        // Only try to read key if we have console input available
        if (IsConsoleInputAvailable())
        {
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
        else
        {
            Console.WriteLine("Running in CI environment - exiting automatically.");
        }
    }

    private static bool IsConsoleInputAvailable()
    {
        try
        {
            // Check if console input is redirected or unavailable
            return !Console.IsInputRedirected && Environment.UserInteractive;
        }
        catch
        {
            return false;
        }
    }

    private static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((context, config) =>
            {
                config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
            })
            .ConfigureServices((context, services) =>
            {
                // Use SQLite instead of SQL Server for cross-platform compatibility
                services.AddDbContext<FacetTestDbContext>(options =>
                    options.UseSqlite(context.Configuration.GetConnectionString("DefaultConnection")));

                services.AddScoped<IUserService, UserService>();
                services.AddScoped<IProductService, ProductService>();
                
                services.AddScoped<UpdateFromFacetTests>();
                services.AddScoped<EfCoreIntegrationTests>();
                services.AddScoped<ValidationAndErrorTests>();
                services.AddScoped<GenerateDtosFeatureTests>();

                // Add logging
                services.AddLogging(builder =>
                {
                    builder.AddConsole();
                    builder.SetMinimumLevel(LogLevel.Information);
                });
            });

    public static async Task InitializeDatabaseAsync(IHost host)
    {
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<FacetTestDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

        try
        {
            logger.LogInformation("Ensuring database is created...");
            await context.Database.EnsureCreatedAsync();
            logger.LogInformation("Database initialized successfully");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to initialize database");
            throw;
        }
    }
}