using Facet.Tests.TestModels;

namespace Facet.Tests.UnitTests.Core.Facet;

/// <summary>
/// Tests that static properties, const fields, and static readonly fields
/// are excluded from generated facets (issue #300).
/// </summary>
public class StaticMemberExclusionTests
{
    [Fact]
    public void Facet_ShouldNotIncludeStaticProperties()
    {
        var dtoType = typeof(StaticMemberTestDto);
        dtoType.GetProperty("AStaticProperty").Should().BeNull("static properties should not be in the facet");
    }

    [Fact]
    public void Facet_ShouldIncludeInstanceProperties()
    {
        var dtoType = typeof(StaticMemberTestDto);
        dtoType.GetProperty("AProperty").Should().NotBeNull("instance properties should be in the facet");
    }

    [Fact]
    public void Facet_ShouldMapInstancePropertiesCorrectly()
    {
        var source = new EntityWithStaticMembers { AProperty = "Hello" };
        var dto = source.ToFacet<EntityWithStaticMembers, StaticMemberTestDto>();
        dto.AProperty.Should().Be("Hello");
    }
}
