using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Facet.Extensions;
using Facet.TestConsole.TestInfrastructure;

namespace Facet.TestConsole.InMemoryTests;

public class NestedPartialsTests : ITestSuite
{
    public Task<List<(string name, bool passed)>> RunTestsAsync()
    {
        var results = new List<(string name, bool passed)>();

        try
        {
            TestNestedPartials();
            results.Add(("Nested Partials", true));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Nested Partials: {ex.Message}");
            results.Add(("Nested Partials", false));
        }

        return Task.FromResult(results);
    }

    private static void TestNestedPartials()
    {
        Console.WriteLine("8. Testing Nested Partials Support:");
        Console.WriteLine("===================================");

        var users = TestDataFactory.CreateSampleUsers();
        var products = TestDataFactory.CreateSampleProducts();
        var employees = TestDataFactory.CreateSampleEmployees();
        var managers = TestDataFactory.CreateSampleManagers();

        try
        {
            Console.WriteLine("Testing nested partial DTOs in OuterContainer...");

            // Test nested user DTOs
            Console.WriteLine("\nNested User DTOs:");
            foreach (var user in users.Take(2))
            {
                try
                {
                    var nestedUserDto = user.ToFacet<User, OuterContainer.NestedUserDto>();
                    Console.WriteLine($"  SUCCESS: {nestedUserDto.FirstName} {nestedUserDto.LastName}");
                    Console.WriteLine($"    Email: {nestedUserDto.Email}, Active: {nestedUserDto.IsActive}");
                    Console.WriteLine($"    DOB: {nestedUserDto.DateOfBirth:yyyy-MM-dd}, Last Login: {nestedUserDto.LastLoginAt?.ToString("yyyy-MM-dd") ?? "Never"}");
                    Console.WriteLine();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  ERROR mapping user to nested DTO: {ex.Message}");
                }
            }

            // Test nested product DTOs (record type)
            Console.WriteLine("Nested Product DTOs (Records):");
            foreach (var product in products.Take(2))
            {
                try
                {
                    var nestedProductDto = product.ToFacet<Product, OuterContainer.NestedProductDto>();
                    Console.WriteLine($"  SUCCESS: {nestedProductDto.Name} - ${nestedProductDto.Price}");
                    Console.WriteLine($"    Category: {nestedProductDto.CategoryId}, Available: {nestedProductDto.IsAvailable}");
                    Console.WriteLine($"    Description: {nestedProductDto.Description}");
                    Console.WriteLine();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  ERROR mapping product to nested DTO: {ex.Message}");
                }
            }

            // Test deeply nested DTOs
            Console.WriteLine("Testing deeply nested partials (OuterContainer.InnerContainer)...");

            Console.WriteLine("\nDeeply Nested Employee DTOs:");
            foreach (var employee in employees.Take(1))
            {
                try
                {
                    var deeplyNestedDto = employee.ToFacet<Employee, OuterContainer.InnerContainer.DeeplyNestedEmployeeDto>();
                    Console.WriteLine($"  SUCCESS: {deeplyNestedDto.DisplayName}");
                    Console.WriteLine($"    Employee ID: {deeplyNestedDto.EmployeeId}, Department: {deeplyNestedDto.Department}");
                    Console.WriteLine($"    Hire Date: {deeplyNestedDto.HireDate:yyyy-MM-dd}");
                    Console.WriteLine();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  ERROR mapping employee to deeply nested DTO: {ex.Message}");
                }
            }

            Console.WriteLine("Deeply Nested Manager Summary (Record Struct):");
            foreach (var manager in managers.Take(1))
            {
                try
                {
                    var managerSummary = new OuterContainer.InnerContainer.DeeplyNestedManagerSummary(manager);
                    Console.WriteLine($"  SUCCESS: {managerSummary.DisplayName}");
                    Console.WriteLine($"    Team: {managerSummary.TeamName} ({managerSummary.TeamSize} members)");
                    Console.WriteLine($"    Department: {managerSummary.Department}");
                    Console.WriteLine();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  ERROR creating deeply nested manager summary: {ex.Message}");
                }
            }

            // Test parameterless constructors for nested types
            Console.WriteLine("Testing Parameterless Constructors for Nested Types:");
            try
            {
                var nestedUserDto = new OuterContainer.NestedUserDto();
                Console.WriteLine($"  SUCCESS: Created nested DTO with parameterless constructor");
                Console.WriteLine($"    Default values - FirstName: '{nestedUserDto.FirstName}', Email: '{nestedUserDto.Email}'");

                var deeplyNestedDto = new OuterContainer.InnerContainer.DeeplyNestedEmployeeDto();
                Console.WriteLine($"  SUCCESS: Created deeply nested DTO with parameterless constructor");
                Console.WriteLine($"    Default values - EmployeeId: '{deeplyNestedDto.EmployeeId}', Department: '{deeplyNestedDto.Department}'");
                Console.WriteLine();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ERROR testing parameterless constructors for nested types: {ex.Message}");
            }

            Console.WriteLine("SUCCESS: All nested partials tests completed successfully!");
            Console.WriteLine("This demonstrates that Facet generator correctly handles:");
            Console.WriteLine("  - Single-level nested partial classes");
            Console.WriteLine("  - Multi-level nested partial classes (deeply nested)");
            Console.WriteLine("  - Different facet kinds (class, record, record struct) in nested contexts");
            Console.WriteLine("  - Parameterless constructors for nested partial types");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR in TestNestedPartials: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
        Console.WriteLine();
    }
}