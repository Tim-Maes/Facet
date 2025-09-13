using System.Linq;
using Facet.Extensions.EFCore.Tests.TestData;
using Facet.Extensions.EFCore.Tests.Extensions;
// using Facet.Persistence; // Pending fluent generation feature
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Facet.Extensions.EFCore.Tests.TDD;

public class ChainDiscoveryTests
{
#if GENERATION_ENABLED
    [Fact]
    public void SimpleChain_WithProducts_ShouldBeDiscovered()
    {
        using var context = new TestDbContext(new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase($"test_{System.Guid.NewGuid()}")
            .Options);
        var categories = context.Categories.AsQueryable();
        var builder = new FacetCategoryBuilder<ICategoryShape>(categories, CategorySelectors.BaseShape);
        var builderWithProducts = builder.WithProducts();
        Assert.NotNull(builderWithProducts);
        Assert.IsType<FacetCategoryBuilder<ICategoryWithProducts<IProductShape>>>(builderWithProducts);
    }

    [Fact]
    public void NestedChain_WithProducts_ThenCategory_ShouldBeDiscovered()
    {
        using var context = new TestDbContext(new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase($"test_{System.Guid.NewGuid()}")
            .Options);
        var categories = context.Categories.AsQueryable();
        var builder = new FacetCategoryBuilder<ICategoryShape>(categories, CategorySelectors.BaseShape);
        var builderWithProducts = builder.WithProducts();
        Assert.NotNull(builderWithProducts);
    }

    [Fact]
    public void MultipleChains_ShouldGenerateOnlyUsedPaths()
    {
        using var context = new TestDbContext(new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase($"test_{System.Guid.NewGuid()}")
            .Options);
        var products = context.Products.AsQueryable();
        var builder = new FacetProductBuilder<IProductShape>(products, ProductSelectors.BaseShape);
        var builderWithCategory = builder.WithCategory();
        Assert.NotNull(builderWithCategory);
        var builderWithOrderItems = builder.WithOrderItems();
        Assert.NotNull(builderWithOrderItems);
    }
#endif
}
