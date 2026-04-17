using System.Linq.Expressions;

namespace Facet.Tests.UnitTests.Core.Facet;

/// <summary>Base entity.</summary>
public class OrderLineBaseEntity338
{
    public int Id { get; set; }
    public string Number { get; set; } = string.Empty;
    public DateTime ExpectedStartTime { get; set; }
}

/// <summary>Derived entity that adds dispatch-specific properties.</summary>
public class OrderLineDispatchEntity338 : OrderLineBaseEntity338
{
    public DateTime DeliveryTime { get; set; }
    public DateTime ShipmentTime { get; set; }
}

public class ModifiedByBaseDto338
{
    public int Id { get; set; }
}

[Facet(typeof(OrderLineBaseEntity338),
       Configuration = typeof(OrderLineBaseDto338MapConfig),
       Include = new[]
       {
           nameof(OrderLineBaseEntity338.Number),
           nameof(OrderLineBaseEntity338.ExpectedStartTime)
       })]
public partial class OrderLineBaseDto338 : ModifiedByBaseDto338
{
}

public class OrderLineBaseDto338MapConfig
    : IFacetProjectionMapConfiguration<OrderLineBaseEntity338, OrderLineBaseDto338>
{
    public static void ConfigureProjection(
        IFacetProjectionBuilder<OrderLineBaseEntity338, OrderLineBaseDto338> builder)
    {
        // Map base properties (these should appear in derived projection too)
        builder.Map(d => d.Number, s => "ORD-" + s.Number);
        builder.Map(d => d.ExpectedStartTime, s => s.ExpectedStartTime.AddHours(1));
    }
}

[Facet(typeof(OrderLineDispatchEntity338),
       Configuration = typeof(OrderLineDispatchDto338MapConfig),
       Include = new[]
       {
           nameof(OrderLineDispatchEntity338.DeliveryTime),
           nameof(OrderLineDispatchEntity338.ShipmentTime)
       })]
public partial class OrderLineDispatchDto338 : OrderLineBaseDto338
{
}

public class OrderLineDispatchDto338MapConfig
    : IFacetProjectionMapConfiguration<OrderLineDispatchEntity338, OrderLineDispatchDto338>
{
    public static void ConfigureProjection(
        IFacetProjectionBuilder<OrderLineDispatchEntity338, OrderLineDispatchDto338> builder)
    {
        // Map dispatch-specific properties
        builder.Map(d => d.DeliveryTime, s => s.DeliveryTime.AddHours(2));
        builder.Map(d => d.ShipmentTime, s => s.ShipmentTime.AddHours(3));
    }
}

public class InheritedFacetProjectionTests
{
    [Fact]
    public void Projection_WithInheritedFacetBase_ShouldMapAllProperties()
    {
        // Arrange
        var baseTime = new DateTime(2024, 1, 1, 10, 0, 0);
        var entity = new OrderLineDispatchEntity338
        {
            Id = 1,
            Number = "123",
            ExpectedStartTime = baseTime,
            DeliveryTime = baseTime.AddDays(1),
            ShipmentTime = baseTime.AddDays(2)
        };

        // Act - Use projection
        var projection = OrderLineDispatchDto338.Projection.Compile();
        var dto = projection(entity);

        // Assert - Properties from derived class should be mapped
        dto.DeliveryTime.Should().Be(baseTime.AddDays(1).AddHours(2),
            "DeliveryTime should be mapped with +2 hours from ConfigureProjection");
        dto.ShipmentTime.Should().Be(baseTime.AddDays(2).AddHours(3),
            "ShipmentTime should be mapped with +3 hours from ConfigureProjection");

        // Assert - Properties from base FACET class should ALSO be mapped
        dto.Number.Should().Be("ORD-123",
            "Number should be mapped from base facet's ConfigureProjection with 'ORD-' prefix");
        dto.ExpectedStartTime.Should().Be(baseTime.AddHours(1),
            "ExpectedStartTime should be mapped from base facet's ConfigureProjection with +1 hour");

        // Assert - Property from non-facet base should be mapped
        dto.Id.Should().Be(1, "Id from non-facet base should be mapped");
    }

    [Fact]
    public void Constructor_WithInheritedFacetBase_ShouldMapAllProperties()
    {
        // Arrange
        var baseTime = new DateTime(2024, 1, 1, 10, 0, 0);
        var entity = new OrderLineDispatchEntity338
        {
            Id = 2,
            Number = "456",
            ExpectedStartTime = baseTime,
            DeliveryTime = baseTime.AddDays(1),
            ShipmentTime = baseTime.AddDays(2)
        };

        // Act - Use constructor
        var dto = new OrderLineDispatchDto338(entity);

        // Assert - All properties should be mapped (constructor uses ConfigureMap)
        dto.Id.Should().Be(2);
        dto.Number.Should().Be("ORD-456");
        dto.ExpectedStartTime.Should().Be(baseTime.AddHours(1));
        dto.DeliveryTime.Should().Be(baseTime.AddDays(1).AddHours(2));
        dto.ShipmentTime.Should().Be(baseTime.AddDays(2).AddHours(3));
    }

