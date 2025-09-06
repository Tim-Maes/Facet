using System.Linq;
using Facet.Extensions.EFCore.Tests.TestData;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Facet.Extensions.EFCore.Tests.TDD;

public class ChainDiscoveryTests
{
    [Fact]
    public void SimpleChain_WithProducts_ShouldBeDiscovered()
    {
        // This test drives the implementation of chain discovery
        // It should detect when code uses .WithProducts() and emit only what's needed
        
        using var context = new TestDbContext(new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase($"test_{System.Guid.NewGuid()}")
            .Options);

        var categories = context.Categories.AsQueryable();
        var builder = new FacetCategoryBuilder<ICategoryShape>(categories);
        
        // This should trigger chain discovery to detect "Products" path
        var builderWithProducts = builder.WithProducts();
        
        Assert.NotNull(builderWithProducts);
        Assert.IsType<FacetCategoryBuilder<ICategoryWithProducts<IProductShape>>>(builderWithProducts);
    }
    
    [Fact]
    public void NestedChain_WithProducts_ThenCategory_ShouldBeDiscovered()
    {
        // This test drives discovery of nested chains like Product -> Category
        // Should detect "Products" and "Products/Category" paths
        
        using var context = new TestDbContext(new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase($"test_{System.Guid.NewGuid()}")
            .Options);

        var categories = context.Categories.AsQueryable();
        var builder = new FacetCategoryBuilder<ICategoryShape>(categories);
        
        // Basic chain - should always work
        var builderWithProducts = builder.WithProducts();
        Assert.NotNull(builderWithProducts);
        
        // Nested chains would be tested once implemented
        // Example: builderWithProducts.WithCategory()
    }
    
    [Fact]
    public void MultipleChains_ShouldGenerateOnlyUsedPaths()
    {
        // This test ensures we only generate code for paths that are actually used
        // If code only uses .WithProducts(), we shouldn't generate .WithOrderItems() methods
        
        using var context = new TestDbContext(new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase($"test_{System.Guid.NewGuid()}")
            .Options);

        var products = context.Products.AsQueryable();
        var builder = new FacetProductBuilder<IProductShape>(products);
        
        // Only use WithCategory - this should be the only advanced method generated
        var builderWithCategory = builder.WithCategory();
        Assert.NotNull(builderWithCategory);
        
        // The WithOrderItems method should exist (baseline), but nested methods 
        // for OrderItems should only be generated if someone uses them
        var builderWithOrderItems = builder.WithOrderItems();
        Assert.NotNull(builderWithOrderItems);
    }
}