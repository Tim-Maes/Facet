# Flatten Attribute Reference

The `[Flatten]` attribute automatically generates a flattened projection of a source type, expanding all nested properties into top-level properties. This is particularly useful for API responses, reporting, and scenarios where you need a denormalized view of your data.

## What is Flattening?

Flattening transforms a hierarchical object structure into a flat structure with all nested properties as top-level properties. Instead of manually writing DTOs with properties like `AddressStreet`, `AddressCity`, etc., Facet generates them automatically by traversing your domain model.

### Example

**Before (Domain Model):**
```csharp
public class Person
{
    public string FirstName { get; set; }
    public Address Address { get; set; }
}

public class Address
{
    public string Street { get; set; }
    public string City { get; set; }
}
```

**After (Flattened DTO):**
```csharp
[Flatten(typeof(Person))]
public partial class PersonFlatDto
{
    // Auto-generated:
    // public string FirstName { get; set; }
    // public string AddressStreet { get; set; }
    // public string AddressCity { get; set; }
}
```

## Key Features

- **Automatic Property Discovery**: Recursively traverses nested objects
- **Null-Safe Access**: Uses null-conditional operators (`?.`) in constructors
- **LINQ Projection**: Generates `Projection` expression for Entity Framework
- **Depth Control**: Configurable maximum traversal depth
- **Exclusion Paths**: Exclude specific nested properties
- **ID Filtering**: Optional `IgnoreNestedIds` to exclude foreign keys and nested IDs
- **FK Clash Detection**: Optional `IgnoreForeignKeyClashes` to eliminate duplicate foreign key data
- **Naming Strategies**: Prefix or leaf-only naming
- **One-Way Operation**: Flattening is intentionally one-way (no ToSource method)

## Usage

### Basic Flattening

```csharp
[Flatten(typeof(Person))]
public partial class PersonFlatDto
{
    // All properties auto-generated
}

// Usage
var person = new Person
{
    FirstName = "John",
    Address = new Address { Street = "123 Main St", City = "Springfield" }
};

var dto = new PersonFlatDto(person);
// dto.FirstName = "John"
// dto.AddressStreet = "123 Main St"
// dto.AddressCity = "Springfield"
```

### LINQ Projection for Entity Framework

```csharp
// Efficient database projection
var dtos = await dbContext.People
    .Where(p => p.IsActive)
    .Select(PersonFlatDto.Projection)
    .ToListAsync();
```

### Multi-Level Nesting

```csharp
public class Person
{
    public string FirstName { get; set; }
    public Address Address { get; set; }
}

public class Address
{
    public string Street { get; set; }
    public Country Country { get; set; }
}

public class Country
{
    public string Name { get; set; }
    public string Code { get; set; }
}

[Flatten(typeof(Person))]
public partial class PersonFlatDto
{
    // Auto-generated:
    // public string FirstName { get; set; }
    // public string AddressStreet { get; set; }
    // public string AddressCountryName { get; set; }
    // public string AddressCountryCode { get; set; }
}
```

## Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `sourceType` | `Type` | *(required)* | The source type to flatten from. |
| `exclude` | `string[]` | `null` | Property paths to exclude from flattening (e.g., `"Address.Country"`, `"Password"`). |
| `Exclude` | `string[]` | `null` | Same as `exclude` (named property). |
| `MaxDepth` | `int` | `3` | Maximum depth to traverse when flattening nested objects. Set to `0` for unlimited (not recommended). |
| `NamingStrategy` | `FlattenNamingStrategy` | `Prefix` | Naming strategy for flattened properties. |
| `IncludeFields` | `bool` | `false` | Include public fields in addition to properties. |
| `GenerateParameterlessConstructor` | `bool` | `true` | Generate a parameterless constructor for object initialization. |
| `GenerateProjection` | `bool` | `true` | Generate a LINQ projection expression for database queries. |
| `UseFullName` | `bool` | `false` | Use fully qualified type name in generated file names to avoid collisions. |
| `IgnoreNestedIds` | `bool` | `false` | When true, only keeps the root-level `Id` property and excludes all foreign key IDs and nested IDs. |
| `IgnoreForeignKeyClashes` | `bool` | `false` | When true, automatically skips nested ID and FK properties that would duplicate foreign key data. |

## Naming Strategies

### Prefix Strategy (Default)

