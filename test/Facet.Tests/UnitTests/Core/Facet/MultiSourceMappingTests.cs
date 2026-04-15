using Facet.Tests.TestModels;

namespace Facet.Tests.UnitTests.Core.Facet;

/// <summary>
/// Tests for the multi-source mapping feature: a single target class decorated with
/// multiple <c>[Facet]</c> attributes, each specifying a different source type.
/// </summary>
public class MultiSourceMappingTests
{
    // ── Construction from source A ────────────────────────────────────────────

    [Fact]
    public void Constructor_FromEntityA_MapsSharedProperties()
    {
        var source = new MultiSourceEntityA { Id = 1, Name = "Alpha", OnlyInA = "A-only" };

        var dto = new MultiSourceDto(source);

        dto.Id.Should().Be(1);
        dto.Name.Should().Be("Alpha");
    }

    [Fact]
    public void Constructor_FromEntityB_MapsSharedProperties()
    {
        var source = new MultiSourceEntityB { Id = 2, Name = "Beta", OnlyInB = "B-only" };

        var dto = new MultiSourceDto(source);

        dto.Id.Should().Be(2);
        dto.Name.Should().Be("Beta");
    }

    // ── FromSource factory methods ────────────────────────────────────────────

    [Fact]
    public void FromSource_WithEntityA_ReturnsCorrectlyMappedDto()
    {
        var source = new MultiSourceEntityA { Id = 10, Name = "EntityA" };

        var dto = MultiSourceDto.FromSource(source);

        dto.Id.Should().Be(10);
        dto.Name.Should().Be("EntityA");
    }

    [Fact]
    public void FromSource_WithEntityB_ReturnsCorrectlyMappedDto()
    {
        var source = new MultiSourceEntityB { Id = 20, Name = "EntityB" };

        var dto = MultiSourceDto.FromSource(source);

        dto.Id.Should().Be(20);
        dto.Name.Should().Be("EntityB");
    }

    // ── Projection expressions ────────────────────────────────────────────────

    [Fact]
    public void Projection_FromEntityA_CanProjectList()
    {
        var sources = new List<MultiSourceEntityA>
        {
            new() { Id = 1, Name = "One" },
            new() { Id = 2, Name = "Two" },
        };

        var dtos = sources.AsQueryable().Select(MultiSourceDto.ProjectionFromMultiSourceEntityA).ToList();

        dtos.Should().HaveCount(2);
        dtos[0].Id.Should().Be(1);
        dtos[1].Name.Should().Be("Two");
    }

    [Fact]
    public void Projection_FromEntityB_CanProjectList()
    {
        var sources = new List<MultiSourceEntityB>
        {
            new() { Id = 3, Name = "Three" },
        };

        var dtos = sources.AsQueryable().Select(MultiSourceDto.ProjectionFromMultiSourceEntityB).ToList();

        dtos.Should().HaveCount(1);
        dtos[0].Id.Should().Be(3);
        dtos[0].Name.Should().Be("Three");
    }

    // ── ToSource methods ──────────────────────────────────────────────────────

    [Fact]
    public void ToMultiSourceEntityA_ReturnsEntityWithMappedProperties()
    {
        var dto = new MultiSourceWithToSourceDto { Id = 5, Name = "Five" };

        var entity = dto.ToMultiSourceEntityA();

        entity.Id.Should().Be(5);
        entity.Name.Should().Be("Five");
    }

    // ── Union-of-members behaviour ────────────────────────────────────────────

    [Fact]
    public void UnionDto_ContainsMembersFromBothSources()
    {
        // Verify that the union DTO exposes properties contributed by BOTH source types.
        var dtoType = typeof(MultiSourceUnionDto);

        dtoType.GetProperty(nameof(MultiSourceEntityA.Id)).Should().NotBeNull("Id is present in both sources");
        dtoType.GetProperty(nameof(MultiSourceEntityA.Name)).Should().NotBeNull("Name is present in both sources");
        dtoType.GetProperty(nameof(MultiSourceEntityA.OnlyInA)).Should().NotBeNull("OnlyInA is contributed by EntityA");
        dtoType.GetProperty(nameof(MultiSourceEntityB.OnlyInB)).Should().NotBeNull("OnlyInB is contributed by EntityB");
    }

