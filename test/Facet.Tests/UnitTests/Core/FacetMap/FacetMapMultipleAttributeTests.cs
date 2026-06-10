using Facet.Tests.TestModels.FacetMap;

namespace Facet.Tests.UnitTests.Core.FacetMap;

public class FacetMapMultipleAttributeTests
{
    [Fact]
    public void MultipleAttributes_ShouldGenerateBothMappings()
    {
        var entity = new UnitEntity { Id = 1, Name = "Meters", Code = "m" };

        var dto = entity.ToUnitDto();
        dto.Should().NotBeNull();
        dto.Id.Should().Be(1);
        dto.Name.Should().Be("Meters");
        dto.Code.Should().Be("m");

        var dropDown = entity.ToUnitDropDownDto();
        dropDown.Should().NotBeNull();
        dropDown.Id.Should().Be(1);
        dropDown.Name.Should().Be("Meters");
    }

    [Fact]
    public void MultipleAttributes_ToSource_ShouldWork()
    {
        var dropDown = new UnitDropDownDto { Id = 2, Name = "Kilograms" };

        var entity = dropDown.ToUnitEntity();
        entity.Should().NotBeNull();
        entity.Id.Should().Be(2);
        entity.Name.Should().Be("Kilograms");
    }

    [Fact]
    public void BeforeMapConfiguration_ShouldBeCalledBeforeMapping()
    {
        var product = new ProductEntity { Id = 1, Name = "Widget", Price = 9.99m };

        var dto = product.ToProductDto();

        dto.WasValidated.Should().BeTrue();
    }

    [Fact]
    public void AfterMapConfiguration_ShouldBeCalledAfterMapping()
    {
        var product = new ProductEntity { Id = 1, Name = "Widget", Price = 9.99m };

        var dto = product.ToProductDto();

        dto.MappedBy.Should().Be("AfterMapConfig");
        dto.Id.Should().Be(1);
        dto.Name.Should().Be("Widget");
        dto.Price.Should().Be(9.99m);
    }
}
