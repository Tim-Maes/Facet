using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Facet.Extensions;
using Facet.TestConsole.TestInfrastructure;

namespace Facet.TestConsole.InMemoryTests;

public class ShorthandOverloadTests : ITestSuite
{
    public Task<List<(string name, bool passed)>> RunTestsAsync()
    {
        var results = new List<(string name, bool passed)>();

        try
        {
            TestShorthandOverloads();
            results.Add(("Shorthand Overloads", true));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Shorthand Overloads: {ex.Message}");
            results.Add(("Shorthand Overloads", false));
        }

        return Task.FromResult(results);
    }

    private static void TestShorthandOverloads()
    {
        Console.WriteLine("6. Testing Shorthand Overloads (omit TSource):");
        Console.WriteLine("===============================");

        var users = TestDataFactory.CreateSampleUsers();
        var products = TestDataFactory.CreateSampleProducts();
        var employees = TestDataFactory.CreateSampleEmployees();

        Console.WriteLine("Single item (inheritance):");
        foreach (var e in employees)
        {
            try
            {
                var dto = e.ToFacet<EmployeeDto>();
                Console.WriteLine($"  EmployeeDto: {dto.DisplayName} | Dept: {dto.Department} | Hire: {dto.HireDate:yyyy-MM-dd}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Error mapping employee to EmployeeDto: {ex.Message}");
            }
        }
        Console.WriteLine();

        Console.WriteLine("Single item (basic & custom mapping):");
        foreach (var u in users)
        {
            try
            {
                var dtoBasic = u.ToFacet<UserDto>();
                var dtoCustom = u.ToFacet<UserDtoWithMapping>();
                Console.WriteLine($"  Basic:  {dtoBasic.FirstName} {dtoBasic.LastName} | Active: {dtoBasic.IsActive}");
                Console.WriteLine($"  Custom: {dtoCustom.FullName} (Age: {dtoCustom.Age}) | Email: {dtoCustom.Email}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Error mapping user to UserDto: {ex.Message}");
            }
        }
        Console.WriteLine();
        
        Console.WriteLine("IEnumerable.SelectFacets<TTarget>:");
        var activeUsers = users.Where(u => u.IsActive).SelectFacets<UserDtoWithMapping>().ToList();
        foreach (var dto in activeUsers)
        {
            try
            {
                Console.WriteLine($"  {dto.FullName} (Age: {dto.Age})");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Error mapping user to UserDtoWithMapping: {ex.Message}");
            }
        }
        Console.WriteLine();

        Console.WriteLine("IQueryable.SelectFacet<TTarget> (simulated):");
        var availableProducts =
            products.AsQueryable()
                    .Where(p => p.IsAvailable)
                    .SelectFacet<ProductDto>()
                    .ToList();

        foreach (var p in availableProducts)
        {
            try
            {
                Console.WriteLine($"  {p.Name}: ${p.Price} - {p.Description}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Error mapping product to ProductDto: {ex.Message}");
            }
        }
        Console.WriteLine();
    }
}