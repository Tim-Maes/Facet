using Facet.Tests.TestModels.FacetMap;

namespace Facet.Tests.UnitTests.Core.FacetMap;

public class FacetMapCollectionTests
{
    [Fact]
    public void ToTarget_ShouldMapCollections()
    {
        var order = new Order
        {
            Id = 1,
            CustomerId = 10,
            OrderNumber = "ORD-001",
            Total = 99.99m,
            OrderDate = new DateTime(2024, 6, 1),
            InternalNotes = "Rush order",
            Lines = new List<OrderLine>
            {
                new() { Id = 1, ProductName = "Widget", Quantity = 2, UnitPrice = 25m },
                new() { Id = 2, ProductName = "Gadget", Quantity = 1, UnitPrice = 49.99m }
            }
        };

        var dto = order.ToOrderDto();

        dto.Should().NotBeNull();
        dto.Id.Should().Be(1);
        dto.CustomerId.Should().Be(10);
        dto.OrderNumber.Should().Be("ORD-001");
        dto.Total.Should().Be(99.99m);
        dto.Lines.Should().HaveCount(2);
        dto.Lines[0].ProductName.Should().Be("Widget");
        dto.Lines[1].Quantity.Should().Be(1);
    }

    [Fact]
    public void ToSource_ShouldMapCollectionsBack()
    {
        var dto = new OrderDto
        {
            Id = 5,
            CustomerId = 20,
            OrderNumber = "ORD-005",
            Total = 150m,
            OrderDate = new DateTime(2024, 7, 1),
            Lines = new List<OrderLine>
            {
                new() { Id = 10, ProductName = "Item A", Quantity = 3, UnitPrice = 50m }
            }
        };

        var entity = dto.ToOrder();

        entity.Should().NotBeNull();
        entity.Id.Should().Be(5);
        entity.OrderNumber.Should().Be("ORD-005");
        entity.Lines.Should().HaveCount(1);
        entity.Lines[0].ProductName.Should().Be("Item A");
    }
}
