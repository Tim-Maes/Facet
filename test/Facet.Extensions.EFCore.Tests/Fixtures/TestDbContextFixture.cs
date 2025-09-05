using System;
using System.Linq;
using Facet.Extensions.EFCore.Tests.TestData;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Facet.Extensions.EFCore.Tests.Fixtures;

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

        services.AddLogging();

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
                new Category { Name = "Electronics", Description = "Electronic devices and gadgets" },
                new Category { Name = "Books", Description = "Books and educational materials" },
                new Category { Name = "Clothing", Description = "Apparel and accessories" }
            };
            Context.Categories.AddRange(categories);

            // Seed Users
            var users = new[]
            {
                new User
                {
                    FirstName = "John",
                    LastName = "Doe",
                    Email = "john.doe@example.com",
                    IsActive = true
                },
                new User
                {
                    FirstName = "Jane",
                    LastName = "Smith",
                    Email = "jane.smith@example.com",
                    IsActive = true
                }
            };
            Context.Users.AddRange(users);

            Context.SaveChanges();

            // Seed Products (after categories are saved to get IDs)
            var products = new[]
            {
                new Product
                {
                    Name = "MacBook Pro",
                    Description = "High-performance laptop for professionals",
                    Price = 1299.99m,
                    CategoryId = categories[0].Id,
                    IsAvailable = true
                },
                new Product
                {
                    Name = "iPhone 15",
                    Description = "Latest smartphone with advanced features",
                    Price = 899.99m,
                    CategoryId = categories[0].Id,
                    IsAvailable = true
                },
                new Product
                {
                    Name = "Clean Code",
                    Description = "A Handbook of Agile Software Craftsmanship",
                    Price = 49.99m,
                    CategoryId = categories[1].Id,
                    IsAvailable = true
                }
            };
            Context.Products.AddRange(products);

            Context.SaveChanges();

            // Seed Orders
            var orders = new[]
            {
                new Order
                {
                    UserId = users[0].Id,
                    TotalAmount = 1349.98m,
                    Status = "Completed"
                },
                new Order
                {
                    UserId = users[1].Id,
                    TotalAmount = 49.99m,
                    Status = "Pending"
                }
            };
            Context.Orders.AddRange(orders);

            Context.SaveChanges();

            // Seed OrderItems
            var orderItems = new[]
            {
                new OrderItem
                {
                    OrderId = orders[0].Id,
                    ProductId = products[0].Id,
                    Quantity = 1,
                    UnitPrice = 1299.99m
                },
                new OrderItem
                {
                    OrderId = orders[0].Id,
                    ProductId = products[2].Id,
                    Quantity = 1,
                    UnitPrice = 49.99m
                },
                new OrderItem
                {
                    OrderId = orders[1].Id,
                    ProductId = products[2].Id,
                    Quantity = 1,
                    UnitPrice = 49.99m
                }
            };
            Context.OrderItems.AddRange(orderItems);

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