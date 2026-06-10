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
    }

    [Fact]
    public void MultipleAttributes_ToSource_ShouldWork()
    {
        var dropDown = new UnitDropDownDto { Id = 2, Name = "Kilograms" };

        // Use the UnitDropDownMapper which has the GenerateToSource = true attribute
        var entity = UnitDropDownMapper.ToUnitEntity(dropDown);
        entity.Should().NotBeNull();
        entity.Id.Should().Be(2);
        entity.Name.Should().Be("Kilograms");
    }

    [Fact]
    public void MultipleAttributes_SameTargetDifferentSources_ShouldWork()
    {
        // Allan's exact scenario: two attributes mapping different sources to same target
        var unitDto = new UnitDto { Id = 1, Name = "Meters", Code = "m" };
        var dropDown1 = UnitDropDownMapper.ToUnitDropDownDto(unitDto);
        dropDown1.Should().NotBeNull();
        dropDown1.Id.Should().Be(1);
        dropDown1.Name.Should().Be("Meters");

        var entity = new UnitEntity { Id = 2, Name = "Kilograms", Code = "kg" };
        var dropDown2 = UnitDropDownMapper.ToUnitDropDownDto(entity);
        dropDown2.Should().NotBeNull();
        dropDown2.Id.Should().Be(2);
        dropDown2.Name.Should().Be("Kilograms");
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

    [Fact]
    public void InitOnlyProperties_ToTarget_ShouldMapCorrectly()
    {
        var entity = new AddressEntity
        {
            AddressLine1 = "123 Main St",
            City = "Springfield",
            PostalCode = "12345",
            Country = "US"
        };

        var dto = entity.ToAddressDto();

        dto.AddressLine1.Should().Be("123 Main St");
        dto.City.Should().Be("Springfield");
        dto.PostalCode.Should().Be("12345");
        dto.Country.Should().Be("US");
    }

    [Fact]
    public void InitOnlyProperties_ToSource_ShouldMapCorrectly()
    {
        var dto = new AddressDto
        {
            AddressLine1 = "456 Oak Ave",
            City = "Shelbyville",
            PostalCode = "67890",
            Country = "CA"
        };

        var entity = dto.ToAddressEntity();

        entity.AddressLine1.Should().Be("456 Oak Ave");
        entity.City.Should().Be("Shelbyville");
        entity.PostalCode.Should().Be("67890");
        entity.Country.Should().Be("CA");
    }
}
