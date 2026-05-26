using System.Linq.Expressions;
using Facet.Mapping;
using Facet.Tests.TestModels;

namespace Facet.Tests.UnitTests.Core.Facet;

public class OrderEntity350
{
    public int Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public OrderLineDispatchEntity350? Dispatch { get; set; }
}

public class OrderLineDispatchEntity350
{
    public int Id { get; set; }
    public DateTime DispatchDate { get; set; }
    public CustomerEntity350? Customer { get; set; }
}

public class CustomerEntity350
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

[Facet(typeof(CustomerEntity350),
    Include = new[] { "Id", "Name", "Email" })]
public partial class CustomerDto350
{
}

[Facet(typeof(OrderLineDispatchEntity350),
    Include = new[] { "Id", "DispatchDate", "Customer" },
    NestedFacets = new[] { typeof(CustomerDto350) })]
public partial class OrderLineDispatchDto350
{
}

[Facet(typeof(OrderEntity350),
    Include = new[] { "Id", "OrderNumber", "Dispatch" },
    NestedFacets = new[] { typeof(OrderLineDispatchDto350) })]
public partial class OrderDto350
{
}

[Facet(typeof(CustomerEntity350),
    Configuration = typeof(CustomerDto350LazyConfig),
    Include = new[] { "Id", "Name", "Email" })]
public partial class CustomerDto350Lazy
{
}

public class CustomerDto350LazyConfig
    : IFacetProjectionMapConfiguration<CustomerEntity350, CustomerDto350Lazy>
{
    public static void ConfigureProjection(
        IFacetProjectionBuilder<CustomerEntity350, CustomerDto350Lazy> builder)
    {
        builder.Map(d => d.Name, s => s.Name.ToUpper());
    }
}

[Facet(typeof(OrderLineDispatchEntity350),
    Configuration = typeof(DispatchDto350LazyConfig),
    Include = new[] { "Id", "DispatchDate", "Customer" },
    NestedFacets = new[] { typeof(CustomerDto350Lazy) })]
public partial class DispatchDto350Lazy
{
}

public class DispatchDto350LazyConfig
    : IFacetProjectionMapConfiguration<OrderLineDispatchEntity350, DispatchDto350Lazy>
{
    public static void ConfigureProjection(
        IFacetProjectionBuilder<OrderLineDispatchEntity350, DispatchDto350Lazy> builder)
    {
    }
}

[Facet(typeof(OrderEntity350),
    Configuration = typeof(OrderDto350LazyConfig),
    Include = new[] { "Id", "OrderNumber", "Dispatch" },
    NestedFacets = new[] { typeof(DispatchDto350Lazy) })]
public partial class OrderDto350Lazy
{
}

public class OrderDto350LazyConfig
    : IFacetProjectionMapConfiguration<OrderEntity350, OrderDto350Lazy>
{
    public static void ConfigureProjection(
        IFacetProjectionBuilder<OrderEntity350, OrderDto350Lazy> builder)
    {
    }
}

public class BaseOrderEntity350
{
    public int Id { get; set; }
    public string Number { get; set; } = string.Empty;
}

public class DerivedOrderEntity350 : BaseOrderEntity350
{
    public OrderLineDispatchEntity350? Dispatch { get; set; }
}

[Facet(typeof(BaseOrderEntity350),
    Include = new[] { "Id", "Number" })]
public partial class BaseOrderDto350
{
}

[Facet(typeof(DerivedOrderEntity350),
    Include = new[] { "Dispatch" },
    NestedFacets = new[] { typeof(OrderLineDispatchDto350) })]
public partial class DerivedOrderDto350 : BaseOrderDto350
{
}

[Facet(typeof(BaseOrderEntity350),
    Configuration = typeof(BaseOrderDto350LzCfg),
    Include = new[] { "Id", "Number" })]
public partial class BaseOrderDto350Lz
{
}

