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
    public void Projection_WithInheritedFacetBase_ThroughQueryable_ShouldMapAllProperties()
    {
        // Arrange
        var baseTime = new DateTime(2024, 1, 1, 10, 0, 0);
        var entities = new[]
        {
            new OrderLineDispatchEntity338
            {
                Id = 11,
                Number = "321",
                ExpectedStartTime = baseTime,
                DeliveryTime = baseTime.AddDays(1),
                ShipmentTime = baseTime.AddDays(2)
            }
        }.AsQueryable();

        // Act - Use projection through IQueryable.Select (real projection path)
        var dto = entities.Select(OrderLineDispatchDto338.Projection).Single();

        // Assert - Derived Facet mappings
        dto.DeliveryTime.Should().Be(baseTime.AddDays(1).AddHours(2));
        dto.ShipmentTime.Should().Be(baseTime.AddDays(2).AddHours(3));

        // Assert - Base Facet mappings
        dto.Number.Should().Be("ORD-321");
        dto.ExpectedStartTime.Should().Be(baseTime.AddHours(1));

        // Assert - Non-Facet base mapping
        dto.Id.Should().Be(11);
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

    [Fact]
    public void InheritedFacet_WithBaseNestedMultiSourceFacet_ToSource_ShouldCallCorrectMethod()
    {
        // Arrange -> Test issue #340: inherited Facet with nested multi-source facet
        var entity = new OrderLineDispatchEntity340
        {
            Id = 1,
            Number = "ORD-001",
            DeliveryTime = new DateTime(2024, 1, 1),
            AssignedToUnit = new UnitEntity340
            {
                Id = 100,
                Name = "Production Unit",
                ValidationResult = "Valid"
            }
        };

        var dto = new OrderLineDispatchDto340(entity);

        // Verify the DTO was constructed correctly
        dto.Number.Should().Be("ORD-001");
        dto.AssignedToUnit.Should().NotBeNull();
        dto.AssignedToUnit.Should().BeOfType<UnitDropDownDto340>();
        dto.AssignedToUnit!.Name.Should().Be("Production Unit");
        dto.AssignedToUnit.ValidationResult.Should().Be("Valid");

        // Act - Call ToSource on the derived Facet
        // This should correctly call ToUnitEntity340() on the nested multi-source facet
        var result = dto.ToSource();

        // Assert - Verify the entity was reconstructed correctly
        result.Number.Should().Be("ORD-001");
        result.AssignedToUnit.Should().NotBeNull();
        result.AssignedToUnit!.Name.Should().Be("Production Unit");
        result.AssignedToUnit.ValidationResult.Should().Be("Valid");
    }

    [Fact]
    public void Projection_WithInheritedFacetBase_WhenBaseFacetHasMultipleSources_ShouldApplyMatchingBaseConfiguration()
    {
        // Arrange
        var baseTime = new DateTime(2024, 2, 1, 9, 0, 0);
        var entity = new OrderLineDispatchEntity341
        {
            Id = 42,
            Number = "ABC",
            ExpectedStartTime = baseTime,
            DeliveryTime = baseTime.AddDays(1)
        };

        // Act
        var dto = OrderLineDispatchDto341.Projection.Compile()(entity);

        // Assert - derived mapping
        dto.DeliveryTime.Should().Be(baseTime.AddDays(1).AddHours(2));

        // Assert - matching base Facet source mapping should be applied
        dto.Number.Should().Be("ORD-ABC");
        dto.ExpectedStartTime.Should().Be(baseTime.AddHours(1));
        dto.Id.Should().Be(42);
    }

    [Fact]
    public void Projection_WithInheritedNestedFacets_ShouldProjectAllNestedTypesCorrectly()
    {
        // Arrange - Regression test for issue #338: derived DTO inheriting base DTO with NestedFacets
        var entity = new OrderLineWithPackingEntity345
        {
            Id = 1,
            Number = "ORD-001",
            ExpectedStartTime = new DateTime(2024, 1, 1),
            Description = "Test",
            AssignedToUnit = new UnitEntity345 { Id = 100, Name = "Unit A" },
            OrderHeader = new OrderHeaderEntity345 { Id = 200, OrderNumber = "HDR-001" },
            InventoryItems = new List<InventoryItemEntity345>
            {
                new() { Id = 300, Identifier = "INV-001" }
            }
        };

        // Act - Use projection (EF Core path)
        var dto = OrderLineWithPackingDto345.Projection.Compile()(entity);

        // Assert - Properties from derived DTO's own NestedFacets should be projected
        dto.InventoryItems.Should().HaveCount(1);
        dto.InventoryItems[0].Should().BeOfType<InventoryItemDto345>();
        dto.InventoryItems[0].Identifier.Should().Be("INV-001");
        dto.Description.Should().Be("Test");

        // Assert - Properties from BASE DTO's NestedFacets should ALSO be projected
        dto.AssignedToUnit.Should().NotBeNull();
        dto.AssignedToUnit.Should().BeOfType<UnitDto345>("AssignedToUnit should be projected as UnitDto345, not UnitEntity345");
        dto.AssignedToUnit!.Name.Should().Be("Unit A");

        dto.OrderHeader.Should().NotBeNull();
        dto.OrderHeader.Should().BeOfType<OrderHeaderDto345>("OrderHeader should be projected as OrderHeaderDto345, not OrderHeaderEntity345");
        dto.OrderHeader.OrderNumber.Should().Be("HDR-001");

        // Assert - Simple properties from base
        dto.Number.Should().Be("ORD-001");
    }

    [Fact]
    public void Projection_WithMultiSourceBase_NestedFacets_ShouldProjectCorrectly()
    {
        // Arrange - Multi-source base Facet where the matching attribute has NestedFacets
        // but another (non-matching) attribute also includes properties.
        // This tests the scenario where GetBaseClassMemberNames sees ALL attributes
        // but GetBaseFacetInfo only selects the best-matching one.
        var entity = new DerivedMultiSrcEntity345
        {
            Id = 1,
            Number = "ORD-001",
            AssignedToUnit = new UnitEntity345 { Id = 100, Name = "Unit A" },
            ExtraInfo = "extra"
        };

        // Act - Use projection
        var dto = DerivedMultiSrcDto345.Projection.Compile()(entity);

        // Assert - NestedFacets from the matching base [Facet] attribute should be projected
        dto.AssignedToUnit.Should().NotBeNull();
        dto.AssignedToUnit.Should().BeOfType<UnitDto345>("AssignedToUnit should be projected as UnitDto345");
        dto.AssignedToUnit!.Name.Should().Be("Unit A");

        // Assert - Simple properties
        dto.Number.Should().Be("ORD-001");
        dto.ExtraInfo.Should().Be("extra");
    }

    [Fact]
    public void Projection_WithConfiguredDerivedFacet_InheritedNestedFacets_ShouldProjectCorrectly()
    {
        // Arrange - Derived Facet has IFacetProjectionMapConfiguration (triggers GenerateLazyProjection)
        // Base Facet has NestedFacets.
        // This tests that GenerateLazyProjection includes nested facet bindings.
        var entity = new DerivedConfiguredEntity345
        {
            Id = 1,
            Number = "ORD-001",
            AssignedToUnit = new UnitEntity345 { Id = 100, Name = "Unit A" },
            ExtraTime = new DateTime(2024, 6, 15, 10, 0, 0)
        };

        // Act
        var dto = DerivedConfiguredDto345.Projection.Compile()(entity);

        // Assert - Derived Configuration mapping
        dto.ExtraTime.Should().Be(new DateTime(2024, 6, 15, 10, 0, 0).AddHours(5),
            "ExtraTime should be mapped with +5 hours from ConfigureProjection");

        // Assert - NestedFacets from base should be projected correctly
        dto.AssignedToUnit.Should().NotBeNull();
        dto.AssignedToUnit.Should().BeOfType<UnitDto345>("AssignedToUnit should be UnitDto345, not UnitEntity345");
        dto.AssignedToUnit!.Name.Should().Be("Unit A");

        // Assert - Simple properties from base
        dto.Number.Should().Be("ORD-001");
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

// --- Test models for inherited Facets with nested multi-source facets and ToSource (issue #340) ---

public class UnitEntity340
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ValidationResult { get; set; } = string.Empty;
}

public class UnitDto340
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ValidationResult { get; set; } = string.Empty;
}

