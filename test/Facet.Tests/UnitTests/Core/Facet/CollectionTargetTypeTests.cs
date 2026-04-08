using System.Collections.ObjectModel;

namespace Facet.Tests.UnitTests.Core.Facet;

// Test entities with Collection<T> navigation properties (EF Core style)
public class CollectionEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Collection<CollectionItemEntity> Items { get; set; } = new();
}

public class CollectionItemEntity
{
    public int Id { get; set; }
    public string Value { get; set; } = string.Empty;
}

// 1. Without override — Collection<T> should be recognized and mapped to Collection<T>
[Facet(typeof(CollectionEntity), NestedFacets = [typeof(CollectionItemFacet)])]
public partial class CollectionEntityFacet { }

[Facet(typeof(CollectionItemEntity))]
public partial class CollectionItemFacet { }

// 2. With CollectionTargetType override — Collection<T> remapped to List<T>
[Facet(typeof(CollectionEntity), NestedFacets = [typeof(CollectionItemWithOverrideFacet)], CollectionTargetType = typeof(List<>))]
public partial class CollectionEntityWithOverrideFacet { }

[Facet(typeof(CollectionItemEntity))]
public partial class CollectionItemWithOverrideFacet { }

// 3. With GenerateToSource — ToSource() should restore Collection<T>
[Facet(typeof(CollectionEntity), NestedFacets = [typeof(CollectionItemWithToSourceFacet)], CollectionTargetType = typeof(List<>), GenerateToSource = true)]
public partial class CollectionEntityWithToSourceFacet { }

[Facet(typeof(CollectionItemEntity), GenerateToSource = true)]
public partial class CollectionItemWithToSourceFacet { }

public class CollectionTargetTypeTests
{
    [Fact]
    public void Collection_ShouldBeRecognized_WithoutOverride()
    {
        // Arrange
        var entity = new CollectionEntity
        {
            Id = 1,
            Name = "Test",
            Items = new Collection<CollectionItemEntity>
            {
                new() { Id = 10, Value = "A" },
                new() { Id = 20, Value = "B" }
            }
        };

        // Act
        var facet = new CollectionEntityFacet(entity);

        // Assert
        facet.Id.Should().Be(1);
        facet.Name.Should().Be("Test");
        facet.Items.Should().NotBeNull();
        facet.Items.Should().BeOfType<Collection<CollectionItemFacet>>();
        facet.Items.Should().HaveCount(2);
        facet.Items[0].Id.Should().Be(10);
        facet.Items[0].Value.Should().Be("A");
        facet.Items[1].Id.Should().Be(20);
        facet.Items[1].Value.Should().Be("B");
    }

    [Fact]
    public void CollectionTargetType_ShouldOverride_ToList()
    {
        // Arrange
        var entity = new CollectionEntity
        {
            Id = 2,
            Name = "Override",
            Items = new Collection<CollectionItemEntity>
            {
                new() { Id = 30, Value = "C" }
            }
        };

        // Act
        var facet = new CollectionEntityWithOverrideFacet(entity);

        // Assert
        facet.Id.Should().Be(2);
        facet.Items.Should().NotBeNull();
        facet.Items.Should().BeOfType<List<CollectionItemWithOverrideFacet>>();
        facet.Items.Should().HaveCount(1);
        facet.Items[0].Id.Should().Be(30);
        facet.Items[0].Value.Should().Be("C");
    }

    [Fact]
    public void CollectionTargetType_ToSource_ShouldUseSourceWrapper()
    {
        // Arrange
        var entity = new CollectionEntity
        {
            Id = 3,
            Name = "ToSource",
            Items = new Collection<CollectionItemEntity>
            {
                new() { Id = 40, Value = "D" }
            }
        };

        // Act
        var facet = new CollectionEntityWithToSourceFacet(entity);

        // Items are List in facet (due to override)
        facet.Items.Should().BeOfType<List<CollectionItemWithToSourceFacet>>();

        // But ToSource() should restore them to Collection<T>
        var source = facet.ToSource();

        // Assert
        source.Id.Should().Be(3);
        source.Name.Should().Be("ToSource");
        source.Items.Should().NotBeNull();
        source.Items.Should().BeOfType<Collection<CollectionItemEntity>>();
        source.Items.Should().HaveCount(1);
        source.Items[0].Id.Should().Be(40);
        source.Items[0].Value.Should().Be("D");
    }
}
