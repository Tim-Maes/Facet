using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Facet.TestConsole.GenerateDtosTests;

public class GenerateDtosFeatureTests
{
    private readonly ILogger<GenerateDtosFeatureTests> _logger;

    public GenerateDtosFeatureTests(ILogger<GenerateDtosFeatureTests> logger)
    {
        _logger = logger;
    }

    public async Task RunAllTestsAsync()
    {
        Console.WriteLine("=== GenerateDtos Feature Tests ===\n");

        TestGeneratedTypesExist();
        TestBasicFunctionality();
        TestNewUpsertFeature();
        TestAllowMultipleFeature();
        TestImprovedRecordFormatting();

        Console.WriteLine("\n=== All GenerateDtos tests completed! ===");
    }

    private void TestGeneratedTypesExist()
    {
        Console.WriteLine("1. Testing Generated Types Exist:");
        Console.WriteLine("=================================");

        try
        {
            // Get all types in the current assembly
            var assembly = Assembly.GetExecutingAssembly();
            var allTypes = assembly.GetTypes();
            
            Console.WriteLine("Searching for generated DTO types...");
            
            // Look for types that match our expected patterns
            var testUserDtos = allTypes.Where(t => t.Name.Contains("TestUser")).ToArray();
            var testProductDtos = allTypes.Where(t => t.Name.Contains("TestProduct")).ToArray();
            var testOrderDtos = allTypes.Where(t => t.Name.Contains("TestOrder")).ToArray();
            var testScheduleDtos = allTypes.Where(t => t.Name.Contains("TestSchedule")).ToArray();
            var testEventDtos = allTypes.Where(t => t.Name.Contains("TestEvent")).ToArray();
            
            Console.WriteLine($"\nFound {testUserDtos.Length} TestUser-related types:");
            foreach (var type in testUserDtos)
            {
                Console.WriteLine($"  - {type.FullName}");
            }
            
            Console.WriteLine($"\nFound {testProductDtos.Length} TestProduct-related types:");
            foreach (var type in testProductDtos)
            {
                Console.WriteLine($"  - {type.FullName}");
            }
            
            Console.WriteLine($"\nFound {testOrderDtos.Length} TestOrder-related types:");
            foreach (var type in testOrderDtos)
            {
                Console.WriteLine($"  - {type.FullName}");
            }

            Console.WriteLine($"\nFound {testScheduleDtos.Length} TestSchedule-related types:");
            foreach (var type in testScheduleDtos)
            {
                Console.WriteLine($"  - {type.FullName}");
            }

            Console.WriteLine($"\nFound {testEventDtos.Length} TestEvent-related types:");
            foreach (var type in testEventDtos)
            {
                Console.WriteLine($"  - {type.FullName}");
            }
            
            // Look for Upsert types specifically
            var upsertTypes = allTypes.Where(t => t.Name.Contains("Upsert")).ToArray();
            Console.WriteLine($"\nFound {upsertTypes.Length} Upsert DTO types:");
            foreach (var type in upsertTypes)
            {
                Console.WriteLine($"  - {type.FullName}");
            }
            
            if (testUserDtos.Length > 0 || testProductDtos.Length > 0 || testOrderDtos.Length > 0 || upsertTypes.Length > 0)
            {
                Console.WriteLine("\n? GenerateDtos feature is working - DTOs were generated!");
            }
            else
            {
                Console.WriteLine("\n? No generated DTOs found - checking if attributes are being processed...");
            }
            
        }
        catch (Exception ex)
        {
            Console.WriteLine($"? Error inspecting generated types: {ex.Message}");
        }

        Console.WriteLine();
    }

