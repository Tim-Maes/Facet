using Facet.Tests.TestModels.FacetMap;

namespace Facet.Tests.UnitTests.Core.FacetMap;

/// <summary>
/// Tests for FacetMap enum conversion (auto-detected when source has enum, target has string/int).
/// </summary>
public class FacetMapEnumConversionTests
{
    #region Enum to String

    [Fact]
    public void ToTarget_ShouldConvertEnumToString()
    {
        var entity = new OrderWithEnumEntity
        {
            Id = 1,
            OrderNumber = "ORD-001",
            Status = OrderStatus.Shipped,
            NullableStatus = OrderStatus.Processing
        };

        var dto = entity.ToOrderWithEnumAsStringDto();

        dto.Should().NotBeNull();
        dto.Id.Should().Be(1);
        dto.OrderNumber.Should().Be("ORD-001");
        dto.Status.Should().Be("Shipped");
        dto.NullableStatus.Should().Be("Processing");
    }

    [Fact]
    public void ToTarget_NullableEnum_NullValue_ShouldMapToNull()
    {
        var entity = new OrderWithEnumEntity
        {
            Id = 2,
            OrderNumber = "ORD-002",
            Status = OrderStatus.Pending,
            NullableStatus = null
        };

        var dto = entity.ToOrderWithEnumAsStringDto();

        dto.Should().NotBeNull();
        dto.Status.Should().Be("Pending");
        dto.NullableStatus.Should().BeNull();
    }

    [Fact]
    public void ToSource_ShouldConvertStringToEnum()
    {
        var dto = new OrderWithEnumAsStringDto
        {
            Id = 3,
            OrderNumber = "ORD-003",
            Status = "Delivered",
            NullableStatus = "Cancelled"
        };

        var entity = dto.ToOrderWithEnumEntity();

        entity.Should().NotBeNull();
        entity.Status.Should().Be(OrderStatus.Delivered);
        entity.NullableStatus.Should().Be(OrderStatus.Cancelled);
    }

    [Fact]
    public void ToSource_NullString_ShouldConvertToNullEnum()
    {
        var dto = new OrderWithEnumAsStringDto
        {
            Id = 4,
            OrderNumber = "ORD-004",
            Status = "Pending",
            NullableStatus = null
        };

        var entity = dto.ToOrderWithEnumEntity();

        entity.Should().NotBeNull();
        entity.Status.Should().Be(OrderStatus.Pending);
        entity.NullableStatus.Should().BeNull();
    }

    #endregion

    #region Enum to Int

    [Fact]
    public void ToTarget_ShouldConvertEnumToInt()
    {
        var entity = new OrderWithEnumEntity
        {
            Id = 5,
            OrderNumber = "ORD-005",
            Status = OrderStatus.Shipped,
            NullableStatus = OrderStatus.Delivered
        };

        var dto = entity.ToOrderWithEnumAsIntDto();

        dto.Should().NotBeNull();
        dto.Status.Should().Be(2); // Shipped = 2
        dto.NullableStatus.Should().Be(3); // Delivered = 3
    }

    [Fact]
    public void ToTarget_NullableEnumToInt_NullValue_ShouldMapToNull()
    {
        var entity = new OrderWithEnumEntity
        {
            Id = 6,
            OrderNumber = "ORD-006",
            Status = OrderStatus.Pending,
            NullableStatus = null
        };

        var dto = entity.ToOrderWithEnumAsIntDto();

        dto.Should().NotBeNull();
        dto.Status.Should().Be(0); // Pending = 0
        dto.NullableStatus.Should().BeNull();
    }

    [Fact]
    public void ToSource_ShouldConvertIntToEnum()
    {
        var dto = new OrderWithEnumAsIntDto
        {
            Id = 7,
            OrderNumber = "ORD-007",
            Status = 4, // Cancelled
            NullableStatus = 1 // Processing
        };

        var entity = dto.ToOrderWithEnumEntity();

        entity.Should().NotBeNull();
        entity.Status.Should().Be(OrderStatus.Cancelled);
        entity.NullableStatus.Should().Be(OrderStatus.Processing);
    }

    [Fact]
    public void ApplyToSource_ShouldConvertEnumProperly()
    {
        var dto = new OrderWithEnumAsStringDto
        {
            Id = 8,
            OrderNumber = "ORD-008",
            Status = "Shipped",
            NullableStatus = null
        };

        var entity = new OrderWithEnumEntity
        {
            Id = 8,
            OrderNumber = "ORD-OLD",
            Status = OrderStatus.Pending,
            NullableStatus = OrderStatus.Processing
        };

        dto.ApplyToOrderWithEnumEntity(entity);

        entity.Status.Should().Be(OrderStatus.Shipped);
        entity.NullableStatus.Should().BeNull();
    }

    #endregion
}
