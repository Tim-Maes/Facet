using System;
using Facet.TestConsole.DTOs;
using Facet.TestConsole.Data;
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
        var originalUser = new User
        {
            Id = 1,
            FirstName = "John",
            LastName = "Doe",
            Email = "john.doe@example.com",
            Password = "secret123",
            IsActive = true,
            DateOfBirth = new DateTime(1990, 5, 15),
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow.AddHours(-2),
        };

        Console.WriteLine($"Original User: {originalUser.FirstName} {originalUser.LastName}");
        Console.WriteLine($"  Email: {originalUser.Email}");
        Console.WriteLine($"  Password: {originalUser.Password} (should be excluded from facet)");

        // Convert to facet (this excludes the Password field)
        var userFacet = originalUser.ToFacet<DbUserDto>();
        Console.WriteLine($"\nFacet created: {userFacet.FirstName} {userFacet.LastName}");

        // Test the new ToEntity functionality
        var reconstructedUser = userFacet.ToEntity<User>();
        Console.WriteLine($"\nReconstructed User: {reconstructedUser.FirstName} {reconstructedUser.LastName}");
        Console.WriteLine($"  Email: {reconstructedUser.Email}");
        Console.WriteLine($"  Password: '{reconstructedUser.Password}' (should be empty/default)");

        // Verify the mapping worked correctly
        var success = reconstructedUser.FirstName == originalUser.FirstName &&
                     reconstructedUser.LastName == originalUser.LastName &&
                     reconstructedUser.Email == originalUser.Email &&
                     string.IsNullOrEmpty(reconstructedUser.Password); // Password should be empty since it was excluded

        Console.WriteLine($"\nMapping verification: {(success ? "SUCCESS" : "FAILED")}");
        
        if (success)
        {
            Console.WriteLine("✅ ToEntity extension method works correctly!");
            Console.WriteLine("✅ Excluded properties (Password) are properly handled!");
            Console.WriteLine("✅ Entity types remain completely untouched!");
        }
        else
        {
            Console.WriteLine("❌ Something went wrong with the mapping");
        }

        Console.WriteLine("\n=== ToEntity Test Completed ===\n");
    }
}
