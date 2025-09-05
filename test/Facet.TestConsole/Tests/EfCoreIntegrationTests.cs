using Facet.Extensions;
using Facet.Extensions.EFCore;
using Facet.Extensions.EFCore.Tasks;
using Facet.TestConsole.Data;
using Facet.TestConsole.DTOs;
using Microsoft.Build.Framework;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
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
        await TestEfModelJsonExport();

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
        
        // SQLite-compatible query - order by name instead of decimal price
        var productDtos = await _context.Products
            .Where(p => p.IsAvailable && p.Price > 100)
            .OrderBy(p => p.Name)  // Changed from OrderBy(p => p.Price) for SQLite compatibility
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

    private async Task TestEfModelJsonExport()
    {
        Console.WriteLine("6. Testing EF Model JSON Export:");
        Console.WriteLine("================================");

        // Create a temporary output file
        var tempJsonFile = Path.Combine(Path.GetTempPath(), "test_efmodel.json");
        
        try
        {
            Console.WriteLine("  Testing ExportEfModelTask:");

            // First, we need to build the test assembly to get the assembly path
            var testAssemblyPath = System.Reflection.Assembly.GetAssembly(typeof(FacetTestDbContext))?.Location;
            
            if (string.IsNullOrEmpty(testAssemblyPath))
            {
                Console.WriteLine("    ❌ Could not determine test assembly path");
                return;
            }

            Console.WriteLine($"    Using assembly: {Path.GetFileName(testAssemblyPath)}");

            // Create and execute the export task
            var exportTask = new ExportEfModelTask
            {
                AssemblyPath = testAssemblyPath,
                ContextTypes = "Facet.TestConsole.Data.FacetTestDbContext",
                OutputPath = tempJsonFile
            };

            // Mock the MSBuild logger
            exportTask.BuildEngine = new TestBuildEngine(_logger);

            Console.WriteLine("    Executing EF model export...");
            var success = exportTask.Execute();

            if (!success)
            {
                Console.WriteLine("    ❌ Export task failed");
                return;
            }

            Console.WriteLine("    ✅ Export task completed successfully");

            // Verify the JSON file was created
            if (!File.Exists(tempJsonFile))
            {
                Console.WriteLine("    ❌ JSON file was not created");
                return;
            }

            Console.WriteLine("    ✅ JSON file was created");

            // Read and parse the JSON
            var jsonContent = await File.ReadAllTextAsync(tempJsonFile);
            Console.WriteLine($"    JSON file size: {jsonContent.Length} characters");

            // Parse and validate the JSON structure
            JsonDocument doc;
            try
            {
                doc = JsonDocument.Parse(jsonContent);
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"    ❌ Invalid JSON: {ex.Message}");
                return;
            }

            Console.WriteLine("    ✅ JSON is valid");

            // Validate expected structure
            if (!doc.RootElement.TryGetProperty("Contexts", out var contextsElement))
            {
                Console.WriteLine("    ❌ Missing 'Contexts' property in root");
                return;
            }

            if (contextsElement.ValueKind != JsonValueKind.Array)
            {
                Console.WriteLine("    ❌ 'Contexts' is not an array");
                return;
            }

            var contextCount = contextsElement.GetArrayLength();
            Console.WriteLine($"    Found {contextCount} context(s)");

            if (contextCount == 0)
            {
                Console.WriteLine("    ❌ No contexts found in export");
                return;
            }

            // Examine the first context
            var firstContext = contextsElement[0];
            
            if (!firstContext.TryGetProperty("Context", out var contextNameElement) ||
                !firstContext.TryGetProperty("Entities", out var entitiesElement))
            {
                Console.WriteLine("    ❌ Context missing required properties");
                return;
            }

            var contextName = contextNameElement.GetString();
            Console.WriteLine($"    Context name: {contextName}");

            if (entitiesElement.ValueKind != JsonValueKind.Array)
            {
                Console.WriteLine("    ❌ 'Entities' is not an array");
                return;
            }

            var entityCount = entitiesElement.GetArrayLength();
            Console.WriteLine($"    Found {entityCount} entities");

            // Validate specific entities we expect
            var entityNames = new List<string>();
            foreach (var entity in entitiesElement.EnumerateArray())
            {
                if (entity.TryGetProperty("Name", out var nameElement))
                {
                    var name = nameElement.GetString();
                    if (name != null)
                        entityNames.Add(name);
                }
            }

            Console.WriteLine($"    Entity names: {string.Join(", ", entityNames)}");

            // Check for expected entities
            var expectedEntities = new[] { "User", "Product", "Category" };
            var foundAllExpected = expectedEntities.All(expected => 
                entityNames.Any(found => found.Contains(expected)));

            if (!foundAllExpected)
            {
                Console.WriteLine("    ⚠️  Some expected entities not found");
                Console.WriteLine($"      Expected: {string.Join(", ", expectedEntities)}");
            }
            else
            {
                Console.WriteLine("    ✅ All expected entities found");
            }

            // Check for navigations in Category entity (should have Products navigation)
            var categoryEntity = entitiesElement.EnumerateArray()
                .FirstOrDefault(e => e.TryGetProperty("Name", out var n) && 
                               n.GetString()?.Contains("Category") == true);

            if (categoryEntity.ValueKind != JsonValueKind.Undefined)
            {
                if (categoryEntity.TryGetProperty("Navigations", out var navigationsElement) &&
                    navigationsElement.ValueKind == JsonValueKind.Array)
                {
                    var navigationCount = navigationsElement.GetArrayLength();
                    Console.WriteLine($"    Category navigations: {navigationCount}");

                    if (navigationCount > 0)
                    {
                        var firstNav = navigationsElement[0];
                        if (firstNav.TryGetProperty("Name", out var navName) &&
                            firstNav.TryGetProperty("IsCollection", out var isCollection))
                        {
                            Console.WriteLine($"      Navigation: {navName.GetString()}, Collection: {isCollection.GetBoolean()}");
                        }
                    }
                }
            }

            Console.WriteLine("    ✅ JSON export validation completed successfully");
            
            // Display the complete JSON output
            Console.WriteLine("\n  Complete JSON output:");
            Console.WriteLine("  " + new string('=', 50));
            var lines = jsonContent.Split('\n');
            foreach (var line in lines)
            {
                Console.WriteLine($"    {line}");
            }
            Console.WriteLine("  " + new string('=', 50));

            doc.Dispose();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    ❌ Test failed with exception: {ex.Message}");
            _logger.LogError(ex, "EF Model JSON Export test failed");
        }
        finally
        {
            // Clean up temp file
            if (File.Exists(tempJsonFile))
            {
                try
                {
                    File.Delete(tempJsonFile);
                    Console.WriteLine("    🧹 Cleaned up temporary JSON file");
                }
                catch
                {
                    Console.WriteLine($"    ⚠️  Could not delete temp file: {tempJsonFile}");
                }
            }
        }

        Console.WriteLine();
    }
}

/// <summary>
/// Mock build engine for testing MSBuild tasks.
/// </summary>
public class TestBuildEngine : IBuildEngine
{
    private readonly Microsoft.Extensions.Logging.ILogger _logger;

    public TestBuildEngine(Microsoft.Extensions.Logging.ILogger logger)
    {
        _logger = logger;
    }

    public bool ContinueOnError => false;

    public int LineNumberOfTaskNode => 0;

    public int ColumnNumberOfTaskNode => 0;

    public string ProjectFileOfTaskNode => "TestProject";

    public bool BuildProjectFile(string projectFileName, string[] targetNames, IDictionary globalProperties, IDictionary targetOutputs)
    {
        return true;
    }

    public void LogCustomEvent(CustomBuildEventArgs e)
    {
        _logger.LogInformation("Custom: {Message}", e.Message);
    }

    public void LogErrorEvent(BuildErrorEventArgs e)
    {
        _logger.LogError("Error: {Message}", e.Message);
    }

    public void LogMessageEvent(BuildMessageEventArgs e)
    {
        _logger.LogInformation("Message: {Message}", e.Message);
    }

    public void LogWarningEvent(BuildWarningEventArgs e)
    {
        _logger.LogWarning("Warning: {Message}", e.Message);
    }
}