using Facet.Extensions;
using Facet.Tests.TestModels;

namespace Facet.Tests.UnitTests.Core.Facet;

/// <summary>
/// Tests for SelectFacet extension method with multi-source facets.
/// Verifies proper error messaging when attempting to use the single-generic SelectFacet
/// with a multi-source DTO (which has no single "Projection" property).
/// </summary>
public class MultiSourceSelectFacetTests
{
    [Fact]
    public void SelectFacet_WithMultiSourceDto_ThrowsInvalidOperationExceptionWithHelpfulMessage()
    {
        // Arrange: Create a queryable of UnitDto (a source type for the multi-source UnitDropDownDto)
        var units = new List<UnitDto>
        {
            new UnitDto { Id = 1, Name = "Unit A", ValidationResult = "Pass" },
            new UnitDto { Id = 2, Name = "Unit B", ValidationResult = "Fail" }
        }.AsQueryable();

        // Act & Assert: Attempting to use SelectFacet<UnitDropDownDto>() should throw
        // a helpful exception message because UnitDropDownDto is a multi-source facet
        var exception = Assert.Throws<InvalidOperationException>(() =>
        {
            var query = units.SelectFacet<UnitDropDownDto>();
            // Force evaluation
            _ = query.ToList();
        });

        // Assert: The error message should guide the user to use the two-generic-parameter overload
        exception.Message.Should().Contain("does not define a public static Projection property");
        exception.Message.Should().Contain("multi-source facet");
        exception.Message.Should().Contain("SelectFacet<TSource, UnitDropDownDto>()");
        exception.Message.Should().Contain("ProjectionFrom");
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
}
