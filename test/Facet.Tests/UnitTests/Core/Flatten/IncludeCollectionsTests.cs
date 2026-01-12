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
        // Arrange & Act
        var dtoType = typeof(ApiResponseFlatWithCollectionsDto);
        var itemsProperty = dtoType.GetProperty("Items");

        // Assert
        itemsProperty.Should().NotBeNull("Items collection should be included when IncludeCollections = true");
        itemsProperty!.PropertyType.Should().Be(typeof(List<ResponseItem>), "Collection type should be preserved");
    }

    [Fact]
    public void Flatten_WithIncludeCollections_ShouldIncludeArrayProperty()
    {
        // Arrange & Act
        var dtoType = typeof(ApiResponseFlatWithCollectionsDto);
        var tagsProperty = dtoType.GetProperty("Tags");

        // Assert
        tagsProperty.Should().NotBeNull("Tags array should be included when IncludeCollections = true");
        tagsProperty!.PropertyType.Should().Be(typeof(string[]), "Array type should be preserved");
    }

    [Fact]
    public void Flatten_WithIncludeCollections_ShouldAlsoIncludeScalarProperties()
    {
        // Arrange & Act
        var dtoType = typeof(ApiResponseFlatWithCollectionsDto);

        // Assert - Scalar properties should still be included
        dtoType.GetProperty("Id").Should().NotBeNull();
        dtoType.GetProperty("Name").Should().NotBeNull();
        dtoType.GetProperty("MetadataCreatedAt").Should().NotBeNull("Nested scalar should be flattened");
        dtoType.GetProperty("MetadataVersion").Should().NotBeNull("Nested scalar should be flattened");
    }

    [Fact]
    public void Flatten_WithoutIncludeCollections_ShouldExcludeListProperty()
    {
        // Arrange & Act
        var dtoType = typeof(ApiResponseFlatWithoutCollectionsDto);
        var itemsProperty = dtoType.GetProperty("Items");

        // Assert
        itemsProperty.Should().BeNull("Items collection should be excluded when IncludeCollections = false (default)");
    }

    [Fact]
    public void Flatten_WithoutIncludeCollections_ShouldExcludeArrayProperty()
    {
        // Arrange & Act
        var dtoType = typeof(ApiResponseFlatWithoutCollectionsDto);
        var tagsProperty = dtoType.GetProperty("Tags");

        // Assert
        tagsProperty.Should().BeNull("Tags array should be excluded when IncludeCollections = false (default)");
    }

    [Fact]
    public void Flatten_WithoutIncludeCollections_ShouldStillIncludeScalarProperties()
    {
        // Arrange & Act
        var dtoType = typeof(ApiResponseFlatWithoutCollectionsDto);

        // Assert - Scalar properties should still be included
        dtoType.GetProperty("Id").Should().NotBeNull();
        dtoType.GetProperty("Name").Should().NotBeNull();
        dtoType.GetProperty("MetadataCreatedAt").Should().NotBeNull();
        dtoType.GetProperty("MetadataVersion").Should().NotBeNull();
    }

    [Fact]
    public void Flatten_WithIncludeCollections_ShouldWorkWithConstructor()
    {
        // Arrange
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

        // Act
        var dto = new ApiResponseFlatWithCollectionsDto(source);

        // Assert
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
        // Arrange & Act
        var dtoType = typeof(EntityWithVariousCollectionsFlatDto);
        var emailsProperty = dtoType.GetProperty("Emails");

        // Assert
        emailsProperty.Should().NotBeNull();
        emailsProperty!.PropertyType.Should().Be(typeof(IEnumerable<string>));
    }

    [Fact]
    public void Flatten_WithIncludeCollections_ShouldPreserveICollection()
    {
        // Arrange & Act
        var dtoType = typeof(EntityWithVariousCollectionsFlatDto);
        var numbersProperty = dtoType.GetProperty("Numbers");

        // Assert
        numbersProperty.Should().NotBeNull();
        numbersProperty!.PropertyType.Should().Be(typeof(ICollection<int>));
    }

    [Fact]
    public void Flatten_WithIncludeCollections_ShouldPreserveIList()
    {
        // Arrange & Act
        var dtoType = typeof(EntityWithVariousCollectionsFlatDto);
        var datesProperty = dtoType.GetProperty("Dates");

        // Assert
        datesProperty.Should().NotBeNull();
        datesProperty!.PropertyType.Should().Be(typeof(IList<DateTime>));
    }

    [Fact]
    public void Flatten_WithIncludeCollections_ShouldPreserveHashSet()
    {
        // Arrange & Act
        var dtoType = typeof(EntityWithVariousCollectionsFlatDto);
        var uniqueValuesProperty = dtoType.GetProperty("UniqueValues");

        // Assert
        uniqueValuesProperty.Should().NotBeNull();
        uniqueValuesProperty!.PropertyType.Should().Be(typeof(HashSet<string>));
    }

    [Fact]
    public void Flatten_WithIncludeCollections_ShouldHandleNullCollection()
    {
        // Arrange
        var source = new ApiResponse
        {
            Id = 1,
            Name = "Test",
            Metadata = new ResponseMetadata(),
            Items = null!,
            Tags = null!
        };

        // Act
        var dto = new ApiResponseFlatWithCollectionsDto(source);

        // Assert - Collections should be null when source is null
        dto.Items.Should().BeNull();
        dto.Tags.Should().BeNull();
    }
}