public class BaseOrderDto350LzCfg
    : IFacetProjectionMapConfiguration<BaseOrderEntity350, BaseOrderDto350Lz>
{
    public static void ConfigureProjection(
        IFacetProjectionBuilder<BaseOrderEntity350, BaseOrderDto350Lz> builder)
    {
        builder.Map(d => d.Number, s => "ORD-" + s.Number);
    }
}

[Facet(typeof(DerivedOrderEntity350),
    Configuration = typeof(DerivedOrderDto350LzCfg),
    Include = new[] { "Dispatch" },
    NestedFacets = new[] { typeof(OrderLineDispatchDto350) })]
public partial class DerivedOrderDto350Lz : BaseOrderDto350Lz
{
}

public class DerivedOrderDto350LzCfg
    : IFacetProjectionMapConfiguration<DerivedOrderEntity350, DerivedOrderDto350Lz>
{
    public static void ConfigureProjection(
        IFacetProjectionBuilder<DerivedOrderEntity350, DerivedOrderDto350Lz> builder)
    {
    }
}

public class AssignedToUnit350
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class OrderHeader350
{
    public int Id { get; set; }
    public string Number { get; set; } = string.Empty;
}

public class DispatchEntity351
{
    public int Id { get; set; }
    public DateTime DispatchDate { get; set; }
    public AssignedToUnit350? AssignedToUnit { get; set; }
    public OrderHeader350? OrderHeader { get; set; }
    public CustomerEntity350? Customer { get; set; }
}

public class ProductEntity351
{
    public int Id { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public DispatchEntity351? Dispatch { get; set; }
}

[Facet(typeof(CustomerEntity350),
    Include = new[] { "Id", "Name", "Email" })]
public partial class CustomerDto351
{
}

[Facet(typeof(DispatchEntity351),
    Configuration = typeof(DispatchDto351Config),
    Include = new[] { "Id", "DispatchDate", "Customer" },
    NestedFacets = new[] { typeof(CustomerDto351) })]
public partial class DispatchDto351
{
    public string? AssignedToUnitName { get; set; }
    public string? OrderHeaderNumber { get; set; }
}

public class DispatchDto351Config
    : IFacetProjectionMapConfiguration<DispatchEntity351, DispatchDto351>
{
    public static void ConfigureProjection(
        IFacetProjectionBuilder<DispatchEntity351, DispatchDto351> builder)
    {
        builder.Map(d => d.AssignedToUnitName, s => s.AssignedToUnit != null ? s.AssignedToUnit.Name : null);
        builder.Map(d => d.OrderHeaderNumber, s => s.OrderHeader != null ? s.OrderHeader.Number : null);
        
    }
}

[Facet(typeof(ProductEntity351),
    Include = new[] { "Id", "ProductName", "Dispatch" },
    NestedFacets = new[] { typeof(DispatchDto351) })]
public partial class ProductDto351
{
}

[Facet(typeof(ProductEntity351),
    Configuration = typeof(ProductDto351LzConfig),
    Include = new[] { "Id", "ProductName", "Dispatch" },
    NestedFacets = new[] { typeof(DispatchDto351) })]
public partial class ProductDto351Lz
{
}

public class ProductDto351LzConfig
    : IFacetProjectionMapConfiguration<ProductEntity351, ProductDto351Lz>
{
    public static void ConfigureProjection(
        IFacetProjectionBuilder<ProductEntity351, ProductDto351Lz> builder)
    {
    }
}

public class OrderEntityNonNull350
{
    public int Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public OrderLineDispatchEntityNonNull350 Dispatch { get; set; } = new();
}

public class OrderLineDispatchEntityNonNull350
{
    public int Id { get; set; }
    public DateTime DispatchDate { get; set; }
    public CustomerEntity350 Customer { get; set; } = new();
}

[Facet(typeof(OrderLineDispatchEntityNonNull350),
    Include = new[] { "Id", "DispatchDate", "Customer" },
    NestedFacets = new[] { typeof(CustomerDto350) })]
public partial class DispatchDtoNonNull350
{
}

[Facet(typeof(OrderEntityNonNull350),
    Include = new[] { "Id", "OrderNumber", "Dispatch" },
    NestedFacets = new[] { typeof(DispatchDtoNonNull350) })]
public partial class OrderDtoNonNull350
{
}

public class ThreeLevelNestedProjectionTests
{
    private OrderEntity350 CreateTestEntity() => new()
    {
        Id = 1,
        OrderNumber = "ORD-001",
        Dispatch = new OrderLineDispatchEntity350
        {
            Id = 2,
            DispatchDate = new DateTime(2024, 6, 15),
            Customer = new CustomerEntity350
            {
                Id = 3,
                Name = "John Doe",
                Email = "john@example.com"
            }
        }
    };

