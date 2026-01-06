using Facet.Mapping;

namespace Facet.Tests.UnitTests.Core.Facet.MappingHooksIntegration;

// Test entity for generated hooks
public class GeneratedHooksEntity
{
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateTime DateOfBirth { get; set; }
    public bool IsActive { get; set; }
}

// BeforeMap hook configuration
public class GeneratedBeforeMapConfig : IFacetBeforeMapConfiguration<GeneratedHooksEntity, GeneratedBeforeMapFacet>
{
    public static void BeforeMap(GeneratedHooksEntity source, GeneratedBeforeMapFacet target)
    {
        target.MappedAt = DateTime.UtcNow;
    }
}

// AfterMap hook configuration
public class GeneratedAfterMapConfig : IFacetAfterMapConfiguration<GeneratedHooksEntity, GeneratedAfterMapFacet>
{
    public static void AfterMap(GeneratedHooksEntity source, GeneratedAfterMapFacet target)
    {
        target.FullName = $"{target.FirstName} {target.LastName}";
    }
}

// Combined hooks configuration
public class GeneratedCombinedConfig : IFacetMapHooksConfiguration<GeneratedHooksEntity, GeneratedCombinedFacet>
{
    public static void BeforeMap(GeneratedHooksEntity source, GeneratedCombinedFacet target)
    {
        target.MappedAt = DateTime.UtcNow;
    }

    public static void AfterMap(GeneratedHooksEntity source, GeneratedCombinedFacet target)
    {
        target.FullName = $"{target.FirstName} {target.LastName}";
    }
}

// Generated facet with BeforeMap
[Facet(typeof(GeneratedHooksEntity), BeforeMapConfiguration = typeof(GeneratedBeforeMapConfig))]
public partial class GeneratedBeforeMapFacet
{
    public DateTime MappedAt { get; set; }
}

// Generated facet with AfterMap
[Facet(typeof(GeneratedHooksEntity), AfterMapConfiguration = typeof(GeneratedAfterMapConfig))]
public partial class GeneratedAfterMapFacet
{
    public string FullName { get; set; } = string.Empty;
}

// Generated facet with both hooks
[Facet(typeof(GeneratedHooksEntity),
    BeforeMapConfiguration = typeof(GeneratedCombinedConfig),
    AfterMapConfiguration = typeof(GeneratedCombinedConfig))]
public partial class GeneratedCombinedFacet
{
    public DateTime MappedAt { get; set; }
    public string FullName { get; set; } = string.Empty;
}

/// <summary>
/// Integration tests for Before/After mapping hooks with generated facets.
/// </summary>
public class MappingHooksIntegrationTests
{
    [Fact]
    public void GeneratedFacet_WithBeforeMap_ShouldSetMappedAt()
    {
        // Arrange
        var entity = new GeneratedHooksEntity
        {
            Id = 1,
            FirstName = "John",
            LastName = "Doe",
            DateOfBirth = DateTime.Today.AddYears(-30),
            IsActive = true
        };
        var beforeCall = DateTime.UtcNow;

        // Act
        var facet = new GeneratedBeforeMapFacet(entity);
        var afterCall = DateTime.UtcNow;

        // Assert
        facet.Id.Should().Be(1);
        facet.FirstName.Should().Be("John");
        facet.LastName.Should().Be("Doe");
        facet.MappedAt.Should().BeOnOrAfter(beforeCall);
        facet.MappedAt.Should().BeOnOrBefore(afterCall);
    }

    [Fact]
    public void GeneratedFacet_WithAfterMap_ShouldComputeFullName()
    {
        // Arrange
        var entity = new GeneratedHooksEntity
        {
            Id = 2,
            FirstName = "Jane",
            LastName = "Smith",
            DateOfBirth = DateTime.Today.AddYears(-25),
            IsActive = true
        };

        // Act
        var facet = new GeneratedAfterMapFacet(entity);

        // Assert
        facet.Id.Should().Be(2);
        facet.FirstName.Should().Be("Jane");
        facet.LastName.Should().Be("Smith");
        facet.FullName.Should().Be("Jane Smith");
    }

    [Fact]
    public void GeneratedFacet_WithCombinedHooks_ShouldCallBothBeforeAndAfter()
    {
        // Arrange
        var entity = new GeneratedHooksEntity
        {
            Id = 3,
            FirstName = "Bob",
            LastName = "Johnson",
            DateOfBirth = DateTime.Today.AddYears(-40),
            IsActive = false
        };
        var beforeCall = DateTime.UtcNow;

        // Act
        var facet = new GeneratedCombinedFacet(entity);
        var afterCall = DateTime.UtcNow;

        // Assert
        facet.Id.Should().Be(3);
        facet.FirstName.Should().Be("Bob");
        facet.LastName.Should().Be("Johnson");
        facet.MappedAt.Should().BeOnOrAfter(beforeCall);
        facet.MappedAt.Should().BeOnOrBefore(afterCall);
        facet.FullName.Should().Be("Bob Johnson");
    }

    [Fact]
    public void GeneratedFacet_WithBeforeMap_ShouldWorkWithFromSource()
    {
        // Arrange
        var entity = new GeneratedHooksEntity
        {
            Id = 4,
            FirstName = "Alice",
            LastName = "Brown",
            DateOfBirth = DateTime.Today.AddYears(-28),
            IsActive = true
        };
        var beforeCall = DateTime.UtcNow;

        // Act
        var facet = GeneratedBeforeMapFacet.FromSource(entity);
        var afterCall = DateTime.UtcNow;

        // Assert
        facet.FirstName.Should().Be("Alice");
        facet.MappedAt.Should().BeOnOrAfter(beforeCall);
        facet.MappedAt.Should().BeOnOrBefore(afterCall);
    }

    [Fact]
    public void GeneratedFacet_WithAfterMap_ShouldWorkWithProjection()
    {
        // Arrange
        var entities = new[]
        {
            new GeneratedHooksEntity { Id = 1, FirstName = "John", LastName = "Doe" },
            new GeneratedHooksEntity { Id = 2, FirstName = "Jane", LastName = "Smith" }
        }.AsQueryable();

        // Act - Projection doesn't call hooks (they're runtime-only)
        var facets = entities.Select(GeneratedAfterMapFacet.Projection).ToList();

        // Assert - Properties are mapped but FullName is NOT computed (hooks don't run in projections)
        facets.Should().HaveCount(2);
        facets[0].FirstName.Should().Be("John");
        facets[1].FirstName.Should().Be("Jane");
    }
}
