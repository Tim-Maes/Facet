using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Facet.Extensions.EFCore.Tasks;
using Facet.Extensions.EFCore.Tests.TestData;
using Microsoft.Build.Framework;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Facet.Extensions.EFCore.Tests.IntegrationTests;

/// <summary>
/// Robust integration tests for validating NavigationProperty serialization.
/// Uses proper service provider setup to ensure DbContext can be instantiated.
/// </summary>
public class RobustNavigationPropertyTests : IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ITestOutputHelper _output;

    public RobustNavigationPropertyTests(ITestOutputHelper output)
    {
        _output = output;
        
        var services = new ServiceCollection();
        
        // Configure EF Core with InMemory database
        services.AddDbContext<TestDbContext>(options =>
            options.UseInMemoryDatabase($"RobustTest_{Guid.NewGuid()}")
                   .EnableSensitiveDataLogging());
        
        // Add logging
        services.AddLogging(builder =>
            builder.AddConsole()
                   .SetMinimumLevel(LogLevel.Information));

        _serviceProvider = services.BuildServiceProvider();
    }

    [Fact]
    public async Task ExportEfModelTask_WithServiceProvider_ActuallyValidatesNavigationProperties()
    {
        // Arrange - ensure we can create and seed the context
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TestDbContext>();
        
        await context.Database.EnsureCreatedAsync();
        await SeedTestDataAsync(context);
        
        // Verify that our setup can actually query the context
        var userCount = await context.Users.CountAsync();
        var categoryCount = await context.Categories.CountAsync();
        _output.WriteLine($"Setup verification: {userCount} users, {categoryCount} categories");
        
        // Arrange - prepare MSBuild task
        var tempJsonFile = Path.Combine(Path.GetTempPath(), $"robust_test_{Guid.NewGuid()}.json");
        var testAssemblyPath = Assembly.GetAssembly(typeof(TestDbContext))?.Location;
        
        Assert.NotNull(testAssemblyPath);

        var exportTask = new ExportEfModelTask
        {
            AssemblyPath = testAssemblyPath,
            ContextTypes = typeof(TestDbContext).FullName!,
            OutputPath = tempJsonFile,
            BuildEngine = new RobustTestBuildEngine(_output)
        };

        try
        {
            _output.WriteLine("=== EXECUTING MSBuild TASK ===");
            
            // Act
            var result = exportTask.Execute();

            // Assert
            Assert.True(result, "Export task should complete successfully");
            Assert.True(File.Exists(tempJsonFile), "JSON file should be created");

            var jsonContent = await File.ReadAllTextAsync(tempJsonFile);
            Assert.NotEmpty(jsonContent);
            
            _output.WriteLine("=== GENERATED JSON CONTENT ===");
            _output.WriteLine(jsonContent);
            _output.WriteLine("=== END JSON CONTENT ===");

            // Parse and validate JSON structure
            using var jsonDocument = JsonDocument.Parse(jsonContent);
            var root = jsonDocument.RootElement;

            Assert.True(root.TryGetProperty("Contexts", out var contextsElement));
            Assert.Equal(JsonValueKind.Array, contextsElement.ValueKind);
            
            var contextCount = contextsElement.GetArrayLength();
            _output.WriteLine($"Found {contextCount} context(s) in JSON");
            
            if (contextCount == 0)
            {
                _output.WriteLine("❌ CRITICAL: No contexts were exported!");
                _output.WriteLine("This means the MSBuild task couldn't instantiate the DbContext.");
                _output.WriteLine("The navigation property validation cannot proceed.");
                Assert.Fail("Expected at least one context to be exported, but found none. The DbContext instantiation failed.");
                return;
            }

            // SUCCESS - we have at least one context
            _output.WriteLine("✅ SUCCESS: DbContext was properly instantiated and exported!");

            var firstContext = contextsElement[0];
            Assert.True(firstContext.TryGetProperty("Context", out var contextNameElement));
            Assert.True(firstContext.TryGetProperty("Entities", out var entitiesElement));

            var contextName = contextNameElement.GetString();
            _output.WriteLine($"Context name: {contextName}");

            Assert.Equal(JsonValueKind.Array, entitiesElement.ValueKind);
            var entityCount = entitiesElement.GetArrayLength();
            
            _output.WriteLine($"Found {entityCount} entities");
            Assert.True(entityCount >= 5, $"Expected at least 5 entities (User, Product, Category, Order, OrderItem), but found {entityCount}");

            // Validate that we have navigation properties
            var totalNavigationCount = 0;
            var entitiesWithNavigations = 0;
            
            foreach (var entity in entitiesElement.EnumerateArray())
            {
                if (entity.TryGetProperty("Name", out var nameElement) &&
                    entity.TryGetProperty("Navigations", out var navigationsElement))
                {
                    var entityName = nameElement.GetString();
                    var navigationCount = navigationsElement.GetArrayLength();
                    
                    if (navigationCount > 0)
                    {
                        entitiesWithNavigations++;
                        totalNavigationCount += navigationCount;
                        _output.WriteLine($"Entity '{entityName}' has {navigationCount} navigation(s)");
                        
                        foreach (var nav in navigationsElement.EnumerateArray())
                        {
                            if (nav.TryGetProperty("Name", out var navName) &&
                                nav.TryGetProperty("Target", out var navTarget) &&
                                nav.TryGetProperty("IsCollection", out var navIsCollection))
                            {
                                _output.WriteLine($"  - {navName.GetString()} -> {navTarget.GetString()} (Collection: {navIsCollection.GetBoolean()})");
                            }
                        }
                    }
                }
            }
            
            _output.WriteLine($"=== FINAL VALIDATION ===");
            _output.WriteLine($"Total entities with navigations: {entitiesWithNavigations}");
            _output.WriteLine($"Total navigation properties: {totalNavigationCount}");
            
            // Assert that we found meaningful navigation data
            Assert.True(entitiesWithNavigations >= 5, $"Expected all 5 entities to have navigations, found {entitiesWithNavigations}");
            Assert.True(totalNavigationCount >= 8, $"Expected at least 8 navigation properties, found {totalNavigationCount}");
            
            _output.WriteLine("✅ SUCCESS: All navigation properties were properly serialized to JSON!");
        }
        finally
        {
            if (File.Exists(tempJsonFile))
            {
                File.Delete(tempJsonFile);
            }
        }
    }

    [Fact]
    public async Task DbContext_CanBeInstantiatedAndQueriedProperly()
    {
        // This test ensures our setup can actually work with the DbContext
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TestDbContext>();
        
        await context.Database.EnsureCreatedAsync();
        await SeedTestDataAsync(context);
        
        // Test that we can query with includes (navigation properties)
        var categoriesWithProducts = await context.Categories
            .Include(c => c.Products)
            .ToListAsync();
            
        var usersWithOrders = await context.Users
            .Include(u => u.Orders)
                .ThenInclude(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
            .ToListAsync();
        
        Assert.NotEmpty(categoriesWithProducts);
        Assert.NotEmpty(usersWithOrders);
        
        var category = categoriesWithProducts.First();
        Assert.NotEmpty(category.Products);
        
        var user = usersWithOrders.First();
        Assert.NotEmpty(user.Orders);
        
        _output.WriteLine($"✅ DbContext works: {categoriesWithProducts.Count} categories, {usersWithOrders.Count} users");
        _output.WriteLine($"✅ Navigation properties work: Category has {category.Products.Count} products");
    }

    [Fact]
    public void EfModel_HasCorrectNavigationConfiguration()
    {
        // Verify EF model configuration
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TestDbContext>();
        
        var model = context.Model;
        
        // Check Category -> Products
        var categoryEntity = model.FindEntityType(typeof(Category));
        Assert.NotNull(categoryEntity);
        var categoryProductsNav = categoryEntity.FindNavigation("Products");
        Assert.NotNull(categoryProductsNav);
        Assert.True(categoryProductsNav.IsCollection);
        
        // Check Product -> Category  
        var productEntity = model.FindEntityType(typeof(Product));
        Assert.NotNull(productEntity);
        var productCategoryNav = productEntity.FindNavigation("Category");
        Assert.NotNull(productCategoryNav);
        Assert.False(productCategoryNav.IsCollection);
        
        // Check User -> Orders
        var userEntity = model.FindEntityType(typeof(User));
        Assert.NotNull(userEntity);
        var userOrdersNav = userEntity.FindNavigation("Orders");
        Assert.NotNull(userOrdersNav);
        Assert.True(userOrdersNav.IsCollection);
        
        _output.WriteLine("✅ EF model navigation configuration is correct");
    }

    private async Task SeedTestDataAsync(TestDbContext context)
    {
        if (await context.Categories.AnyAsync())
            return; // Already seeded

        var category = new Category
        {
            Name = "Electronics",
            Description = "Electronic devices and gadgets"
        };
        context.Categories.Add(category);

        var product = new Product
        {
            Name = "MacBook Pro",
            Description = "High-performance laptop",
            Price = 1299.99m,
            Category = category
        };
        context.Products.Add(product);

        var user = new User
        {
            FirstName = "John",
            LastName = "Doe",
            Email = "john.doe@example.com"
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
        
        _output.WriteLine("✅ Test data seeded successfully");
    }

    public void Dispose()
    {
        if (_serviceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}

/// <summary>
/// Test build engine for robust testing.
/// </summary>
public class RobustTestBuildEngine : IBuildEngine
{
    private readonly ITestOutputHelper _output;

    public RobustTestBuildEngine(ITestOutputHelper output)
    {
        _output = output;
    }

    public bool ContinueOnError => false;
    public int LineNumberOfTaskNode => 0;
    public int ColumnNumberOfTaskNode => 0;
    public string ProjectFileOfTaskNode => "RobustTest";

    public bool BuildProjectFile(string projectFileName, string[] targetNames,
                               System.Collections.IDictionary globalProperties,
                               System.Collections.IDictionary targetOutputs)
    {
        return true;
    }

    public void LogCustomEvent(CustomBuildEventArgs e)
    {
        _output.WriteLine($"[MSBuild Custom] {e.Message}");
    }

    public void LogErrorEvent(BuildErrorEventArgs e)
    {
        _output.WriteLine($"[MSBuild ERROR] {e.Message}");
    }

    public void LogMessageEvent(BuildMessageEventArgs e)
    {
        _output.WriteLine($"[MSBuild Message] {e.Message}");
    }

    public void LogWarningEvent(BuildWarningEventArgs e)
    {
        _output.WriteLine($"[MSBuild Warning] {e.Message}");
    }
}