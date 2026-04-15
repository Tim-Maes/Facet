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
}