    [Fact]
    public void Projection_InlinePath_ThreeLevelsDeep_ShouldPopulateAllLevels()
    {
        var entity = CreateTestEntity();

        var projection = OrderDto350.Projection.Compile();
        var dto = projection(entity);

        dto.Id.Should().Be(1);
        dto.OrderNumber.Should().Be("ORD-001");

        dto.Dispatch.Should().NotBeNull("Second level (Dispatch) should be populated");
        dto.Dispatch!.Id.Should().Be(2);
        dto.Dispatch.DispatchDate.Should().Be(new DateTime(2024, 6, 15));

        dto.Dispatch.Customer.Should().NotBeNull("Third level (Customer) MUST be populated");
        dto.Dispatch.Customer!.Id.Should().Be(3);
        dto.Dispatch.Customer.Name.Should().Be("John Doe");
        dto.Dispatch.Customer.Email.Should().Be("john@example.com");
    }

    [Fact]
    public void Projection_InlinePath_ThreeLevelsDeep_WithNullSecondLevel_ShouldReturnNull()
    {
        var entity = new OrderEntity350
        {
            Id = 1,
            OrderNumber = "ORD-002",
            Dispatch = null
        };

        var projection = OrderDto350.Projection.Compile();
        var dto = projection(entity);

        dto.Id.Should().Be(1);
        dto.Dispatch.Should().BeNull("Dispatch is null in source");
    }

    [Fact]
    public void Projection_InlinePath_ThreeLevelsDeep_WithNullThirdLevel_ShouldReturnNull()
    {
        var entity = new OrderEntity350
        {
            Id = 1,
            OrderNumber = "ORD-003",
            Dispatch = new OrderLineDispatchEntity350
            {
                Id = 2,
                DispatchDate = new DateTime(2024, 6, 15),
                Customer = null
            }
        };

        var projection = OrderDto350.Projection.Compile();
        var dto = projection(entity);

        dto.Dispatch.Should().NotBeNull();
        dto.Dispatch!.Customer.Should().BeNull("Customer is null in source");
    }

    [Fact]
    public void Projection_InlinePath_ThreeLevelsDeep_ViaQueryable_ShouldPopulateAll()
    {
        var entities = new[] { CreateTestEntity() }.AsQueryable();

        var dtos = entities.Select(OrderDto350.Projection).ToList();

        dtos.Should().HaveCount(1);
        var dto = dtos[0];
        dto.Dispatch.Should().NotBeNull();
        dto.Dispatch!.Customer.Should().NotBeNull("Third level should be populated via Queryable");
        dto.Dispatch.Customer!.Name.Should().Be("John Doe");
    }

    [Fact]
    public void Projection_LazyPath_ThreeLevelsDeep_ShouldPopulateAllLevels()
    {
        var entity = CreateTestEntity();

        var projection = OrderDto350Lazy.Projection.Compile();
        var dto = projection(entity);

        dto.Id.Should().Be(1);
        dto.OrderNumber.Should().Be("ORD-001");

        dto.Dispatch.Should().NotBeNull("Second level (Dispatch) should be populated");
        dto.Dispatch!.Id.Should().Be(2);
        dto.Dispatch.DispatchDate.Should().Be(new DateTime(2024, 6, 15));

        dto.Dispatch.Customer.Should().NotBeNull("Third level (Customer) MUST be populated via lazy projection");
        dto.Dispatch.Customer!.Id.Should().Be(3);
        dto.Dispatch.Customer.Name.Should().Be("JOHN DOE", "Name should be uppercased via ConfigureProjection");
        dto.Dispatch.Customer.Email.Should().Be("john@example.com");
    }

