using System;

namespace Facet.Tests.TestModels;

public class Person
{
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateTime DateOfBirth { get; set; }
    public Address Address { get; set; } = null!;
    public ContactInfo ContactInfo { get; set; } = null!;
}

public class Address
{
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string ZipCode { get; set; } = string.Empty;
    public Country Country { get; set; } = null!;
}

public class Country
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
}

public class ContactInfo
{
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
}

[Flatten(typeof(Person))]
public partial class PersonFlatDto
{
}

[Flatten(typeof(Person), MaxDepth = 2)]
public partial class PersonFlatDepth2Dto
{
}

[Flatten(typeof(Person), "ContactInfo")]
public partial class PersonFlatWithoutContactDto
{
}

[Flatten(typeof(Person), "Address.Country")]
public partial class PersonFlatWithoutCountryDto
{
}

[Flatten(typeof(Person), NamingStrategy = FlattenNamingStrategy.LeafOnly)]
public partial class PersonFlatLeafOnlyDto
{
}

public class Company
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Address HeadquartersAddress { get; set; } = null!;
    public List<Worker> Workers { get; set; } = new();
}

public class Worker
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public int CompanyId { get; set; }
}

[Flatten(typeof(Company))]
public partial class CompanyFlatDto
{
}

public class Order
{
    public int Id { get; set; }
    public DateTime OrderDate { get; set; }
    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public decimal Total { get; set; }
}

public class Customer
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int? PreferredAddressId { get; set; }
}

[Flatten(typeof(Order), IgnoreNestedIds = true)]
public partial class OrderFlatDto
{
}

[Flatten(typeof(Order), IgnoreNestedIds = false)]
public partial class OrderFlatWithAllIdsDto
{
}

public class PersonWithFk
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int? AddressId { get; set; } 
    public AddressWithId? Address { get; set; } 
}

public class AddressWithId
{
    public int Id { get; set; }
    public string Line1 { get; set; } = string.Empty;
    public string Line2 { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
}

[Flatten(typeof(PersonWithFk), IgnoreForeignKeyClashes = true)]
public partial class PersonWithFkFlatDto
{
}

[Flatten(typeof(PersonWithFk), IgnoreForeignKeyClashes = false)]
public partial class PersonWithFkFlatNoIgnoreDto
{
}

public class OrderWithFks
{
    public int Id { get; set; }
    public DateTime OrderDate { get; set; }
    public int CustomerId { get; set; } 
    public CustomerWithAddress Customer { get; set; } = null!;
    public int? ShippingAddressId { get; set; } 
    public AddressWithId? ShippingAddress { get; set; }
}

public class CustomerWithAddress
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int? HomeAddressId { get; set; } 
    public AddressWithId? HomeAddress { get; set; }
}

[Flatten(typeof(OrderWithFks), IgnoreForeignKeyClashes = true)]
public partial class OrderWithFksFlatDto
{
}

public class Position
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class ItemType
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class ExtendedData
{
    public int Id { get; set; }
    public int PositionId { get; set; }
    public Position Position { get; set; } = null!;
    public int TypeId { get; set; }
    public ItemType Type { get; set; } = null!;
}

public class DataItem
{
    public int Id { get; set; }
    public string DataValue { get; set; } = string.Empty;
    public int ExtendedDataId { get; set; }
    public ExtendedData ExtendedData { get; set; } = null!;
}

[Flatten(typeof(DataItem), NamingStrategy = FlattenNamingStrategy.SmartLeaf, IgnoreNestedIds = true)]
public partial class DataItemSmartLeafDto
{
}

[Flatten(typeof(DataItem), NamingStrategy = FlattenNamingStrategy.LeafOnly, IgnoreNestedIds = true)]
public partial class DataItemLeafOnlyDto
{
}

public class CatalogProduct
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
}

public class CatalogCategory
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
}

public class ProductCatalog
{
    public int Id { get; set; }
    public CatalogProduct Product { get; set; } = null!;
    public CatalogCategory Category { get; set; } = null!;
}

[Flatten(typeof(ProductCatalog), NamingStrategy = FlattenNamingStrategy.SmartLeaf, IgnoreNestedIds = true)]
public partial class ProductCatalogSmartLeafDto
{
}

public class DataEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ICollection<ExtendedEntity>? Extended { get; set; }
}

public class ExtendedEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int DataValue { get; set; }
}

[Facet(typeof(ExtendedEntity))]
public partial class ExtendedFacet;

[Facet(typeof(DataEntity), NestedFacets = [typeof(ExtendedFacet)], FlattenTo = [typeof(DataFlattenedDto)])]
public partial class DataFacet;

