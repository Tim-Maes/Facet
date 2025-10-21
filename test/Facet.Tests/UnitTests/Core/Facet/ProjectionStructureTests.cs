using System.Linq.Expressions;
using Facet.Tests.TestModels;

namespace Facet.Tests.UnitTests.Core.Facet;

/// <summary>
/// Tests to verify the structure of generated projection expressions,
/// particularly for EF Core compatibility with nested facets.
/// </summary>
public class ProjectionStructureTests
{
    [Fact]
    public void Projection_ShouldUseObjectInitializer_NotConstructor()
    {
        // Arrange & Act
        var projection = CompanyFacet.Projection;

        // Assert
        projection.Should().NotBeNull();

        // The projection should be a lambda expression
        var body = projection.Body;
        body.Should().BeOfType<MemberInitExpression>(
            "EF Core can translate MemberInitExpression (object initializer) but not constructor calls");

        var memberInit = (MemberInitExpression)body;

        // Verify it's initializing properties, not calling a constructor with parameters
        memberInit.NewExpression.Arguments.Should().BeEmpty(
            "Object initializer should use parameterless constructor");

        // Verify it has member bindings for properties
        memberInit.Bindings.Should().NotBeEmpty("Should have property assignments");
    }

    [Fact]
    public void Projection_WithNestedFacet_ShouldAccessNavigationPropertyMembers()
    {
        // Arrange & Act
        var projection = CompanyFacet.Projection;

        // Assert
        var body = (MemberInitExpression)projection.Body;

        // Find the HeadquartersAddress binding
        var addressBinding = body.Bindings
            .OfType<MemberAssignment>()
            .FirstOrDefault(b => b.Member.Name == "HeadquartersAddress");

        addressBinding.Should().NotBeNull("Should have HeadquartersAddress property assignment");

        // The expression should access source.HeadquartersAddress
        // This is critical for EF Core to know to load the navigation property
        var addressExpression = addressBinding!.Expression.ToString();
        addressExpression.Should().Contain("source.HeadquartersAddress",
            "Projection must access the navigation property for EF Core to load it");
    }

    [Fact]
    public void Projection_WithCollectionNestedFacet_ShouldAccessCollectionMembers()
    {
        // Arrange & Act
        var projection = OrderFacet.Projection;

        // Assert
        var body = (MemberInitExpression)projection.Body;

        // Find the Items collection binding
        var itemsBinding = body.Bindings
            .OfType<MemberAssignment>()
            .FirstOrDefault(b => b.Member.Name == "Items");

        itemsBinding.Should().NotBeNull("Should have Items collection property assignment");

        // The expression should use Select on source.Items
        var itemsExpression = itemsBinding!.Expression.ToString();
        itemsExpression.Should().Contain("source.Items",
            "Projection must access the navigation collection for EF Core to load it");
        itemsExpression.Should().Contain("Select",
            "Collection projection should use Select");
    }

    [Fact]
    public void Projection_ToString_ShouldShowObjectInitializerSyntax()
    {
        // Arrange & Act
        var projection = CompanyFacet.Projection;
        var projectionString = projection.ToString();

        // Assert
        // Should see "source => new CompanyFacet { ... }"
        // NOT "source => new CompanyFacet(source)"
        projectionString.Should().NotContain("new CompanyFacet(source)",
            "Should use object initializer, not constructor call");
    }

    [Fact]
    public void Projection_WithNullableNestedFacet_ShouldHaveNullCheck()
    {
        // Arrange & Act
        var projection = DataTableFacetDto.Projection;

        // Assert
        var body = (MemberInitExpression)projection.Body;

        // Find the ExtendedData binding (nullable nested facet)
        var extendedDataBinding = body.Bindings
            .OfType<MemberAssignment>()
            .FirstOrDefault(b => b.Member.Name == "ExtendedData");

        extendedDataBinding.Should().NotBeNull("Should have ExtendedData property assignment");

        // The expression should have a conditional for null check
        var expression = extendedDataBinding!.Expression;
        expression.NodeType.Should().Be(ExpressionType.Conditional,
            "Nullable nested facet should use conditional expression (ternary operator)");
    }
}