Concatenates the full path to create the property name:

```csharp
[Flatten(typeof(Person), NamingStrategy = FlattenNamingStrategy.Prefix)]
public partial class PersonFlatDto { }

// Generated properties:
// - FirstName
// - AddressStreet
// - AddressCity
// - AddressCountryName
```

### LeafOnly Strategy

Uses only the leaf property name:

```csharp
[Flatten(typeof(Person), NamingStrategy = FlattenNamingStrategy.LeafOnly)]
public partial class PersonFlatDto { }

// Generated properties:
// - FirstName
// - Street
// - City
// - Name // Warning: Can cause name collisions!
```

**Warning**: `LeafOnly` can cause name collisions if multiple nested objects have properties with the same name. Facet will automatically add numeric suffixes (e.g., `Name2`, `Name3`) to resolve collisions.

## Excluding Properties

### Exclude Top-Level Properties

```csharp
[Flatten(typeof(Person), exclude: "DateOfBirth", "InternalNotes")]
public partial class PersonFlatDto { }
```

### Exclude Nested Properties

Use dot notation to exclude specific nested paths:

```csharp
[Flatten(typeof(Person), exclude: "Address.Country")]
public partial class PersonFlatDto
{
    // Generated:
    // - FirstName
    // - AddressStreet
    // - AddressCity
    // (Country properties excluded)
}
```

### Exclude Entire Nested Objects

```csharp
[Flatten(typeof(Person), exclude: "ContactInfo")]
public partial class PersonFlatDto
{
    // ContactInfo and all its nested properties excluded
}
```

## Ignoring Nested IDs

The `IgnoreNestedIds` parameter provides a convenient way to exclude all ID properties except the root-level `Id`. This is particularly useful for API responses where you want to display data but don't need all the foreign key IDs and nested entity IDs.

### Without IgnoreNestedIds (Default)

```csharp
public class Order
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public Customer Customer { get; set; }
    public DateTime OrderDate { get; set; }
}

public class Customer
{
    public int Id { get; set; }
    public string Name { get; set; }
}

[Flatten(typeof(Order))]
public partial class OrderFlatDto
{
    // Generated:
    // public int Id { get; set; }
    // public int CustomerId { get; set; }        // Foreign key from root
    // public int CustomerId2 { get; set; }       // Customer.Id (name collision, gets suffix)
    // public string CustomerName { get; set; }
    // public DateTime OrderDate { get; set; }
}
```

### With IgnoreNestedIds

```csharp
[Flatten(typeof(Order), IgnoreNestedIds = true)]
public partial class OrderFlatDto
{
    // Generated:
    // public int Id { get; set; }                // Root-level Id preserved
    // public string CustomerName { get; set; }
    // public DateTime OrderDate { get; set; }
    // (CustomerId and CustomerId2/Customer.Id excluded)
}
```

### Behavior Rules

1. **Root-level `Id` is always kept**: The top-level `Id` property of the source type is included
2. **Foreign key IDs are excluded**: Properties like `CustomerId`, `ProductId` are excluded at the root level
3. **All nested IDs are excluded**: Any `Id` property from nested objects is excluded

### Use Cases

- **API responses** where you don't want to expose database keys
- **Reports** where IDs clutter the output
- **Search results** where you only need the primary ID for navigation
- **Export files** where human-readable data is preferred over foreign keys

### Example: Clean API Response

```csharp
public class Product
{
    public int Id { get; set; }
    public string Name { get; set; }
    public int CategoryId { get; set; }
    public Category Category { get; set; }
    public int ManufacturerId { get; set; }
    public Manufacturer Manufacturer { get; set; }
}

public class Category
{
    public int Id { get; set; }
    public string Name { get; set; }
}

public class Manufacturer
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Country { get; set; }
}

[Flatten(typeof(Product), IgnoreNestedIds = true)]
public partial class ProductDisplayDto
{
    // Generated:
    // public int Id { get; set; }                    // Root ID kept
    // public string Name { get; set; }
    // public string CategoryName { get; set; }       // Category.Id excluded
    // public string ManufacturerName { get; set; }   // Manufacturer.Id excluded
    // public string ManufacturerCountry { get; set; }
    // (CategoryId and ManufacturerId excluded at root)
}

// Clean API response without exposing internal IDs
[HttpGet("products/{id}")]
public async Task<IActionResult> GetProduct(int id)
{
    var product = await dbContext.Products
        .Where(p => p.Id == id)
        .Select(ProductDisplayDto.Projection)
        .FirstOrDefaultAsync();

    return Ok(product);
    // Response: { "id": 1, "name": "Widget", "categoryName": "Tools",
    //             "manufacturerName": "ACME", "manufacturerCountry": "USA" }
}
```

