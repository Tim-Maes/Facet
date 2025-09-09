using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Facet.Extensions;
using Facet.TestConsole.TestInfrastructure;

namespace Facet.TestConsole.InMemoryTests;

public class ModernRecordTests : ITestSuite
{
    public Task<List<(string name, bool passed)>> RunTestsAsync()
    {
        var results = new List<(string name, bool passed)>();

        try
        {
            TestModernRecordFeatures();
            results.Add(("Modern Record Features", true));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Modern Record Features: {ex.Message}");
            results.Add(("Modern Record Features", false));
        }

        return Task.FromResult(results);
    }

    private static void TestModernRecordFeatures()
    {
        Console.WriteLine("1.5 Testing Modern Record Features:");
        Console.WriteLine("===================================");

        var modernUsers = TestDataFactory.CreateSampleModernUsers();

        Console.WriteLine("Modern User DTOs (with auto-detected record, init-only & required properties):");
        foreach (var user in modernUsers)
        {
            try
            {
                var userDto = user.ToFacet<ModernUser, ModernUserDto>();
                Console.WriteLine($"  {userDto.FirstName} {userDto.LastName}");
                Console.WriteLine($"    ID: {userDto.Id} (required init-only)");
                Console.WriteLine($"    Email: {userDto.Email ?? "N/A"}");
                Console.WriteLine($"    Created: {userDto.CreatedAt:yyyy-MM-dd}");
                Console.WriteLine($"    Full Name: {userDto.FullName}");
                Console.WriteLine($"    Display: {userDto.DisplayName}");
                Console.WriteLine();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Error mapping {user.FirstName} {user.LastName}: {ex.Message}");
                Console.WriteLine();
            }
        }

        Console.WriteLine("Compact User DTOs (auto-detected record struct - using constructor):");
        var compactUsers = modernUsers.Select(u => new CompactUser(u.Id, $"{u.FirstName} {u.LastName}", u.CreatedAt)).ToList();
        foreach (var compact in compactUsers)
        {
            try
            {
                var compactDto = new CompactUserDto(compact);
                Console.WriteLine($"  {compactDto.Name} (ID: {compactDto.Id}, Created: {compactDto.CreatedAt:yyyy-MM-dd})");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Error mapping compact user: {ex.Message}");
            }
        }
        Console.WriteLine();

        Console.WriteLine("Modern User Classes (mutable by default for classes):");
        foreach (var user in modernUsers)
        {
            try
            {
                var userClass = user.ToFacet<ModernUser, ModernUserClass>();
                Console.WriteLine($"  {userClass.FirstName} {userClass.LastName}");
                Console.WriteLine($"    ID: {userClass.Id} (mutable in class)");
                Console.WriteLine($"    Created: {userClass.CreatedAt:yyyy-MM-dd}");
                Console.WriteLine();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Error mapping {user.FirstName} {user.LastName} to class: {ex.Message}");
                Console.WriteLine();
            }
        }
    }
}