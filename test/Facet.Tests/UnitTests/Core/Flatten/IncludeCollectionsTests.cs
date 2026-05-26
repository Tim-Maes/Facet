using Facet.Tests.TestModels;

namespace Facet.Tests.UnitTests.Core.Flatten;

/// <summary>
/// Tests for the IncludeCollections feature in the Flatten attribute (GitHub issue #242).
/// This allows collection properties to be included as-is in flattened objects without
/// flattening their contents.
/// </summary>
public class IncludeCollectionsTests
{
    [Fact]
    public void Flatten_WithIncludeCollections_ShouldIncludeListProperty()
    {
        var dtoType = typeof(ApiResponseFlatWithCollectionsDto);
        var itemsProperty = dtoType.GetProperty("Items");

        itemsProperty.Should().NotBeNull("Items collection should be included when IncludeCollections = true");
        itemsProperty!.PropertyType.Should().Be(typeof(List<ResponseItem>), "Collection type should be preserved");
    }

    [Fact]
    public void Flatten_WithIncludeCollections_ShouldIncludeArrayProperty()
    {
        var dtoType = typeof(ApiResponseFlatWithCollectionsDto);
        var tagsProperty = dtoType.GetProperty("Tags");

        tagsProperty.Should().NotBeNull("Tags array should be included when IncludeCollections = true");
        tagsProperty!.PropertyType.Should().Be(typeof(string[]), "Array type should be preserved");
    }

    [Fact]
    public void Flatten_WithIncludeCollections_ShouldAlsoIncludeScalarProperties()
    {
        var dtoType = typeof(ApiResponseFlatWithCollectionsDto);

        dtoType.GetProperty("Id").Should().NotBeNull();
        dtoType.GetProperty("Name").Should().NotBeNull();
        dtoType.GetProperty("MetadataCreatedAt").Should().NotBeNull("Nested scalar should be flattened");
        dtoType.GetProperty("MetadataVersion").Should().NotBeNull("Nested scalar should be flattened");
    }

    [Fact]
    public void Flatten_WithoutIncludeCollections_ShouldExcludeListProperty()
    {
        var dtoType = typeof(ApiResponseFlatWithoutCollectionsDto);
        var itemsProperty = dtoType.GetProperty("Items");

        itemsProperty.Should().BeNull("Items collection should be excluded when IncludeCollections = false (default)");
    }

    [Fact]
    public void Flatten_WithoutIncludeCollections_ShouldExcludeArrayProperty()
    {
        var dtoType = typeof(ApiResponseFlatWithoutCollectionsDto);
        var tagsProperty = dtoType.GetProperty("Tags");

        tagsProperty.Should().BeNull("Tags array should be excluded when IncludeCollections = false (default)");
    }

    [Fact]
    public void Flatten_WithoutIncludeCollections_ShouldStillIncludeScalarProperties()
    {
        var dtoType = typeof(ApiResponseFlatWithoutCollectionsDto);

        dtoType.GetProperty("Id").Should().NotBeNull();
        dtoType.GetProperty("Name").Should().NotBeNull();
        dtoType.GetProperty("MetadataCreatedAt").Should().NotBeNull();
        dtoType.GetProperty("MetadataVersion").Should().NotBeNull();
    }

    [Fact]
    public void Flatten_WithIncludeCollections_ShouldWorkWithConstructor()
    {
        var source = new ApiResponse
        {
            Id = 1,
            Name = "Test Response",
            Metadata = new ResponseMetadata
            {
                CreatedAt = new DateTime(2024, 1, 15),
                Version = "1.0"
            },
            Items = new List<ResponseItem>
            {
                new ResponseItem { ItemId = 101, ItemName = "Item 1", Price = 9.99m },
                new ResponseItem { ItemId = 102, ItemName = "Item 2", Price = 19.99m }
            },
            Tags = new[] { "tag1", "tag2", "tag3" }
        };

        var dto = new ApiResponseFlatWithCollectionsDto(source);

        dto.Id.Should().Be(1);
        dto.Name.Should().Be("Test Response");
        dto.MetadataCreatedAt.Should().Be(new DateTime(2024, 1, 15));
        dto.MetadataVersion.Should().Be("1.0");
        dto.Items.Should().HaveCount(2);
        dto.Items[0].ItemId.Should().Be(101);
        dto.Items[1].ItemName.Should().Be("Item 2");
        dto.Tags.Should().BeEquivalentTo(new[] { "tag1", "tag2", "tag3" });
    }

    [Fact]
    public void Flatten_WithIncludeCollections_ShouldPreserveIEnumerable()
    {
        var dtoType = typeof(EntityWithVariousCollectionsFlatDto);
        var emailsProperty = dtoType.GetProperty("Emails");

        emailsProperty.Should().NotBeNull();
        emailsProperty!.PropertyType.Should().Be(typeof(IEnumerable<string>));
    }

    [Fact]
    public void Flatten_WithIncludeCollections_ShouldPreserveICollection()
    {
        var dtoType = typeof(EntityWithVariousCollectionsFlatDto);
        var numbersProperty = dtoType.GetProperty("Numbers");

        numbersProperty.Should().NotBeNull();
        numbersProperty!.PropertyType.Should().Be(typeof(ICollection<int>));
    }

    [Fact]
    public void Flatten_WithIncludeCollections_ShouldPreserveIList()
    {
        var dtoType = typeof(EntityWithVariousCollectionsFlatDto);
        var datesProperty = dtoType.GetProperty("Dates");

        datesProperty.Should().NotBeNull();
        datesProperty!.PropertyType.Should().Be(typeof(IList<DateTime>));
    }

    [Fact]
    public void Flatten_WithIncludeCollections_ShouldPreserveHashSet()
    {
        var dtoType = typeof(EntityWithVariousCollectionsFlatDto);
        var uniqueValuesProperty = dtoType.GetProperty("UniqueValues");

        uniqueValuesProperty.Should().NotBeNull();
        uniqueValuesProperty!.PropertyType.Should().Be(typeof(HashSet<string>));
    }

    [Fact]
    public void Flatten_WithIncludeCollections_ShouldHandleNullCollection()
    {
        var source = new ApiResponse
        {
            Id = 1,
            Name = "Test",
            Metadata = new ResponseMetadata(),
            Items = null!,
            Tags = null!
        };

        var dto = new ApiResponseFlatWithCollectionsDto(source);

        dto.Items.Should().BeNull();
        dto.Tags.Should().BeNull();
    }
}
