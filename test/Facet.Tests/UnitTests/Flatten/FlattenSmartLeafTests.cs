using Facet.Tests.TestModels;

namespace Facet.Tests.UnitTests.Flatten;

public class FlattenSmartLeafTests
{
    [Fact]
    public void DataItemSmartLeafDto_ShouldResolveCollisionsWithParentName()
    {
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

        var dto = new DataItemSmartLeafDto(dataItem);

        var type = typeof(DataItemSmartLeafDto);

        Assert.Equal(1, dto.Id);

        Assert.NotNull(type.GetProperty("DataValue"));
        Assert.Equal("Test Value", type.GetProperty("DataValue")!.GetValue(dto));

        Assert.NotNull(type.GetProperty("PositionName"));
        Assert.NotNull(type.GetProperty("TypeName"));
        Assert.Equal("Manager", type.GetProperty("PositionName")!.GetValue(dto));
        Assert.Equal("Full-Time", type.GetProperty("TypeName")!.GetValue(dto));

        Assert.Null(type.GetProperty("Name"));
        Assert.Null(type.GetProperty("Name2"));

        Assert.Null(type.GetProperty("ExtendedDataPositionName"));
        Assert.Null(type.GetProperty("ExtendedDataTypeName"));
    }

    [Fact]
    public void DataItemLeafOnlyDto_ShouldUseNumericSuffixForCollisions()
    {
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

        var dto = new DataItemLeafOnlyDto(dataItem);

        var type = typeof(DataItemLeafOnlyDto);

        Assert.NotNull(type.GetProperty("Name"));
        Assert.NotNull(type.GetProperty("Name2"));

        Assert.Null(type.GetProperty("PositionName"));
        Assert.Null(type.GetProperty("TypeName"));
    }

    [Fact]
    public void ProductCatalogSmartLeafDto_ShouldResolveMultipleCollisionsFromSameParent()
    {
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

        var dto = new ProductCatalogSmartLeafDto(catalog);

        var type = typeof(ProductCatalogSmartLeafDto);

        Assert.Equal(1, dto.Id);

        Assert.NotNull(type.GetProperty("ProductName"));
        Assert.NotNull(type.GetProperty("ProductCode"));
        Assert.NotNull(type.GetProperty("CategoryName"));
        Assert.NotNull(type.GetProperty("CategoryCode"));

        Assert.Equal("Widget", type.GetProperty("ProductName")!.GetValue(dto));
        Assert.Equal("WGT-001", type.GetProperty("ProductCode")!.GetValue(dto));
        Assert.Equal("Tools", type.GetProperty("CategoryName")!.GetValue(dto));
        Assert.Equal("TLS", type.GetProperty("CategoryCode")!.GetValue(dto));

        Assert.Null(type.GetProperty("Name"));
        Assert.Null(type.GetProperty("Code"));
    }

    [Fact]
    public void DataItemSmartLeafDto_ShouldHandleNullNestedObjects()
    {
        var dataItem = new DataItem
        {
            Id = 1,
            DataValue = "Test Value",
            ExtendedDataId = 10,
            ExtendedData = null!
        };

        var dto = new DataItemSmartLeafDto(dataItem);

        var type = typeof(DataItemSmartLeafDto);
        Assert.Equal(1, dto.Id);
        Assert.Equal("Test Value", type.GetProperty("DataValue")!.GetValue(dto));
        Assert.Null(type.GetProperty("PositionName")!.GetValue(dto));
        Assert.Null(type.GetProperty("TypeName")!.GetValue(dto));
    }

    [Fact]
    public void DataItemSmartLeafDto_Projection_ShouldWork()
    {
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

        var projection = DataItemSmartLeafDto.Projection.Compile();
        var dto = projection(dataItem);

        var type = typeof(DataItemSmartLeafDto);
        Assert.Equal(1, dto.Id);
        Assert.Equal("Test Value", type.GetProperty("DataValue")!.GetValue(dto));
        Assert.Equal("Manager", type.GetProperty("PositionName")!.GetValue(dto));
        Assert.Equal("Full-Time", type.GetProperty("TypeName")!.GetValue(dto));
    }

    [Fact]
    public void DataItemSmartLeafDto_ParameterlessConstructor_ShouldWork()
    {
        var dto = new DataItemSmartLeafDto();

        Assert.NotNull(dto);
        Assert.Equal(0, dto.Id);
    }
}
