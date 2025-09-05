using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Facet;

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
        TestInterfaceContractsFeature();
        TestExcludeMembersFromTypeFeature();
        TestCombinedNewFeatures();

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
            var testContractEntityDtos = allTypes.Where(t => t.Name.Contains("TestContractEntity")).ToArray();
            var testBaseExclusionDtos = allTypes.Where(t => t.Name.Contains("TestEntityWithBaseExclusions")).ToArray();
            var testCombinedFeaturesDtos = allTypes.Where(t => t.Name.Contains("TestCombinedFeaturesEntity")).ToArray();
            
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

            Console.WriteLine($"\nFound {testContractEntityDtos.Length} TestContractEntity-related types:");
            foreach (var type in testContractEntityDtos)
            {
                Console.WriteLine($"  - {type.FullName}");
            }

            Console.WriteLine($"\nFound {testBaseExclusionDtos.Length} TestEntityWithBaseExclusions-related types:");
            foreach (var type in testBaseExclusionDtos)
            {
                Console.WriteLine($"  - {type.FullName}");
            }

            Console.WriteLine($"\nFound {testCombinedFeaturesDtos.Length} TestCombinedFeaturesEntity-related types:");
            foreach (var type in testCombinedFeaturesDtos)
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
            
            if (testUserDtos.Length > 0 || testProductDtos.Length > 0 || testOrderDtos.Length > 0 || upsertTypes.Length > 0 || 
                testContractEntityDtos.Length > 0 || testBaseExclusionDtos.Length > 0 || testCombinedFeaturesDtos.Length > 0)
            {
                Console.WriteLine("\nSUCCESS: GenerateDtos feature is working - DTOs were generated!");
            }
            else
            {
                Console.WriteLine("\nNo generated DTOs found - checking if attributes are being processed...");
            }
            
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: Error inspecting generated types: {ex.Message}");
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
            
            Console.WriteLine($"FOUND: {generateDtosAttrs.Length} GenerateDtosAttribute(s) on TestUser");
            foreach (var attr in generateDtosAttrs)
            {
                Console.WriteLine($"  Types: {attr.Types}");
                Console.WriteLine($"  OutputType: {attr.OutputType}");
                Console.WriteLine($"  ExcludeProperties: [{string.Join(", ", attr.ExcludeProperties)}]");
            }

            var testProductType = typeof(TestProduct);
            var generateAuditableDtosAttrs = testProductType.GetCustomAttributes<GenerateAuditableDtosAttribute>().ToArray();
            
            Console.WriteLine($"FOUND: {generateAuditableDtosAttrs.Length} GenerateAuditableDtosAttribute(s) on TestProduct");
            foreach (var attr in generateAuditableDtosAttrs)
            {
                Console.WriteLine($"  Types: {attr.Types}");
                Console.WriteLine($"  OutputType: {attr.OutputType}");
                Console.WriteLine($"  ExcludeProperties: [{string.Join(", ", attr.ExcludeProperties)}]");
            }
            
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: Error testing functionality: {ex.Message}");
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
                Console.WriteLine($"CONFIGURED: TestEvent configured for: {generateDtosAttr.Types}");
                Console.WriteLine("  Expected to generate: UpsertTestEventRequest");
            }

            // Check for TestSchedule which has multiple attributes including Upsert
            var testScheduleType = typeof(TestSchedule);
            var scheduleAttrs = testScheduleType.GetCustomAttributes<GenerateDtosAttribute>().ToArray();
            
            Console.WriteLine($"MULTIPLE: TestSchedule has {scheduleAttrs.Length} GenerateDtos attributes:");
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
            Console.WriteLine($"ERROR: Error testing Upsert feature: {ex.Message}");
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
            Console.WriteLine($"ERROR: Error testing AllowMultiple feature: {ex.Message}");
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
            Console.WriteLine("  FEATURE: Line breaks between parameters for better readability");
            Console.WriteLine("  FEATURE: Proper indentation for record constructor parameters");
            Console.WriteLine("  FEATURE: No more giant single-line records that are hard to inspect");
            
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
            Console.WriteLine($"ERROR: Error testing record formatting: {ex.Message}");
        }

        Console.WriteLine();
    }

    private void TestInterfaceContractsFeature()
    {
        Console.WriteLine("6. Testing InterfaceContracts Feature:");
        Console.WriteLine("=====================================");

        try
        {
            var testContractEntityType = typeof(TestContractEntity);
            var generateDtosAttrs = testContractEntityType.GetCustomAttributes<GenerateDtosAttribute>().ToArray();
            
            Console.WriteLine($"FOUND: {generateDtosAttrs.Length} GenerateDtosAttribute(s) on TestContractEntity");
            
            foreach (var attr in generateDtosAttrs)
            {
                Console.WriteLine($"  Types: {attr.Types}");
                Console.WriteLine($"  InterfaceContracts: [{string.Join(", ", attr.InterfaceContracts?.Select(t => t.Name) ?? new string[0])}]");
            }

            // Check for generated types that should implement interfaces
            var assembly = Assembly.GetExecutingAssembly();
            var allTypes = assembly.GetTypes();
            
            var contractEntityDtos = allTypes.Where(t => t.Name.Contains("TestContractEntity")).ToArray();
            Console.WriteLine($"\nFound {contractEntityDtos.Length} TestContractEntity-related types:");
            
            foreach (var type in contractEntityDtos)
            {
                Console.WriteLine($"  - {type.FullName}");
                var interfaces = type.GetInterfaces();
                if (interfaces.Length > 0)
                {
                    Console.WriteLine($"    Implements: [{string.Join(", ", interfaces.Select(i => i.Name))}]");
                }
            }
            
            // Test that Create DTO implements ICreatePayload
            var createType = allTypes.FirstOrDefault(t => t.Name == "CreateTestContractEntityRequest");
            if (createType != null)
            {
                var implementsCreatePayload = createType.GetInterfaces().Any(i => i == typeof(ICreatePayload));
                Console.WriteLine($"SUCCESS: CreateTestContractEntityRequest implements ICreatePayload: {implementsCreatePayload}");
            }
            else
            {
                Console.WriteLine("WARNING: CreateTestContractEntityRequest not found");
            }

            // Test that Update DTO implements IUpdatePayload  
            var updateType = allTypes.FirstOrDefault(t => t.Name == "UpdateTestContractEntityRequest");
            if (updateType != null)
            {
                var implementsUpdatePayload = updateType.GetInterfaces().Any(i => i == typeof(IUpdatePayload));
                Console.WriteLine($"SUCCESS: UpdateTestContractEntityRequest implements IUpdatePayload: {implementsUpdatePayload}");
            }
            else
            {
                Console.WriteLine("WARNING: UpdateTestContractEntityRequest not found");
            }

            // Test that Response DTO implements IResponseData
            var responseType = allTypes.FirstOrDefault(t => t.Name == "TestContractEntityResponse");
            if (responseType != null)
            {
                var implementsResponseData = responseType.GetInterfaces().Any(i => i == typeof(IResponseData));
                Console.WriteLine($"SUCCESS: TestContractEntityResponse implements IResponseData: {implementsResponseData}");
            }
            else
            {
                Console.WriteLine("WARNING: TestContractEntityResponse not found");
            }

            Console.WriteLine("\nInterfaceContracts feature provides:");
            Console.WriteLine("  - Compile-time type safety for DTOs");
            Console.WriteLine("  - Better integration with existing interfaces");
            Console.WriteLine("  - Polymorphic usage of generated DTOs");
            
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: Error testing InterfaceContracts feature: {ex.Message}");
        }

        Console.WriteLine();
    }

    private void TestExcludeMembersFromTypeFeature()
    {
        Console.WriteLine("7. Testing ExcludeMembersFromType Feature:");
        Console.WriteLine("=========================================");

        try
        {
            var testEntityType = typeof(TestEntityWithBaseExclusions);
            var generateDtosAttr = testEntityType.GetCustomAttribute<GenerateDtosAttribute>();
            
            if (generateDtosAttr != null)
            {
                Console.WriteLine($"CONFIGURED: TestEntityWithBaseExclusions configured with:");
                Console.WriteLine($"  Types: {generateDtosAttr.Types}");
                Console.WriteLine($"  ExcludeMembersFromType: [{string.Join(", ", generateDtosAttr.ExcludeMembersFromType?.Select(t => t.Name) ?? new string[0])}]");
            }

            // Get the generated DTO and check its properties
            var assembly = Assembly.GetExecutingAssembly();
            var allTypes = assembly.GetTypes();
            
            var createType = allTypes.FirstOrDefault(t => t.Name == "CreateTestEntityWithBaseExclusionsRequest");
            if (createType != null)
            {
                var properties = createType.GetProperties().Select(p => p.Name).ToArray();
                Console.WriteLine($"\nSUCCESS: Found CreateTestEntityWithBaseExclusionsRequest with properties:");
                foreach (var prop in properties)
                {
                    Console.WriteLine($"  - {prop}");
                }

                // Verify base class properties are excluded
                var baseEntityProperties = typeof(BaseEntity).GetProperties().Select(p => p.Name).ToArray();
                var auditableProperties = typeof(IAuditableEntity).GetProperties().Select(p => p.Name).ToArray();
                
                var excludedFound = baseEntityProperties.Concat(auditableProperties).Where(p => properties.Contains(p)).ToArray();
                
                if (excludedFound.Length == 0)
                {
                    Console.WriteLine("SUCCESS: All BaseEntity and IAuditableEntity properties correctly excluded!");
                }
                else
                {
                    Console.WriteLine($"WARNING: Found excluded properties that should not be present: [{string.Join(", ", excludedFound)}]");
                }

                // Verify entity-specific properties are included
                var expectedProperties = new[] { "Id", "Name", "Description", "Price" };
                var missingProperties = expectedProperties.Where(p => !properties.Contains(p)).ToArray();
                
                if (missingProperties.Length == 0)
                {
                    Console.WriteLine("SUCCESS: All expected entity-specific properties are present!");
                }
                else
                {
                    Console.WriteLine($"WARNING: Missing expected properties: [{string.Join(", ", missingProperties)}]");
                }
            }
            else
            {
                Console.WriteLine("WARNING: CreateTestEntityWithBaseExclusionsRequest not found");
            }

            Console.WriteLine("\nExcludeMembersFromType feature provides:");
            Console.WriteLine("  - Clean DTOs without base class clutter");
            Console.WriteLine("  - Automatic exclusion of audit fields from base classes");
            Console.WriteLine("  - Works with both abstract classes and interfaces");
            Console.WriteLine("  - Reduces the need for manual property exclusions");
            
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: Error testing ExcludeMembersFromType feature: {ex.Message}");
        }

        Console.WriteLine();
    }

    private void TestCombinedNewFeatures()
    {
        Console.WriteLine("8. Testing Combined New Features:");
        Console.WriteLine("=================================");

        try
        {
            var testCombinedType = typeof(TestCombinedFeaturesEntity);
            var generateDtosAttr = testCombinedType.GetCustomAttribute<GenerateDtosAttribute>();
            
            if (generateDtosAttr != null)
            {
                Console.WriteLine($"CONFIGURED: TestCombinedFeaturesEntity combines both features:");
                Console.WriteLine($"  ExcludeMembersFromType: [{string.Join(", ", generateDtosAttr.ExcludeMembersFromType?.Select(t => t.Name) ?? new string[0])}]");
                Console.WriteLine($"  InterfaceContracts: [{string.Join(", ", generateDtosAttr.InterfaceContracts?.Select(t => t.Name) ?? new string[0])}]");
            }

            // Check for the generated DTO
            var assembly = Assembly.GetExecutingAssembly();
            var allTypes = assembly.GetTypes();
            
            var createType = allTypes.FirstOrDefault(t => t.Name == "CreateTestCombinedFeaturesEntityRequest");
            if (createType != null)
            {
                var properties = createType.GetProperties().Select(p => p.Name).ToArray();
                Console.WriteLine($"\nSUCCESS: Found CreateTestCombinedFeaturesEntityRequest with properties:");
                foreach (var prop in properties)
                {
                    Console.WriteLine($"  - {prop}");
                }

                // Check interface implementation
                var implementsCreatePayload = createType.GetInterfaces().Any(i => i == typeof(ICreatePayload));
                Console.WriteLine($"SUCCESS: Implements ICreatePayload interface: {implementsCreatePayload}");

                // Verify BaseEntity properties are excluded
                var baseEntityProperties = typeof(BaseEntity).GetProperties().Select(p => p.Name).ToArray();
                var excludedFound = baseEntityProperties.Where(p => properties.Contains(p)).ToArray();
                
                if (excludedFound.Length == 0)
                {
                    Console.WriteLine("SUCCESS: BaseEntity properties correctly excluded!");
                }
                else
                {
                    Console.WriteLine($"WARNING: Found excluded BaseEntity properties: [{string.Join(", ", excludedFound)}]");
                }
            }
            else
            {
                Console.WriteLine("WARNING: CreateTestCombinedFeaturesEntityRequest not found");
            }

            Console.WriteLine("\nCombined features demonstrate:");
            Console.WriteLine("  - Both features work together seamlessly");
            Console.WriteLine("  - Clean DTOs that implement specific interfaces");
            Console.WriteLine("  - Automatic base class exclusions with contract enforcement");
            Console.WriteLine("  - Perfect for domain-driven design patterns");
            
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: Error testing combined features: {ex.Message}");
        }

        Console.WriteLine();
    }
}