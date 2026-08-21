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

// ========================================
// Enum conversion tests - source has enum, target has string/int
// ========================================
public enum OrderStatus
{
    Pending = 0,
    Processing = 1,
    Shipped = 2,
    Delivered = 3,
    Cancelled = 4
}

public class OrderWithEnumEntity
{
    public int Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public OrderStatus Status { get; set; }
    public OrderStatus? NullableStatus { get; set; }
}

// Target with string representation of enum
public class OrderWithEnumAsStringDto
{
    public int Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? NullableStatus { get; set; }
}

// Target with int representation of enum
public class OrderWithEnumAsIntDto
{
    public int Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public int Status { get; set; }
    public int? NullableStatus { get; set; }
}

[FacetMap(typeof(OrderWithEnumEntity), typeof(OrderWithEnumAsStringDto), GenerateToSource = true)]
public static partial class OrderEnumToStringMapper;

[FacetMap(typeof(OrderWithEnumEntity), typeof(OrderWithEnumAsIntDto), GenerateToSource = true)]
public static partial class OrderEnumToIntMapper;

// ========================================
// MapFrom tests - [MapFrom] on target property
// ========================================
public class PersonEntity
{
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

public class PersonSummaryDto
{
    public int Id { get; set; }
    [MapFrom("FirstName")]
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

[FacetMap(typeof(PersonEntity), typeof(PersonSummaryDto))]
public static partial class PersonSummaryMapper;

// ========================================
// MapWhen tests - [MapWhen] on target property
// ========================================
public class SensorEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public double Value { get; set; }
    public bool IsActive { get; set; }
}

public class SensorDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    [MapWhen("IsActive")]
    public double Value { get; set; }
    public bool IsActive { get; set; }
}

[FacetMap(typeof(SensorEntity), typeof(SensorDto))]
public static partial class SensorMapper;

// ========================================
// Nullable value type conversion tests - source has nullable, target has non-nullable (and vice versa)
// ========================================
public class NullableSourceEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int? NullableCount { get; set; }
    public decimal? NullableAmount { get; set; }
    public DateTime? NullableDate { get; set; }
    public bool? NullableFlag { get; set; }
}

public class NonNullableTargetDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int NullableCount { get; set; }
    public decimal NullableAmount { get; set; }
    public DateTime NullableDate { get; set; }
    public bool NullableFlag { get; set; }
}

// Reverse: source has non-nullable, target has nullable
public class NonNullableSourceEntity
{
    public int Id { get; set; }
    public int Count { get; set; }
    public decimal Amount { get; set; }
}

public class NullableTargetDto
{
    public int Id { get; set; }
    public int? Count { get; set; }
    public decimal? Amount { get; set; }
}

[FacetMap(typeof(NullableSourceEntity), typeof(NonNullableTargetDto), GenerateToSource = true)]
public static partial class NullableToNonNullableMapper;

[FacetMap(typeof(NonNullableSourceEntity), typeof(NullableTargetDto), GenerateToSource = true)]
public static partial class NonNullableToNullableMapper;

// ========================================
// Incompatible type tests - properties with same name but incompatible types should be skipped
// ========================================
public class PrinterSettingsType
{
    public string Format { get; set; } = string.Empty;
    public int Copies { get; set; }
}

public class EntityWithStringProp
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string PrinterSettings { get; set; } = string.Empty;
}

public class DtoWithComplexProp
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public PrinterSettingsType PrinterSettings { get; set; } = new();
}

[FacetMap(typeof(EntityWithStringProp), typeof(DtoWithComplexProp), GenerateToSource = true)]
public static partial class IncompatibleTypeMapper;

// ========================================
// Dictionary property tests - Dictionary properties should be handled correctly
// ========================================
public class EntityWithDictionary
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Dictionary<string, int> Metadata { get; set; } = new();
}

public class DtoWithDictionary
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Dictionary<string, int> Metadata { get; set; } = new();
}

// Same dictionary types should map fine
[FacetMap(typeof(EntityWithDictionary), typeof(DtoWithDictionary), GenerateToSource = true)]
public static partial class DictionaryMapper;

// Different dictionary value types should be skipped
public class EntityWithDifferentDict
{
    public int Id { get; set; }
    public Dictionary<string, int> Scores { get; set; } = new();
}

public class DtoWithDifferentDict
{
    public int Id { get; set; }
    public Dictionary<string, string> Scores { get; set; } = new();
}

[FacetMap(typeof(EntityWithDifferentDict), typeof(DtoWithDifferentDict))]
public static partial class DifferentDictMapper;

// ========================================
// Test FacetMap with IFacetProjectionMapConfiguration where NO properties auto-match
// All mapping done via builder.Map() with nested projection
// ========================================
public class WarehouseItemEntity
{
    public int ItemId { get; set; }
    public PackingLevelEntity PackingLevel { get; set; } = new();
    public ProductionLineEntity? ProductionLine { get; set; }
}

public class PackingLevelEntity
{
    public int TotalLabels { get; set; }
}

public class ProductionLineEntity
{
    public int? MultiRangeIntakeRef { get; set; }
    public int? TransferRef { get; set; }
}

public class WarehouseItemDto
{
    public int Id { get; set; }
    public int LabelCount { get; set; }
    public int? IntakeReference { get; set; }
    public int? TransferReference { get; set; }
}

