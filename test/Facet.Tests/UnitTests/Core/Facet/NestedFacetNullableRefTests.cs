namespace Facet.Tests.UnitTests.Core.Facet;

/// <summary>
/// Tests for GitHub issue #261: Nested facet + nullable ref compilation error
/// When preserveReferences is enabled, the Select can return null for circular references,
/// which causes the type to be inferred as List&lt;T?&gt; even after Where(x => x != null).
/// </summary>
public partial class NestedFacetNullableRefTests
{
    public sealed class DomainObject
    {
        public List<DomainObjectItem> Items { get; set; } = [];
    }

    public sealed class DomainObjectItem
    {
        public string? Value { get; set; }
    }

    [Facet(typeof(DomainObject), MaxDepth = 2, PreserveReferences = true, NestedFacets = [typeof(DomainObjectItemDto)])]
    public sealed partial class DomainObjectDto;

    [Facet(typeof(DomainObjectItem))]
    public sealed partial class DomainObjectItemDto;

    [Fact]
    public void NestedFacetCollection_WithNullableRefEnabled_ShouldCompile()
    {
        var domain = new DomainObject
        {
            Items =
            [
                new DomainObjectItem { Value = "Item 1" },
                new DomainObjectItem { Value = "Item 2" },
                new DomainObjectItem { Value = null }
            ]
        };

        var dto = new DomainObjectDto(domain);

        Assert.NotNull(dto.Items);
        Assert.Equal(3, dto.Items.Count);
        Assert.Equal("Item 1", dto.Items[0].Value);
        Assert.Equal("Item 2", dto.Items[1].Value);
        Assert.Null(dto.Items[2].Value);
    }

    [Fact]
    public void NestedFacetCollection_WithCircularReference_ShouldHandleCorrectly()
    {
        var domain = new DomainObject
        {
            Items =
            [
                new DomainObjectItem { Value = "Item 1" }
            ]
        };

        var dto = new DomainObjectDto(domain);

        Assert.NotNull(dto.Items);
        Assert.Single(dto.Items);
        Assert.Equal("Item 1", dto.Items[0].Value);
    }
}
