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

// ──────────────────────────────────────────────────────────────────────────────
// Regression test for PR #365
// When the first source of a multi-source facet is itself a generated facet
// (its properties are not visible to the source generator), FindNestedFacetModel
// must still pick the model whose SourceTypeName matches the member's
// NestedFacetSourceTypeName (i.e. the second source), not blindly use index 0.
// ──────────────────────────────────────────────────────────────────────────────

public class UnitEntity365
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// This DTO is itself a generated facet.  Its properties (Id, Name) are produced
/// by the source generator and therefore are NOT visible to the generator when it
/// processes other types that reference UnitDto365 as a source.  That causes the
/// UnitDto365-source model of UnitDropDownDto365 to have zero members.
/// </summary>
[Facet(typeof(UnitEntity365),
    Include = [nameof(UnitEntity365.Id), nameof(UnitEntity365.Name)])]
public partial class UnitDto365;

/// <summary>
/// Multi-source facet: first source is a generated DTO (zero visible properties),
/// second source is a concrete entity.  The parent entity uses UnitEntity365,
/// so the projection must use the UnitEntity365 source model.
/// </summary>
[Facet(typeof(UnitDto365),
    Include = [nameof(UnitDto365.Id), nameof(UnitDto365.Name)])]
[Facet(typeof(UnitEntity365),
    Include = [nameof(UnitEntity365.Id), nameof(UnitEntity365.Name)])]
public partial class UnitDropDownDto365;

public class ParentEntity365
{
    public int Id { get; set; }
    public UnitEntity365 Unit { get; set; } = null!;
}

[Facet(typeof(ParentEntity365),
    Include = [nameof(ParentEntity365.Id), nameof(ParentEntity365.Unit)],
    NestedFacets = [typeof(UnitDropDownDto365)])]
public partial class ParentDto365;

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

    /// <summary>
    /// Projection path: when the first source of the multi-source nested facet is a generated
    /// DTO (properties invisible to the generator → zero members in that source model),
    /// the Projection expression must still correctly map the properties from the parent
    /// entity's source type (UnitEntity365), not produce an empty initializer.
    /// </summary>
    [Fact]
    public void Projection_NestedMultiSourceFacet_FirstSourceIsGeneratedDto_ShouldMapProperties()
    {
        var source = new ParentEntity365
        {
            Id = 42,
            Unit = new UnitEntity365 { Id = 10, Name = "Name-10" }
        };

        var projection = ParentDto365.Projection;
        var result = new[] { source }.AsQueryable().Select(projection).Single();

        result.Id.Should().Be(42);
        result.Unit.Should().NotBeNull();
        result.Unit!.Id.Should().Be(10);
        result.Unit!.Name.Should().Be("Name-10");
    }

    /// <summary>
    /// Projection via Projection property on the multi-source facet:
    /// UnitDropDownDto365.ProjectionFromUnitEntity365 must correctly map Id and Name.
    /// </summary>
    [Fact]
    public void ProjectionFromEntity_MultiSourceFacet_SecondSource_ShouldMapProperties()
    {
        var source = new UnitEntity365 { Id = 7, Name = "Seven" };

        var result = new[] { source }.AsQueryable()
            .Select(UnitDropDownDto365.ProjectionFromUnitEntity365)
            .Single();

        result.Id.Should().Be(7);
        result.Name.Should().Be("Seven");
    }
}