## Ignoring Foreign Key Clashes

The `IgnoreForeignKeyClashes` parameter helps eliminate duplicate ID data when flattening entities with foreign key relationships. When enabled, it automatically detects and skips properties that would represent the same data as a foreign key property.

### The Problem: Foreign Key Duplication

In Entity Framework models with foreign keys and navigation properties, you often have both:
1. A foreign key property (e.g., `AddressId`)
2. A navigation property (e.g., `Address`)
3. The referenced entity's ID (e.g., `Address.Id`)

When flattening, both `AddressId` and `Address.Id` become `AddressId`, causing naming collisions and representing the same data twice.

### Without IgnoreForeignKeyClashes (Default)

```csharp
public class Person
{
    public int Id { get; set; }
    public string Name { get; set; }
    public int? AddressId { get; set; }  // Foreign key
    public Address Address { get; set; }  // Navigation property
}

public class Address
{
    public int Id { get; set; }
    public string Line1 { get; set; }
    public string City { get; set; }
}

[Flatten(typeof(Person))]
public partial class PersonFlatDto
{
    // Generated:
    // public int Id { get; set; }
    // public string Name { get; set; }
    // public int? AddressId { get; set; }        // FK property
    // public int? AddressId2 { get; set; }       // Address.Id (collision!)
    // public string AddressLine1 { get; set; }
    // public string AddressCity { get; set; }
}
```

### With IgnoreForeignKeyClashes

```csharp
[Flatten(typeof(Person), IgnoreForeignKeyClashes = true)]
public partial class PersonFlatDto
{
    // Generated:
    // public int Id { get; set; }
    // public string Name { get; set; }
    // public int? AddressId { get; set; }     // FK property kept
    // public string AddressLine1 { get; set; }
    // public string AddressCity { get; set; }
    // (Address.Id is skipped - would be duplicate of AddressId)
}
```

### Behavior Rules

1. **Detects FK patterns**: Identifies properties ending with "Id" that have a matching navigation property
2. **Skips nested IDs**: When a navigation property is flattened, its `Id` property is skipped if it would match a FK
3. **Skips nested FKs**: Foreign keys within nested objects are also skipped to avoid deep duplicates
4. **Preserves root FKs**: Foreign keys at the root level are always included
5. **Works at all depths**: Handles complex scenarios like `Customer.HomeAddressId` and `Customer.HomeAddress.Id`

### Example: Complex Nested Foreign Keys

```csharp
public class Order
{
    public int Id { get; set; }
    public DateTime OrderDate { get; set; }
    public int CustomerId { get; set; }           // Root FK
    public Customer Customer { get; set; }
    public int? ShippingAddressId { get; set; }   // Root FK
    public Address ShippingAddress { get; set; }
}

public class Customer
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
    public int? HomeAddressId { get; set; }       // Nested FK
    public Address HomeAddress { get; set; }
}

public class Address
{
    public int Id { get; set; }
    public string Line1 { get; set; }
    public string City { get; set; }
}

[Flatten(typeof(Order), IgnoreForeignKeyClashes = true)]
public partial class OrderFlatDto
{
    // Generated:
    // public int Id { get; set; }
    // public DateTime OrderDate { get; set; }
    // public int CustomerId { get; set; }                 // Root FK included
    // public string CustomerName { get; set; }
    // public string CustomerEmail { get; set; }
    // public string CustomerHomeAddressLine1 { get; set; }
    // public string CustomerHomeAddressCity { get; set; }
    // public int? ShippingAddressId { get; set; }        // Root FK included
    // public string ShippingAddressLine1 { get; set; }
    // public string ShippingAddressCity { get; set; }
    //
    // Skipped (would be duplicates):
    // - Customer.Id (would clash with CustomerId)
    // - Customer.HomeAddressId (nested FK)
    // - Customer.HomeAddress.Id (would clash with CustomerHomeAddressId)
    // - ShippingAddress.Id (would clash with ShippingAddressId)
}
```

