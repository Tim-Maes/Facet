using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Facet.Extensions.EFCore.Tasks;
using Facet.Extensions.EFCore.Tests.TestData;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Build.Framework;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Facet.Extensions.EFCore.Tests.IntegrationTests;

/// <summary>
/// Integration tests for validating NavigationProperty serialization using WebApplicationFactory.
/// This ensures the DbContext can be properly instantiated in a realistic environment.
/// </summary>
public class NavigationPropertyIntegrationTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;
    private readonly ITestOutputHelper _output;

    public NavigationPropertyIntegrationTests(TestWebApplicationFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;
    }

    [Fact]
    public async Task ExportEfModelTask_WithWebApplicationFactory_ProperlySerializesNavigationProperties()
    {
        // Arrange
        var tempJsonFile = Path.Combine(Path.GetTempPath(), $"integration_test_{Guid.NewGuid()}.json");
        
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TestDbContext>();
        
        // Ensure the database is created and seeded
        await context.Database.EnsureCreatedAsync();
        await SeedTestDataAsync(context);
        
        var testAssemblyPath = Assembly.GetAssembly(typeof(TestDbContext))?.Location;
        Assert.NotNull(testAssemblyPath);

        var exportTask = new ExportEfModelTask
        {
            AssemblyPath = testAssemblyPath,
            ContextTypes = typeof(TestDbContext).FullName!,
            OutputPath = tempJsonFile,
            BuildEngine = new IntegrationTestBuildEngine(_output)
        };

        try
        {
            // Act
            var result = exportTask.Execute();

            // Assert
            Assert.True(result, "Export task should complete successfully");
            Assert.True(File.Exists(tempJsonFile), "JSON file should be created");

            var jsonContent = await File.ReadAllTextAsync(tempJsonFile);
            Assert.NotEmpty(jsonContent);
            
            _output.WriteLine("Generated JSON content:");
            _output.WriteLine(jsonContent);

            // Parse and validate JSON structure
            using var jsonDocument = JsonDocument.Parse(jsonContent);
            var root = jsonDocument.RootElement;

            // Validate root structure
            Assert.True(root.TryGetProperty("Contexts", out var contextsElement));
            Assert.Equal(JsonValueKind.Array, contextsElement.ValueKind);
            
            // Should have exactly one context
            Assert.Equal(1, contextsElement.GetArrayLength());

            var firstContext = contextsElement[0];
            Assert.True(firstContext.TryGetProperty("Context", out var contextNameElement));
            Assert.True(firstContext.TryGetProperty("Entities", out var entitiesElement));

            var contextName = contextNameElement.GetString();
            Assert.Equal(typeof(TestDbContext).FullName, contextName);

            // Validate entities
            Assert.Equal(JsonValueKind.Array, entitiesElement.ValueKind);
            var entityCount = entitiesElement.GetArrayLength();
            Assert.Equal(5, entityCount); // User, Product, Category, Order, OrderItem

            _output.WriteLine($"✅ Found {entityCount} entities as expected");

            // Validate specific navigation properties
            await ValidateNavigationPropertiesAsync(entitiesElement);
        }
        finally
        {
            if (File.Exists(tempJsonFile))
            {
                File.Delete(tempJsonFile);
            }
        }
    }

    private async Task SeedTestDataAsync(TestDbContext context)
    {
        if (await context.Categories.AnyAsync())
            return; // Already seeded

        var category = new Category
        {
            Name = "Electronics",
            Description = "Electronic devices"
        };
        context.Categories.Add(category);

        var product = new Product
        {
            Name = "Laptop",
            Description = "Gaming laptop",
            Price = 1299.99m,
            Category = category
        };
        context.Products.Add(product);

        var user = new User
        {
            FirstName = "John",
            LastName = "Doe",
            Email = "john@example.com"
        };
        context.Users.Add(user);

        var order = new Order
        {
            User = user,
            TotalAmount = 1299.99m,
            Status = "Completed"
        };
        context.Orders.Add(order);

        var orderItem = new OrderItem
        {
            Order = order,
            Product = product,
            Quantity = 1,
            UnitPrice = 1299.99m
        };
        context.OrderItems.Add(orderItem);

        await context.SaveChangesAsync();
    }

    private async Task ValidateNavigationPropertiesAsync(JsonElement entitiesElement)
    {
        var entityLookup = new Dictionary<string, JsonElement>();
        
        // Build lookup for easier entity finding
        foreach (var entity in entitiesElement.EnumerateArray())
        {
            if (entity.TryGetProperty("Name", out var nameElement))
            {
                var entityName = nameElement.GetString();
                if (entityName != null)
                {
                    var simpleName = entityName.Contains('.') ? entityName.Split('.')[^1] : entityName;
                    entityLookup[simpleName] = entity;
                }
            }
        }

        _output.WriteLine($"Entity lookup contains: {string.Join(", ", entityLookup.Keys)}");

        // Validate all expected navigation properties
        var validationTasks = new[]
        {
            ValidateEntityNavigationAsync(entityLookup, "Category", "Products", true),
            ValidateEntityNavigationAsync(entityLookup, "Product", "Category", false),
            ValidateEntityNavigationAsync(entityLookup, "Product", "OrderItems", true),
            ValidateEntityNavigationAsync(entityLookup, "User", "Orders", true),
            ValidateEntityNavigationAsync(entityLookup, "Order", "User", false),
            ValidateEntityNavigationAsync(entityLookup, "Order", "OrderItems", true),
            ValidateEntityNavigationAsync(entityLookup, "OrderItem", "Order", false),
            ValidateEntityNavigationAsync(entityLookup, "OrderItem", "Product", false)
        };

        await Task.WhenAll(validationTasks);
    }

    private async Task ValidateEntityNavigationAsync(
        Dictionary<string, JsonElement> entityLookup, 
        string entityName, 
        string navigationName, 
        bool expectedIsCollection)
    {
        await Task.Yield(); // Make it async for demonstration

        if (!entityLookup.TryGetValue(entityName, out var entity))
        {
            Assert.True(false, $"Entity '{entityName}' not found in lookup");
            return;
        }

        if (!entity.TryGetProperty("Navigations", out var navigationsElement))
        {
            Assert.True(false, $"Entity '{entityName}' has no Navigations property");
            return;
        }

        var foundNavigation = false;
        foreach (var navigation in navigationsElement.EnumerateArray())
        {
            if (navigation.TryGetProperty("Name", out var nameElement) &&
                navigation.TryGetProperty("IsCollection", out var isCollectionElement))
            {
                var navName = nameElement.GetString();
                if (navName == navigationName)
                {
                    var actualIsCollection = isCollectionElement.GetBoolean();
                    Assert.Equal(expectedIsCollection, actualIsCollection);
                    
                    _output.WriteLine($"✅ {entityName}.{navigationName} -> IsCollection: {actualIsCollection}");
                    foundNavigation = true;
                    break;
                }
            }
        }

        Assert.True(foundNavigation, $"Navigation '{navigationName}' not found on entity '{entityName}'");
    }

    [Fact]
    public void DbContext_CanBeInstantiatedFromServiceProvider()
    {
        // This test validates that our DbContext setup works correctly
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TestDbContext>();
        
        Assert.NotNull(context);
        Assert.NotNull(context.Users);
        Assert.NotNull(context.Products);
        Assert.NotNull(context.Categories);
        Assert.NotNull(context.Orders);
        Assert.NotNull(context.OrderItems);
        
        _output.WriteLine("✅ DbContext instantiated successfully from DI container");
    }

    [Fact]
    public async Task DbContext_NavigationPropertiesConfiguredCorrectly()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TestDbContext>();
        
        await context.Database.EnsureCreatedAsync();
        
        // Verify model configuration
        var model = context.Model;
        var categoryEntity = model.FindEntityType(typeof(Category));
        var productEntity = model.FindEntityType(typeof(Product));
        
        Assert.NotNull(categoryEntity);
        Assert.NotNull(productEntity);
        
        var categoryProductsNav = categoryEntity.FindNavigation("Products");
        var productCategoryNav = productEntity.FindNavigation("Category");
        
        Assert.NotNull(categoryProductsNav);
        Assert.NotNull(productCategoryNav);
        Assert.True(categoryProductsNav.IsCollection);
        Assert.False(productCategoryNav.IsCollection);
        
        _output.WriteLine("✅ Navigation properties configured correctly in EF model");
    }
}