/// <summary>
/// Multi-source facet that can map from either UnitDto340 or UnitEntity340.
/// </summary>
[Facet(typeof(UnitDto340),
       GenerateToSource = true,
       Include = new[] { nameof(UnitDto340.Name), nameof(UnitDto340.ValidationResult) })]
[Facet(typeof(UnitEntity340),
       GenerateToSource = true,
       Include = new[] { nameof(UnitEntity340.Name), nameof(UnitEntity340.ValidationResult) })]
public partial class UnitDropDownDto340
{
}

public class OrderLineBaseEntity340
{
    public int Id { get; set; }
    public string Number { get; set; } = string.Empty;
    public UnitEntity340? AssignedToUnit { get; set; }
}

/// <summary>
/// Base Facet with nested multi-source facet.
/// </summary>
[Facet(typeof(OrderLineBaseEntity340),
       GenerateToSource = true,
       Include = new[]
       {
           nameof(OrderLineBaseEntity340.Number),
           nameof(OrderLineBaseEntity340.AssignedToUnit)
       },
       NestedFacets = new[] { typeof(UnitDropDownDto340) })]
public partial class OrderLineBaseDto340
{
}

public class OrderLineDispatchEntity340 : OrderLineBaseEntity340
{
    public DateTime DeliveryTime { get; set; }
}

