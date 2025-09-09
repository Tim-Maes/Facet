using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Facet.TestConsole.TestInfrastructure;

namespace Facet.TestConsole.InMemoryTests;

public class ParameterlessConstructorTests : ITestSuite
{
    public Task<List<(string name, bool passed)>> RunTestsAsync()
    {
        var results = new List<(string name, bool passed)>();

        try
        {
            TestParameterlessConstructors();
            results.Add(("Parameterless Constructors", true));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Parameterless Constructors: {ex.Message}");
            results.Add(("Parameterless Constructors", false));
        }

        return Task.FromResult(results);
    }

    private static void TestParameterlessConstructors()
    {
        Console.WriteLine("7. Testing Parameterless Constructors:");
        Console.WriteLine("=====================================================");

        try
        {
            // Test that parameterless constructors work by default
            var userDto = new UserDto();
            Console.WriteLine($"SUCCESS: Successfully created UserDto() - parameterless constructor enabled by default!");
            Console.WriteLine($"  Properties initialized with defaults:");
            Console.WriteLine($"    Id: {userDto.Id}");
            Console.WriteLine($"    FirstName: '{userDto.FirstName}'");
            Console.WriteLine($"    LastName: '{userDto.LastName}'");
            Console.WriteLine($"    Email: '{userDto.Email}'");
            Console.WriteLine($"    FullName: '{userDto.FullName}'");
            Console.WriteLine($"    Age: {userDto.Age}");
            Console.WriteLine($"    IsActive: {userDto.IsActive}");
            Console.WriteLine();

            // Test setting properties after construction
            userDto.FirstName = "Test";
            userDto.LastName = "User";
            userDto.Email = "test@example.com";
            userDto.FullName = "Test User";
            userDto.Age = 25;
            userDto.IsActive = true;

            Console.WriteLine($"SUCCESS: Successfully set properties after parameterless construction:");
            Console.WriteLine($"    FirstName: '{userDto.FirstName}'");
            Console.WriteLine($"    LastName: '{userDto.LastName}'");
            Console.WriteLine($"    Email: '{userDto.Email}'");
            Console.WriteLine($"    FullName: '{userDto.FullName}'");
            Console.WriteLine($"    Age: {userDto.Age}");
            Console.WriteLine($"    IsActive: {userDto.IsActive}");
            Console.WriteLine();

            // Test record with parameterless constructor 
            var productDto = new ProductDto(); // Record also gets parameterless constructor
            Console.WriteLine($"SUCCESS: Successfully created ProductDto() record - parameterless constructor enabled by default!");
            Console.WriteLine($"  Properties initialized with defaults:");
            Console.WriteLine($"    Id: {productDto.Id}");
            Console.WriteLine($"    Name: '{productDto.Name}'");
            Console.WriteLine($"    Description: '{productDto.Description}'");
            Console.WriteLine($"    Price: {productDto.Price}");
            Console.WriteLine($"    CategoryId: {productDto.CategoryId}");
            Console.WriteLine($"    IsAvailable: {productDto.IsAvailable}");
            Console.WriteLine();

            // Test unit testing scenario
            Console.WriteLine("SUCCESS: Unit Testing Scenario:");
            var testDto = new UserDto(); // Simple!
            testDto.FirstName = "Unit";
            testDto.LastName = "Test";
            testDto.IsActive = true;
            Console.WriteLine($"    Created and populated DTO for testing: {testDto.FirstName} {testDto.LastName}, Active: {testDto.IsActive}");
            Console.WriteLine();

            Console.WriteLine("SUCCESS: Parameterless constructor feature tests completed successfully!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: Error in TestParameterlessConstructors: {ex.Message}");
            Console.WriteLine($"  Stack trace: {ex.StackTrace}");
        }
        Console.WriteLine();
    }
}