namespace Facet.Tests.UnitTests.Core.Facet;

// Test entities
public class AccessibilityTestEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

// Internal facet - should generate with internal accessibility
[Facet(typeof(AccessibilityTestEntity), GenerateToSource = true)]
internal partial class InternalFacet;

// Public facet - should generate with public accessibility
[Facet(typeof(AccessibilityTestEntity), GenerateToSource = true)]
public partial class PublicFacet;

public class AccessibilityTests
{
    [Fact]
    public void InternalFacet_ShouldCompileAndWork()
    {
        // Arrange
        var entity = new AccessibilityTestEntity
        {
            Id = 1,
            Name = "Test"
        };

        // Act
        var facet = new InternalFacet(entity);

        // Assert
        facet.Should().NotBeNull();
        facet.Id.Should().Be(1);
        facet.Name.Should().Be("Test");
    }

    [Fact]
    public void PublicFacet_ShouldCompileAndWork()
    {
        // Arrange
        var entity = new AccessibilityTestEntity
        {
            Id = 2,
            Name = "Public Test"
        };

        // Act
        var facet = new PublicFacet(entity);

        // Assert
        facet.Should().NotBeNull();
        facet.Id.Should().Be(2);
        facet.Name.Should().Be("Public Test");
    }

    [Fact]
    public void InternalFacet_ShouldHaveInternalAccessibility()
    {
        // Arrange & Act
        var facetType = typeof(InternalFacet);

        // Assert - Type should not be public (internal types are not public)
        facetType.IsPublic.Should().BeFalse("InternalFacet should have internal accessibility");
        facetType.IsNotPublic.Should().BeTrue("InternalFacet should have internal accessibility");
    }

    [Fact]
    public void PublicFacet_ShouldHavePublicAccessibility()
    {
        // Arrange & Act
        var facetType = typeof(PublicFacet);

        // Assert
        facetType.IsPublic.Should().BeTrue("PublicFacet should have public accessibility");
    }

    [Fact]
    public void InternalFacet_ToSource_ShouldWork()
    {
        // Arrange
        var facet = new InternalFacet
        {
            Id = 3,
            Name = "ToSource Test"
        };

        // Act
        var entity = facet.ToSource();

        // Assert
        entity.Should().NotBeNull();
        entity.Id.Should().Be(3);
        entity.Name.Should().Be("ToSource Test");
    }

    [Fact]
    public void InternalFacet_Projection_ShouldWork()
    {
        // Arrange
        var entities = new[]
        {
            new AccessibilityTestEntity { Id = 1, Name = "First" },
            new AccessibilityTestEntity { Id = 2, Name = "Second" }
        }.AsQueryable();

        // Act
        var facets = entities.Select(InternalFacet.Projection).ToList();

        // Assert
        facets.Should().HaveCount(2);
        facets[0].Id.Should().Be(1);
        facets[1].Name.Should().Be("Second");
    }
}
