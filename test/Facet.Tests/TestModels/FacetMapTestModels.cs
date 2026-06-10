namespace Facet.Tests.TestModels.FacetMap;

// Source entities (simulating domain layer)
public class Customer
{
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; }
    public decimal Balance { get; set; }
}

public class Order
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public DateTime OrderDate { get; set; }
    public string InternalNotes { get; set; } = string.Empty;
    public List<OrderLine> Lines { get; set; } = new();
}

public class OrderLine
{
    public int Id { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}

// Target DTOs (simulating shared/contracts project - plain POCOs, no Facet dependency)
public class CustomerDto
{
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public decimal Balance { get; set; }
}

public class OrderDto
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public DateTime OrderDate { get; set; }
    public List<OrderLine> Lines { get; set; } = new();
}

public class OrderLineDto
{
    public int Id { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}

// Marker classes with [FacetMap] attribute
[FacetMap(typeof(Customer), typeof(CustomerDto), "PasswordHash", "CreatedAt", GenerateToSource = true)]
public static partial class CustomerMappings { }

[FacetMap(typeof(Order), typeof(OrderDto), "InternalNotes", GenerateToSource = true)]
public static partial class OrderMappings { }

[FacetMap(typeof(OrderLine), typeof(OrderLineDto), GenerateToSource = true, GenerateProjection = true)]
public static partial class OrderLineMappings { }

// Include-only test
public class SimpleEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Secret { get; set; } = string.Empty;
}

public class SimpleDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

[FacetMap(typeof(SimpleEntity), typeof(SimpleDto), Include = new[] { "Id", "Name" })]
public static partial class SimpleMappings { }

// Multiple FacetMap attributes on one class test
public class UnitEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
}

public class UnitDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
}

public class UnitDropDownDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

[FacetMap(typeof(UnitEntity), typeof(UnitDto))]
[FacetMap(typeof(UnitEntity), typeof(UnitDropDownDto), GenerateToSource = true)]
public static partial class UnitMapper { }

// BeforeMap/AfterMap configuration test
public class ProductEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
}

public class ProductDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string MappedBy { get; set; } = string.Empty;
    public bool WasValidated { get; set; }
}

public class ProductBeforeMapConfig : Facet.Mapping.IFacetBeforeMapConfiguration<ProductEntity, ProductDto>
{
    public static void BeforeMap(ProductEntity source, ProductDto target)
    {
        target.WasValidated = true;
    }
}

public class ProductAfterMapConfig : Facet.Mapping.IFacetAfterMapConfiguration<ProductEntity, ProductDto>
{
    public static void AfterMap(ProductEntity source, ProductDto target)
    {
        target.MappedBy = "AfterMapConfig";
    }
}

[FacetMap(typeof(ProductEntity), typeof(ProductDto),
    BeforeMapConfiguration = typeof(ProductBeforeMapConfig),
    AfterMapConfiguration = typeof(ProductAfterMapConfig))]
public static partial class ProductMappings { }
