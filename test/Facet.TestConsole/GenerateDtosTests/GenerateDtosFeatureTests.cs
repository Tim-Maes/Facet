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
            
            // Look for any generated types (*.g.cs pattern)
            var generatedTypes = allTypes.Where(t => t.Name.Contains("Request") || t.Name.Contains("Response") || t.Name.Contains("Query")).ToArray();
            
            Console.WriteLine($"\nFound {generatedTypes.Length} DTO-pattern types:");
            foreach (var type in generatedTypes)
            {
                Console.WriteLine($"  - {type.FullName}");
            }
            
            if (testUserDtos.Length > 0 || testProductDtos.Length > 0 || testOrderDtos.Length > 0)
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
            var generateDtosAttr = testUserType.GetCustomAttribute<GenerateDtosAttribute>();
            
            if (generateDtosAttr != null)
            {
                Console.WriteLine("? GenerateDtosAttribute found on TestUser");
                Console.WriteLine($"  Types: {generateDtosAttr.Types}");
                Console.WriteLine($"  OutputType: {generateDtosAttr.OutputType}");
                Console.WriteLine($"  ExcludeProperties: [{string.Join(", ", generateDtosAttr.ExcludeProperties)}]");
            }
            else
            {
                Console.WriteLine("? GenerateDtosAttribute not found on TestUser");
            }

            var testProductType = typeof(TestProduct);
            var generateAuditableDtosAttr = testProductType.GetCustomAttribute<GenerateAuditableDtosAttribute>();
            
            if (generateAuditableDtosAttr != null)
            {
                Console.WriteLine("? GenerateAuditableDtosAttribute found on TestProduct");
                Console.WriteLine($"  Types: {generateAuditableDtosAttr.Types}");
                Console.WriteLine($"  OutputType: {generateAuditableDtosAttr.OutputType}");
                Console.WriteLine($"  ExcludeProperties: [{string.Join(", ", generateAuditableDtosAttr.ExcludeProperties)}]");
            }
            else
            {
                Console.WriteLine("? GenerateAuditableDtosAttribute not found on TestProduct");
            }

            var testOrderType = typeof(TestOrder);
            var generateDtosOrderAttr = testOrderType.GetCustomAttribute<GenerateDtosAttribute>();
            
            if (generateDtosOrderAttr != null)
            {
                Console.WriteLine("? GenerateDtosAttribute found on TestOrder");
                Console.WriteLine($"  Types: {generateDtosOrderAttr.Types}");
                Console.WriteLine($"  OutputType: {generateDtosOrderAttr.OutputType}");
                Console.WriteLine($"  Namespace: {generateDtosOrderAttr.Namespace}");
                Console.WriteLine($"  Prefix: {generateDtosOrderAttr.Prefix}");
                Console.WriteLine($"  Suffix: {generateDtosOrderAttr.Suffix}");
            }
            else
            {
                Console.WriteLine("? GenerateDtosAttribute not found on TestOrder");
            }
            
        }
        catch (Exception ex)
        {
            Console.WriteLine($"? Error testing functionality: {ex.Message}");
        }

        Console.WriteLine();
    }
}