### Use Cases

- **Entity Framework models** with explicit foreign key properties
- **API responses** that don't need duplicate ID data
- **Cleaner DTOs** without naming collisions from IDs
- **Database-first models** that follow FK conventions

### Combining with IgnoreNestedIds

You can use both `IgnoreNestedIds` and `IgnoreForeignKeyClashes` together:

```csharp
[Flatten(typeof(Order), IgnoreNestedIds = true, IgnoreForeignKeyClashes = true)]
public partial class OrderDisplayDto
{
    // This combination:
    // 1. Ignores ALL ID properties except root (IgnoreNestedIds)
    // 2. No FK clashes to worry about since FKs are also IDs (both work together)
    //
    // Generated:
    // public int Id { get; set; }              // Root ID only
    // public DateTime OrderDate { get; set; }
    // public string CustomerName { get; set; }
    // public string CustomerEmail { get; set; }
    // (All foreign keys and IDs excluded)
}
```

**Note**: When using both together, `IgnoreNestedIds` takes precedence since it removes all ID properties (which includes FKs). However, `IgnoreForeignKeyClashes` provides more granular control if you want to keep root-level FKs while avoiding clash duplicates.

## Controlling Depth

### Default Depth (3 Levels)

```csharp
[Flatten(typeof(Person))]
public partial class PersonFlatDto { }
// Traverses up to 3 levels deep
```

### Custom Depth

```csharp
[Flatten(typeof(Person), MaxDepth = 2)]
public partial class PersonFlatDto { }
// Only traverses 2 levels deep
// Person.Address.Country would NOT be included
```

### Safety Limit

Even with `MaxDepth = 0` (unlimited), Facet enforces a safety limit of 10 levels to prevent stack overflow from circular references or extremely deep hierarchies.

## What Gets Flattened?

Facet automatically determines which types should be flattened as "leaf" properties and which should be recursed into:

### Always Flattened (Leaf Properties)
- Primitive types (`int`, `bool`, `decimal`, etc.)
- `string`
- Enums
- `DateTime`, `DateTimeOffset`, `TimeSpan`
- `Guid`
- Value types with 0-2 properties

### Recursed Into (Nested Objects)
- Complex reference types with properties
- Value types with 3+ properties

### Completely Ignored
- Collections (Lists, Arrays, IEnumerable, etc.) - These are skipped entirely and don't generate any flattened properties

## Null Handling

Flattened DTOs use null-conditional operators for safe access:

```csharp
// Generated constructor
public PersonFlatDto(Person source)
{
    this.FirstName = source.FirstName;
    this.AddressStreet = source.Address?.Street;
    this.AddressCity = source.Address?.City;
    this.AddressCountryName = source.Address?.Country?.Name;
}
```

This means null nested objects won't throw exceptions:

```csharp
var person = new Person { FirstName = "John", Address = null };
var dto = new PersonFlatDto(person);
// dto.AddressStreet is null (not an exception)
```

## Projection Expressions

Flattened types generate a static `Projection` property for efficient database queries:

```csharp
// Generated code
public static Expression<Func<Person, PersonFlatDto>> Projection =>
    source => new PersonFlatDto
    {
        FirstName = source.FirstName,
        AddressStreet = source.Address.Street,
        AddressCity = source.Address.City
    };

// Usage with Entity Framework
var dtos = await dbContext.People
    .Where(p => p.IsActive)
    .Select(PersonFlatDto.Projection)
    .ToListAsync();
```

## Why No ToSource Method?

Unlike the `[Facet]` attribute, flattened types **do not** generate `ToSource` methods. This is intentional because:

1. **Ambiguity**: It's unclear which flattened properties map to which nested objects
2. **Data Loss**: Flattening is lossy - you can't reliably reconstruct the original hierarchy
3. **Intent**: Flattening is designed for read-only projections (API responses, reports)

If you need bidirectional mapping, use the `[Facet]` attribute with `NestedFacets` instead.

## Complete Examples

### API Response DTO