    [Fact]
    public void Projection_LazyPath_ThreeLevelsDeep_ViaQueryable_ShouldPopulateAll()
    {
        var entities = new[] { CreateTestEntity() }.AsQueryable();

        var dtos = entities.Select(OrderDto350Lazy.Projection).ToList();

        dtos.Should().HaveCount(1);
        var dto = dtos[0];
        dto.Dispatch.Should().NotBeNull();
        dto.Dispatch!.Customer.Should().NotBeNull("Third level should be populated via lazy Queryable");
        dto.Dispatch.Customer!.Name.Should().Be("JOHN DOE");
    }

    [Fact]
    public void Constructor_ThreeLevelsDeep_ShouldPopulateAllLevels()
    {
        var entity = CreateTestEntity();

        var dto = new OrderDto350(entity);

        dto.Id.Should().Be(1);
        dto.OrderNumber.Should().Be("ORD-001");

        dto.Dispatch.Should().NotBeNull("Second level should be populated");
        dto.Dispatch!.Id.Should().Be(2);

        dto.Dispatch.Customer.Should().NotBeNull("Third level MUST be populated via constructor");
        dto.Dispatch.Customer!.Id.Should().Be(3);
        dto.Dispatch.Customer.Name.Should().Be("John Doe");
    }

    private ProductEntity351 CreateProduct351Entity() => new()
    {
        Id = 1,
        ProductName = "Widget",
        Dispatch = new DispatchEntity351
        {
            Id = 2,
            DispatchDate = new DateTime(2024, 6, 15),
            AssignedToUnit = new AssignedToUnit350 { Id = 10, Name = "Unit Alpha" },
            OrderHeader = new OrderHeader350 { Id = 20, Number = "HDR-001" },
            Customer = new CustomerEntity350
            {
                Id = 3,
                Name = "John Doe",
                Email = "john@example.com"
            }
        }
    };

    [Fact]
    public void Projection_ManualDeepMaps_PlusNestedFacet_InlinePath_ShouldPopulateAll()
    {
        var entity = CreateProduct351Entity();

        var projection = ProductDto351.Projection.Compile();
        var dto = projection(entity);

        dto.Id.Should().Be(1);
        dto.ProductName.Should().Be("Widget");

        dto.Dispatch.Should().NotBeNull("Second level (Dispatch) should be populated");
        dto.Dispatch!.Id.Should().Be(2);

        dto.Dispatch.AssignedToUnitName.Should().Be("Unit Alpha",
            "Manually mapped property via ConfigureProjection should work");
        dto.Dispatch.OrderHeaderNumber.Should().Be("HDR-001",
            "Manually mapped property via ConfigureProjection should work");

        dto.Dispatch.Customer.Should().NotBeNull(
            "Third level nested facet (Customer) NOT in ConfigureProjection MUST still be populated");
        dto.Dispatch.Customer!.Id.Should().Be(3);
        dto.Dispatch.Customer.Name.Should().Be("John Doe");
        dto.Dispatch.Customer.Email.Should().Be("john@example.com");
    }

