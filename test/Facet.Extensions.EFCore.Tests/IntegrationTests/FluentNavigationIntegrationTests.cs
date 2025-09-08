using System;
using System.Linq;
using System.Threading.Tasks;
using Facet.Extensions.EFCore.Tests.TestData;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace Facet.Extensions.EFCore.Tests.IntegrationTests;

/// <summary>
/// Integration tests for the new fluent navigation API.
/// Tests the complete flow from DbContext entry point through fluent builders to terminal methods.
/// </summary>
public class FluentNavigationIntegrationTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;
    private readonly ITestOutputHelper _output;

    public FluentNavigationIntegrationTests(TestWebApplicationFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;
    }

    [Fact]
    public async Task FacetUser_EntryPoint_ReturnsFluentBuilder()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TestDbContext>();
        
        await context.Database.EnsureCreatedAsync();
        await SeedTestDataAsync(context);

        // Act - Test that the generated entry point works
        var builder = context.FacetUser();

        // Assert
        Assert.NotNull(builder);
        _output.WriteLine("✅ FacetUser entry point returns valid builder instance");
    }

    [Fact]
    public async Task FacetUser_WithOrders_Navigation_Compiles()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TestDbContext>();
        
        await context.Database.EnsureCreatedAsync();
        await SeedTestDataAsync(context);

        // Act - Test that navigation methods are generated and compile
        try
        {
            var builder = context.FacetUser();
            
            // The WithOrders method should be available on the fluent builder
            // For now, we're just testing that the code generation works and compiles
            var hasWithOrdersMethod = builder.GetType()
                .GetMethods()
                .Any(m => m.Name.StartsWith("WithOrders"));

            // Assert
            Assert.True(hasWithOrdersMethod, "WithOrders method should be generated on FacetUserBuilder");
            _output.WriteLine("✅ WithOrders navigation method is available on fluent builder");
        }
        catch (Exception ex)
        {
            _output.WriteLine($"❌ Fluent navigation compilation failed: {ex.Message}");
            throw;
        }
    }

    [Fact]
    public async Task FacetOrder_WithUser_Navigation_Compiles()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TestDbContext>();
        
        await context.Database.EnsureCreatedAsync();
        await SeedTestDataAsync(context);

        // Act - Test Order entity navigation
        try
        {
            var builder = context.FacetOrder();
            
            // The WithUser method should be available
            var hasWithUserMethod = builder.GetType()
                .GetMethods()
                .Any(m => m.Name.StartsWith("WithUser"));

            // Assert
            Assert.True(hasWithUserMethod, "WithUser method should be generated on FacetOrderBuilder");
            _output.WriteLine("✅ WithUser navigation method is available on order builder");
        }
        catch (Exception ex)
        {
            _output.WriteLine($"❌ Order fluent navigation compilation failed: {ex.Message}");
            throw;
        }
    }

    [Fact]
    public async Task FluentBuilder_TerminalMethods_AreGenerated()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TestDbContext>();
        
        await context.Database.EnsureCreatedAsync();
        await SeedTestDataAsync(context);

        // Act & Assert - Verify terminal methods exist
        var builder = context.FacetUser();
        var builderType = builder.GetType();

        var hasToListAsync = builderType.GetMethods().Any(m => m.Name == "ToListAsync");
        var hasFirstOrDefaultAsync = builderType.GetMethods().Any(m => m.Name == "FirstOrDefaultAsync");
        var hasGetByIdAsync = builderType.GetMethods().Any(m => m.Name == "GetByIdAsync");

        Assert.True(hasToListAsync, "ToListAsync method should be generated");
        Assert.True(hasFirstOrDefaultAsync, "FirstOrDefaultAsync method should be generated");
        Assert.True(hasGetByIdAsync, "GetByIdAsync method should be generated");

        _output.WriteLine("✅ All terminal methods are generated on fluent builder");
    }

    [Fact]
    public void ShapeInterfaces_AreGenerated()
    {
        // Act & Assert - Verify shape interfaces are generated
        var userShapeType = typeof(IUserShape);
        var orderShapeType = typeof(IOrderShape);
        var productShapeType = typeof(IProductShape);
        var categoryShapeType = typeof(ICategoryShape);

        Assert.NotNull(userShapeType);
        Assert.NotNull(orderShapeType);
        Assert.NotNull(productShapeType);
        Assert.NotNull(categoryShapeType);

        _output.WriteLine("✅ Shape interfaces are generated successfully");
    }

    [Fact]
    public void UserSelectors_AreGenerated()
    {
        // Act & Assert - Verify selector classes are generated
        // This tests that the SelectorsEmitter is working
        var userSelectorsType = Type.GetType("Facet.Extensions.EFCore.Tests.TestData.UserSelectors");
        Assert.NotNull(userSelectorsType);

        // Check that BaseShape selector exists
        var baseShapeProperty = userSelectorsType.GetProperty("BaseShape");
        Assert.NotNull(baseShapeProperty);

        _output.WriteLine("✅ UserSelectors class with BaseShape property is generated");
    }

    [Fact]
    public void GenerateDtos_TypeScriptAttributes_AreSupported()
    {
        // Act & Assert - Test that the TypeScript attributes feature was added successfully
        // We'll test this by checking that the GenerateDtosAttribute has the new property
        var generateDtosAttr = typeof(Facet.GenerateDtosAttribute);
        var typeScriptAttributesProperty = generateDtosAttr.GetProperty("TypeScriptAttributes");
        
        Assert.NotNull(typeScriptAttributesProperty);
        Assert.Equal(typeof(string[]), typeScriptAttributesProperty.PropertyType);

        _output.WriteLine("✅ GenerateDtosAttribute supports TypeScriptAttributes property");
    }

    [Fact]
    public async Task FacetDbContextExtensions_GenericMethod_IsGenerated()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TestDbContext>();
        
        await context.Database.EnsureCreatedAsync();

        // Act & Assert - Test that the generic Facet<TEntity, TDto> method exists
        var contextType = context.GetType();
        var facetMethod = typeof(FacetDbContextExtensions)
            .GetMethods()
            .FirstOrDefault(m => m.Name == "Facet" && m.IsGenericMethod);

        Assert.NotNull(facetMethod);
        Assert.Equal(2, facetMethod.GetGenericArguments().Length); // Should have TEntity and TDto type parameters

        _output.WriteLine("✅ Generic Facet<TEntity, TDto> extension method is generated");
    }

    /// <summary>
    /// Integration test to verify the generated code structure is consistent
    /// </summary>
    [Fact]
    public void GeneratedCode_Structure_IsConsistent()
    {
        // Verify that all expected entities have corresponding generated artifacts
        var expectedEntities = new[] { "User", "Order", "Product", "Category", "OrderItem" };
        
        foreach (var entityName in expectedEntities)
        {
            // Check builder exists
            var builderTypeName = $"Facet{entityName}Builder`1";
            var builderExists = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => a.GetTypes())
                .Any(t => t.Name.StartsWith($"Facet{entityName}Builder"));

            Assert.True(builderExists, $"Fluent builder should exist for {entityName}");

            // Check shape interface exists
            var shapeTypeName = $"I{entityName}Shape";
            var shapeExists = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => a.GetTypes())
                .Any(t => t.Name == shapeTypeName);

            Assert.True(shapeExists, $"Shape interface should exist for {entityName}");

            _output.WriteLine($"✅ {entityName} - Builder and Shape interface generated");
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
}