/// <summary>
/// Custom WebApplicationFactory for integration tests.
/// </summary>
public class TestWebApplicationFactory : WebApplicationFactory<TestStartup>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseStartup<TestStartup>();
        
        builder.ConfigureServices(services =>
        {
            // Override any services if needed for testing
        });
    }
}

/// <summary>
/// Test implementation of IBuildEngine for integration tests.
/// </summary>
public class IntegrationTestBuildEngine : IBuildEngine
{
    private readonly ITestOutputHelper _output;

    public IntegrationTestBuildEngine(ITestOutputHelper output)
    {
        _output = output;
    }

    public bool ContinueOnError => false;
    public int LineNumberOfTaskNode => 0;
    public int ColumnNumberOfTaskNode => 0;
    public string ProjectFileOfTaskNode => "IntegrationTest";

    public bool BuildProjectFile(string projectFileName, string[] targetNames,
                               System.Collections.IDictionary globalProperties,
                               System.Collections.IDictionary targetOutputs)
    {
        return true;
    }

    public void LogCustomEvent(CustomBuildEventArgs e)
    {
        _output.WriteLine($"Custom: {e.Message}");
    }

    public void LogErrorEvent(BuildErrorEventArgs e)
    {
        _output.WriteLine($"Error: {e.Message}");
    }

    public void LogMessageEvent(BuildMessageEventArgs e)
    {
        _output.WriteLine($"Message: {e.Message}");
    }

    public void LogWarningEvent(BuildWarningEventArgs e)
    {
        _output.WriteLine($"Warning: {e.Message}");
    }
}