    [Fact]
    public void Projection_ManualDeepMaps_PlusNestedFacet_LazyPath_ShouldPopulateAll()
    {
        var entity = CreateProduct351Entity();

        var projection = ProductDto351Lz.Projection.Compile();
        var dto = projection(entity);

        dto.Id.Should().Be(1);
        dto.ProductName.Should().Be("Widget");

        dto.Dispatch.Should().NotBeNull("Second level (Dispatch) should be populated");
        dto.Dispatch!.Id.Should().Be(2);

        dto.Dispatch.AssignedToUnitName.Should().Be("Unit Alpha");
        dto.Dispatch.OrderHeaderNumber.Should().Be("HDR-001");

        dto.Dispatch.Customer.Should().NotBeNull(
            "Third level nested facet MUST be populated even through lazy path chain");
        dto.Dispatch.Customer!.Id.Should().Be(3);
        dto.Dispatch.Customer.Name.Should().Be("John Doe");
    }

    [Fact]
    public void Projection_ManualDeepMaps_PlusNestedFacet_ViaQueryable_ShouldPopulateAll()
    {
        var entities = new[] { CreateProduct351Entity() }.AsQueryable();

        var dtos = entities.Select(ProductDto351.Projection).ToList();

        var dto = dtos[0];
        dto.Dispatch.Should().NotBeNull();
        dto.Dispatch!.AssignedToUnitName.Should().Be("Unit Alpha");
        dto.Dispatch.Customer.Should().NotBeNull("Third level via Queryable MUST be populated");
        dto.Dispatch.Customer!.Name.Should().Be("John Doe");
    }

    [Fact]
    public void Projection_InheritedFacet_ThreeLevelsDeep_ShouldPopulateAllLevels()
    {
        var entity = new DerivedOrderEntity350
        {
            Id = 1,
            Number = "ORD-001",
            Dispatch = new OrderLineDispatchEntity350
            {
                Id = 2,
                DispatchDate = new DateTime(2024, 6, 15),
                Customer = new CustomerEntity350
                {
                    Id = 3,
                    Name = "John Doe",
                    Email = "john@example.com"
                }
            }
        };

        var projection = DerivedOrderDto350.Projection.Compile();
        var dto = projection(entity);

        dto.Id.Should().Be(1);
        dto.Number.Should().Be("ORD-001");

        dto.Dispatch.Should().NotBeNull("Second level (Dispatch) should be populated");
        dto.Dispatch!.Id.Should().Be(2);
        dto.Dispatch.DispatchDate.Should().Be(new DateTime(2024, 6, 15));

        dto.Dispatch.Customer.Should().NotBeNull("Third level (Customer) MUST be populated with inherited facet");
        dto.Dispatch.Customer!.Id.Should().Be(3);
        dto.Dispatch.Customer.Name.Should().Be("John Doe");
        dto.Dispatch.Customer.Email.Should().Be("john@example.com");
    }

    [Fact]
    public void Projection_InheritedFacet_WithConfigureProjection_ThreeLevelsDeep_ShouldPopulateAllLevels()
    {
        var entity = new DerivedOrderEntity350
        {
            Id = 1,
            Number = "ORD-001",
            Dispatch = new OrderLineDispatchEntity350
            {
                Id = 2,
                DispatchDate = new DateTime(2024, 6, 15),
                Customer = new CustomerEntity350
                {
                    Id = 3,
                    Name = "John Doe",
                    Email = "john@example.com"
                }
            }
        };

        var projection = DerivedOrderDto350Lz.Projection.Compile();
        var dto = projection(entity);

        dto.Id.Should().Be(1);
        dto.Number.Should().Be("ORD-ORD-001", "Number should be mapped from base ConfigureProjection");

        dto.Dispatch.Should().NotBeNull("Second level (Dispatch) should be populated");
        dto.Dispatch!.Id.Should().Be(2);

        dto.Dispatch.Customer.Should().NotBeNull("Third level (Customer) MUST be populated with inherited+lazy");
        dto.Dispatch.Customer!.Id.Should().Be(3);
        dto.Dispatch.Customer.Name.Should().Be("John Doe");
    }

