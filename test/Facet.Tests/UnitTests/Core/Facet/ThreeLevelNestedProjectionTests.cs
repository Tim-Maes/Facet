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
        // just a dummy mapping
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
        // just a dummy mapping
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
        // Manually map deep paths
        builder.Map(d => d.AssignedToUnitName, s => s.AssignedToUnit != null ? s.AssignedToUnit.Name : null);
        builder.Map(d => d.OrderHeaderNumber, s => s.OrderHeader != null ? s.OrderHeader.Number : null);
        // Customer is intentionally NOT mapped here — should be auto-projected as nested facet
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
        // nothing extra — just triggers the lazy path
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
        // Arrange
        var entity = CreateTestEntity();

        // Act
        var projection = OrderDto350.Projection.Compile();
        var dto = projection(entity);

        // Assert - Level 1
        dto.Id.Should().Be(1);
        dto.OrderNumber.Should().Be("ORD-001");

        // Assert - Level 2
        dto.Dispatch.Should().NotBeNull("Second level (Dispatch) should be populated");
        dto.Dispatch!.Id.Should().Be(2);
        dto.Dispatch.DispatchDate.Should().Be(new DateTime(2024, 6, 15));

        // Assert - Level 3 (the critical one)
        dto.Dispatch.Customer.Should().NotBeNull("Third level (Customer) MUST be populated");
        dto.Dispatch.Customer!.Id.Should().Be(3);
        dto.Dispatch.Customer.Name.Should().Be("John Doe");
        dto.Dispatch.Customer.Email.Should().Be("john@example.com");
    }

    [Fact]
    public void Projection_InlinePath_ThreeLevelsDeep_WithNullSecondLevel_ShouldReturnNull()
    {
        // Arrange
        var entity = new OrderEntity350
        {
            Id = 1,
            OrderNumber = "ORD-002",
            Dispatch = null
        };

        // Act
        var projection = OrderDto350.Projection.Compile();
        var dto = projection(entity);

        // Assert
        dto.Id.Should().Be(1);
        dto.Dispatch.Should().BeNull("Dispatch is null in source");
    }

    [Fact]
    public void Projection_InlinePath_ThreeLevelsDeep_WithNullThirdLevel_ShouldReturnNull()
    {
        // Arrange
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

        // Act
        var projection = OrderDto350.Projection.Compile();
        var dto = projection(entity);

        // Assert
        dto.Dispatch.Should().NotBeNull();
        dto.Dispatch!.Customer.Should().BeNull("Customer is null in source");
    }

    [Fact]
    public void Projection_InlinePath_ThreeLevelsDeep_ViaQueryable_ShouldPopulateAll()
    {
        // Arrange
        var entities = new[] { CreateTestEntity() }.AsQueryable();

        // Act
        var dtos = entities.Select(OrderDto350.Projection).ToList();

        // Assert
        dtos.Should().HaveCount(1);
        var dto = dtos[0];
        dto.Dispatch.Should().NotBeNull();
        dto.Dispatch!.Customer.Should().NotBeNull("Third level should be populated via Queryable");
        dto.Dispatch.Customer!.Name.Should().Be("John Doe");
    }

    [Fact]
    public void Projection_LazyPath_ThreeLevelsDeep_ShouldPopulateAllLevels()
    {
        // Arrange
        var entity = CreateTestEntity();

        // Act
        var projection = OrderDto350Lazy.Projection.Compile();
        var dto = projection(entity);

        // Assert - Level 1
        dto.Id.Should().Be(1);
        dto.OrderNumber.Should().Be("ORD-001");

        // Assert - Level 2
        dto.Dispatch.Should().NotBeNull("Second level (Dispatch) should be populated");
        dto.Dispatch!.Id.Should().Be(2);
        dto.Dispatch.DispatchDate.Should().Be(new DateTime(2024, 6, 15));

        // Assert - Level 3 (the critical one)
        dto.Dispatch.Customer.Should().NotBeNull("Third level (Customer) MUST be populated via lazy projection");
        dto.Dispatch.Customer!.Id.Should().Be(3);
        dto.Dispatch.Customer.Name.Should().Be("JOHN DOE", "Name should be uppercased via ConfigureProjection");
        dto.Dispatch.Customer.Email.Should().Be("john@example.com");
    }

    [Fact]
    public void Projection_LazyPath_ThreeLevelsDeep_ViaQueryable_ShouldPopulateAll()
    {
        // Arrange
        var entities = new[] { CreateTestEntity() }.AsQueryable();

        // Act
        var dtos = entities.Select(OrderDto350Lazy.Projection).ToList();

        // Assert
        dtos.Should().HaveCount(1);
        var dto = dtos[0];
        dto.Dispatch.Should().NotBeNull();
        dto.Dispatch!.Customer.Should().NotBeNull("Third level should be populated via lazy Queryable");
        dto.Dispatch.Customer!.Name.Should().Be("JOHN DOE");
    }

    [Fact]
    public void Constructor_ThreeLevelsDeep_ShouldPopulateAllLevels()
    {
        // Arrange
        var entity = CreateTestEntity();

        // Act
        var dto = new OrderDto350(entity);

        // Assert - Level 1
        dto.Id.Should().Be(1);
        dto.OrderNumber.Should().Be("ORD-001");

        // Assert - Level 2
        dto.Dispatch.Should().NotBeNull("Second level should be populated");
        dto.Dispatch!.Id.Should().Be(2);

        // Assert - Level 3
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
        // Arrange — Level 1 uses inline path, Level 2 has ConfigureProjection (lazy)
        var entity = CreateProduct351Entity();

        // Act
        var projection = ProductDto351.Projection.Compile();
        var dto = projection(entity);

        // Assert - Level 1
        dto.Id.Should().Be(1);
        dto.ProductName.Should().Be("Widget");

        // Assert - Level 2
        dto.Dispatch.Should().NotBeNull("Second level (Dispatch) should be populated");
        dto.Dispatch!.Id.Should().Be(2);

        // Assert - Manually mapped deep properties SHOULD work
        dto.Dispatch.AssignedToUnitName.Should().Be("Unit Alpha",
            "Manually mapped property via ConfigureProjection should work");
        dto.Dispatch.OrderHeaderNumber.Should().Be("HDR-001",
            "Manually mapped property via ConfigureProjection should work");

        // Assert - Level 3 nested facet NOT manually mapped - THIS IS THE BUG AREA
        dto.Dispatch.Customer.Should().NotBeNull(
            "Third level nested facet (Customer) NOT in ConfigureProjection MUST still be populated");
        dto.Dispatch.Customer!.Id.Should().Be(3);
        dto.Dispatch.Customer.Name.Should().Be("John Doe");
        dto.Dispatch.Customer.Email.Should().Be("john@example.com");
    }

    [Fact]
    public void Projection_ManualDeepMaps_PlusNestedFacet_LazyPath_ShouldPopulateAll()
    {
        // Arrange — Level 1 ALSO uses lazy path (ConfigureProjection)
        var entity = CreateProduct351Entity();

        // Act
        var projection = ProductDto351Lz.Projection.Compile();
        var dto = projection(entity);

        // Assert - Level 1
        dto.Id.Should().Be(1);
        dto.ProductName.Should().Be("Widget");

        // Assert - Level 2
        dto.Dispatch.Should().NotBeNull("Second level (Dispatch) should be populated");
        dto.Dispatch!.Id.Should().Be(2);

        // Assert - Manually mapped deep properties
        dto.Dispatch.AssignedToUnitName.Should().Be("Unit Alpha");
        dto.Dispatch.OrderHeaderNumber.Should().Be("HDR-001");

        // Assert - Level 3 nested facet
        dto.Dispatch.Customer.Should().NotBeNull(
            "Third level nested facet MUST be populated even through lazy path chain");
        dto.Dispatch.Customer!.Id.Should().Be(3);
        dto.Dispatch.Customer.Name.Should().Be("John Doe");
    }

    [Fact]
    public void Projection_ManualDeepMaps_PlusNestedFacet_ViaQueryable_ShouldPopulateAll()
    {
        // Arrange
        var entities = new[] { CreateProduct351Entity() }.AsQueryable();

        // Act
        var dtos = entities.Select(ProductDto351.Projection).ToList();

        // Assert
        var dto = dtos[0];
        dto.Dispatch.Should().NotBeNull();
        dto.Dispatch!.AssignedToUnitName.Should().Be("Unit Alpha");
        dto.Dispatch.Customer.Should().NotBeNull("Third level via Queryable MUST be populated");
        dto.Dispatch.Customer!.Name.Should().Be("John Doe");
    }

    [Fact]
    public void Projection_InheritedFacet_ThreeLevelsDeep_ShouldPopulateAllLevels()
    {
        // Arrange
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

        // Act
        var projection = DerivedOrderDto350.Projection.Compile();
        var dto = projection(entity);

        // Assert - Level 1 (inherited from base)
        dto.Id.Should().Be(1);
        dto.Number.Should().Be("ORD-001");

        // Assert - Level 2
        dto.Dispatch.Should().NotBeNull("Second level (Dispatch) should be populated");
        dto.Dispatch!.Id.Should().Be(2);
        dto.Dispatch.DispatchDate.Should().Be(new DateTime(2024, 6, 15));

        // Assert - Level 3 (the critical one)
        dto.Dispatch.Customer.Should().NotBeNull("Third level (Customer) MUST be populated with inherited facet");
        dto.Dispatch.Customer!.Id.Should().Be(3);
        dto.Dispatch.Customer.Name.Should().Be("John Doe");
        dto.Dispatch.Customer.Email.Should().Be("john@example.com");
    }

    [Fact]
    public void Projection_InheritedFacet_WithConfigureProjection_ThreeLevelsDeep_ShouldPopulateAllLevels()
    {
        // Arrange
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

        // Act
        var projection = DerivedOrderDto350Lz.Projection.Compile();
        var dto = projection(entity);

        // Assert - Level 1 (inherited, with ConfigureProjection mapping)
        dto.Id.Should().Be(1);
        dto.Number.Should().Be("ORD-ORD-001", "Number should be mapped from base ConfigureProjection");

        // Assert - Level 2
        dto.Dispatch.Should().NotBeNull("Second level (Dispatch) should be populated");
        dto.Dispatch!.Id.Should().Be(2);

        // Assert - Level 3
        dto.Dispatch.Customer.Should().NotBeNull("Third level (Customer) MUST be populated with inherited+lazy");
        dto.Dispatch.Customer!.Id.Should().Be(3);
        dto.Dispatch.Customer.Name.Should().Be("John Doe");
    }

    [Fact]
    public void Projection_NonNullableNested_ThreeLevelsDeep_ShouldPopulateAllLevels()
    {
        // Arrange
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

        // Act
        var projection = OrderDtoNonNull350.Projection.Compile();
        var dto = projection(entity);

        // Assert - Level 1
        dto.Id.Should().Be(1);
        dto.OrderNumber.Should().Be("ORD-001");

        // Assert - Level 2 (non-nullable)
        dto.Dispatch.Should().NotBeNull("Non-nullable Dispatch should be populated");
        dto.Dispatch!.Id.Should().Be(2);

        // Assert - Level 3 (non-nullable, critical)
        dto.Dispatch.Customer.Should().NotBeNull("Non-nullable third level MUST be populated");
        dto.Dispatch.Customer!.Id.Should().Be(3);
        dto.Dispatch.Customer.Name.Should().Be("Jane Smith");
        dto.Dispatch.Customer.Email.Should().Be("jane@example.com");
    }
}
