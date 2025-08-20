using Facet.Extensions;
using Facet.Extensions.EFCore;
using Facet.TestConsole.Data;
using Facet.TestConsole.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Facet.TestConsole.Tests;

public class EfCoreIntegrationTests
{
    private readonly FacetTestDbContext _context;
    private readonly ILogger<EfCoreIntegrationTests> _logger;

    public EfCoreIntegrationTests(FacetTestDbContext context, ILogger<EfCoreIntegrationTests> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task RunAllTestsAsync()
    {
        Console.WriteLine("=== EF Core Integration Tests ===\n");

        await TestAsyncProjections();
        await TestLinqToEntitiesQueries();
        await TestEntityTrackingIntegration();
        await TestComplexQueryProjections();
        await TestPerformanceComparisons();

        Console.WriteLine("\n=== All EF Core integration tests completed! ===");
    }

    private async Task TestAsyncProjections()
    {
        Console.WriteLine("1. Testing Async Projections:");
        Console.WriteLine("==============================");

        Console.WriteLine("  Testing ToFacetsAsync:");
        var userDtos = await _context.Users
            .Where(u => u.IsActive)
            .ToFacetsAsync<DbUserDto>();
        
        Console.WriteLine($"    Retrieved {userDtos.Count} user DTOs");
        foreach (var dto in userDtos)
        {
            Console.WriteLine($"      - {dto.FirstName} {dto.LastName} ({dto.Email})");
        }

        Console.WriteLine("\n  Testing FirstFacetAsync:");
        var firstUserDto = await _context.Users
            .Where(u => u.Email.Contains("john"))
            .FirstFacetAsync<DbUserDto>();
        
        if (firstUserDto != null)
        {
            Console.WriteLine($"    Found first user: {firstUserDto.FirstName} {firstUserDto.LastName}");
        }

        Console.WriteLine("\n  Testing SingleFacetAsync:");
        try
        {
            var singleUserDto = await _context.Users
                .Where(u => u.Email == "john.doe@example.com")
                .SingleFacetAsync<DbUserDto>();
            
            if (singleUserDto != null)
            {
                Console.WriteLine($"    Found unique user: {singleUserDto.FirstName} {singleUserDto.LastName}");
            }
        }
        catch (InvalidOperationException ex)
        {
            Console.WriteLine($"    Expected exception for non-unique query: {ex.Message}");
        }

        Console.WriteLine();
    }

    private async Task TestLinqToEntitiesQueries()
    {
        Console.WriteLine("2. Testing LINQ to Entities Queries:");
        Console.WriteLine("====================================");

        Console.WriteLine("  Testing complex LINQ query with projections:");
        var productDtos = await _context.Products
            .Where(p => p.IsAvailable && p.Price > 100)
            .OrderBy(p => p.Price)
            .ToFacetsAsync<DbProductDto>();

        Console.WriteLine($"    Retrieved {productDtos.Count} available products over $100:");
        foreach (var dto in productDtos)
        {
            Console.WriteLine($"      - {dto.Name}: ${dto.Price}");
        }

        Console.WriteLine("\n  Testing pagination with projections:");
        var pagedUsers = await _context.Users
            .OrderBy(u => u.LastName)
            .ThenBy(u => u.FirstName)
            .Skip(0)
            .Take(2)
            .ToFacetsAsync<DbUserDto>();

        Console.WriteLine($"    Retrieved page 1 ({pagedUsers.Count} users):");
        foreach (var dto in pagedUsers)
        {
            Console.WriteLine($"      - {dto.FirstName} {dto.LastName}");
        }

        Console.WriteLine();
    }

    private async Task TestEntityTrackingIntegration()
    {
        Console.WriteLine("3. Testing Entity Tracking Integration:");
        Console.WriteLine("======================================");

        Console.WriteLine("  Testing DTO creation from tracked entities:");
        
        var trackedUsers = await _context.Users.Take(2).ToListAsync();
        Console.WriteLine($"    Loaded {trackedUsers.Count} tracked entities");

        foreach (var user in trackedUsers)
        {
            var dto = user.ToFacet<DbUserDto>();
            Console.WriteLine($"      Tracked entity -> DTO: {dto.FirstName} {dto.LastName}");
            
            var entry = _context.Entry(user);
            Console.WriteLine($"        Entity state: {entry.State}");
        }

        Console.WriteLine("\n  Testing update -> DTO conversion:");
        var userToUpdate = trackedUsers.First();
        var originalBio = userToUpdate.Bio;
        
        userToUpdate.Bio = "Updated bio for tracking test";
        var updatedDto = userToUpdate.ToFacet<DbUserDto>();
        
        Console.WriteLine($"    Updated entity bio: {updatedDto.Bio}");
        Console.WriteLine($"    Entity state after update: {_context.Entry(userToUpdate).State}");
        
        userToUpdate.Bio = originalBio;

        Console.WriteLine();
    }

    private async Task TestComplexQueryProjections()
    {
        Console.WriteLine("4. Testing Complex Query Projections:");
        Console.WriteLine("=====================================");

        Console.WriteLine("  Testing aggregation queries:");
        
        var usersByActivity = await _context.Users
            .GroupBy(u => u.IsActive)
            .Select(g => new { IsActive = g.Key, Count = g.Count() })
            .ToListAsync();

        foreach (var group in usersByActivity)
        {
            Console.WriteLine($"    {(group.IsActive ? "Active" : "Inactive")} users: {group.Count}");
        }

        Console.WriteLine("\n  Testing date-based filtering with projections:");
        var recentUsers = await _context.Users
            .Where(u => u.CreatedAt > DateTime.UtcNow.AddDays(-120))
            .ToFacetsAsync<DbUserDto>();

        Console.WriteLine($"    Users created in last 120 days: {recentUsers.Count}");

        Console.WriteLine("\n  Testing null-safe projections:");
        var usersWithLastLogin = await _context.Users
            .Where(u => u.LastLoginAt != null)
            .ToFacetsAsync<DbUserDto>();

        Console.WriteLine($"    Users who have logged in: {usersWithLastLogin.Count}");
        foreach (var dto in usersWithLastLogin)
        {
            Console.WriteLine($"      - {dto.FirstName} {dto.LastName}: {dto.LastLoginAt:yyyy-MM-dd HH:mm}");
        }

        Console.WriteLine();
    }

    private async Task TestPerformanceComparisons()
    {
        Console.WriteLine("5. Testing Performance Comparisons:");
        Console.WriteLine("===================================");

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        stopwatch.Restart();
        var facetProjection = await _context.Users
            .ToFacetsAsync<DbUserDto>();
        stopwatch.Stop();
        var facetTime = stopwatch.ElapsedMilliseconds;

        Console.WriteLine($"  Facet projection: {facetProjection.Count} records in {facetTime}ms");

        stopwatch.Restart();
        var loadedUsers = await _context.Users.ToListAsync();
        var convertedDtos = loadedUsers.SelectFacets<DbUserDto>().ToList();
        stopwatch.Stop();
        var loadConvertTime = stopwatch.ElapsedMilliseconds;

        Console.WriteLine($"  Load+Convert: {convertedDtos.Count} records in {loadConvertTime}ms");

        // Test 3: Manual projection (for comparison)
        stopwatch.Restart();
        var manualProjection = await _context.Users
            .Select(u => new 
            {
                Id = u.Id,
                FirstName = u.FirstName,
                LastName = u.LastName,
                Email = u.Email,
                IsActive = u.IsActive,
                DateOfBirth = u.DateOfBirth,
                LastLoginAt = u.LastLoginAt,
                ProfilePictureUrl = u.ProfilePictureUrl,
                Bio = u.Bio
            })
            .ToListAsync();
        stopwatch.Stop();
        var manualTime = stopwatch.ElapsedMilliseconds;

        Console.WriteLine($"  Manual projection: {manualProjection.Count} records in {manualTime}ms");

        Console.WriteLine("\n  Performance Analysis:");
        Console.WriteLine($"    Facet vs Load+Convert: {(loadConvertTime > facetTime ? "Facet is faster" : "Load+Convert is faster")} by {Math.Abs(facetTime - loadConvertTime)}ms");
        Console.WriteLine($"    Facet vs Manual: {(manualTime > facetTime ? "Facet is faster" : "Manual is faster")} by {Math.Abs(facetTime - manualTime)}ms");

        Console.WriteLine();
    }
}