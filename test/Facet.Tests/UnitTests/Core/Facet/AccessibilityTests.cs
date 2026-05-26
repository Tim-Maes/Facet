namespace Facet.Tests.UnitTests.Core.Facet;

public class AccessibilityTestEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

[Facet(typeof(AccessibilityTestEntity), GenerateToSource = true)]
internal partial class InternalFacet;

[Facet(typeof(AccessibilityTestEntity), GenerateToSource = true)]
public partial class PublicFacet;

public class AccessibilityTests
{
    [Fact]
    public void InternalFacet_ShouldCompileAndWork()
    {
        var entity = new AccessibilityTestEntity
        {
            Id = 1,
            Name = "Test"
        };

        var facet = new InternalFacet(entity);

        facet.Should().NotBeNull();
        facet.Id.Should().Be(1);
        facet.Name.Should().Be("Test");
    }

    [Fact]
    public void PublicFacet_ShouldCompileAndWork()
    {
        var entity = new AccessibilityTestEntity
        {
            Id = 2,
            Name = "Public Test"
        };

        var facet = new PublicFacet(entity);

        facet.Should().NotBeNull();
        facet.Id.Should().Be(2);
        facet.Name.Should().Be("Public Test");
    }

    [Fact]
    public void InternalFacet_ShouldHaveInternalAccessibility()
    {
        var facetType = typeof(InternalFacet);

        facetType.IsPublic.Should().BeFalse("InternalFacet should have internal accessibility");
        facetType.IsNotPublic.Should().BeTrue("InternalFacet should have internal accessibility");
    }

    [Fact]
    public void PublicFacet_ShouldHavePublicAccessibility()
    {
        var facetType = typeof(PublicFacet);

        facetType.IsPublic.Should().BeTrue("PublicFacet should have public accessibility");
    }

    [Fact]
    public void InternalFacet_ToSource_ShouldWork()
    {
        var facet = new InternalFacet
        {
            Id = 3,
            Name = "ToSource Test"
        };

        var entity = facet.ToSource();

        entity.Should().NotBeNull();
        entity.Id.Should().Be(3);
        entity.Name.Should().Be("ToSource Test");
    }

    [Fact]
    public void InternalFacet_Projection_ShouldWork()
    {
        var entities = new[]
        {
            new AccessibilityTestEntity { Id = 1, Name = "First" },
            new AccessibilityTestEntity { Id = 2, Name = "Second" }
        }.AsQueryable();

        var facets = entities.Select(InternalFacet.Projection).ToList();

        facets.Should().HaveCount(2);
        facets[0].Id.Should().Be(1);
        facets[1].Name.Should().Be("Second");
    }
}
