using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Facet.Extensions;
using Facet.TestConsole.TestInfrastructure;

namespace Facet.TestConsole.InMemoryTests;

public class LinqProjectionTests : ITestSuite
{
    public Task<List<(string name, bool passed)>> RunTestsAsync()
    {
        var results = new List<(string name, bool passed)>();

        try
        {
            TestLinqProjections();
            results.Add(("LINQ Projections", true));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] LINQ Projections: {ex.Message}");
            results.Add(("LINQ Projections", false));
        }

        return Task.FromResult(results);
    }

    private static void TestLinqProjections()
    {
        Console.WriteLine("5. Testing LINQ Projections:");
        Console.WriteLine("=============================");

        var users = TestDataFactory.CreateSampleUsers();
        var products = TestDataFactory.CreateSampleProducts();

        Console.WriteLine("Active users (via SelectFacets):");
        var activeUserDtos = users
            .Where(u => u.IsActive)
            .SelectFacets<User, UserDtoWithMapping>()
            .ToList();

        foreach (var dto in activeUserDtos)
        {
            Console.WriteLine($"  {dto.FullName} (Age: {dto.Age}) - {dto.Email}");
        }
        Console.WriteLine();

        Console.WriteLine("Available products (via SelectFacet):");
        var availableProducts = products
            .AsQueryable()
            .Where(p => p.IsAvailable)
            .SelectFacet<Product, ProductDto>()
            .ToList();

        foreach (var dto in availableProducts)
        {
            Console.WriteLine($"  {dto.Name}: ${dto.Price} - {dto.Description}");
        }
        Console.WriteLine();
    }
}