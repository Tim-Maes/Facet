using System;
using Facet.Extensions;

namespace Facet.TestConsole.Tests;

public class ToEntityTests
{
    public static void RunAllTests()
    {
        TestBasicToEntityMapping();
    }

    public static void TestBasicToEntityMapping()
    {
        Console.WriteLine("=== Testing ToEntity Extension Method ===\n");

        // Create a test user entity (using the correct Data.User class)
        var userFacet = new UserDto
        {
            Id = 1,
            FirstName = "John",
            LastName = "Doe",
            Email = "john.doe@example.com",
            IsActive = true,
            DateOfBirth = new DateTime(1990, 5, 15),
            LastLoginAt = DateTime.UtcNow.AddHours(-2),
        };

        // Test the new ToEntity functionality
        var reconstructedUser = userFacet.ToEntity<User>();
        Console.WriteLine($"\nReconstructed User: {reconstructedUser.FirstName} {reconstructedUser.LastName}");
        Console.WriteLine($"  Email: {reconstructedUser.Email}");
        Console.WriteLine($"  Password: '{reconstructedUser.Password}' (should be empty/default)");

        // Verify the mapping worked correctly
        var success = reconstructedUser.FirstName == userFacet.FirstName &&
                     reconstructedUser.LastName == userFacet.LastName &&
                     reconstructedUser.Email == userFacet.Email &&
                     string.IsNullOrEmpty(reconstructedUser.Password); // Password should be empty since it was excluded

        Console.WriteLine($"\nMapping verification: {(success ? "SUCCESS" : "FAILED")}");
        
        if (success)
        {
            Console.WriteLine("✅ ToEntity extension method works correctly!");
            Console.WriteLine("✅ Excluded properties (Password) are properly handled!");
        }
        else
        {
            Console.WriteLine("❌ Something went wrong with the mapping");
        }

        Console.WriteLine("\n=== ToEntity Test Completed ===\n");
    }
}
