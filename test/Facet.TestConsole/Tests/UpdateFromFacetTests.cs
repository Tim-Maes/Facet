using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Facet.TestConsole.DTOs;
using Facet.TestConsole.Data;
using Facet.Extensions.EFCore;
using Facet.Extensions;

namespace Facet.TestConsole.Tests;

public class UpdateFromFacetTests
{
    private readonly FacetTestDbContext _context;
    private readonly ILogger<UpdateFromFacetTests> _logger;

    public UpdateFromFacetTests(FacetTestDbContext context, ILogger<UpdateFromFacetTests> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task RunAllTestsAsync()
    {
        Console.WriteLine("=== UpdateFromFacet Feature Tests (Entity -> DTO -> Mutate -> Entity) ===\n");

        // Temporarily reduce EF logging noise for clearer test output
        var originalLogLevel = _logger.IsEnabled(LogLevel.Information);

        await TestBasicMutationWorkflow();
        await TestSelectivePropertyMutation();
        await TestChangeTrackingMutation();
        await TestNoChangesMutation();
        await TestMultiplePropertyMutation();
        await TestNullValueMutation();
        await TestDifferentFacetKindMutations();
        await TestBulkMutationOperations();

        Console.WriteLine("\n=== All UpdateFromFacet mutation tests completed! ===");
    }

    private async Task TestBasicMutationWorkflow()
    {
        Console.WriteLine("1. Testing Basic Mutation Workflow (Entity ? DTO ? Mutate ? Entity):");
        Console.WriteLine("=====================================================================");

        var user = await _context.Users.FindAsync(1);
        if (user == null)
        {
            Console.WriteLine("User not found");
            return;
        }

        Console.WriteLine($"BEFORE: User {user.FirstName} {user.LastName}");
        Console.WriteLine($"  Email: {user.Email}");
        Console.WriteLine($"  Bio: {user.Bio ?? "NULL"}");
        Console.WriteLine($"  Profile Picture: {user.ProfilePictureUrl ?? "NULL"}");

        var userDto = user.ToFacet<UpdateDbUserDto>();
        Console.WriteLine($"\nDTO created from entity: {userDto.FirstName} {userDto.LastName}");

        userDto.LastName = "MutatedLastName";
        userDto.Bio = "This bio was mutated through DTO modification";
        userDto.ProfilePictureUrl = "https://example.com/mutated-avatar.jpg";
        userDto.LastLoginAt = DateTime.UtcNow;

        Console.WriteLine($"\nDTO after mutation: {userDto.FirstName} {userDto.LastName}");
        Console.WriteLine($"  Bio: {userDto.Bio}");
        Console.WriteLine($"  Profile Picture: {userDto.ProfilePictureUrl}");

        var result = user.UpdateFromFacetWithChanges(userDto, _context);
        await _context.SaveChangesAsync();

        Console.WriteLine($"\nAFTER applying mutated DTO: User {user.FirstName} {user.LastName}");
        Console.WriteLine($"  Email: {user.Email} (should be unchanged)");
        Console.WriteLine($"  Bio: {user.Bio}");
        Console.WriteLine($"  Profile Picture: {user.ProfilePictureUrl}");
        Console.WriteLine($"  Changed properties: {string.Join(", ", result.ChangedProperties)}");

        var refreshedUser = await _context.Users.FindAsync(1);
        Console.WriteLine($"\nVERIFICATION (fresh from DB): {refreshedUser!.FirstName} {refreshedUser.LastName}");
        Console.WriteLine($"  Bio: {refreshedUser.Bio}");
        Console.WriteLine($"  Profile Picture: {refreshedUser.ProfilePictureUrl}");

        Console.WriteLine();
    }

    private async Task TestSelectivePropertyMutation()
    {
        Console.WriteLine("2. Testing Selective Property Mutation:");
        Console.WriteLine("======================================");

        var user = await _context.Users.FindAsync(2);
        if (user == null) return;

        Console.WriteLine($"BEFORE: {user.FirstName} {user.LastName} ({user.Email})");

        var userDto = user.ToFacet<UpdateDbUserDto>();

        var originalEmail = userDto.Email;
        userDto.Email = "mutated.email@example.com";  // Only change email

        Console.WriteLine($"Mutating only Email: {originalEmail} -> {userDto.Email}");

        var result = user.UpdateFromFacetWithChanges(userDto, _context);
        await _context.SaveChangesAsync();

        Console.WriteLine($"AFTER: {user.FirstName} {user.LastName} ({user.Email})");
        Console.WriteLine($"Changed properties: {string.Join(", ", result.ChangedProperties)}");
        Console.WriteLine($"Expected: Only 'Email' should be changed");

        var dbUser = await _context.Users.AsNoTracking().FirstAsync(u => u.Id == 2);
        Console.WriteLine($"DB Verification: Email = {dbUser.Email}");

        Console.WriteLine();
    }

    private async Task TestChangeTrackingMutation()
    {
        Console.WriteLine("3. Testing Change Tracking with Multiple Mutations:");
        Console.WriteLine("==================================================");

        var user = await _context.Users.FindAsync(3);
        if (user == null) return;

        Console.WriteLine($"BEFORE: {user.FirstName} {user.LastName}");
        Console.WriteLine($"  IsActive: {user.IsActive}");
        Console.WriteLine($"  LastLoginAt: {user.LastLoginAt?.ToString("yyyy-MM-dd HH:mm") ?? "NULL"}");
        Console.WriteLine($"  Bio: {user.Bio ?? "NULL"}");

        var userDto = user.ToFacet<UpdateDbUserDto>();
        
        userDto.IsActive = true;
        userDto.LastLoginAt = DateTime.UtcNow;
        userDto.Bio = "Reactivated user with new bio";

        Console.WriteLine($"\nApplying mutations...");

        var result = user.UpdateFromFacetWithChanges(userDto, _context);
        await _context.SaveChangesAsync();

        Console.WriteLine($"AFTER: {user.FirstName} {user.LastName}");
        Console.WriteLine($"  IsActive: {user.IsActive}");
        Console.WriteLine($"  LastLoginAt: {user.LastLoginAt:yyyy-MM-dd HH:mm}");
        Console.WriteLine($"  Bio: {user.Bio}");
        Console.WriteLine($"Changed properties: {string.Join(", ", result.ChangedProperties)}");
        Console.WriteLine($"Number of changes: {result.ChangedProperties.Count}");

        Console.WriteLine();
    }

    private async Task TestNoChangesMutation()
    {
        Console.WriteLine("4. Testing No-Changes Mutation (DTO not modified):");
        Console.WriteLine("==================================================");

        var user = await _context.Users.FindAsync(1);
        if (user == null) return;

        var userDto = user.ToFacet<UpdateDbUserDto>();
        
        Console.WriteLine($"Created DTO from user {user.FirstName} {user.LastName}");
        Console.WriteLine("Applying DTO back without any mutations...");

        var result = user.UpdateFromFacetWithChanges(userDto, _context);

        Console.WriteLine($"Changed properties: {(result.ChangedProperties.Any() ? string.Join(", ", result.ChangedProperties) : "NONE")}");
        Console.WriteLine($"HasChanges: {result.HasChanges}");
        Console.WriteLine("Expected: No properties should be changed");

        Console.WriteLine();
    }

    private async Task TestMultiplePropertyMutation()
    {
        Console.WriteLine("5. Testing Multiple Property Mutation on Products:");
        Console.WriteLine("=================================================");

        var product = await _context.Products.FindAsync(1);
        if (product == null) return;

        Console.WriteLine($"BEFORE: {product.Name} - ${product.Price}");
        Console.WriteLine($"  Description: {product.Description}");
        Console.WriteLine($"  Available: {product.IsAvailable}");
        Console.WriteLine($"  Category: {product.CategoryId}");

        var productDto = product.ToFacet<UpdateDbProductDto>();
        
        productDto.Name = "Mutated MacBook Pro M3 Max";
        productDto.Price = 2499.99m;
        productDto.Description = "Mutated description: Even more powerful laptop";
        productDto.IsAvailable = false;

        Console.WriteLine($"\nApplying multiple mutations...");

        var result = product.UpdateFromFacetWithChanges(productDto, _context);
        await _context.SaveChangesAsync();

        Console.WriteLine($"AFTER: {product.Name} - ${product.Price}");
        Console.WriteLine($"  Description: {product.Description}");
        Console.WriteLine($"  Available: {product.IsAvailable}");
        Console.WriteLine($"  Category: {product.CategoryId} (should be unchanged)");
        Console.WriteLine($"Changed properties: {string.Join(", ", result.ChangedProperties)}");

        var dbProduct = await _context.Products.AsNoTracking().FirstAsync(p => p.Id == 1);
        Console.WriteLine($"DB Verification: {dbProduct.Name} - ${dbProduct.Price} - Available: {dbProduct.IsAvailable}");

        Console.WriteLine();
    }

    private async Task TestNullValueMutation()
    {
        Console.WriteLine("6. Testing Null Value Mutation:");
        Console.WriteLine("===============================");

        var user = await _context.Users.FindAsync(2);
        if (user == null) return;

        Console.WriteLine($"BEFORE: {user.FirstName} {user.LastName}");
        Console.WriteLine($"  Bio: {user.Bio ?? "NULL"}");
        Console.WriteLine($"  ProfilePictureUrl: {user.ProfilePictureUrl ?? "NULL"}");

        var userDto = user.ToFacet<UpdateDbUserDto>();
        
        userDto.Bio = null;
        userDto.ProfilePictureUrl = null;

        Console.WriteLine($"Setting Bio and ProfilePictureUrl to NULL...");

        var result = user.UpdateFromFacetWithChanges(userDto, _context);
        await _context.SaveChangesAsync();

        Console.WriteLine($"AFTER: {user.FirstName} {user.LastName}");
        Console.WriteLine($"  Bio: {user.Bio ?? "NULL"}");
        Console.WriteLine($"  ProfilePictureUrl: {user.ProfilePictureUrl ?? "NULL"}");
        Console.WriteLine($"Changed properties: {string.Join(", ", result.ChangedProperties)}");

        Console.WriteLine();
    }

    private async Task TestDifferentFacetKindMutations()
    {
        Console.WriteLine("7. Testing Different Facet Kind Mutations:");
        Console.WriteLine("==========================================");

        var user = await _context.Users.FindAsync(1);
        if (user == null) return;

        Console.WriteLine($"Testing with user: {user.FirstName} {user.LastName}");

        Console.WriteLine("\n  Testing Record DTO mutation:");
        var userRecord = user.ToFacet<DbUserRecord>();
        Console.WriteLine($"    Original Record: {userRecord.FirstName} {userRecord.LastName}");
        
        var mutatedRecord = userRecord with { 
            FirstName = "MutatedFirst", 
            LastName = "MutatedLast" 
        };
        Console.WriteLine($"    Mutated Record: {mutatedRecord.FirstName} {mutatedRecord.LastName}");

        var recordResult = user.UpdateFromFacetWithChanges(mutatedRecord, _context);
        Console.WriteLine($"    Record mutation applied, changed: {string.Join(", ", recordResult.ChangedProperties)}");

        Console.WriteLine("\n  Testing Struct DTO:");
        var userSummary = new DbUserSummary(user);
        Console.WriteLine($"    Struct DTO: {userSummary.FirstName} {userSummary.LastName} - Active: {userSummary.IsActive}");

        Console.WriteLine();
    }

    private async Task TestBulkMutationOperations()
    {
        Console.WriteLine("8. Testing Bulk Mutation Operations:");
        Console.WriteLine("====================================");

        var users = await _context.Users.Take(3).ToListAsync();
        Console.WriteLine($"Processing {users.Count} users for bulk mutation...");

        foreach (var user in users)
        {
            Console.WriteLine($"\n  Processing: {user.FirstName} {user.LastName}");
            
            var userDto = user.ToFacet<UpdateDbUserDto>();
            
            userDto.Bio = $"Bulk updated at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}";
            
            var result = user.UpdateFromFacetWithChanges(userDto, _context);
            Console.WriteLine($"    Changed: {string.Join(", ", result.ChangedProperties)}");
        }

        await _context.SaveChangesAsync();
        Console.WriteLine($"\nBulk mutation completed for {users.Count} users");

        var updatedUsers = await _context.Users.AsNoTracking().Take(3).ToListAsync();
        foreach (var user in updatedUsers)
        {
            Console.WriteLine($"  Verified: {user.FirstName} {user.LastName} - Bio: {user.Bio}");
        }

        Console.WriteLine();
    }
}