```csharp
public class Order
{
    public int Id { get; set; }
    public DateTime OrderDate { get; set; }
    public Customer Customer { get; set; }
    public Address ShippingAddress { get; set; }
    public decimal Total { get; set; }
}

public class Customer
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
}

public class Address
{
    public string Street { get; set; }
    public string City { get; set; }
    public string State { get; set; }
    public string ZipCode { get; set; }
}

[Flatten(typeof(Order))]
public partial class OrderListDto
{
    // Auto-generated:
    // public int Id { get; set; }
    // public DateTime OrderDate { get; set; }
    // public int CustomerId { get; set; }
    // public string CustomerName { get; set; }
    // public string CustomerEmail { get; set; }
    // public string ShippingAddressStreet { get; set; }
    // public string ShippingAddressCity { get; set; }
    // public string ShippingAddressState { get; set; }
    // public string ShippingAddressZipCode { get; set; }
    // public decimal Total { get; set; }
}

// API Usage
[HttpGet("orders")]
public async Task<IActionResult> GetOrders()
{
    var orders = await dbContext.Orders
        .Where(o => o.Status == OrderStatus.Completed)
        .Select(OrderListDto.Projection)
        .ToListAsync();

    return Ok(orders);
}
```

### Report Generation

```csharp
public class Employee
{
    public int Id { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public Department Department { get; set; }
    public Address HomeAddress { get; set; }
    public decimal Salary { get; set; }
}

public class Department
{
    public string Name { get; set; }
    public string Code { get; set; }
    public Manager Manager { get; set; }
}

public class Manager
{
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string Email { get; set; }
}

[Flatten(typeof(Employee), exclude: "Salary")] // Exclude sensitive data
public partial class EmployeeReportDto { }

// Generate report
var report = await dbContext.Employees
    .Select(EmployeeReportDto.Projection)
    .ToListAsync();

await GenerateExcelReport(report);
```

### Limiting Depth for Performance

```csharp
// Deep hierarchy
public class Organization
{
    public string Name { get; set; }
    public Location Headquarters { get; set; }
}

public class Location
{
    public string Name { get; set; }
    public Address Address { get; set; }
}

public class Address
{
    public string Street { get; set; }
    public City City { get; set; }
}

public class City
{
    public string Name { get; set; }
    public State State { get; set; }
}

public class State
{
    public string Name { get; set; }
    public Country Country { get; set; }
}

public class Country
{
    public string Name { get; set; }
    public string Code { get; set; }
}

// Limit depth to 3 levels
[Flatten(typeof(Organization), MaxDepth = 3)]
public partial class OrganizationSummaryDto
{
    // Includes:
    // - Name
    // - HeadquartersName
    // - HeadquartersAddressStreet
    // Does NOT include City, State, Country (beyond depth 3)
}
```

## Best Practices

1. **Use for Read-Only Scenarios**: Flattening is ideal for API responses, reports, and display models
2. **Set Appropriate MaxDepth**: Don't flatten more than you need - it affects both performance and readability
3. **Exclude Sensitive Data**: Use `exclude` parameter to omit passwords, salaries, or internal fields
4. **Consider IgnoreNestedIds**: For public APIs and reports, use `IgnoreNestedIds = true` to avoid exposing database implementation details
5. **Prefer Prefix Naming**: Avoid `LeafOnly` unless you're certain there won't be name collisions
6. **Combine with LINQ**: Use the `Projection` property for efficient database queries
7. **Document Flattened Types**: Add XML comments to explain what was flattened and why

## Common Patterns

### Search Results

```csharp
[Flatten(typeof(SearchResult), MaxDepth = 2)]
public partial class SearchResultDto { }
```

### Dashboard Widgets

```csharp
[Flatten(typeof(DashboardMetrics))]
public partial class MetricsSummaryDto { }
```

### Export/Import

```csharp
[Flatten(typeof(Product), exclude: ["InternalNotes", "CostPrice"], IgnoreNestedIds = true)]
public partial class ProductExportDto { }
```

## Troubleshooting

### Name Collisions

**Problem**: Multiple properties end up with the same name when using `LeafOnly` strategy.

**Solution**: Use `Prefix` strategy (default) or manually rename properties in your domain model.

### Properties Missing

**Problem**: Expected properties aren't included in the flattened DTO.

**Solution**: Check `MaxDepth` setting - you might need to increase it, or the property type might not be considered a "leaf" type.

### Circular References

**Problem**: Worried about circular references causing infinite loops.

**Solution**: Facet has built-in protection - it tracks visited types and won't recurse infinitely, plus there's a safety limit of 10 levels.

## Comparison: Flatten vs Facet with NestedFacets

