namespace Facet.Tests.UnitTests.Core.Facet;

// Test entities
public class InheritedMemberEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int State { get; set; }
    public string LocalName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

// Base class for facets
public abstract class BaseMemberFacet
{
    public int Id { get; set; }
    public int State { get; set; }
}

// Facet that inherits from base class - should not generate Id and State again
[Facet(typeof(InheritedMemberEntity), exclude: new[] { "LocalName" })]
public partial class InheritedMemberFacet : BaseMemberFacet
{
}

// Another base class scenario
public abstract class BaseWithName
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

[Facet(typeof(InheritedMemberEntity), Include = new[] { "Id", "Name", "Description" })]
public partial class InheritedIncludeFacet : BaseWithName
{
}

public class InheritedMemberTests
{
    [Fact]
    public void Constructor_ShouldNotGenerateDuplicateProperties()
    {
        // Arrange
        var entity = new InheritedMemberEntity
        {
            Id = 1,
            Name = "Test",
            State = 42,
            LocalName = "Local",
            Description = "Description"
        };

        // Act
        var facet = new InheritedMemberFacet(entity);

        // Assert
        facet.Id.Should().Be(1);
        facet.State.Should().Be(42);
        facet.Name.Should().Be("Test");
        facet.Description.Should().Be("Description");
    }

    [Fact]
    public void FacetType_ShouldNotHaveDuplicateProperties()
    {
        // Verify that the facet type doesn't have duplicate properties
        var facetType = typeof(InheritedMemberFacet);
        var declaredProperties = facetType.GetProperties(System.Reflection.BindingFlags.DeclaredOnly | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

        // Id and State should NOT be in declared properties since they're inherited
        var propertyNames = declaredProperties.Select(p => p.Name).ToList();
        propertyNames.Should().NotContain("Id");
        propertyNames.Should().NotContain("State");

        // Name and Description should be in declared properties (not in base)
        propertyNames.Should().Contain("Name");
        propertyNames.Should().Contain("Description");
    }

    [Fact]
    public void IncludeMode_ShouldNotGenerateDuplicateProperties()
    {
        // Arrange
        var entity = new InheritedMemberEntity
        {
            Id = 2,
            Name = "Test2",
            Description = "Desc2"
        };

        // Act
        var facet = new InheritedIncludeFacet(entity);

        // Assert
        facet.Id.Should().Be(2);
        facet.Name.Should().Be("Test2");
        facet.Description.Should().Be("Desc2");

        // Verify no duplicate properties
        var facetType = typeof(InheritedIncludeFacet);
        var declaredProperties = facetType.GetProperties(System.Reflection.BindingFlags.DeclaredOnly | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        var propertyNames = declaredProperties.Select(p => p.Name).ToList();

        // Id and Name should NOT be declared since they're inherited
        propertyNames.Should().NotContain("Id");
        propertyNames.Should().NotContain("Name");

        // Description should be declared
        propertyNames.Should().Contain("Description");
    }

    [Fact]
    public void Projection_ShouldWorkWithInheritedProperties()
    {
        // Arrange
        var entities = new[]
        {
            new InheritedMemberEntity { Id = 1, Name = "Test1", State = 10, Description = "Desc1" },
            new InheritedMemberEntity { Id = 2, Name = "Test2", State = 20, Description = "Desc2" }
        }.AsQueryable();

        // Act
        var facets = entities.Select(InheritedMemberFacet.Projection).ToList();

        // Assert
        facets.Should().HaveCount(2);
        facets[0].Id.Should().Be(1);
        facets[0].State.Should().Be(10);
        facets[0].Name.Should().Be("Test1");
        facets[1].Id.Should().Be(2);
        facets[1].State.Should().Be(20);
        facets[1].Name.Should().Be("Test2");
    }
}
