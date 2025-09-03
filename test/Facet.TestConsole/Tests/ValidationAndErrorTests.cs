using Facet.Extensions.EFCore;
using Facet.TestConsole.Data;
using Facet.TestConsole.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Facet.TestConsole.Tests;

public class ValidationAndErrorTests
{
    private readonly FacetTestDbContext _context;
    private readonly ILogger<ValidationAndErrorTests> _logger;

    public ValidationAndErrorTests(FacetTestDbContext context, ILogger<ValidationAndErrorTests> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task RunAllTestsAsync()
    {
        Console.WriteLine("=== Validation and Error Handling Tests ===\n");

        await TestEntityValidation();
        await TestDtoValidation();
        await TestConcurrencyConflicts();
        await TestConstraintViolations();
        await TestErrorRecovery();

        Console.WriteLine("\n=== All validation and error tests completed! ===");
    }

    private async Task TestEntityValidation()
    {
        Console.WriteLine("1. Testing Entity Validation:");
        Console.WriteLine("=============================");

        Console.WriteLine("  Testing required field validation:");
        try
        {
            var invalidUser = new Data.User
            {
                IsActive = true,
                DateOfBirth = DateTime.Now.AddYears(-25),
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(invalidUser);
            await _context.SaveChangesAsync();
            Console.WriteLine("    ERROR: Should have failed validation!");
        }
        catch (DbUpdateException ex)
        {
            Console.WriteLine($"    SUCCESS: Correctly caught validation error: {ex.InnerException?.Message ?? ex.Message}");
            _context.ChangeTracker.Clear();
        }

        Console.WriteLine("\n  Testing email uniqueness constraint:");
        try
        {
            var duplicateEmailUser = new Data.User
            {
                FirstName = "Duplicate",
                LastName = "User",
                Email = "john.doe@example.com", // This email already exists
                Password = "password123",
                IsActive = true,
                DateOfBirth = DateTime.Now.AddYears(-30),
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(duplicateEmailUser);
            await _context.SaveChangesAsync();
            Console.WriteLine("    ERROR: Should have failed uniqueness constraint!");
        }
        catch (DbUpdateException ex)
        {
            Console.WriteLine($"    SUCCESS: Correctly caught uniqueness constraint violation: {ex.InnerException?.Message ?? ex.Message}");
            _context.ChangeTracker.Clear();
        }

        Console.WriteLine();
    }

    private async Task TestDtoValidation()
    {
        Console.WriteLine("2. Testing DTO Validation:");
        Console.WriteLine("==========================");

        var user = await _context.Users.FirstAsync();
        
        Console.WriteLine("  Testing DTO property constraints:");
        
        var updateDto = new UpdateDbUserDto(user)
        {
            Email = "invalid-email-format",
            FirstName = "",
            DateOfBirth = DateTime.Now.AddYears(10)
        };

        try
        {
            user.UpdateFromFacet(updateDto, _context);
            await _context.SaveChangesAsync();
            Console.WriteLine("    ERROR: Should have failed validation!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    SUCCESS: Correctly caught DTO validation error: {ex.Message}");
            _context.ChangeTracker.Clear();
            await _context.Entry(user).ReloadAsync();
        }

        Console.WriteLine("\n  Testing valid DTO update:");
        var validUpdateDto = new UpdateDbUserDto(user)
        {
            Bio = "Updated bio through validation test"
        };

        user.UpdateFromFacet(validUpdateDto, _context);
        await _context.SaveChangesAsync();
        Console.WriteLine($"    SUCCESS: Successfully updated user bio: {user.Bio}");

        Console.WriteLine();
    }

    private async Task TestConcurrencyConflicts()
    {
        Console.WriteLine("3. Testing Concurrency Conflicts:");
        Console.WriteLine("=================================");

        Console.WriteLine("  Simulating concurrent update scenario:");

        var user1 = await _context.Users.FirstAsync();
        var originalBio = user1.Bio;

        var updateDto1 = new UpdateDbUserDto(user1)
        {
            Bio = "Updated by first operation"
        };
        user1.UpdateFromFacet(updateDto1, _context);
        await _context.SaveChangesAsync();
        Console.WriteLine($"    First update completed: {user1.Bio}");

        var updateDto2 = new UpdateDbUserDto(user1)
        {
            Bio = "Updated by second operation"
        };
        user1.UpdateFromFacet(updateDto2, _context);
        await _context.SaveChangesAsync();
        Console.WriteLine($"    Second update completed: {user1.Bio}");

        user1.Bio = originalBio;
        await _context.SaveChangesAsync();

        Console.WriteLine();
    }

    private async Task TestConstraintViolations()
    {
        Console.WriteLine("4. Testing Database Constraint Violations:");
        Console.WriteLine("==========================================");

        // Test foreign key constraint (if we had one)
        Console.WriteLine("  Testing foreign key constraints:");
        
        var product = await _context.Products.FirstAsync();
        var originalCategoryId = product.CategoryId;

        try
        {
            var updateDto = new UpdateDbProductDto(product)
            {
                CategoryId = 999 // Non-existent category
            };

            product.UpdateFromFacet(updateDto, _context);
            await _context.SaveChangesAsync();
            Console.WriteLine("    ERROR: Should have failed foreign key constraint!");
        }
        catch (DbUpdateException ex)
        {
            Console.WriteLine($"    SUCCESS: Correctly caught foreign key constraint violation: {ex.InnerException?.Message ?? ex.Message}");
            _context.ChangeTracker.Clear();
            await _context.Entry(product).ReloadAsync();
        }

        Console.WriteLine("\n  Testing string length constraints:");
        var user = await _context.Users.FirstAsync();
        var originalFirstName = user.FirstName;

        try
        {
            var updateDto = new UpdateDbUserDto(user)
            {
                FirstName = new string('A', 150) // Exceeds MaxLength(100)
            };

            user.UpdateFromFacet(updateDto, _context);
            await _context.SaveChangesAsync();
            Console.WriteLine("    ERROR: Should have failed string length constraint!");
        }
        catch (DbUpdateException ex)
        {
            Console.WriteLine($"    SUCCESS: Correctly caught string length constraint violation: {ex.InnerException?.Message ?? ex.Message}");
            _context.ChangeTracker.Clear();
            await _context.Entry(user).ReloadAsync();
        }

        Console.WriteLine();
    }

    private async Task TestErrorRecovery()
    {
        Console.WriteLine("5. Testing Error Recovery:");
        Console.WriteLine("==========================");

        Console.WriteLine("  Testing graceful error recovery:");
        
        var user = await _context.Users.FirstAsync();
        var originalEmail = user.Email;
        var originalBio = user.Bio;

        var operations = new List<(string description, Func<Task> operation)>
        {
            ("Valid bio update", async () => {
                var dto = new UpdateDbUserDto(user) { Bio = "Recovery test bio" };
                user.UpdateFromFacet(dto, _context);
                await _context.SaveChangesAsync();
            }),
            
            ("Invalid email update", async () => {
                var dto = new UpdateDbUserDto(user) { Email = "jane.smith@example.com" }; // Duplicate email
                user.UpdateFromFacet(dto, _context);
                await _context.SaveChangesAsync();
            }),
            
            ("Valid profile picture update", async () => {
                var dto = new UpdateDbUserDto(user) { ProfilePictureUrl = "https://example.com/recovery-test.jpg" };
                user.UpdateFromFacet(dto, _context);
                await _context.SaveChangesAsync();
            })
        };

        foreach (var (description, operation) in operations)
        {
            try
            {
                await operation();
                Console.WriteLine($"    SUCCESS: {description}: Success");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"    INFO: {description}: Failed - {ex.Message}");
                _context.ChangeTracker.Clear();
                await _context.Entry(user).ReloadAsync();
            }
        }

        Console.WriteLine($"\n  Final user state:");
        Console.WriteLine($"    Email: {user.Email} (should be original: {originalEmail})");
        Console.WriteLine($"    Bio: {user.Bio}");
        Console.WriteLine($"    Profile Picture: {user.ProfilePictureUrl}");

        user.Bio = originalBio;
        user.Email = originalEmail;
        user.ProfilePictureUrl = null;
        await _context.SaveChangesAsync();

        Console.WriteLine();
    }
}