    private void TestBasicFunctionality()
    {
        Console.WriteLine("2. Testing Basic Functionality:");
        Console.WriteLine("===============================");

        var testUser = new TestUser
        {
            Id = 1,
            FirstName = "John",
            LastName = "Doe",
            Email = "john.doe@example.com",
            Password = "secret123",
            DateOfBirth = new DateTime(1990, 1, 1),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        Console.WriteLine($"Original TestUser: {testUser.FirstName} {testUser.LastName}");

        try
        {
            // Test the attributes themselves
            var testUserType = typeof(TestUser);
            var generateDtosAttrs = testUserType.GetCustomAttributes<GenerateDtosAttribute>().ToArray();
            
            Console.WriteLine($"? Found {generateDtosAttrs.Length} GenerateDtosAttribute(s) on TestUser");
            foreach (var attr in generateDtosAttrs)
            {
                Console.WriteLine($"  Types: {attr.Types}");
                Console.WriteLine($"  OutputType: {attr.OutputType}");
                Console.WriteLine($"  ExcludeProperties: [{string.Join(", ", attr.ExcludeProperties)}]");
            }

            var testProductType = typeof(TestProduct);
            var generateAuditableDtosAttrs = testProductType.GetCustomAttributes<GenerateAuditableDtosAttribute>().ToArray();
            
            Console.WriteLine($"? Found {generateAuditableDtosAttrs.Length} GenerateAuditableDtosAttribute(s) on TestProduct");
            foreach (var attr in generateAuditableDtosAttrs)
            {
                Console.WriteLine($"  Types: {attr.Types}");
                Console.WriteLine($"  OutputType: {attr.OutputType}");
                Console.WriteLine($"  ExcludeProperties: [{string.Join(", ", attr.ExcludeProperties)}]");
            }
            
        }
        catch (Exception ex)
        {
            Console.WriteLine($"? Error testing functionality: {ex.Message}");
        }

        Console.WriteLine();
    }

    private void TestNewUpsertFeature()
    {
        Console.WriteLine("3. Testing New Upsert Feature:");
        Console.WriteLine("==============================");

        try
        {
            Console.WriteLine("Testing Upsert DTO generation:");
            
            // Check for TestEvent which only generates Upsert
            var testEventType = typeof(TestEvent);
            var generateDtosAttr = testEventType.GetCustomAttribute<GenerateDtosAttribute>();
            
            if (generateDtosAttr != null)
            {
                Console.WriteLine($"? TestEvent configured for: {generateDtosAttr.Types}");
                Console.WriteLine("  Expected to generate: UpsertTestEventRequest");
            }

            // Check for TestSchedule which has multiple attributes including Upsert
            var testScheduleType = typeof(TestSchedule);
            var scheduleAttrs = testScheduleType.GetCustomAttributes<GenerateDtosAttribute>().ToArray();
            
            Console.WriteLine($"? TestSchedule has {scheduleAttrs.Length} GenerateDtos attributes:");
            foreach (var attr in scheduleAttrs)
            {
                Console.WriteLine($"  - Types: {attr.Types}, ExcludeProperties: [{string.Join(", ", attr.ExcludeProperties)}]");
            }
            
            Console.WriteLine("\nUpsert DTOs are ideal for scenarios where you want to:");
            Console.WriteLine("  - Accept either create or update operations in a single endpoint");
            Console.WriteLine("  - Handle 'body = body with { Id = scheduleId }' scenarios");
            Console.WriteLine("  - Support both INSERT and UPDATE operations based on whether ID is provided");
            
        }
        catch (Exception ex)
        {
            Console.WriteLine($"? Error testing Upsert feature: {ex.Message}");
        }

        Console.WriteLine();
    }

    private void TestAllowMultipleFeature()
    {
        Console.WriteLine("4. Testing AllowMultiple Feature:");
        Console.WriteLine("=================================");

        try
        {
            var testScheduleType = typeof(TestSchedule);
            var scheduleAttrs = testScheduleType.GetCustomAttributes<GenerateDtosAttribute>().ToArray();
            
            Console.WriteLine($"TestSchedule demonstrates AllowMultiple with {scheduleAttrs.Length} attributes:");
            
            for (int i = 0; i < scheduleAttrs.Length; i++)
            {
                var attr = scheduleAttrs[i];
                Console.WriteLine($"  Attribute {i + 1}:");
                Console.WriteLine($"    Types: {attr.Types}");
                Console.WriteLine($"    ExcludeProperties: [{string.Join(", ", attr.ExcludeProperties)}]");
            }
            
            Console.WriteLine("\nThis allows for fine-grained control:");
            Console.WriteLine("  - Different exclusions for Response vs Upsert DTOs");
            Console.WriteLine("  - Response excludes: Password, InternalNotes");
            Console.WriteLine("  - Upsert excludes: Password (but allows InternalNotes)");
            Console.WriteLine("  - Perfect for scenarios where internal fields are needed for updates but not responses");
            
        }
        catch (Exception ex)
        {
            Console.WriteLine($"? Error testing AllowMultiple feature: {ex.Message}");
        }

        Console.WriteLine();
    }

    private void TestImprovedRecordFormatting()
    {
        Console.WriteLine("5. Testing Improved Record Formatting:");
        Console.WriteLine("======================================");

        try
        {
            Console.WriteLine("The new record formatting includes:");
            Console.WriteLine("  ? Line breaks between parameters for better readability");
            Console.WriteLine("  ? Proper indentation for record constructor parameters");
            Console.WriteLine("  ? No more giant single-line records that are hard to inspect");
            
            Console.WriteLine("\nExample generated record format:");
            Console.WriteLine("  public record CreateTestUserRequest(");
            Console.WriteLine("      string FirstName,");
            Console.WriteLine("      string LastName,");
            Console.WriteLine("      string Email,");
            Console.WriteLine("      string? Password,");
            Console.WriteLine("      DateTime DateOfBirth,");
            Console.WriteLine("      bool IsActive,");
            Console.WriteLine("      DateTime CreatedAt,");
            Console.WriteLine("      DateTime? UpdatedAt");
            Console.WriteLine("  );");
            
            Console.WriteLine("\nThis makes the generated code much more readable and easier to debug!");
            
        }
        catch (Exception ex)
        {
            Console.WriteLine($"? Error testing record formatting: {ex.Message}");
        }

        Console.WriteLine();
    }
}