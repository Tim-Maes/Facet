using Facet.Tests.TestModels;

namespace Facet.Tests.UnitTests.Flatten;

public class FlattenSmartLeafTests
{
    [Fact]
    public void DataItemSmartLeafDto_ShouldResolveCollisionsWithParentName()
    {
        // Arrange
        var dataItem = new DataItem
        {
            Id = 1,
            DataValue = "Test Value",
            ExtendedDataId = 10,
            ExtendedData = new ExtendedData
            {
                Id = 10,
                PositionId = 100,
                Position = new Position
                {
                    Id = 100,
                    Name = "Manager"
                },
                TypeId = 200,
                Type = new ItemType
                {
                    Id = 200,
                    Name = "Full-Time"
                }
            }
        };

        // Act
        var dto = new DataItemSmartLeafDto(dataItem);

        // Assert - verify SmartLeaf naming (parent prefix only on collision)
        var type = typeof(DataItemSmartLeafDto);

        // Root properties should exist as-is
        Assert.Equal(1, dto.Id);

        // Non-colliding property should use leaf name only
        Assert.NotNull(type.GetProperty("DataValue"));
        Assert.Equal("Test Value", type.GetProperty("DataValue")!.GetValue(dto));

        // Colliding "Name" properties should use parent prefix
        Assert.NotNull(type.GetProperty("PositionName"));
        Assert.NotNull(type.GetProperty("TypeName"));
        Assert.Equal("Manager", type.GetProperty("PositionName")!.GetValue(dto));
        Assert.Equal("Full-Time", type.GetProperty("TypeName")!.GetValue(dto));

        // Should NOT have numeric suffix names
        Assert.Null(type.GetProperty("Name"));
        Assert.Null(type.GetProperty("Name2"));

        // Should NOT have full prefix names
        Assert.Null(type.GetProperty("ExtendedDataPositionName"));
        Assert.Null(type.GetProperty("ExtendedDataTypeName"));
    }

    [Fact]
    public void DataItemLeafOnlyDto_ShouldUseNumericSuffixForCollisions()
    {
        // Arrange
        var dataItem = new DataItem
        {
            Id = 1,
            DataValue = "Test Value",
            ExtendedDataId = 10,
            ExtendedData = new ExtendedData
            {
                Id = 10,
                PositionId = 100,
                Position = new Position
                {
                    Id = 100,
                    Name = "Manager"
                },
                TypeId = 200,
                Type = new ItemType
                {
                    Id = 200,
                    Name = "Full-Time"
                }
            }
        };

        // Act
        var dto = new DataItemLeafOnlyDto(dataItem);

        // Assert - verify LeafOnly naming (numeric suffix on collision)
        var type = typeof(DataItemLeafOnlyDto);

        // Should have numeric suffix names for collisions
        Assert.NotNull(type.GetProperty("Name"));
        Assert.NotNull(type.GetProperty("Name2"));

        // Should NOT have parent prefix names
        Assert.Null(type.GetProperty("PositionName"));
        Assert.Null(type.GetProperty("TypeName"));
    }

    [Fact]
    public void ProductCatalogSmartLeafDto_ShouldResolveMultipleCollisionsFromSameParent()
    {
        // Arrange
        var catalog = new ProductCatalog
        {
            Id = 1,
            Product = new CatalogProduct
            {
                Id = 10,
                Name = "Widget",
                Code = "WGT-001"
            },
            Category = new CatalogCategory
            {
                Id = 20,
                Name = "Tools",
                Code = "TLS"
            }
        };

        // Act
        var dto = new ProductCatalogSmartLeafDto(catalog);

        // Assert - verify multiple collisions resolved
        var type = typeof(ProductCatalogSmartLeafDto);

        // Root Id
        Assert.Equal(1, dto.Id);

        // Both Name and Code collide, so both should get parent prefix
        Assert.NotNull(type.GetProperty("ProductName"));
        Assert.NotNull(type.GetProperty("ProductCode"));
        Assert.NotNull(type.GetProperty("CategoryName"));
        Assert.NotNull(type.GetProperty("CategoryCode"));

        Assert.Equal("Widget", type.GetProperty("ProductName")!.GetValue(dto));
        Assert.Equal("WGT-001", type.GetProperty("ProductCode")!.GetValue(dto));
        Assert.Equal("Tools", type.GetProperty("CategoryName")!.GetValue(dto));
        Assert.Equal("TLS", type.GetProperty("CategoryCode")!.GetValue(dto));

        // Should NOT have unprefixed names
        Assert.Null(type.GetProperty("Name"));
        Assert.Null(type.GetProperty("Code"));
    }

    [Fact]
    public void DataItemSmartLeafDto_ShouldHandleNullNestedObjects()
    {
        // Arrange
        var dataItem = new DataItem
        {
            Id = 1,
            DataValue = "Test Value",
            ExtendedDataId = 10,
            ExtendedData = null!
        };

        // Act
        var dto = new DataItemSmartLeafDto(dataItem);

        // Assert
        var type = typeof(DataItemSmartLeafDto);
        Assert.Equal(1, dto.Id);
        Assert.Equal("Test Value", type.GetProperty("DataValue")!.GetValue(dto));
        Assert.Null(type.GetProperty("PositionName")!.GetValue(dto));
        Assert.Null(type.GetProperty("TypeName")!.GetValue(dto));
    }

    [Fact]
    public void DataItemSmartLeafDto_Projection_ShouldWork()
    {
        // Arrange
        var dataItem = new DataItem
        {
            Id = 1,
            DataValue = "Test Value",
            ExtendedDataId = 10,
            ExtendedData = new ExtendedData
            {
                Id = 10,
                PositionId = 100,
                Position = new Position { Id = 100, Name = "Manager" },
                TypeId = 200,
                Type = new ItemType { Id = 200, Name = "Full-Time" }
            }
        };

        // Act
        var projection = DataItemSmartLeafDto.Projection.Compile();
        var dto = projection(dataItem);

        // Assert
        var type = typeof(DataItemSmartLeafDto);
        Assert.Equal(1, dto.Id);
        Assert.Equal("Test Value", type.GetProperty("DataValue")!.GetValue(dto));
        Assert.Equal("Manager", type.GetProperty("PositionName")!.GetValue(dto));
        Assert.Equal("Full-Time", type.GetProperty("TypeName")!.GetValue(dto));
    }

    [Fact]
    public void DataItemSmartLeafDto_ParameterlessConstructor_ShouldWork()
    {
        // Act
        var dto = new DataItemSmartLeafDto();

        // Assert
        Assert.NotNull(dto);
        Assert.Equal(0, dto.Id);
    }
}
