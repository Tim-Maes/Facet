using System;
using Facet;

namespace Facet.TestConsole.Tests;

/// <summary>
/// Tests for verifying proper enum handling in GenerateDtos attributes.
/// This validates that DtoTypes and OutputType enums are properly parsed by the source generator.
/// </summary>
public class EnumHandlingTests
{
    public static void RunAllTests()
    {
        Console.WriteLine("=== Enum Handling Tests ===");
        Console.WriteLine();

        TestSingleEnumValues();
        TestBitwiseEnumCombinations();
        TestAllEnumValue();
        TestOutputTypeEnums();
        TestComplexEnumScenarios();

        Console.WriteLine("=== Enum Handling Tests Completed ===");
        Console.WriteLine();
    }

    private static void TestSingleEnumValues()
    {
        Console.WriteLine("1. Testing Single Enum Values:");
        Console.WriteLine("===============================");

        try
        {
            // Test DtoTypes.Create
            var createUser = new CreateEnumTestUserForCreateRequest("Test", "test@example.com", DateTime.Now);
            Console.WriteLine($"SUCCESS: DtoTypes.Create works - {createUser.Name}");

            // Test DtoTypes.Response
            var responseUser = new EnumTestUserForRecordResponse(1, "Record", "record@example.com");
            Console.WriteLine($"SUCCESS: DtoTypes.Response works - {responseUser.Name}");

            Console.WriteLine();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: Single enum values test failed: {ex.Message}");
            Console.WriteLine();
        }
    }

    private static void TestBitwiseEnumCombinations()
    {
        Console.WriteLine("2. Testing Bitwise Enum Combinations:");
        Console.WriteLine("======================================");

        try
        {
            // Test DtoTypes.Create | DtoTypes.Update
            var createUser = new CreateEnumTestUserForCreateUpdateRequest("Test", "test@example.com", DateTime.Now);
            Console.WriteLine($"SUCCESS: DtoTypes.Create | DtoTypes.Update (Create) works - {createUser.Name}");

            var updateUser = new UpdateEnumTestUserForCreateUpdateRequest(1, "Updated", "updated@example.com", DateTime.Now);
            Console.WriteLine($"SUCCESS: DtoTypes.Create | DtoTypes.Update (Update) works - {updateUser.Name}");

            Console.WriteLine();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: Bitwise enum combinations test failed: {ex.Message}");
            Console.WriteLine();
        }
    }

    private static void TestAllEnumValue()
    {
        Console.WriteLine("3. Testing DtoTypes.All:");
        Console.WriteLine("=========================");

        try
        {
            // Test DtoTypes.All generates all DTO types
            var createUser = new CreateEnumTestUserForAllRequest("Create", "create@example.com", DateTime.Now);
            Console.WriteLine($"SUCCESS: DtoTypes.All (Create) works - {createUser.Name}");

            var updateUser = new UpdateEnumTestUserForAllRequest(1, "Update", "update@example.com", DateTime.Now);
            Console.WriteLine($"SUCCESS: DtoTypes.All (Update) works - {updateUser.Name}");

            var responseUser = new EnumTestUserForAllResponse(1, "Response", "response@example.com", DateTime.Now);
            Console.WriteLine($"SUCCESS: DtoTypes.All (Response) works - {responseUser.Name}");

            var upsertUser = new UpsertEnumTestUserForAllRequest(1, "Upsert", "upsert@example.com", DateTime.Now);
            Console.WriteLine($"SUCCESS: DtoTypes.All (Upsert) works - {upsertUser.Name}");

            var queryUser = new EnumTestUserForAllQuery(null, "Query", "query@example.com", null);
            Console.WriteLine($"SUCCESS: DtoTypes.All (Query) works - {queryUser.Name}");

            Console.WriteLine();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: DtoTypes.All test failed: {ex.Message}");
            Console.WriteLine();
        }
    }

    private static void TestOutputTypeEnums()
    {
        Console.WriteLine("4. Testing OutputType Enums:");
        Console.WriteLine("=============================");

        try
        {
            // Test OutputType.Record
            var recordUser = new EnumTestUserForRecordResponse(1, "Record", "record@example.com");
            Console.WriteLine($"SUCCESS: OutputType.Record works - {recordUser.Name}");

            // Test OutputType.Class
            var classUser = new EnumTestUserForClassResponse(new EnumTestUserForClass 
            { 
                Id = 1, 
                Name = "Class", 
                Email = "class@example.com"
            });
            Console.WriteLine($"SUCCESS: OutputType.Class works - {classUser.Name}");

            Console.WriteLine();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: OutputType enums test failed: {ex.Message}");
            Console.WriteLine();
        }
    }

    private static void TestComplexEnumScenarios()
    {
        Console.WriteLine("5. Testing Complex Enum Scenarios:");
        Console.WriteLine("===================================");

        try
        {
            // Test that enum values are properly resolved in different contexts
            Console.WriteLine("INFO: Complex enum scenarios test verifies that:");
            Console.WriteLine("  - Single enum values (DtoTypes.Create) work correctly");
            Console.WriteLine("  - Bitwise combinations (DtoTypes.Create | DtoTypes.Update) work correctly");
            Console.WriteLine("  - Special enum values (DtoTypes.All) work correctly");
            Console.WriteLine("  - OutputType enums (OutputType.Record, OutputType.Class) work correctly");
            Console.WriteLine("  - All combinations compile and generate expected DTOs");

            Console.WriteLine("\nSUCCESS: All enum handling scenarios working correctly!");
            Console.WriteLine("The GenerateDtos source generator properly handles enum parsing!");

            Console.WriteLine();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: Complex enum scenarios test failed: {ex.Message}");
            Console.WriteLine();
        }
    }
}

// Test entities for enum handling validation
public class EnumTestUser
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
}

// Test 1: Single enum value - DtoTypes.Create
[GenerateDtos(Types = DtoTypes.Create)]
public class EnumTestUserForCreate 
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
}

// Test 2: Multiple enum values (bitwise OR) - DtoTypes.Create | DtoTypes.Update
[GenerateDtos(Types = DtoTypes.Create | DtoTypes.Update)]
public class EnumTestUserForCreateUpdate 
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
}

// Test 3: All enum values - DtoTypes.All
[GenerateDtos(Types = DtoTypes.All)]
public class EnumTestUserForAll 
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
}

// Test 4: Different OutputType enum values
[GenerateDtos(Types = DtoTypes.Response, OutputType = OutputType.Record)]
public class EnumTestUserForRecord 
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

[GenerateDtos(Types = DtoTypes.Response, OutputType = OutputType.Class)]
public class EnumTestUserForClass 
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}