| Feature | `[Flatten]` | `[Facet]` with `NestedFacets` |
|---------|-------------|-------------------------------|
| **Property Structure** | All properties at top level | Preserves nested structure |
| **Naming** | `AddressStreet` | `Address.Street` |
| **ToSource Method** | No (one-way only) | Yes (bidirectional) |
| **Use Case** | API responses, reports, exports | Full CRUD, domain mapping |
| **Setup** | Single attribute, automatic | Requires defining each nested facet |
| **Flexibility** | Less (automatic) | More (explicit control) |

## FlattenTo Property (Unpack Collections to Rows)

While the `[Flatten]` attribute flattens nested objects into a single DTO, the `FlattenTo` property on the `[Facet]` attribute unpacks collection properties into multiple rows. This is useful for reports, exports, and scenarios where you need to denormalize a parent-child relationship.

### What is FlattenTo?

FlattenTo transforms a facet with a collection property into multiple rows, combining the parent's properties with each collection item:

```csharp
// Source entities
public class DataEntity
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public ICollection<ExtendedEntity> Extended { get; set; }
}

public class ExtendedEntity
{
    public int Id { get; set; }
    public string Name { get; set; }
    public int DataValue { get; set; }
}

// Facet for the collection item
[Facet(typeof(ExtendedEntity))]
public partial class ExtendedFacet;

// Facet for the parent with FlattenTo
[Facet(typeof(DataEntity),
    NestedFacets = [typeof(ExtendedFacet)],
    FlattenTo = [typeof(DataFlattenedDto)])]
public partial class DataFacet;

// Flattened target (you define the properties you want)
public partial class DataFlattenedDto
{
    // Properties from parent (DataEntity)
    public int Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }

    // Properties from collection item (ExtendedEntity)
    // Use a prefix to avoid Name collision
    public string ExtendedName { get; set; }
    public int DataValue { get; set; }
}
```

### Usage

```csharp
var entity = new DataEntity
{
    Id = 1,
    Name = "Parent",
    Description = "Parent Description",
    Extended = new List<ExtendedEntity>
    {
        new() { Id = 10, Name = "Item 1", DataValue = 100 },
        new() { Id = 20, Name = "Item 2", DataValue = 200 }
    }
};

var facet = new DataFacet(entity);
var rows = facet.FlattenTo();

// Result: 2 rows
// Row 1: { Id: 1, Name: "Parent", Description: "Parent Description", ExtendedName: "Item 1", DataValue: 100 }
// Row 2: { Id: 1, Name: "Parent", Description: "Parent Description", ExtendedName: "Item 2", DataValue: 200 }
```

### Key Features

- **Row-per-item output**: Creates one output row for each collection item
- **Parent data replication**: Parent properties are copied to each row
- **Type-safe**: Target types are defined explicitly with the properties you need
- **Null-safe**: Returns empty list when collection is null or empty

### Handling Name Collisions

When both parent and child have properties with the same name, prefix the child property:

```csharp
public class FlattenedOutput
{
    // From parent
    public int Id { get; set; }
    public string Name { get; set; }

    // From child - prefixed to avoid collision
    public int ItemId { get; set; }      // Maps from child's Id
    public string ItemName { get; set; } // Maps from child's Name
}
```

### Use Cases

**Report Generation:**
```csharp
var reportRows = invoices
    .Select(i => new InvoiceFacet(i))
    .SelectMany(f => f.FlattenTo())
    .ToList();

await GenerateExcelReport(reportRows);
```

**CSV Export:**
```csharp
var exportRows = orderFacet.FlattenTo();
await csvWriter.WriteRecordsAsync(exportRows);
```

### Comparison: FlattenTo vs Flatten Attribute

| Feature | `FlattenTo` property | `[Flatten]` attribute |
|---------|---------------------|----------------------|
| **Purpose** | Unpack collections to rows | Flatten nested objects to properties |
| **Output** | Multiple rows (List) | Single object |
| **Input** | Facet with collection | Entity with nested objects |
| **Control** | You define target properties | Properties auto-generated |
| **Use case** | Reports, exports | API responses, DTOs |

## See Also

- [Facet Attribute Reference](03_AttributeReference.md)
- [Advanced Scenarios](06_AdvancedScenarios.md)
- [Extension Methods](05_Extensions.md)
- [What is Being Generated?](07_WhatIsBeingGenerated.md)
