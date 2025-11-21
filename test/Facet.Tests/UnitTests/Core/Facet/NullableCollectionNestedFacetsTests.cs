namespace Facet.Tests.UnitTests.Core.Facet;

public class StringLookup
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

public class StringIdentifier
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<StringLookup> StringLookups { get; set; } = new();
}

// Facet DTOs
[Facet(typeof(StringLookup), NullableProperties = true, GenerateToSource = true)]
public partial class StringLookupDto;

[Facet(typeof(StringIdentifier),
    Include = [nameof(StringIdentifier.Id), nameof(StringIdentifier.Name), nameof(StringIdentifier.StringLookups)],
    NestedFacets = [typeof(StringLookupDto)],
    NullableProperties = true,
    GenerateToSource = true)]
public partial class StringIdentifierLookupDto;

public class NullableCollectionNestedFacetsTests
{
    [Fact]
    public void Constructor_ShouldHandleCollectionNestedFacet_WithNullableProperties()
    {
        // Arrange
        var stringIdentifier = new StringIdentifier
        {
            Id = 1,
            Name = "Test Identifier",
            StringLookups = new List<StringLookup>
            {
                new() { Id = 10, Name = "Lookup1", Value = "Value1" },
                new() { Id = 20, Name = "Lookup2", Value = "Value2" }
            }
        };

        // Act
        var dto = new StringIdentifierLookupDto(stringIdentifier);

        // Assert
        dto.Should().NotBeNull();
        dto.Id.Should().Be(1);
        dto.Name.Should().Be("Test Identifier");
        dto.StringLookups.Should().NotBeNull();
        dto.StringLookups.Should().HaveCount(2);
    }

    [Fact]
    public void Projection_ShouldHandleCollectionNestedFacet_WithNullableProperties()
    {
        // Arrange
        var identifiers = new[]
        {
            new StringIdentifier
            {
                Id = 1,
                Name = "Identifier 1",
                StringLookups = new List<StringLookup>
                {
                    new() { Id = 10, Name = "Lookup1", Value = "Value1" }
                }
            }
        }.AsQueryable();

        // Act
        var dtos = identifiers.Select(StringIdentifierLookupDto.Projection).ToList();

        // Assert
        dtos.Should().HaveCount(1);
        dtos[0].Id.Should().Be(1);
        dtos[0].StringLookups.Should().NotBeNull();
        dtos[0].StringLookups!.Should().HaveCount(1);
    }

    [Fact]
    public void ToSource_ShouldHandleCollectionNestedFacet_WithNullableProperties()
    {
        // Arrange
        var dto = new StringIdentifierLookupDto
        {
            Id = 1,
            Name = "Test Identifier",
            StringLookups = new List<StringLookupDto>
            {
                new() { Id = 10, Name = "Lookup1", Value = "Value1" },
                new() { Id = 20, Name = "Lookup2", Value = "Value2" }
            }
        };

        // Act
        var entity = dto.ToSource();

        // Assert
        entity.Should().NotBeNull();
        entity.Id.Should().Be(1);
        entity.Name.Should().Be("Test Identifier");
        entity.StringLookups.Should().HaveCount(2);
    }
}
