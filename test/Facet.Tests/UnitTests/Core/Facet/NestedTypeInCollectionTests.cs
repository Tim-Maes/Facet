using System.Collections.Immutable;
using Facet.Tests.TestModels.NestedTypeInCollection;

namespace Facet.Tests.UnitTests.Core.Facet;

public class NestedTypeInCollectionTests
{
    [Fact]
    public void Facet_ShouldCompile_WhenPropertyIsImmutableListOfNestedType()
    {
        var source = new Bar
        {
            J = ImmutableList<FooBar.BarFoo>.Empty
        };

        var dto = new BarDto(source);

        dto.Should().NotBeNull();
        dto.J.Should().BeEmpty();
    }

    [Fact]
    public void Facet_ShouldMapNestedFacet_WhenParentHasImmutableListOfNestedType()
    {
        var source = new FooBar
        {
            Bar = new Bar
            {
                J = ImmutableList.Create(new FooBar.BarFoo { L = "test" })
            }
        };

        var dto = new FooBarDto(source);

        dto.Should().NotBeNull();
        dto.Bar.Should().NotBeNull();
        dto.Bar.J.Should().HaveCount(1);
    }
}
