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

// Init-only properties test
public class AddressEntity
{
    public string AddressLine1 { get; init; } = string.Empty;
    public string City { get; init; } = string.Empty;
    public string PostalCode { get; init; } = string.Empty;
    public string Country { get; set; } = string.Empty;
}

public class AddressDto
{
    public string AddressLine1 { get; init; } = string.Empty;
    public string City { get; init; } = string.Empty;
    public string PostalCode { get; init; } = string.Empty;
    public string Country { get; set; } = string.Empty;
}

[FacetMap(typeof(AddressEntity), typeof(AddressDto), GenerateToSource = true)]
public static partial class AddressMappings { }

// Same target from different sources (Allan's exact scenario)
[FacetMap(typeof(UnitDto), typeof(UnitDropDownDto))]
[FacetMap(typeof(UnitEntity), typeof(UnitDropDownDto), GenerateToSource = true)]
public static partial class UnitDropDownMapper;

// ========================================
// Nested type mapping tests - different element types between source and target
// ========================================

// Nested entity types (source side)
public class ContactEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public CultureInfoEntity? CultureInfo { get; set; }
    public ICollection<ContactAddressEntity> Addresses { get; set; } = new List<ContactAddressEntity>();
}

public class CultureInfoEntity
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
}

public class ContactAddressEntity
{
    public int Id { get; set; }
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
}

// Nested DTO types (target side) - same property names but different types
public class ContactEntityDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public CultureInfoEntityDto? CultureInfo { get; set; }
    public ICollection<ContactAddressEntityDto> Addresses { get; set; } = new List<ContactAddressEntityDto>();
}

public class CultureInfoEntityDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
}

public class ContactAddressEntityDto
{
    public int Id { get; set; }
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
}

// Mappers for the nested types (these must exist for the extension method calls to resolve)
[FacetMap(typeof(CultureInfoEntity), typeof(CultureInfoEntityDto), GenerateToSource = true)]
public static partial class CultureInfoMapper;

[FacetMap(typeof(ContactAddressEntity), typeof(ContactAddressEntityDto), GenerateToSource = true)]
public static partial class ContactAddressMapper;

// The parent mapper that has properties with different types on source vs target
[FacetMap(typeof(ContactEntity), typeof(ContactEntityDto), GenerateToSource = true)]
public static partial class ContactMapper;

// ========================================
// IFacetProjectionMapConfiguration test
// ========================================
public class InvoiceEntity
{
    public int Id { get; set; }
    public string Number { get; set; } = string.Empty;
    public decimal SubTotal { get; set; }
    public decimal Tax { get; set; }
}

public class InvoiceDto
{
    public int Id { get; set; }
    public string Number { get; set; } = string.Empty;
    public decimal SubTotal { get; set; }
    public decimal Tax { get; set; }
    public decimal Total { get; set; }
}

public class InvoiceProjectionConfig : Facet.Mapping.IFacetProjectionMapConfiguration<InvoiceEntity, InvoiceDto>
{
    public static void ConfigureProjection(Facet.Mapping.IFacetProjectionBuilder<InvoiceEntity, InvoiceDto> builder)
    {
        builder.Map(target => target.Total, source => source.SubTotal + source.Tax);
    }
}

[FacetMap(typeof(InvoiceEntity), typeof(InvoiceDto), Configuration = typeof(InvoiceProjectionConfig))]
public static partial class InvoiceMappings;
