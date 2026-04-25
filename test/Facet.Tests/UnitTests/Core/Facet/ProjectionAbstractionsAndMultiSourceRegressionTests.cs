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

public interface IContactContract353
{
    int Id { get; set; }
    string Name { get; set; }
}

public class ContactEntity353 : IContactContract353
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

[Facet(typeof(IContactContract353),
    Configuration = typeof(ContactFacet353InterfaceConfig),
    Include = new[] { nameof(IContactContract353.Id), nameof(IContactContract353.Name) })]
[Facet(typeof(ContactEntity353),
    Include = new[] { nameof(ContactEntity353.Id), nameof(ContactEntity353.Name) })]
public partial class ContactFacet353
{
}

public class ContactFacet353InterfaceConfig
    : IFacetProjectionMapConfiguration<IContactContract353, ContactFacet353>
{
    public static void ConfigureProjection(
        IFacetProjectionBuilder<IContactContract353, ContactFacet353> builder)
    {
        builder.Map(d => d.Name, s => s.Name + " (iface)");
    }
}

public class Level5Entity353
{
    public int Id { get; set; }
    public IContactContract353? Contact { get; set; }
}

public class Level5ConcreteEntity353
{
    public int Id { get; set; }
    public ContactEntity353? Contact { get; set; }
}

public class Level4Entity353
{
    public int Id { get; set; }
    public Level5Entity353? Level5 { get; set; }
}

public class Level3Entity353
{
    public int Id { get; set; }
    public Level4Entity353? Level4 { get; set; }
}

public class Level2Entity353
{
    public int Id { get; set; }
    public Level3Entity353? Level3 { get; set; }
}

public class Level1Entity353
{
    public int Id { get; set; }
    public Level2Entity353? Level2 { get; set; }
}

[Facet(typeof(Level5Entity353),
    Include = new[] { nameof(Level5Entity353.Id), nameof(Level5Entity353.Contact) },
    NestedFacets = new[] { typeof(ContactFacet353) })]
public partial class Level5Dto353
{
}

[Facet(typeof(Level5ConcreteEntity353),
    Include = new[] { nameof(Level5ConcreteEntity353.Id), nameof(Level5ConcreteEntity353.Contact) },
    NestedFacets = new[] { typeof(ContactFacet353) })]
public partial class Level5ConcreteDto353
{
}

[Facet(typeof(Level4Entity353),
    Include = new[] { nameof(Level4Entity353.Id), nameof(Level4Entity353.Level5) },
    NestedFacets = new[] { typeof(Level5Dto353) })]
public partial class Level4Dto353
{
}

[Facet(typeof(Level3Entity353),
    Configuration = typeof(Level3Dto353Config),
    Include = new[] { nameof(Level3Entity353.Id), nameof(Level3Entity353.Level4) },
    NestedFacets = new[] { typeof(Level4Dto353) })]
public partial class Level3Dto353
{
}

public class Level3Dto353Config
    : IFacetProjectionMapConfiguration<Level3Entity353, Level3Dto353>
{
    public static void ConfigureProjection(
        IFacetProjectionBuilder<Level3Entity353, Level3Dto353> builder)
    {
        builder.Map(d => d.Id, s => s.Id + 30);
    }
}

[Facet(typeof(Level2Entity353),
    Include = new[] { nameof(Level2Entity353.Id), nameof(Level2Entity353.Level3) },
    NestedFacets = new[] { typeof(Level3Dto353) })]
public partial class Level2Dto353
{
}

[Facet(typeof(Level1Entity353),
    Include = new[] { nameof(Level1Entity353.Id), nameof(Level1Entity353.Level2) },
    NestedFacets = new[] { typeof(Level2Dto353) })]
public partial class Level1Dto353
{
}

[Facet(typeof(Level1Entity353),
    Configuration = typeof(Level1Dto353LazyConfig),
    Include = new[] { nameof(Level1Entity353.Id), nameof(Level1Entity353.Level2) },
    NestedFacets = new[] { typeof(Level2Dto353) })]
public partial class Level1Dto353Lazy
{
}

public class Level1Dto353LazyConfig
    : IFacetProjectionMapConfiguration<Level1Entity353, Level1Dto353Lazy>
{
    public static void ConfigureProjection(
        IFacetProjectionBuilder<Level1Entity353, Level1Dto353Lazy> builder)
    {
        builder.Map(d => d.Id, s => s.Id + 1000);
    }
}

public class BaseRootEntity353
{
    public int Id { get; set; }
    public Level2Entity353? Level2 { get; set; }
}

public class DerivedRootEntity353 : BaseRootEntity353
{
    public string Code { get; set; } = string.Empty;
}

[Facet(typeof(BaseRootEntity353),
    Include = new[] { nameof(BaseRootEntity353.Id), nameof(BaseRootEntity353.Level2) },
    NestedFacets = new[] { typeof(Level2Dto353) })]
public partial class BaseRootDto353
{
}

[Facet(typeof(DerivedRootEntity353),
    Include = new[] { nameof(DerivedRootEntity353.Code) })]