    [Fact]
    public void UnionDto_ConstructorFromA_MapsAllAProperties()
    {
        var source = new MultiSourceEntityA { Id = 7, Name = "Seven", OnlyInA = "aaa" };

        var dto = new MultiSourceUnionDto(source);

        dto.Id.Should().Be(7);
        dto.Name.Should().Be("Seven");
        dto.OnlyInA.Should().Be("aaa");
    }

    [Fact]
    public void UnionDto_ConstructorFromB_MapsAllBProperties()
    {
        var source = new MultiSourceEntityB { Id = 8, Name = "Eight", OnlyInB = "bbb" };

        var dto = new MultiSourceUnionDto(source);

        dto.Id.Should().Be(8);
        dto.Name.Should().Be("Eight");
        dto.OnlyInB.Should().Be("bbb");
    }

    [Fact]
    public void NestedMultiSourceFacet_ToSource_ShouldCallCorrectSourceSpecificMethod()
    {
        // Arrange: Create a parent DTO with a nested multi-source facet
        var orderLine = new OrderLineBaseEntity
        {
            Id = 1,
            Number = "ORD-001",
            AssignedToUnit = new UnitEntity
            {
                Id = 100,
                Name = "Production Unit",
                ValidationResult = "Valid"
            }
        };

        var dto = new OrderLineBaseUpsertDto(orderLine);

        // Verify the DTO was constructed correctly
        dto.Number.Should().Be("ORD-001");
        dto.AssignedToUnit.Should().NotBeNull();
        dto.AssignedToUnit!.Name.Should().Be("Production Unit");
        dto.AssignedToUnit.ValidationResult.Should().Be("Valid");

        // Act: Call ToSource on the parent DTO
        // This should internally call ToUnitEntity() on the nested multi-source facet
        var result = dto.ToSource();

        // Assert: Verify the parent entity was reconstructed correctly
        result.Number.Should().Be("ORD-001");
        result.AssignedToUnit.Should().NotBeNull();
        result.AssignedToUnit!.Name.Should().Be("Production Unit");
        result.AssignedToUnit.ValidationResult.Should().Be("Valid");
    }

    [Fact]
    public void NestedMultiSourceFacet_WithNullNestedProperty_ShouldHandleCorrectly()
    {
        // Arrange: Create a parent DTO with null nested property
        var dto = new OrderLineBaseUpsertDto
        {
            Number = "ORD-002",
            AssignedToUnit = null
        };

        // Act: Call ToSource on the parent DTO with null nested property
        var result = dto.ToSource();

        // Assert: Verify null is preserved
        result.Number.Should().Be("ORD-002");
        result.AssignedToUnit.Should().BeNull();
    }

    [Fact]
    public void MultiSourceNestedFacet_ShouldGenerateSourceSpecificToSourceMethods()
    {
        // Verify that UnitDropDownDto (multi-source facet) generates source-specific ToSource methods
        var unitDropDown = new UnitDropDownDto
        {
            Name = "Test Unit",
            ValidationResult = "Passed"
        };

        // Should have ToUnitDto() method
        var unitDto = unitDropDown.ToUnitDto();
        unitDto.Name.Should().Be("Test Unit");
        unitDto.ValidationResult.Should().Be("Passed");

        // Should have ToUnitEntity() method
        var unitEntity = unitDropDown.ToUnitEntity();
        unitEntity.Name.Should().Be("Test Unit");
        unitEntity.ValidationResult.Should().Be("Passed");

        var methods = typeof(UnitDropDownDto).GetMethods();
        methods.Should().Contain(m => m.Name == "ToUnitDto");
        methods.Should().Contain(m => m.Name == "ToUnitEntity");
    }
}
