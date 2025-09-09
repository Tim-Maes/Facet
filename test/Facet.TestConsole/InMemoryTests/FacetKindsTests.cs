using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Facet.Extensions;
using Facet.TestConsole.TestInfrastructure;

namespace Facet.TestConsole.InMemoryTests;

public class FacetKindsTests : ITestSuite
{
    public Task<List<(string name, bool passed)>> RunTestsAsync()
    {
        var results = new List<(string name, bool passed)>();

        try
        {
            TestDifferentFacetKinds();
            results.Add(("Different Facet Kinds", true));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Different Facet Kinds: {ex.Message}");
            results.Add(("Different Facet Kinds", false));
        }

        return Task.FromResult(results);
    }

    private static void TestDifferentFacetKinds()
    {
        Console.WriteLine("4. Testing Different Facet Kinds:");
        Console.WriteLine("==================================");

        var users = TestDataFactory.CreateSampleUsers();
        var products = TestDataFactory.CreateSampleProducts();

        Console.WriteLine("Record DTOs:");
        foreach (var product in products)
        {
            var productDto = product.ToFacet<Product, ProductDto>();
            Console.WriteLine($"  {productDto.Name}: ${productDto.Price} (Available: {productDto.IsAvailable})");
        }
        Console.WriteLine();

        Console.WriteLine("Struct DTOs:");
        foreach (var product in products)
        {
            var productSummary = new ProductSummary(product);
            Console.WriteLine($"  {productSummary.Name}: ${productSummary.Price}");
        }
        Console.WriteLine();

        Console.WriteLine("Record Struct DTOs:");
        foreach (var user in users)
        {
            var userSummary = new UserSummary(user);
            Console.WriteLine($"  {userSummary.FirstName} {userSummary.LastName} ({userSummary.Email})");
        }
        Console.WriteLine();
    }
}