public partial class DataFlattenedDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    
    // Note: ExtendedName avoids collision with parent's Name property
    public string ExtendedName { get; set; } = string.Empty;
    public int DataValue { get; set; }
}

public class DataTableEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public ICollection<DataExtendedLookupEntity> ExtendedLookups { get; set; } = new List<DataExtendedLookupEntity>();
}

public class DataExtendedLookupEntity
{
    public int Id { get; set; }
    public int DataId { get; set; }
    public DataTableEntity Data { get; set; } = null!;
    public int ExtendedId { get; set; }
    public DataExtendedEntity Extended { get; set; } = null!;
}

public class DataExtendedEntity
{
    public int Id { get; set; }
    public string ExtendedValue { get; set; } = string.Empty;
    public int NumericValue { get; set; }
}

[Facet(typeof(DataExtendedEntity))]
public partial class DataExtendedFacet;

[Facet(typeof(DataExtendedLookupEntity),
    NestedFacets = [typeof(DataExtendedFacet)])]
public partial class DataExtendedLookupFacet;

[Facet(typeof(DataTableEntity),
    NestedFacets = [typeof(DataExtendedLookupFacet)],
    FlattenTo = [typeof(DataTableFlattenedDto)])]
public partial class DataTableFacet;

public partial class DataTableFlattenedDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    public int DataId { get; set; }
    public int ExtendedId { get; set; }

    public string ExtendedValue { get; set; } = string.Empty;
    public int NumericValue { get; set; }
}

public class ProductCatalogEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public ICollection<ProductLookupEntity> ProductLookups { get; set; } = new List<ProductLookupEntity>();
}

public class ProductLookupEntity
{
    public int Id { get; set; }
    public CategoryEntity Category { get; set; } = null!;
    public SupplierEntity Supplier { get; set; } = null!;
    public BrandEntity Brand { get; set; } = null!;
}

public class CategoryEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;  
    public string Code { get; set; } = string.Empty;
}

public class SupplierEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;  
    public string ContactEmail { get; set; } = string.Empty;
}

public class BrandEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;  
    public string Country { get; set; } = string.Empty;
}

[Facet(typeof(CategoryEntity))]
public partial class CategoryFacet;

[Facet(typeof(SupplierEntity))]
public partial class SupplierFacet;

[Facet(typeof(BrandEntity))]
public partial class BrandFacet;

[Facet(typeof(ProductLookupEntity),
    NestedFacets = [typeof(CategoryFacet), typeof(SupplierFacet), typeof(BrandFacet)])]
public partial class ProductLookupFacet;

[Facet(typeof(ProductCatalogEntity),
    NestedFacets = [typeof(ProductLookupFacet)],
    FlattenTo = [typeof(ProductCatalogFlattenedDto)])]
public partial class ProductCatalogFacet;

public partial class ProductCatalogFlattenedDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    public string CategoryName { get; set; } = string.Empty;      
    public string Code { get; set; } = string.Empty;              

    public string SupplierName { get; set; } = string.Empty;      
    public string ContactEmail { get; set; } = string.Empty;      

    public string BrandName { get; set; } = string.Empty;         
    public string Country { get; set; } = string.Empty;           
}

public class ApiResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public ResponseMetadata Metadata { get; set; } = null!;
    public List<ResponseItem> Items { get; set; } = new();
    public string[] Tags { get; set; } = Array.Empty<string>();
}

public class ResponseMetadata
{
    public DateTime CreatedAt { get; set; }
    public string Version { get; set; } = string.Empty;
}

public class ResponseItem
{
    public int ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public decimal Price { get; set; }
}

[Flatten(typeof(ApiResponse), IncludeCollections = true)]
public partial class ApiResponseFlatWithCollectionsDto
{
}

[Flatten(typeof(ApiResponse))]
public partial class ApiResponseFlatWithoutCollectionsDto
{
}

public class EntityWithVariousCollections
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public IEnumerable<string> Emails { get; set; } = Enumerable.Empty<string>();
    public ICollection<int> Numbers { get; set; } = new List<int>();
    public IList<DateTime> Dates { get; set; } = new List<DateTime>();
    public HashSet<string> UniqueValues { get; set; } = new();
}

[Flatten(typeof(EntityWithVariousCollections), IncludeCollections = true)]
public partial class EntityWithVariousCollectionsFlatDto
{
}

[Flatten(typeof(Company), Exclude = [nameof(@Company.HeadquartersAddress.ZipCode)])]
public partial class CompanyFlatWithNameOfExcludeDto
{
}

[Flatten(typeof(Person), Exclude = [nameof(@Person.Address.Country)])]
public partial class PersonFlatWithNameOfExcludeDto
{
}

