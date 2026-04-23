using Facet.Mapping;

namespace Facet.Tests.UnitTests.Core.Facet;

public interface ICustomerContract352
{
    int Id { get; set; }
    string Name { get; set; }
    string Email { get; set; }
}

public class CustomerEntity352 : ICustomerContract352
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

[Facet(typeof(ICustomerContract352),
    Configuration = typeof(CustomerContractFacet352Config),
    Include = new[] { nameof(ICustomerContract352.Id), nameof(ICustomerContract352.Name), nameof(ICustomerContract352.Email) })]
[Facet(typeof(CustomerEntity352),
    Include = new[] { nameof(CustomerEntity352.Id), nameof(CustomerEntity352.Name), nameof(CustomerEntity352.Email) })]
public partial class CustomerFacet352
{
}

public class CustomerContractFacet352Config
    : IFacetProjectionMapConfiguration<ICustomerContract352, CustomerFacet352>
{
    public static void ConfigureProjection(
        IFacetProjectionBuilder<ICustomerContract352, CustomerFacet352> builder)
    {
        builder.Map(d => d.Name, s => s.Name + " (contract)");
    }
}

public class DispatchEntity352
{
    public int Id { get; set; }
    public ICustomerContract352? Customer { get; set; }
}

[Facet(typeof(DispatchEntity352),
    Include = new[] { nameof(DispatchEntity352.Id), nameof(DispatchEntity352.Customer) },
    NestedFacets = new[] { typeof(CustomerFacet352) })]
public partial class DispatchDto352
{
}

[Facet(typeof(DispatchEntity352),
    Configuration = typeof(DispatchDto352LazyConfig),
    Include = new[] { nameof(DispatchEntity352.Id), nameof(DispatchEntity352.Customer) },
    NestedFacets = new[] { typeof(CustomerFacet352) })]
public partial class DispatchDto352Lazy
{
}

public class DispatchDto352LazyConfig
    : IFacetProjectionMapConfiguration<DispatchEntity352, DispatchDto352Lazy>
{
    public static void ConfigureProjection(
        IFacetProjectionBuilder<DispatchEntity352, DispatchDto352Lazy> builder)
    {
        builder.Map(d => d.Id, s => s.Id + 1000);
    }
}

public class BaseOrderEntity352
{
    public int Id { get; set; }
    public DispatchEntity352? Dispatch { get; set; }
}

public class DerivedOrderEntity352 : BaseOrderEntity352
{
    public string OrderNumber { get; set; } = string.Empty;
}

[Facet(typeof(BaseOrderEntity352),
    Include = new[] { nameof(BaseOrderEntity352.Id), nameof(BaseOrderEntity352.Dispatch) },
    NestedFacets = new[] { typeof(DispatchDto352) })]
public partial class BaseOrderDto352
{
}

[Facet(typeof(DerivedOrderEntity352),
    Include = new[] { nameof(DerivedOrderEntity352.OrderNumber) })]
public partial class DerivedOrderDto352 : BaseOrderDto352
{
}

public class ProjectionAbstractionsAndMultiSourceRegressionTests
{
    [Fact]
    public void Projection_InterfaceNestedFacet_InlinePath_ShouldUseInterfaceSpecificProjection()
    {
        var source = new DispatchEntity352
        {
            Id = 1,
            Customer = new CustomerEntity352 { Id = 10, Name = "Alice", Email = "alice@example.com" }
        };

        var dto = DispatchDto352.Projection.Compile()(source);

        dto.Id.Should().Be(1);
        dto.Customer.Should().NotBeNull();
        dto.Customer!.Name.Should().Be("Alice (contract)");
        dto.Customer.Email.Should().Be("alice@example.com");
    }

    [Fact]
    public void Projection_InterfaceNestedFacet_WithLazyParentProjection_ShouldKeepNestedConfiguration()
    {
        var source = new DispatchEntity352
        {
            Id = 7,
            Customer = new CustomerEntity352 { Id = 77, Name = "Bob", Email = "bob@example.com" }
        };

        var dto = DispatchDto352Lazy.Projection.Compile()(source);

        dto.Id.Should().Be(1007);
        dto.Customer.Should().NotBeNull();
        dto.Customer!.Name.Should().Be("Bob (contract)");
    }

    [Fact]
    public void Projection_InheritedFacet_WithAbstractNestedFacet_ShouldProjectNestedConfiguration()
    {
        var source = new DerivedOrderEntity352
        {
            Id = 2,
            OrderNumber = "ORD-352",
            Dispatch = new DispatchEntity352
            {
                Id = 20,
                Customer = new CustomerEntity352 { Id = 30, Name = "Carol", Email = "carol@example.com" }
            }
        };

        var dto = DerivedOrderDto352.Projection.Compile()(source);

        dto.Id.Should().Be(2);
        dto.OrderNumber.Should().Be("ORD-352");
        dto.Dispatch.Should().NotBeNull();
        dto.Dispatch!.Customer.Should().NotBeNull();
        dto.Dispatch.Customer!.Name.Should().Be("Carol (contract)");
    }

}
