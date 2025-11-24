using Facet.Tests.TestModels;

namespace Facet.Tests.UnitTests.Core.Facet;

public class FlattenToTests
{
    [Fact]
    public void FlattenTo_ShouldCombineParentAndCollectionItemProperties()
    {
        // Arrange
        var data = new DataEntity
        {
            Id = 1,
            Name = "Parent Data",
            Description = "Parent Description",
            Extended = new List<ExtendedEntity>
            {
                new() { Id = 10, Name = "Extended 1", DataValue = 100 },
                new() { Id = 20, Name = "Extended 2", DataValue = 200 },
                new() { Id = 30, Name = "Extended 3", DataValue = 300 }
            }
        };

        var facet = new DataFacet(data);

        // Act
        var flattened = facet.FlattenTo();

        // Assert
        Assert.NotNull(flattened);
        Assert.Equal(3, flattened.Count);

        // First flattened row
        Assert.Equal(1, flattened[0].Id);                    // From parent
        Assert.Equal("Parent Data", flattened[0].Name);      // From parent
        Assert.Equal("Parent Description", flattened[0].Description); // From parent
        Assert.Equal(100, flattened[0].DataValue);           // From collection item
        Assert.Equal("Extended 1", flattened[0].ExtendedName); // From collection item

        // Second flattened row
        Assert.Equal(1, flattened[1].Id);
        Assert.Equal("Parent Data", flattened[1].Name);
        Assert.Equal("Parent Description", flattened[1].Description);
        Assert.Equal(200, flattened[1].DataValue);
        Assert.Equal("Extended 2", flattened[1].ExtendedName);

        // Third flattened row
        Assert.Equal(1, flattened[2].Id);
        Assert.Equal("Parent Data", flattened[2].Name);
        Assert.Equal("Parent Description", flattened[2].Description);
        Assert.Equal(300, flattened[2].DataValue);
        Assert.Equal("Extended 3", flattened[2].ExtendedName);
    }

    [Fact]
    public void FlattenTo_WithNullCollection_ShouldReturnEmptyList()
    {
        // Arrange
        var data = new DataEntity
        {
            Id = 1,
            Name = "Parent Data",
            Description = "Parent Description",
            Extended = null!
        };

        var facet = new DataFacet(data);

        // Act
        var flattened = facet.FlattenTo();

        // Assert
        Assert.NotNull(flattened);
        Assert.Empty(flattened);
    }

    [Fact]
    public void FlattenTo_WithEmptyCollection_ShouldReturnEmptyList()
    {
        // Arrange
        var data = new DataEntity
        {
            Id = 1,
            Name = "Parent Data",
            Description = "Parent Description",
            Extended = new List<ExtendedEntity>()
        };

        var facet = new DataFacet(data);

        // Act
        var flattened = facet.FlattenTo();

        // Assert
        Assert.NotNull(flattened);
        Assert.Empty(flattened);
    }

    [Fact]
    public void FlattenTo_WithSingleCollectionItem_ShouldReturnSingleRow()
    {
        // Arrange
        var data = new DataEntity
        {
            Id = 42,
            Name = "Single Parent",
            Description = "Single Description",
            Extended = new List<ExtendedEntity>
            {
                new() { Id = 99, Name = "Single Extended", DataValue = 777 }
            }
        };

        var facet = new DataFacet(data);

        // Act
        var flattened = facet.FlattenTo();

        // Assert
        Assert.NotNull(flattened);
        Assert.Single(flattened);
        Assert.Equal(42, flattened[0].Id);
        Assert.Equal("Single Parent", flattened[0].Name);
        Assert.Equal("Single Description", flattened[0].Description);
        Assert.Equal(777, flattened[0].DataValue);
        Assert.Equal("Single Extended", flattened[0].ExtendedName);
    }

    [Fact]
    public void FlattenTo_ShouldReplicateParentDataForEachCollectionItem()
    {
        // Arrange
        var data = new DataEntity
        {
            Id = 5,
            Name = "Shared Parent",
            Description = "Shared Description",
            Extended = new List<ExtendedEntity>
            {
                new() { Id = 1, Name = "Item 1", DataValue = 10 },
                new() { Id = 2, Name = "Item 2", DataValue = 20 }
            }
        };

        var facet = new DataFacet(data);

        // Act
        var flattened = facet.FlattenTo();

        // Assert
        Assert.Equal(2, flattened.Count);
        
        // Both rows should have the same parent data
        foreach (var row in flattened)
        {
            Assert.Equal(5, row.Id);
            Assert.Equal("Shared Parent", row.Name);
            Assert.Equal("Shared Description", row.Description);
        }

        // But different collection item data
        Assert.Equal(10, flattened[0].DataValue);
        Assert.Equal(20, flattened[1].DataValue);
    }
}
