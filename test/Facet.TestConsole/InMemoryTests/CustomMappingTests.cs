using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Facet.Extensions;
using Facet.TestConsole.TestInfrastructure;

namespace Facet.TestConsole.InMemoryTests;

public class CustomMappingTests : ITestSuite
{
    public Task<List<(string name, bool passed)>> RunTestsAsync()
    {
        var results = new List<(string name, bool passed)>();

        try
        {
            TestCustomMappingDto();
            results.Add(("Custom Mapping DTO", true));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Custom Mapping DTO: {ex.Message}");
            results.Add(("Custom Mapping DTO", false));
        }

        return Task.FromResult(results);
    }

    private static void TestCustomMappingDto()
    {
        Console.WriteLine("3. Testing DTO with Custom Mapping:");
        Console.WriteLine("====================================");

        var users = TestDataFactory.CreateSampleUsers();

        foreach (var user in users)
        {
            var userDto = user.ToFacet<User, UserDtoWithMapping>();
            Console.WriteLine($"User: {userDto.FullName} (Age: {userDto.Age})");
            Console.WriteLine($"  Email: {userDto.Email}, Active: {userDto.IsActive}");
            Console.WriteLine();
        }
    }
}