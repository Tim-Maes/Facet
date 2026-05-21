using Facet.Extensions;
using Facet.Tests.TestModels;

namespace Facet.Tests.UnitTests.Core.Facet;

/// <summary>
/// Tests for SelectFacet extension method with multi-source facets.
/// Verifies that SelectFacet auto-detects the source type and finds the matching
/// ProjectionFrom{SourceName} property for multi-source facets.
/// </summary>
public class MultiSourceSelectFacetTests
{
    [Fact]
    public void SelectFacet_WithMultiSourceDto_AutoDetectsSourceType()
    {
        // Arrange: Create a queryable of UnitDto (a source type for the multi-source UnitDropDownDto)
        var units = new List<UnitDto>
        {
            new UnitDto { Id = 1, Name = "Unit A", ValidationResult = "Pass" },
            new UnitDto { Id = 2, Name = "Unit B", ValidationResult = "Fail" }
        }.AsQueryable();

        // Act: The single-generic SelectFacet now detects source type and uses ProjectionFromUnitDto
        var result = units.SelectFacet<UnitDropDownDto>().ToList();

        // Assert
        result.Should().HaveCount(2);
        result[0].Name.Should().Be("Unit A");
        result[0].ValidationResult.Should().Be("Pass");
        result[1].Name.Should().Be("Unit B");
        result[1].ValidationResult.Should().Be("Fail");
    }

    [Fact]
    public void SelectFacet_WithMultiSourceDto_TwoGenericParameterOverload_Works()
    {
        // Arrange: Create a queryable of UnitDto
        var units = new List<UnitDto>
        {
            new UnitDto { Id = 1, Name = "Unit A", ValidationResult = "Pass" },
            new UnitDto { Id = 2, Name = "Unit B", ValidationResult = "Fail" }
        }.AsQueryable();

        // Act: Use the two-generic-parameter overload (strongly typed)
        var query = units.SelectFacet<UnitDto, UnitDropDownDto>();
        var result = query.ToList();

        // Assert
        result.Should().HaveCount(2);
        result[0].Name.Should().Be("Unit A");
        result[0].ValidationResult.Should().Be("Pass");
        result[1].Name.Should().Be("Unit B");
        result[1].ValidationResult.Should().Be("Fail");
    }

    [Fact]
    public void SelectFacet_WithMultiSourceDto_UsingSourceSpecificProjection_Works()
    {
        // Arrange: Create a queryable of UnitEntity (the other source type for UnitDropDownDto)
        var entities = new List<UnitEntity>
        {
            new UnitEntity { Id = 10, Name = "Entity X", ValidationResult = "Valid" }
        }.AsQueryable();

        // Act: Use the source-specific projection property directly
        var result = entities.Select(UnitDropDownDto.ProjectionFromUnitEntity).ToList();

        // Assert
        result.Should().HaveCount(1);
        result[0].Name.Should().Be("Entity X");
        result[0].ValidationResult.Should().Be("Valid");
    }

    [Fact]
    public void SelectFacet_WithMultiSourceDto_SecondSourceType_AutoDetects()
    {
        // Arrange: Create a queryable of UnitEntity (another source for UnitDropDownDto)
        var entities = new List<UnitEntity>
        {
            new UnitEntity { Id = 10, Name = "Entity Y", ValidationResult = "Valid" }
        }.AsQueryable();

        // Act: The single-generic SelectFacet detects UnitEntity and uses ProjectionFromUnitEntity
        var result = entities.SelectFacet<UnitDropDownDto>().ToList();

        // Assert
        result.Should().HaveCount(1);
        result[0].Name.Should().Be("Entity Y");
        result[0].ValidationResult.Should().Be("Valid");
    }

    [Fact]
    public void SingleSourceFacet_HasProjectionFromAlias()
    {
        // Arrange: Single-source facets now also generate ProjectionFrom{SourceName}
        var users = new List<User>
        {
            new User { Id = 1, FirstName = "Alice", LastName = "Smith", Email = "alice@test.com", Password = "secret", CreatedAt = System.DateTime.Now }
        }.AsQueryable();

        // Act: Use the source-specific alias (ProjectionFromUser)
        var result = users.Select(UserDto.ProjectionFromUser).ToList();

        // Assert
        result.Should().HaveCount(1);
        result[0].Email.Should().Be("alice@test.com");
    }

    [Fact]
    public void SingleSourceFacet_ProjectionAndProjectionFromX_ReturnSameResult()
    {
        // Arrange
        var users = new List<User>
        {
            new User { Id = 1, FirstName = "Bob", LastName = "Jones", Email = "bob@test.com", Password = "pwd", CreatedAt = System.DateTime.Now }
        }.AsQueryable();

        // Act: Both should produce identical results
        var viaProjection = users.Select(UserDto.Projection).ToList();
        var viaProjectionFrom = users.Select(UserDto.ProjectionFromUser).ToList();

        // Assert
        viaProjection.Should().HaveCount(1);
        viaProjectionFrom.Should().HaveCount(1);
        viaProjection[0].Email.Should().Be(viaProjectionFrom[0].Email);
        viaProjection[0].Id.Should().Be(viaProjectionFrom[0].Id);
    }
}
