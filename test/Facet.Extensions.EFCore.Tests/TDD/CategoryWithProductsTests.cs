using System.Linq;
using Facet.Extensions.EFCore.Tests.TestData;
using Xunit;

namespace Facet.Extensions.EFCore.Tests.TDD;

public class CategoryWithProductsTests
{
    [Fact]
    public void FacetCategoryBuilder_WithProducts_ShouldReturnCorrectType()
    {
        var categories = new[] { new Category() }.AsQueryable();
        var builder = new FacetCategoryBuilder<ICategoryShape>(categories);
        
        var builderWithProducts = builder.WithProducts();
        
        Assert.NotNull(builderWithProducts);
        Assert.IsType<FacetCategoryBuilder<ICategoryWithProducts<IProductShape>>>(builderWithProducts);
    }
    
    [Fact] 
    public void FacetCategoryBuilder_WithProducts_ShouldGenerateIncludeExpression()
    {
        var categories = new[] { new Category() }.AsQueryable();
        var builder = new FacetCategoryBuilder<ICategoryShape>(categories);
        
        var builderWithProducts = builder.WithProducts();
        
        Assert.NotNull(builderWithProducts);
    }
}