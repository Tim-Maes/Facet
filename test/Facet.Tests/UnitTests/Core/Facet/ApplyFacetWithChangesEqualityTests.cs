using Facet.Tests.TestModels;

namespace Facet.Tests.UnitTests.Core.Facet;

/// <summary>
/// Tests that ApplyFacetWithChanges uses element-wise equality for collections
/// instead of reference equality (issue #305).
/// </summary>
public class ApplyFacetWithChangesEqualityTests
{
    [Fact]
    public void ApplyFacetWithChanges_WhenNonEmptyTagsAreValueEqual_ShouldNotReportChanges()
    {
        var seed = new TaggedItemFacet { Id = "1", Name = "Widget", Tags = ["a", "b"] };
        var item = seed.ToSource();

        // Identical values but different collection instances
        var identical = new TaggedItemFacet { Id = "1", Name = "Widget", Tags = ["a", "b"] };

        var result = item.ApplyFacetWithChanges(identical);

        result.HasChanges.Should().BeFalse(
            because: "collections with identical elements should not be considered changed");
    }

    [Fact]
    public void ApplyFacetWithChanges_WhenEmptyTagsUsed_ShouldNotReportChanges()
    {
        var seed = new TaggedItemFacet { Id = "1", Name = "Widget", Tags = [] };
        var item = seed.ToSource();

        var identical = new TaggedItemFacet { Id = "1", Name = "Widget", Tags = [] };

        var result = item.ApplyFacetWithChanges(identical);

        result.HasChanges.Should().BeFalse(
            because: "empty collections should be equal regardless of instance identity");
    }

    [Fact]
    public void ApplyFacetWithChanges_WhenTagsActuallyDiffer_ShouldReportChanges()
    {
        var seed = new TaggedItemFacet { Id = "1", Name = "Widget", Tags = ["a", "b"] };
        var item = seed.ToSource();

        var modified = new TaggedItemFacet { Id = "1", Name = "Widget", Tags = ["a", "c"] };

        var result = item.ApplyFacetWithChanges(modified);

        result.HasChanges.Should().BeTrue();
        result.ChangedProperties.Should().Contain("Tags");
    }

    [Fact]
    public void ApplyFacetWithChanges_WhenTagsLengthDiffers_ShouldReportChanges()
    {
        var seed = new TaggedItemFacet { Id = "1", Name = "Widget", Tags = ["a", "b"] };
        var item = seed.ToSource();

        var modified = new TaggedItemFacet { Id = "1", Name = "Widget", Tags = ["a"] };

        var result = item.ApplyFacetWithChanges(modified);

        result.HasChanges.Should().BeTrue();
        result.ChangedProperties.Should().Contain("Tags");
    }

    [Fact]
    public void ApplyFacet_WhenCollectionsAreEqual_ShouldNotOverwrite()
    {
        var seed = new TaggedItemFacet { Id = "1", Name = "Widget", Tags = ["a", "b"] };
        var item = seed.ToSource();
        var originalTags = item.Tags; // Keep reference to original

        var identical = new TaggedItemFacet { Id = "1", Name = "Widget", Tags = ["a", "b"] };

        item.ApplyFacet(identical);

        // The original collection should be preserved (not replaced) since values are equal
        item.Tags.Should().BeSameAs(originalTags);
    }
}