    [Fact]
    public void Projection_NonNullableNested_ThreeLevelsDeep_ShouldPopulateAllLevels()
    {
        var entity = new OrderEntityNonNull350
        {
            Id = 1,
            OrderNumber = "ORD-001",
            Dispatch = new OrderLineDispatchEntityNonNull350
            {
                Id = 2,
                DispatchDate = new DateTime(2024, 6, 15),
                Customer = new CustomerEntity350
                {
                    Id = 3,
                    Name = "Jane Smith",
                    Email = "jane@example.com"
                }
            }
        };

        var projection = OrderDtoNonNull350.Projection.Compile();
        var dto = projection(entity);

        dto.Id.Should().Be(1);
        dto.OrderNumber.Should().Be("ORD-001");

        dto.Dispatch.Should().NotBeNull("Non-nullable Dispatch should be populated");
        dto.Dispatch!.Id.Should().Be(2);

        dto.Dispatch.Customer.Should().NotBeNull("Non-nullable third level MUST be populated");
        dto.Dispatch.Customer!.Id.Should().Be(3);
        dto.Dispatch.Customer.Name.Should().Be("Jane Smith");
        dto.Dispatch.Customer.Email.Should().Be("jane@example.com");
    }
}

public class RootEntity360
{
    public int Id { get; set; }
    public string RootName { get; set; } = string.Empty;
    public MiddleEntity360? Middle { get; set; }
}

public class MiddleEntity360
{
    public int Id { get; set; }
    public string MiddleName { get; set; } = string.Empty;
    public DeepEntity360? Deep { get; set; }
}

public class DeepEntity360
{
    public int Id { get; set; }
    public string DeepName { get; set; } = string.Empty;
}

[Facet(typeof(DeepEntity360),
    Configuration = typeof(DeepFacet360Config),
    Include = new[] { "Id", "DeepName" })]
public partial class DeepFacet360
{
}

public class DeepFacet360Config : IFacetProjectionMapConfiguration<DeepEntity360, DeepFacet360>
{
    public static void ConfigureProjection(IFacetProjectionBuilder<DeepEntity360, DeepFacet360> builder)
    {
        builder.Map(d => d.DeepName, s => "DEEP-" + s.DeepName);
    }
}

[Facet(typeof(MiddleEntity360),
    Include = new[] { "Id", "MiddleName", "Deep" },
    NestedFacets = new[] { typeof(DeepFacet360) })]
public partial class MiddleFacet360
{
}

[Facet(typeof(RootEntity360),
    Include = new[] { "Id", "RootName", "Middle" },
    NestedFacets = new[] { typeof(MiddleFacet360) })]
public partial class RootFacet360
{
}

public class DeepestLevelConfigProjectionTests
{
    private RootEntity360 CreateEntity() => new()
    {
        Id = 1,
        RootName = "root",
        Middle = new MiddleEntity360
        {
            Id = 2,
            MiddleName = "middle",
            Deep = new DeepEntity360 { Id = 3, DeepName = "leaf" }
        }
    };

    [Fact]
    public void Projection_OnlyDeepestLevel_HasConfigureProjection_ShouldApplyConfig()
    {
        var entity = CreateEntity();

        var projection = RootFacet360.Projection.Compile();
        var dto = projection(entity);

        dto.Id.Should().Be(1);
        dto.Middle.Should().NotBeNull();
        dto.Middle!.Id.Should().Be(2);
        dto.Middle.Deep.Should().NotBeNull();
        dto.Middle.Deep!.Id.Should().Be(3);
        dto.Middle.Deep.DeepName.Should().Be("DEEP-leaf",
            "ConfigureProjection on the deepest nested facet must be applied even when parent and grandparent have no config");
    }

    [Fact]
    public void Projection_OnlyDeepestLevel_HasConfigureProjection_ViaQueryable_ShouldApplyConfig()
    {
        var entities = new[] { CreateEntity() }.AsQueryable();

        var dtos = entities.Select(RootFacet360.Projection).ToList();

        dtos.Should().HaveCount(1);
        dtos[0].Middle!.Deep!.DeepName.Should().Be("DEEP-leaf",
            "ConfigureProjection on the deepest nested facet must be applied via Queryable");
    }

