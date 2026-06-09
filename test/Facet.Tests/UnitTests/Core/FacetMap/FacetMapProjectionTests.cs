using Facet.Tests.TestModels.FacetMap;

namespace Facet.Tests.UnitTests.Core.FacetMap;

public class FacetMapProjectionTests
{
    [Fact]
    public void Projection_ShouldBeNonNull()
    {
        var projection = OrderLineMappings.OrderLineDtoProjection;

        projection.Should().NotBeNull();
    }

    [Fact]
    public void Projection_ShouldCompileAndExecute()
    {
        var projection = OrderLineMappings.OrderLineDtoProjection;
        var compiled = projection.Compile();

        var entity = new OrderLine
        {
            Id = 1,
            ProductName = "Widget",
            Quantity = 3,
            UnitPrice = 9.99m
        };

        var dto = compiled(entity);

        dto.Id.Should().Be(1);
        dto.ProductName.Should().Be("Widget");
        dto.Quantity.Should().Be(3);
        dto.UnitPrice.Should().Be(9.99m);
    }

    [Fact]
    public void Projection_ShouldWorkWithLinqQueries()
    {
        var entities = new List<OrderLine>
        {
            new() { Id = 1, ProductName = "A", Quantity = 1, UnitPrice = 10m },
            new() { Id = 2, ProductName = "B", Quantity = 2, UnitPrice = 20m },
            new() { Id = 3, ProductName = "C", Quantity = 3, UnitPrice = 30m }
        };

        var projection = OrderLineMappings.OrderLineDtoProjection;
        var dtos = entities.AsQueryable().Select(projection).ToList();

        dtos.Should().HaveCount(3);
        dtos[0].ProductName.Should().Be("A");
        dtos[1].Quantity.Should().Be(2);
        dtos[2].UnitPrice.Should().Be(30m);
    }

    [Fact]
    public void CustomerProjection_ShouldExist()
    {
        var projection = CustomerMappings.CustomerDtoProjection;

        projection.Should().NotBeNull();
    }
}
