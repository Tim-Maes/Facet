using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Facet.UnitTests.TestFixtures;

/// <summary>
/// Test fixture for Entity Framework integration tests.
/// Uses in-memory SQLite database for fast, isolated testing.
/// </summary>
public class TestDbContextFixture : IDisposable
{
    public TestDbContext Context { get; }
    public IServiceProvider ServiceProvider { get; }

    public TestDbContextFixture()
    {
        var services = new ServiceCollection();

        services.AddDbContext<TestDbContext>(options =>
            options.UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}")
                   .EnableSensitiveDataLogging());

        services.AddLogging(builder => 
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        ServiceProvider = services.BuildServiceProvider();
        Context = ServiceProvider.GetRequiredService<TestDbContext>();

        // Ensure database is created and seed test data
        Context.Database.EnsureCreated();
        SeedTestData();
    }

    private void SeedTestData()
    {
        if (!Context.Categories.Any())
        {
            // Seed Categories
            var categories = new[]
            {
                new Category 
                { 
                    Id = 1,
                    Name = "Electronics", 
                    Description = "Electronic devices and gadgets",
                    CreatedAt = DateTime.UtcNow.AddDays(-30)
                },
                new Category 
                { 
                    Id = 2,
                    Name = "Books", 
                    Description = "Books and educational materials",
                    CreatedAt = DateTime.UtcNow.AddDays(-25)
                },
                new Category 
                { 
                    Id = 3,
                    Name = "Clothing", 
                    Description = "Apparel and accessories",
                    CreatedAt = DateTime.UtcNow.AddDays(-20)
                }
            };
            Context.Categories.AddRange(categories);
            Context.SaveChanges();

            // Seed Users
            var users = new[]
            {
                new User
                {
                    Id = 1,
                    FirstName = "John",
                    LastName = "Doe",
                    Email = "john.doe@example.com",
                    Password = "hashedpassword123",
                    IsActive = true,
                    DateOfBirth = new DateTime(1990, 5, 15),
                    CreatedAt = DateTime.UtcNow.AddDays(-100),
                    LastLoginAt = DateTime.UtcNow.AddHours(-2),
                    Bio = "Software developer passionate about clean code"
                },
                new User
                {
                    Id = 2,
                    FirstName = "Jane",
                    LastName = "Smith",
                    Email = "jane.smith@example.com",
                    Password = "hashedpassword456",
                    IsActive = true,
                    DateOfBirth = new DateTime(1985, 12, 3),
                    CreatedAt = DateTime.UtcNow.AddDays(-90),
                    LastLoginAt = DateTime.UtcNow.AddDays(-1),
                    Bio = "UX designer with 10+ years experience"
                },
                new User
                {
                    Id = 3,
                    FirstName = "Bob",
                    LastName = "Johnson",
                    Email = "bob.johnson@example.com",
                    Password = "hashedpassword789",
                    IsActive = false,
                    DateOfBirth = new DateTime(1992, 8, 22),
                    CreatedAt = DateTime.UtcNow.AddDays(-80),
                    LastLoginAt = null,
                    Bio = "Data analyst and machine learning enthusiast"
                }
            };
            Context.Users.AddRange(users);
            Context.SaveChanges();

            // Seed Products
            var products = new[]
            {
                new Product
                {
                    Id = 1,
                    Name = "MacBook Pro",
                    Description = "High-performance laptop for professionals",
                    Price = 1299.99m,
                    CategoryId = 1,
                    IsAvailable = true,
                    CreatedAt = DateTime.UtcNow.AddDays(-50),
                    InternalNotes = "Supplier: Apple Inc. - Margin: 25%"
                },
                new Product
                {
                    Id = 2,
                    Name = "iPhone 15",
                    Description = "Latest smartphone with advanced features",
                    Price = 899.99m,
                    CategoryId = 1,
                    IsAvailable = true,
                    CreatedAt = DateTime.UtcNow.AddDays(-40),
                    InternalNotes = "Supplier: Apple Inc. - Margin: 30%"
                },
                new Product
                {
                    Id = 3,
                    Name = "Clean Code",
                    Description = "A Handbook of Agile Software Craftsmanship",
                    Price = 49.99m,
                    CategoryId = 2,
                    IsAvailable = true,
                    CreatedAt = DateTime.UtcNow.AddDays(-30),
                    InternalNotes = "Publisher: Prentice Hall - Margin: 40%"
                },
                new Product
                {
                    Id = 4,
                    Name = "Winter Jacket",
                    Description = "Warm and comfortable winter jacket",
                    Price = 159.99m,
                    CategoryId = 3,
                    IsAvailable = false,
                    CreatedAt = DateTime.UtcNow.AddDays(-20),
                    InternalNotes = "Supplier: OutdoorGear Co. - Margin: 50%"
                }
            };
            Context.Products.AddRange(products);
            Context.SaveChanges();
        }
    }

    public void Dispose()
    {
        Context?.Dispose();
        if (ServiceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}