/// <summary>
/// Derived Facet that inherits from base Facet with nested multi-source facet.
/// This tests issue #340 - ensuring ToSource correctly calls ToUnitEntity340() instead of ToSource().
/// </summary>
[Facet(typeof(OrderLineDispatchEntity340),
       GenerateToSource = true,
       Include = new[] { nameof(OrderLineDispatchEntity340.DeliveryTime) })]
public partial class OrderLineDispatchDto340 : OrderLineBaseDto340
{
}

public class OtherSourceEntity341
{
    public int Id { get; set; }
    public string OtherCode { get; set; } = string.Empty;
}

public class OrderLineBaseEntity341
{
    public int Id { get; set; }
    public string Number { get; set; } = string.Empty;
    public DateTime ExpectedStartTime { get; set; }
}

public class OrderLineDispatchEntity341 : OrderLineBaseEntity341
{
    public DateTime DeliveryTime { get; set; }
}

[Facet(typeof(OtherSourceEntity341),
       Include = new[]
       {
            nameof(OtherSourceEntity341.OtherCode)
       })]
[Facet(typeof(OrderLineBaseEntity341),
       Configuration = typeof(OrderLineBaseDto341MapConfig),
       Include = new[]
       {
            nameof(OrderLineBaseEntity341.Number),
            nameof(OrderLineBaseEntity341.ExpectedStartTime)
       })]
public partial class OrderLineBaseDto341 : ModifiedByBaseDto338
{
}

public class OrderLineBaseDto341MapConfig
    : IFacetProjectionMapConfiguration<OrderLineBaseEntity341, OrderLineBaseDto341>
{
    public static void ConfigureProjection(
        IFacetProjectionBuilder<OrderLineBaseEntity341, OrderLineBaseDto341> builder)
    {
        builder.Map(d => d.Number, s => "ORD-" + s.Number);
        builder.Map(d => d.ExpectedStartTime, s => s.ExpectedStartTime.AddHours(1));
    }
}

[Facet(typeof(OrderLineDispatchEntity341),
       Configuration = typeof(OrderLineDispatchDto341MapConfig),
       Include = new[]
       {
            nameof(OrderLineDispatchEntity341.DeliveryTime)
       })]
public partial class OrderLineDispatchDto341 : OrderLineBaseDto341
{
}

public class OrderLineDispatchDto341MapConfig
    : IFacetProjectionMapConfiguration<OrderLineDispatchEntity341, OrderLineDispatchDto341>
{
    public static void ConfigureProjection(
        IFacetProjectionBuilder<OrderLineDispatchEntity341, OrderLineDispatchDto341> builder)
    {
        builder.Map(d => d.DeliveryTime, s => s.DeliveryTime.AddHours(2));
    }
}

public class UnitEntity345
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

[Facet(typeof(UnitEntity345))]
public partial class UnitDto345
{
}

public class OrderHeaderEntity345
{
    public int Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
}

[Facet(typeof(OrderHeaderEntity345))]
public partial class OrderHeaderDto345
{
}

public class InventoryItemEntity345
{
    public int Id { get; set; }
    public string Identifier { get; set; } = string.Empty;
}

[Facet(typeof(InventoryItemEntity345))]
public partial class InventoryItemDto345
{
}

