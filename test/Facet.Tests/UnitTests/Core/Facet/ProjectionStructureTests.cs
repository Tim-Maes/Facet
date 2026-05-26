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
        var projection = CompanyFacet.Projection;

        projection.Should().NotBeNull();

        var body = projection.Body;
        body.Should().BeOfType<MemberInitExpression>(
            "EF Core can translate MemberInitExpression (object initializer) but not constructor calls");

        var memberInit = (MemberInitExpression)body;

        memberInit.NewExpression.Arguments.Should().BeEmpty(
            "Object initializer should use parameterless constructor");

        memberInit.Bindings.Should().NotBeEmpty("Should have property assignments");
    }

    [Fact]
    public void Projection_WithNestedFacet_ShouldAccessNavigationPropertyMembers()
    {
        var projection = CompanyFacet.Projection;

        var body = (MemberInitExpression)projection.Body;

        var addressBinding = body.Bindings
            .OfType<MemberAssignment>()
            .FirstOrDefault(b => b.Member.Name == "HeadquartersAddress");

        addressBinding.Should().NotBeNull("Should have HeadquartersAddress property assignment");

        var addressExpression = addressBinding!.Expression.ToString();
        addressExpression.Should().Contain("source.HeadquartersAddress",
            "Projection must access the navigation property for EF Core to load it");
    }

    [Fact]
    public void Projection_WithCollectionNestedFacet_ShouldAccessCollectionMembers()
    {
        var projection = OrderFacet.Projection;

        var body = (MemberInitExpression)projection.Body;

        var itemsBinding = body.Bindings
            .OfType<MemberAssignment>()
            .FirstOrDefault(b => b.Member.Name == "Items");

        itemsBinding.Should().NotBeNull("Should have Items collection property assignment");

        var itemsExpression = itemsBinding!.Expression.ToString();
        itemsExpression.Should().Contain("source.Items",
            "Projection must access the navigation collection for EF Core to load it");
        itemsExpression.Should().Contain("Select",
            "Collection projection should use Select");
    }

    [Fact]
    public void Projection_ToString_ShouldShowObjectInitializerSyntax()
    {
        var projection = CompanyFacet.Projection;
        var projectionString = projection.ToString();

        projectionString.Should().NotContain("new CompanyFacet(source)",
            "Should use object initializer, not constructor call");
    }

    [Fact]
    public void Projection_WithNullableNestedFacet_ShouldHaveNullCheck()
    {
        var projection = DataTableFacetDto.Projection;

        var body = (MemberInitExpression)projection.Body;

        var extendedDataBinding = body.Bindings
            .OfType<MemberAssignment>()
            .FirstOrDefault(b => b.Member.Name == "ExtendedData");

        extendedDataBinding.Should().NotBeNull("Should have ExtendedData property assignment");

        var expression = extendedDataBinding!.Expression;
        expression.NodeType.Should().Be(ExpressionType.Conditional,
            "Nullable nested facet should use conditional expression (ternary operator)");
    }
}