public partial class DerivedRootDto353 : BaseRootDto353
{
}

public class FiveLevelProjectionRegressionTests
{
    private static Level2Entity353 CreateLevel2Tree(int baseId, string contactName) =>
        new()
        {
            Id = baseId + 2,
            Level3 = new Level3Entity353
            {
                Id = baseId + 3,
                Level4 = new Level4Entity353
                {
                    Id = baseId + 4,
                    Level5 = new Level5Entity353
                    {
                        Id = baseId + 5,
                        Contact = new ContactEntity353
                        {
                            Id = baseId + 6,
                            Name = contactName
                        }
                    }
                }
            }
        };

    [Fact]
    public void Projection_FiveLevels_Inline_ShouldMapAllLevels()
    {
        var source = new Level1Entity353
        {
            Id = 1,
            Level2 = CreateLevel2Tree(1, "Alpha")
        };

        var dto = Level1Dto353.Projection.Compile()(source);

        dto.Id.Should().Be(1);
        dto.Level2.Should().NotBeNull();
        dto.Level2!.Id.Should().Be(3);
        dto.Level2.Level3.Should().NotBeNull();
        dto.Level2.Level3!.Id.Should().Be(34);
        dto.Level2.Level3.Level4.Should().NotBeNull();
        dto.Level2.Level3.Level4!.Id.Should().Be(5);
        dto.Level2.Level3.Level4.Level5.Should().NotBeNull();
        dto.Level2.Level3.Level4.Level5!.Id.Should().Be(6);
        dto.Level2.Level3.Level4.Level5.Contact.Should().NotBeNull();
        dto.Level2.Level3.Level4.Level5.Contact!.Name.Should().Be("Alpha (iface)");
    }

    [Fact]
    public void Projection_FiveLevels_LazyRoot_ShouldMapAllLevels()
    {
        var source = new Level1Entity353
        {
            Id = 2,
            Level2 = CreateLevel2Tree(10, "Bravo")
        };

        var dto = Level1Dto353Lazy.Projection.Compile()(source);

        dto.Id.Should().Be(1002);
        dto.Level2.Should().NotBeNull();
        dto.Level2!.Level3.Should().NotBeNull();
        dto.Level2.Level3!.Id.Should().Be(43);
        dto.Level2.Level3.Level4.Should().NotBeNull();
        dto.Level2.Level3.Level4!.Level5.Should().NotBeNull();
        dto.Level2.Level3.Level4.Level5!.Contact.Should().NotBeNull();
        dto.Level2.Level3.Level4.Level5.Contact!.Name.Should().Be("Bravo (iface)");
    }

    [Fact]
    public void Projection_FiveLevels_InheritedRoot_ShouldMapAllLevels()
    {
        var source = new DerivedRootEntity353
        {
            Id = 8,
            Code = "DER-8",
            Level2 = CreateLevel2Tree(20, "Charlie")
        };

        var dto = DerivedRootDto353.Projection.Compile()(source);

        dto.Id.Should().Be(8);
        dto.Code.Should().Be("DER-8");
        dto.Level2.Should().NotBeNull();
        dto.Level2!.Level3.Should().NotBeNull();
        dto.Level2.Level3!.Level4.Should().NotBeNull();
        dto.Level2.Level3.Level4!.Level5.Should().NotBeNull();
        dto.Level2.Level3.Level4.Level5!.Contact.Should().NotBeNull();
        dto.Level2.Level3.Level4.Level5.Contact!.Name.Should().Be("Charlie (iface)");
    }

    [Fact]
    public void Projection_FiveLevels_MultiSourceNestedFacet_ShouldSelectMatchingProjectionByNestedSourceType()
    {
        var interfaceSource = new Level5Entity353
        {
            Id = 100,
            Contact = new ContactEntity353 { Id = 101, Name = "Delta" }
        };
        var concreteSource = new Level5ConcreteEntity353
        {
            Id = 200,
            Contact = new ContactEntity353 { Id = 201, Name = "Echo" }
        };

        var interfaceDto = Level5Dto353.Projection.Compile()(interfaceSource);
        var concreteDto = Level5ConcreteDto353.Projection.Compile()(concreteSource);

        interfaceDto.Contact.Should().NotBeNull();
        interfaceDto.Contact!.Name.Should().Be("Delta (iface)");

        concreteDto.Contact.Should().NotBeNull();
        concreteDto.Contact!.Name.Should().Be("Echo");
    }

    [Fact]
    public void Projection_FiveLevels_WithNullIntermediateNode_ShouldKeepNestedNodeNull()
    {
        var source = new Level1Entity353
        {
            Id = 5,
            Level2 = new Level2Entity353
            {
                Id = 50,
                Level3 = new Level3Entity353
                {
                    Id = 60,
                    Level4 = null
                }
            }
        };

        var dto = Level1Dto353.Projection.Compile()(source);

        dto.Level2.Should().NotBeNull();
        dto.Level2!.Level3.Should().NotBeNull();
        dto.Level2.Level3!.Id.Should().Be(90);
        dto.Level2.Level3.Level4.Should().BeNull();
    }
}