/// <summary>Base entity with navigation properties.</summary>
public class OrderLineBaseEntity345
{
    public int Id { get; set; }
    public string Number { get; set; } = string.Empty;
    public DateTime ExpectedStartTime { get; set; }
    public UnitEntity345? AssignedToUnit { get; set; }
    public OrderHeaderEntity345 OrderHeader { get; set; } = null!;
}

/// <summary>Derived entity with additional navigation properties.</summary>
public class OrderLineWithPackingEntity345 : OrderLineBaseEntity345
{
    public List<InventoryItemEntity345> InventoryItems { get; set; } = new();
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// Base DTO with NestedFacets for navigation properties.
/// </summary>
[Facet(typeof(OrderLineBaseEntity345),
       Include = new[]
       {
           nameof(OrderLineBaseEntity345.Number),
           nameof(OrderLineBaseEntity345.ExpectedStartTime),
           nameof(OrderLineBaseEntity345.AssignedToUnit),
           nameof(OrderLineBaseEntity345.OrderHeader)
       },
       NestedFacets = new[]
       {
           typeof(UnitDto345),
           typeof(OrderHeaderDto345)
       })]
public partial class OrderLineBaseDto345
{
}

/// <summary>
/// Derived DTO with its own NestedFacets, inheriting from base DTO.
/// This tests issue #338 regression: inherited NestedFacets should be projected correctly.
/// </summary>
[Facet(typeof(OrderLineWithPackingEntity345),
       Include = new[]
       {
           nameof(OrderLineWithPackingEntity345.InventoryItems),
           nameof(OrderLineWithPackingEntity345.Description)
       },
       NestedFacets = new[]
       {
           typeof(InventoryItemDto345)
       })]
public partial class OrderLineWithPackingDto345 : OrderLineBaseDto345
{
}

// --- Test models for multi-source base Facet with NestedFacets (issue #338 regression) ---

/// <summary>Unrelated source entity for the second [Facet] attribute.</summary>
public class OtherSourceEntity345
{
    public int Id { get; set; }
    public string OtherCode { get; set; } = string.Empty;
}

/// <summary>Base entity with a navigation property.</summary>
public class BaseMultiSrcEntity345
{
    public int Id { get; set; }
    public string Number { get; set; } = string.Empty;
    public UnitEntity345? AssignedToUnit { get; set; }
}

/// <summary>Derived entity.</summary>
public class DerivedMultiSrcEntity345 : BaseMultiSrcEntity345
{
    public string ExtraInfo { get; set; } = string.Empty;
}

/// <summary>
/// Multi-source base Facet. The matching source (BaseMultiSrcEntity345) has NestedFacets.
/// The non-matching source (OtherSourceEntity345) also includes properties.
/// GetBaseClassMemberNames iterates ALL attributes, GetBaseFacetInfo selects only the matching one.
/// </summary>
[Facet(typeof(OtherSourceEntity345),
       Include = new[] { nameof(OtherSourceEntity345.OtherCode) })]
[Facet(typeof(BaseMultiSrcEntity345),
       Include = new[]
       {
           nameof(BaseMultiSrcEntity345.Number),
           nameof(BaseMultiSrcEntity345.AssignedToUnit)
       },
       NestedFacets = new[] { typeof(UnitDto345) })]
public partial class BaseMultiSrcDto345
{
}

/// <summary>Derived Facet inheriting from multi-source base.</summary>
[Facet(typeof(DerivedMultiSrcEntity345),
       Include = new[] { nameof(DerivedMultiSrcEntity345.ExtraInfo) })]
public partial class DerivedMultiSrcDto345 : BaseMultiSrcDto345
{
}

// --- Test models for IFacetProjectionMapConfiguration + inherited NestedFacets ---

/// <summary>Base entity with navigation property for configured projection test.</summary>
public class BaseConfiguredEntity345
{
    public int Id { get; set; }
    public string Number { get; set; } = string.Empty;
    public UnitEntity345? AssignedToUnit { get; set; }
}

/// <summary>Derived entity with extra properties.</summary>
public class DerivedConfiguredEntity345 : BaseConfiguredEntity345
{
    public DateTime ExtraTime { get; set; }
}

/// <summary>
/// Base Facet with NestedFacets (no Configuration).
/// </summary>
[Facet(typeof(BaseConfiguredEntity345),
       Include = new[]
       {
           nameof(BaseConfiguredEntity345.Number),
           nameof(BaseConfiguredEntity345.AssignedToUnit)
       },
       NestedFacets = new[] { typeof(UnitDto345) })]
public partial class BaseConfiguredDto345
{
}

/// <summary>
/// Derived Facet WITH IFacetProjectionMapConfiguration.
/// This triggers GenerateLazyProjection which must handle inherited NestedFacets.
/// </summary>
[Facet(typeof(DerivedConfiguredEntity345),
       Configuration = typeof(DerivedConfiguredDto345MapConfig),
       Include = new[] { nameof(DerivedConfiguredEntity345.ExtraTime) })]
public partial class DerivedConfiguredDto345 : BaseConfiguredDto345
{
}

public class DerivedConfiguredDto345MapConfig
    : IFacetProjectionMapConfiguration<DerivedConfiguredEntity345, DerivedConfiguredDto345>
{
    public static void ConfigureProjection(
        IFacetProjectionBuilder<DerivedConfiguredEntity345, DerivedConfiguredDto345> builder)
    {
        builder.Map(d => d.ExtraTime, s => s.ExtraTime.AddHours(5));
    }
}

// Below tests are reproduction of the actual issue:

public class UnitEntity345_18
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

[Facet(typeof(UnitEntity345_18))]
public partial class UnitDto345_18;

/// <summary>Base entity.</summary>
public class ModifiedByBaseEntity345_18
{
    public UnitEntity345_18 AssignedToUnit { get; set; }
}

/// <summary>Base entity.</summary>
public class OrderLineBaseEntity345_18 : ModifiedByBaseEntity345_18
{
    public int Id { get; set; }
    public string Number { get; set; } = string.Empty;
    public DateTime ExpectedStartTime { get; set; }
}

/// <summary>Derived entity that adds dispatch-specific properties.</summary>
public class OrderLineDispatchEntity345_18 : OrderLineBaseEntity345_18
{
    public DateTime DeliveryTime { get; set; }
    public DateTime ShipmentTime { get; set; }
}

[Facet(typeof(ModifiedByBaseEntity345_18),
       Include =
       [
           nameof(ModifiedByBaseEntity345_18.AssignedToUnit)
       ],
       NestedFacets = [
           typeof(UnitDto345_18)
       ])]
public partial class ModifiedByBaseDto345_18;

[Facet(typeof(OrderLineBaseEntity345_18),
       Configuration = typeof(OrderLineBaseDto345_18MapConfig),
       Include =
       [
           nameof(OrderLineBaseEntity345_18.Number),
           nameof(OrderLineBaseEntity345_18.ExpectedStartTime)
       ])]
public partial class OrderLineBaseDto345_18 : ModifiedByBaseDto345_18;
public class OrderLineBaseDto345_18MapConfig
    : IFacetProjectionMapConfiguration<OrderLineBaseEntity345_18, OrderLineBaseDto345_18>
{
    public static void ConfigureProjection(
        IFacetProjectionBuilder<OrderLineBaseEntity345_18, OrderLineBaseDto345_18> builder)
    {
        // Map base properties (these should appear in derived projection too)
        builder.Map(d => d.Number, s => "ORD-" + s.Number);
        builder.Map(d => d.ExpectedStartTime, s => s.ExpectedStartTime.AddHours(1));
    }
}

[Facet(typeof(OrderLineDispatchEntity345_18),
       Configuration = typeof(OrderLineDispatchDto345_18MapConfig),
       Include =
       [
           nameof(OrderLineDispatchEntity345_18.DeliveryTime),
           nameof(OrderLineDispatchEntity345_18.ShipmentTime)
       ])]
public partial class OrderLineDispatchDto345_18 : OrderLineBaseDto345_18;
public class OrderLineDispatchDto345_18MapConfig
    : IFacetProjectionMapConfiguration<OrderLineDispatchEntity345_18, OrderLineDispatchDto345_18>
{
    public static void ConfigureProjection(
        IFacetProjectionBuilder<OrderLineDispatchEntity345_18, OrderLineDispatchDto345_18> builder)
    {
        // Map dispatch-specific properties
        builder.Map(d => d.DeliveryTime, s => s.DeliveryTime.AddHours(2));
        builder.Map(d => d.ShipmentTime, s => s.ShipmentTime.AddHours(3));
    }
}