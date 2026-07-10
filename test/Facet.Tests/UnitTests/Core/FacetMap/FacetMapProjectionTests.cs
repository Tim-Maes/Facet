using Facet.Extensions;
using Facet.Tests.TestModels.FacetMap;

namespace Facet.Tests.UnitTests.Core.FacetMap;

public class FacetMapProjectionTests
{
    [Fact]
    public void Projection_ShouldBeNonNull()
    {
        var projection = OrderLineMappings.OrderLineToOrderLineDtoProjection;

        projection.Should().NotBeNull();
    }

    [Fact]
    public void Projection_ShouldCompileAndExecute()
    {
        var projection = OrderLineMappings.OrderLineToOrderLineDtoProjection;
        var compiled = projection.Compile();

        var entity = new OrderLine
        {
            Id = 1,
            ProductName = "Widget",
            Quantity = 3,
            UnitPrice = 9.99m
        };

        var dto = compiled(entity);

        dto.Id.Should().Be(1);
        dto.ProductName.Should().Be("Widget");
        dto.Quantity.Should().Be(3);
        dto.UnitPrice.Should().Be(9.99m);
    }

    [Fact]
    public void Projection_ShouldWorkWithLinqQueries()
    {
        var entities = new List<OrderLine>
        {
            new() { Id = 1, ProductName = "A", Quantity = 1, UnitPrice = 10m },
            new() { Id = 2, ProductName = "B", Quantity = 2, UnitPrice = 20m },
            new() { Id = 3, ProductName = "C", Quantity = 3, UnitPrice = 30m }
        };

        var projection = OrderLineMappings.OrderLineToOrderLineDtoProjection;
        var dtos = entities.AsQueryable().Select(projection).ToList();

        dtos.Should().HaveCount(3);
        dtos[0].ProductName.Should().Be("A");
        dtos[1].Quantity.Should().Be(2);
        dtos[2].UnitPrice.Should().Be(30m);
    }

    [Fact]
    public void CustomerProjection_ShouldExist()
    {
        var projection = CustomerMappings.CustomerToCustomerDtoProjection;

        projection.Should().NotBeNull();
    }

    [Fact]
    public void SelectFacet_TwoGenericParams_ShouldWorkWithFacetMapProjection()
    {
        // This tests that SelectFacet<TSource, TTarget>() discovers FacetMap projections
        var entities = new List<OrderLine>
        {
            new() { Id = 1, ProductName = "A", Quantity = 1, UnitPrice = 10m },
            new() { Id = 2, ProductName = "B", Quantity = 2, UnitPrice = 20m }
        }.AsQueryable();

        var dtos = entities.SelectFacet<OrderLine, OrderLineDto>().ToList();

        dtos.Should().HaveCount(2);
        dtos[0].Id.Should().Be(1);
        dtos[0].ProductName.Should().Be("A");
        dtos[1].Id.Should().Be(2);
        dtos[1].Quantity.Should().Be(2);
    }

    [Fact]
    public void SelectFacet_SingleGenericParam_ShouldWorkWithFacetMapProjection()
    {
        // This tests that SelectFacet<TTarget>() discovers FacetMap projections
        // This is the pattern used by generic methods like:
        //   queryable.SelectFacet<TOut>()
        var entities = new List<OrderLine>
        {
            new() { Id = 1, ProductName = "Widget", Quantity = 3, UnitPrice = 9.99m }
        }.AsQueryable();

        var dtos = ((IQueryable)entities).SelectFacet<OrderLineDto>().ToList();

        dtos.Should().HaveCount(1);
        dtos[0].Id.Should().Be(1);
        dtos[0].ProductName.Should().Be("Widget");
        dtos[0].Quantity.Should().Be(3);
        dtos[0].UnitPrice.Should().Be(9.99m);
    }

    [Fact]
    public void SelectFacet_GenericMethod_ShouldWorkWithFacetMapProjection()
    {
        // This tests the exact pattern the user reported as broken:
        // A generic method that uses SelectFacet<TOut>() without knowing the target type at compile time
        var entities = new List<OrderLine>
        {
            new() { Id = 1, ProductName = "Test", Quantity = 5, UnitPrice = 15.50m }
        }.AsQueryable();

        var result = ProjectOrCast<OrderLine, OrderLineDto>(entities);

        result.Should().HaveCount(1);
        result.First().ProductName.Should().Be("Test");
        result.First().Quantity.Should().Be(5);
    }

    [Fact]
    public void SelectFacet_CustomerProjection_ShouldWorkViaGenericDiscovery()
    {
        var entities = new List<Customer>
        {
            new() { Id = 1, FirstName = "John", LastName = "Doe", Email = "john@test.com", IsActive = true, Balance = 100m }
        }.AsQueryable();

        var dtos = entities.SelectFacet<Customer, CustomerDto>().ToList();

        dtos.Should().HaveCount(1);
        dtos[0].Id.Should().Be(1);
        dtos[0].FirstName.Should().Be("John");
        dtos[0].LastName.Should().Be("Doe");
        dtos[0].Email.Should().Be("john@test.com");
        dtos[0].IsActive.Should().BeTrue();
        dtos[0].Balance.Should().Be(100m);
    }

    /// <summary>
    /// Simulates the generic pattern the user reported as broken:
    /// A method that accepts IQueryable&lt;T&gt; and projects to TOut using SelectFacet.
    /// </summary>
    private static IEnumerable<TOut> ProjectOrCast<T, TOut>(IQueryable<T> queryable) where TOut : class
    {
        return typeof(TOut) == typeof(T)
            ? queryable.Cast<TOut>().ToList()
            : queryable.SelectFacet<T, TOut>().ToList();
    }
}
