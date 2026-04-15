namespace Facet.Tests.UnitTests.Core.Facet;

// --- Source entities ---

public class UnitEntity335
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ValidationResult { get; set; } = string.Empty;
}

public class UnitDto335
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ValidationResult { get; set; } = string.Empty;
}

public class OrderLineEntity335
{
    public int Id { get; set; }
    public string Number { get; set; } = string.Empty;
    public UnitEntity335? AssignedToUnit { get; set; }
}

// --- Multi-source nested facet: attribute order UnitDto first, UnitEntity second ---

[Facet(typeof(UnitDto335),
    Include = [nameof(UnitDto335.Name), nameof(UnitDto335.ValidationResult)])]
[Facet(typeof(UnitEntity335),
    Include = [nameof(UnitEntity335.Name), nameof(UnitEntity335.ValidationResult)])]
public partial class UnitDropDownDto335;

// --- Parent facet using the multi-source facet as a nested property ---

[Facet(typeof(OrderLineEntity335),
    Include = [nameof(OrderLineEntity335.AssignedToUnit), nameof(OrderLineEntity335.Number)],
    NestedFacets = [typeof(UnitDropDownDto335)])]
public partial class OrderLineDto335;

/// <summary>
/// Tests for multi-source facets used as nested facets.
/// Regression test for issue #335: nested facet resolution should work
/// regardless of [Facet] attribute order on the multi-source type.
/// </summary>
public class MultiSourceNestedFacetTests
{
    [Fact]
    public void NestedProperty_ShouldUseCorrectFacetType_RegardlessOfAttributeOrder()
    {
        var source = new OrderLineEntity335
        {
            Id = 1,
            Number = "OL-001",
            AssignedToUnit = new UnitEntity335 { Id = 10, Name = "Unit A", ValidationResult = "OK" }
        };

        var dto = new OrderLineDto335(source);

        dto.Number.Should().Be("OL-001");
        dto.AssignedToUnit.Should().NotBeNull();
        dto.AssignedToUnit.Should().BeOfType<UnitDropDownDto335>();
        dto.AssignedToUnit!.Name.Should().Be("Unit A");
        dto.AssignedToUnit.ValidationResult.Should().Be("OK");
    }

    [Fact]
    public void FromSource_ShouldMapNestedMultiSourceFacet()
    {
        var source = new OrderLineEntity335
        {
            Id = 2,
            Number = "OL-002",
            AssignedToUnit = new UnitEntity335 { Id = 20, Name = "Unit B", ValidationResult = "WARN" }
        };

        var dto = OrderLineDto335.FromSource(source);

        dto.AssignedToUnit.Should().NotBeNull();
        dto.AssignedToUnit!.Name.Should().Be("Unit B");
        dto.AssignedToUnit.ValidationResult.Should().Be("WARN");
    }

    [Fact]
    public void NullNestedProperty_ShouldMapToNull()
    {
        var source = new OrderLineEntity335
        {
            Id = 3,
            Number = "OL-003",
            AssignedToUnit = null
        };

        var dto = new OrderLineDto335(source);

        dto.Number.Should().Be("OL-003");
        dto.AssignedToUnit.Should().BeNull();
    }
}