    [Fact]
    public void BaseProjection_ShouldMapBaseProperties()
    {
        // Arrange
        var baseTime = new DateTime(2024, 1, 1, 10, 0, 0);
        var entity = new OrderLineBaseEntity338
        {
            Id = 3,
            Number = "789",
            ExpectedStartTime = baseTime
        };

        // Act - Use base projection directly
        var projection = OrderLineBaseDto338.Projection.Compile();
        var dto = projection(entity);

        // Assert
        dto.Id.Should().Be(3);
        dto.Number.Should().Be("ORD-789");
        dto.ExpectedStartTime.Should().Be(baseTime.AddHours(1));
    }

    [Fact]
    public void InheritedFacet_WithBaseNestedFacets_ShouldMapNestedPropertiesCorrectly()
    {
        // Arrange
        var baseTime = new DateTime(2024, 1, 1, 10, 0, 0);
        var entity = new OrderLineDispatchEntity338Nested
        {
            Id = 1,
            Number = "ORD-001",
            ExpectedStartTime = baseTime,
            DeliveryTime = baseTime.AddDays(1),
            ShipmentTime = baseTime.AddDays(2),
            AssignedToUnit = new UnitEntity338 { Id = 100, Name = "Unit A" },
            OrderHeader = new OrderHeaderEntity338 { Id = 200, OrderNumber = "HDR-001" }
        };

        // Act
        var dto = new OrderLineDispatchDto338Nested(entity);

        // Assert - Verify nested facets from base are mapped as DTOs, not entities
        dto.AssignedToUnit.Should().NotBeNull("AssignedToUnit should be mapped from base Facet");
        dto.AssignedToUnit.Should().BeOfType<UnitDto338>("AssignedToUnit should be mapped as UnitDto338, not UnitEntity338");
        dto.AssignedToUnit!.Name.Should().Be("Unit A");

        dto.OrderHeader.Should().NotBeNull("OrderHeader should be mapped from base Facet");
        dto.OrderHeader.Should().BeOfType<OrderHeaderDto338>("OrderHeader should be mapped as OrderHeaderDto338, not OrderHeaderEntity338");
        dto.OrderHeader.OrderNumber.Should().Be("HDR-001");

        // Verify other properties are also mapped correctly
        dto.Number.Should().Be("ORD-001");
        dto.ExpectedStartTime.Should().Be(baseTime);
        dto.DeliveryTime.Should().Be(baseTime.AddDays(1));
        dto.ShipmentTime.Should().Be(baseTime.AddDays(2));
    }
}

public class UnitEntity338
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

[Facet(typeof(UnitEntity338))]
public partial class UnitDto338
{
}

public class OrderHeaderEntity338
{
    public int Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
}

[Facet(typeof(OrderHeaderEntity338))]
public partial class OrderHeaderDto338
{
}

public class OrderLineBaseEntity338Nested
{
    public int Id { get; set; }
    public string Number { get; set; } = string.Empty;
    public DateTime ExpectedStartTime { get; set; }
    public UnitEntity338? AssignedToUnit { get; set; }
    public OrderHeaderEntity338 OrderHeader { get; set; } = null!;
}

[Facet(typeof(OrderLineBaseEntity338Nested),
       Include = new[]
       {
           nameof(OrderLineBaseEntity338Nested.Number),
           nameof(OrderLineBaseEntity338Nested.ExpectedStartTime),
           nameof(OrderLineBaseEntity338Nested.AssignedToUnit),
           nameof(OrderLineBaseEntity338Nested.OrderHeader)
       },
       NestedFacets = new[]
       {
           typeof(UnitDto338),
           typeof(OrderHeaderDto338)
       })]
public partial class OrderLineBaseDto338Nested
{
}

public class OrderLineDispatchEntity338Nested : OrderLineBaseEntity338Nested
{
    public DateTime DeliveryTime { get; set; }
    public DateTime ShipmentTime { get; set; }
}

[Facet(typeof(OrderLineDispatchEntity338Nested),
       Include = new[]
       {
           nameof(OrderLineDispatchEntity338Nested.DeliveryTime),
           nameof(OrderLineDispatchEntity338Nested.ShipmentTime)
       })]
public partial class OrderLineDispatchDto338Nested : OrderLineBaseDto338Nested
{
}
