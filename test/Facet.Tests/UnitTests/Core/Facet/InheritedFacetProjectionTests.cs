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
}
