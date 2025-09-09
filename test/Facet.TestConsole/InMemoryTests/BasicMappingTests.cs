using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Facet.Extensions;
using Facet.TestConsole.TestInfrastructure;

namespace Facet.TestConsole.InMemoryTests;

public class BasicMappingTests : ITestSuite
{
    public Task<List<(string name, bool passed)>> RunTestsAsync()
    {
        var results = new List<(string name, bool passed)>();

        try
        {
            TestBasicDtoMapping();
            results.Add(("Basic DTO Mapping", true));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Basic DTO Mapping: {ex.Message}");
            results.Add(("Basic DTO Mapping", false));
        }

        return Task.FromResult(results);
    }

    private static void TestBasicDtoMapping()
    {
        Console.WriteLine("2. Testing Basic DTO Mapping:");
        Console.WriteLine("==============================");

        var users = TestDataFactory.CreateSampleUsers();

        foreach (var user in users)
        {
            var userDto = user.ToFacet<User, UserDto>();
            Console.WriteLine($"User: {userDto.FirstName} {userDto.LastName} ({userDto.Email})");
            Console.WriteLine($"  Active: {userDto.IsActive}, DOB: {userDto.DateOfBirth:yyyy-MM-dd}");
            Console.WriteLine($"  Last Login: {userDto.LastLoginAt?.ToString("yyyy-MM-dd HH:mm") ?? "Never"}");
            Console.WriteLine();
        }
    }
}