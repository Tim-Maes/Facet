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
        var units = new List<UnitDto>
        {
            new UnitDto { Id = 1, Name = "Unit A", ValidationResult = "Pass" },
            new UnitDto { Id = 2, Name = "Unit B", ValidationResult = "Fail" }
        }.AsQueryable();

        var result = units.SelectFacet<UnitDropDownDto>().ToList();

        result.Should().HaveCount(2);
        result[0].Name.Should().Be("Unit A");
        result[0].ValidationResult.Should().Be("Pass");
        result[1].Name.Should().Be("Unit B");
        result[1].ValidationResult.Should().Be("Fail");
    }

    [Fact]
    public void SelectFacet_WithMultiSourceDto_TwoGenericParameterOverload_Works()
    {
        var units = new List<UnitDto>
        {
            new UnitDto { Id = 1, Name = "Unit A", ValidationResult = "Pass" },
            new UnitDto { Id = 2, Name = "Unit B", ValidationResult = "Fail" }
        }.AsQueryable();

        var query = units.SelectFacet<UnitDto, UnitDropDownDto>();
        var result = query.ToList();

        result.Should().HaveCount(2);
        result[0].Name.Should().Be("Unit A");
        result[0].ValidationResult.Should().Be("Pass");
        result[1].Name.Should().Be("Unit B");
        result[1].ValidationResult.Should().Be("Fail");
    }

    [Fact]
    public void SelectFacet_WithMultiSourceDto_UsingSourceSpecificProjection_Works()
    {
        var entities = new List<UnitEntity>
        {
            new UnitEntity { Id = 10, Name = "Entity X", ValidationResult = "Valid" }
        }.AsQueryable();

        var result = entities.Select(UnitDropDownDto.ProjectionFromUnitEntity).ToList();

        result.Should().HaveCount(1);
        result[0].Name.Should().Be("Entity X");
        result[0].ValidationResult.Should().Be("Valid");
    }

    [Fact]
    public void SelectFacet_WithMultiSourceDto_SecondSourceType_AutoDetects()
    {
        var entities = new List<UnitEntity>
        {
            new UnitEntity { Id = 10, Name = "Entity Y", ValidationResult = "Valid" }
        }.AsQueryable();

        var result = entities.SelectFacet<UnitDropDownDto>().ToList();

        result.Should().HaveCount(1);
        result[0].Name.Should().Be("Entity Y");
        result[0].ValidationResult.Should().Be("Valid");
    }

    [Fact]
    public void SingleSourceFacet_HasProjectionFromAlias()
    {
        var users = new List<User>
        {
            new User { Id = 1, FirstName = "Alice", LastName = "Smith", Email = "alice@test.com", Password = "secret", CreatedAt = System.DateTime.Now }
        }.AsQueryable();

        var result = users.Select(UserDto.ProjectionFromUser).ToList();

        result.Should().HaveCount(1);
        result[0].Email.Should().Be("alice@test.com");
    }

    [Fact]
    public void SingleSourceFacet_ProjectionAndProjectionFromX_ReturnSameResult()
    {
        var users = new List<User>
        {
            new User { Id = 1, FirstName = "Bob", LastName = "Jones", Email = "bob@test.com", Password = "pwd", CreatedAt = System.DateTime.Now }
        }.AsQueryable();

        var viaProjection = users.Select(UserDto.Projection).ToList();
        var viaProjectionFrom = users.Select(UserDto.ProjectionFromUser).ToList();

        viaProjection.Should().HaveCount(1);
        viaProjectionFrom.Should().HaveCount(1);
        viaProjection[0].Email.Should().Be(viaProjectionFrom[0].Email);
        viaProjection[0].Id.Should().Be(viaProjectionFrom[0].Id);
    }
}
