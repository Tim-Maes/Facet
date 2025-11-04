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
- **Naming Strategies**: Prefix or leaf-only naming
- **One-Way Operation**: Flattening is intentionally one-way (no BackTo method)

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

## Why No BackTo Method?

Unlike the `[Facet]` attribute, flattened types **do not** generate `BackTo` methods. This is intentional because:

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
| **BackTo Method** | No (one-way only) | Yes (bidirectional) |
| **Use Case** | API responses, reports, exports | Full CRUD, domain mapping |
| **Setup** | Single attribute, automatic | Requires defining each nested facet |
| **Flexibility** | Less (automatic) | More (explicit control) |

## See Also

- [Facet Attribute Reference](03_AttributeReference.md)
- [Advanced Scenarios](06_AdvancedScenarios.md)
- [Extension Methods](05_Extensions.md)
- [What is Being Generated?](07_WhatIsBeingGenerated.md)