    [Fact]
    public void Projection_OnlyDeepestLevel_NullMiddle_ShouldReturnNull()
    {
        var entity = new RootEntity360 { Id = 1, RootName = "root", Middle = null };
        var dto = RootFacet360.Projection.Compile()(entity);
        dto.Middle.Should().BeNull();
    }

    [Fact]
    public void Projection_OnlyDeepestLevel_NullDeep_ShouldReturnNullDeep()
    {
        var entity = new RootEntity360
        {
            Id = 1,
            RootName = "root",
            Middle = new MiddleEntity360 { Id = 2, MiddleName = "middle", Deep = null }
        };
        var dto = RootFacet360.Projection.Compile()(entity);
        dto.Middle.Should().NotBeNull();
        dto.Middle!.Deep.Should().BeNull();
    }
}

public class DispatchEntity361
{
    public int Id { get; set; }
    public string Reference { get; set; } = string.Empty;
    public List<DispatchProductEntity361> Products { get; set; } = [];
}

public class DispatchProductEntity361
{
    public int Id { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public int Quantity { get; set; }
}

[Facet(typeof(DispatchProductEntity361),
    Configuration = typeof(DispatchProductFacet361Config),
    Include = new[] { "Id", "ProductCode", "Quantity" })]
public partial class DispatchProductFacet361
{
}

public class DispatchProductFacet361Config : IFacetProjectionMapConfiguration<DispatchProductEntity361, DispatchProductFacet361>
{
    public static void ConfigureProjection(IFacetProjectionBuilder<DispatchProductEntity361, DispatchProductFacet361> builder)
    {
        builder.Map(d => d.ProductCode, s => "PROD-" + s.ProductCode);
    }
}

[Facet(typeof(DispatchEntity361),
    Include = new[] { "Id", "Reference", "Products" },
    NestedFacets = new[] { typeof(DispatchProductFacet361) })]
public partial class DispatchFacet361
{
}

public class CollectionNestedFacetWithConfigTests
{
    private DispatchEntity361 CreateEntity() => new()
    {
        Id = 1,
        Reference = "DISP-001",
        Products =
        [
            new DispatchProductEntity361 { Id = 10, ProductCode = "ABC", Quantity = 5 },
            new DispatchProductEntity361 { Id = 11, ProductCode = "XYZ", Quantity = 3 },
        ]
    };

    [Fact]
    public void Projection_CollectionNestedFacet_WithConfigOnElementType_ShouldApplyConfig()
    {
        // Parent took the inline path and ConfigureProjection on the element type was skipped.
        var entity = CreateEntity();

        var dto = DispatchFacet361.Projection.Compile()(entity);

        dto.Id.Should().Be(1);
        dto.Reference.Should().Be("DISP-001");
        dto.Products.Should().HaveCount(2);
        dto.Products![0].ProductCode.Should().Be("PROD-ABC",
            "ConfigureProjection on the collection element type must be applied");
        dto.Products[1].ProductCode.Should().Be("PROD-XYZ");
    }

    [Fact]
    public void Projection_CollectionNestedFacet_WithConfigOnElementType_ViaQueryable_ShouldApplyConfig()
    {
        var entities = new[] { CreateEntity() }.AsQueryable();

        var dtos = entities.Select(DispatchFacet361.Projection).ToList();

        dtos.Should().HaveCount(1);
        dtos[0].Products.Should().HaveCount(2);
        dtos[0].Products![0].ProductCode.Should().Be("PROD-ABC",
            "ConfigureProjection on collection element type must apply via Queryable");
    }

    [Fact]
    public void Projection_CollectionNestedFacet_EmptyCollection_ShouldReturnEmpty()
    {
        var entity = new DispatchEntity361 { Id = 2, Reference = "DISP-002", Products = [] };
        var dto = DispatchFacet361.Projection.Compile()(entity);
        dto.Products.Should().BeEmpty();
    }
}
