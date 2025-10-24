namespace Facet.Tests.UnitTests.Core.Facet;

// Test entities that mimic EF Core entities with nullable foreign keys but non-nullable navigation properties
// This is a common pattern where the FK is nullable but the navigation property is not marked with ?
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor

public class DataExampleEntity
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;

    public int? StringResourceId { get; set; }
    public virtual StringResourceEntity StringResource { get; set; }  // Non-nullable but can be null at runtime

    public int? ExtendedDataId { get; set; }
    public virtual ExtendedDataEntity ExtendedData { get; set; }  // Non-nullable but can be null at runtime
}

public class StringResourceEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class ExtendedDataEntity
{
    public int Id { get; set; }
    public string Metadata { get; set; } = string.Empty;
}

#pragma warning restore CS8618

// Facet DTOs
[Facet(typeof(StringResourceEntity))]
public partial class StringResourceDto;

[Facet(typeof(ExtendedDataEntity))]
public partial class ExtendedDto;

[Facet(
    typeof(DataExampleEntity),
    NestedFacets = [typeof(StringResourceDto), typeof(ExtendedDto)])]
public partial class DataExampleFacet;

public class NullableForeignKeyTests
{
    [Fact]
    public void Projection_ShouldHandleNullNavigationProperty_WhenForeignKeyIsNull()
    {
        // Arrange
        var dataExamples = new[]
        {
            new DataExampleEntity
            {
                Id = 1,
                Code = "TEST001",
                StringResourceId = null,
                StringResource = null!,
                ExtendedDataId = null,
                ExtendedData = null!
            },
            new DataExampleEntity
            {
                Id = 2,
                Code = "TEST002",
                StringResourceId = 100,
                StringResource = new StringResourceEntity { Id = 100, Name = "Resource 1" },
                ExtendedDataId = 200,
                ExtendedData = new ExtendedDataEntity { Id = 200, Metadata = "Metadata 1" }
            }
        }.AsQueryable();

        // Act
        var dtos = dataExamples.Select(DataExampleFacet.Projection).ToList();

        // Assert
        dtos.Should().HaveCount(2);

        // First item has null navigation properties
        dtos[0].Id.Should().Be(1);
        dtos[0].Code.Should().Be("TEST001");
        dtos[0].StringResource.Should().BeNull();
        dtos[0].ExtendedData.Should().BeNull();

        // Second item has populated navigation properties
        dtos[1].Id.Should().Be(2);
        dtos[1].Code.Should().Be("TEST002");
        dtos[1].StringResource.Should().NotBeNull();
        dtos[1].StringResource!.Id.Should().Be(100);
        dtos[1].ExtendedData.Should().NotBeNull();
        dtos[1].ExtendedData!.Id.Should().Be(200);
    }

    [Fact]
    public void Constructor_ShouldHandleNullNavigationProperty_WhenForeignKeyIsNull()
    {
        // Arrange
        var dataExample = new DataExampleEntity
        {
            Id = 1,
            Code = "TEST001",
            StringResourceId = null,
            StringResource = null!,
            ExtendedDataId = null,
            ExtendedData = null!
        };

        // Act - This should not throw
        var dto = new DataExampleFacet(dataExample);

        // Assert
        dto.Id.Should().Be(1);
        dto.StringResource.Should().BeNull();
        dto.ExtendedData.Should().BeNull();
    }
}