public class WarehouseItemMapConfig : Facet.Mapping.IFacetProjectionMapConfiguration<WarehouseItemEntity, WarehouseItemDto>
{
    public static void ConfigureProjection(Facet.Mapping.IFacetProjectionBuilder<WarehouseItemEntity, WarehouseItemDto> builder)
    {
        builder.Map(target => target.Id, source => source.ItemId);
        builder.Map(target => target.LabelCount, source => source.PackingLevel.TotalLabels);
        builder.Map(target => target.IntakeReference, source => source.ProductionLine != null ? source.ProductionLine.MultiRangeIntakeRef : null);
        builder.Map(target => target.TransferReference, source => source.ProductionLine != null ? source.ProductionLine.TransferRef : null);
    }
}

[FacetMap(typeof(WarehouseItemEntity), typeof(WarehouseItemDto), Configuration = typeof(WarehouseItemMapConfig), GenerateProjection = true)]
public static partial class WarehouseItemMapper;

// ========================================
// DDD-style composite retrieval DTO with nested complex objects
// Tests the scenario where:
// 1. Multiple DTOs map from the same source entity
// 2. A composite "retrieval" DTO contains nested DTOs as properties
// 3. The composite mapper uses IFacetProjectionMapConfiguration to reference other mappers' projections
// ========================================

// Source entity
public class InventoryItemEntity420
{
    public long Id { get; set; }
    public string Identifier { get; set; } = string.Empty;
    public int? Count { get; set; }
    public decimal? WeightInKg { get; set; }
    public DateTime ProducedTime { get; set; }
    public DateTime? OverrideProducedTime { get; set; }
    public string StoreroomLocationNumber { get; set; } = string.Empty;
    public long StoreroomId { get; set; }
}

// DTO 1: basic item DTO
public class InventoryItemDto420
{
    public long Id { get; set; }
    public string Identifier { get; set; } = string.Empty;
    public int? Count { get; set; }
    public decimal? WeightInKg { get; set; }
    public DateTime ProducedTime { get; set; }
}

// DTO 2: overview DTO with computed fields
public class InventoryManagerOverviewDto420
{
    public string Identifier { get; set; } = string.Empty;
    public int? Count { get; set; }
    public string StoreroomLocationNumber { get; set; } = string.Empty;
    public long StoreroomId { get; set; }
    public DateTime ProducedTime { get; set; }
}

// Composite retrieval DTO: contains both nested DTOs
public class InventoryRetrievalDto420
{
    public InventoryItemDto420 InventoryItemDto { get; set; } = null!;
    public InventoryManagerOverviewDto420 InventoryManagerOverviewDto { get; set; } = null!;
}

// Config for DTO 1 mapper
public class InventoryItemDto420MapConfig : Facet.Mapping.IFacetProjectionMapConfiguration<InventoryItemEntity420, InventoryItemDto420>
{
    public static void ConfigureProjection(Facet.Mapping.IFacetProjectionBuilder<InventoryItemEntity420, InventoryItemDto420> builder)
    {
        builder.Map(target => target.ProducedTime, source => source.OverrideProducedTime ?? source.ProducedTime);
    }
}

// Mapper for DTO 1
[FacetMap(typeof(InventoryItemEntity420), typeof(InventoryItemDto420), Configuration = typeof(InventoryItemDto420MapConfig), GenerateProjection = true)]
public static partial class InventoryItemDto420Mapper;

// Config for DTO 2 mapper
public class InventoryManagerOverviewDto420MapConfig : Facet.Mapping.IFacetProjectionMapConfiguration<InventoryItemEntity420, InventoryManagerOverviewDto420>
{
    public static void ConfigureProjection(Facet.Mapping.IFacetProjectionBuilder<InventoryItemEntity420, InventoryManagerOverviewDto420> builder)
    {
        builder.Map(target => target.ProducedTime, source => source.OverrideProducedTime ?? source.ProducedTime);
    }
}

// Mapper for DTO 2
[FacetMap(typeof(InventoryItemEntity420), typeof(InventoryManagerOverviewDto420), Configuration = typeof(InventoryManagerOverviewDto420MapConfig), GenerateProjection = true)]
public static partial class InventoryManagerOverviewDto420Mapper;

// Config for composite retrieval mapper - references other mappers' projections directly
public class InventoryRetrievalDto420MapConfig : Facet.Mapping.IFacetProjectionMapConfiguration<InventoryItemEntity420, InventoryRetrievalDto420>
{
    public static void ConfigureProjection(Facet.Mapping.IFacetProjectionBuilder<InventoryItemEntity420, InventoryRetrievalDto420> builder)
    {
        // Pass projection from other mapper directly - this is the DDD pattern
        builder.Map(target => target.InventoryItemDto, InventoryItemDto420Mapper.InventoryItemEntity420ToInventoryItemDto420Projection);
        builder.Map(target => target.InventoryManagerOverviewDto, InventoryManagerOverviewDto420Mapper.InventoryItemEntity420ToInventoryManagerOverviewDto420Projection);
    }
}

// Mapper for composite retrieval DTO
[FacetMap(typeof(InventoryItemEntity420), typeof(InventoryRetrievalDto420), Configuration = typeof(InventoryRetrievalDto420MapConfig), GenerateProjection = true)]
public static partial class InventoryRetrievalDto420Mapper;
