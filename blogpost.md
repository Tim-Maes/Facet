---
layout: post
title: "Introducing Flatten: Automatic Object Flattening with Facet"
date: 2025-01-04
categories: [dotnet, csharp, source-generators]
tags: [facet, dto, api, source-generation, entity-framework]
---

## Introduction

I'm excited to introduce the newest addition to the [Facet](https://github.com/facet-tools/Facet) library: the **Flatten attribute**! This powerful source generator automatically transforms hierarchical object structures into flat DTOs, eliminating the tedious manual work of creating denormalized data transfer objects.

If you've ever found yourself manually writing DTOs like this:

```csharp
public class PersonDto
{
    public int Id { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string AddressStreet { get; set; }
    public string AddressCity { get; set; }
    public string AddressState { get; set; }
    public string AddressZipCode { get; set; }
    public string AddressCountryName { get; set; }
    public string AddressCountryCode { get; set; }
    // ... and on and on
}
```

Then writing a constructor to map all these properties:

```csharp
public PersonDto(Person person)
{
    Id = person.Id;
    FirstName = person.FirstName;
    LastName = person.LastName;
    AddressStreet = person.Address?.Street;
    AddressCity = person.Address?.City;
    AddressState = person.Address?.State;
    AddressZipCode = person.Address?.ZipCode;
    AddressCountryName = person.Address?.Country?.Name;
    AddressCountryCode = person.Address?.Country?.Code;
    // ... and on and on
}
```

You know how tedious and error-prone this can be. The Flatten attribute solves this problem completely.

## What is Flatten?

The `[Flatten]` attribute is a source generator that automatically discovers all properties in a nested object hierarchy and generates a flat DTO with all nested properties promoted to the top level. It handles:

- **Automatic Property Discovery**: Recursively traverses your domain model to find all flattenable properties
- **Null-Safe Access**: Generates code with null-conditional operators (`?.`) to prevent NullReferenceExceptions
- **LINQ Projection Support**: Creates static `Expression` properties for efficient Entity Framework queries
- **Flexible Configuration**: Control depth, exclude specific paths, and customize naming strategies
- **ID Filtering**: Optionally exclude foreign keys and nested IDs for cleaner API responses
- **FK Clash Detection**: Eliminate duplicate foreign key data automatically

## Why Flatten Objects?

Flattening is useful in several real-world scenarios:

### 1. API Responses

Instead of returning nested JSON with complex object graphs:

```json
{
  "firstName": "John",
  "lastName": "Doe",
  "address": {
    "street": "123 Main St",
    "city": "Springfield",
    "country": {
      "name": "USA",
      "code": "US"
    }
  }
}
```

You can return a cleaner, flat structure:

```json
{
  "firstName": "John",
  "lastName": "Doe",
  "addressStreet": "123 Main St",
  "addressCity": "Springfield",
  "addressCountryName": "USA",
  "addressCountryCode": "US"
}
```

### 2. Report Generation

Reports often need all data at one level without nested objects. Flat DTOs are perfect for generating CSV files, Excel spreadsheets, or tabular reports.

### 3. Search Results

UI components displaying search results often need all relevant information in a flat list structure for easy binding and display.

### 4. Database Efficiency

Using LINQ projection expressions, you can select only the fields you need and let Entity Framework generate efficient SQL queries that run entirely in the database.

## Getting Started

### Basic Usage

Let's start with a simple example. Suppose you have these domain models:

```csharp
public class Person
{
    public int Id { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public DateTime DateOfBirth { get; set; }
    public Address Address { get; set; }
    public ContactInfo ContactInfo { get; set; }
}

public class Address
{
    public string Street { get; set; }
    public string City { get; set; }
    public string State { get; set; }
    public string ZipCode { get; set; }
    public Country Country { get; set; }
}

public class Country
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Code { get; set; }
}

public class ContactInfo
{
    public string Email { get; set; }
    public string Phone { get; set; }
}
```

To create a flattened DTO, simply add the `[Flatten]` attribute:

```csharp
[Flatten(typeof(Person))]
public partial class PersonFlatDto { }
```

That's it! The source generator will create all the properties and mapping logic for you:

```csharp
public partial class PersonFlatDto
{
    public int Id { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public DateTime DateOfBirth { get; set; }
    public string AddressStreet { get; set; }
    public string AddressCity { get; set; }
    public string AddressState { get; set; }
    public string AddressZipCode { get; set; }
    public int AddressCountryId { get; set; }
    public string AddressCountryName { get; set; }
    public string AddressCountryCode { get; set; }
    public string ContactInfoEmail { get; set; }
    public string ContactInfoPhone { get; set; }

    // Constructor for easy conversion
    public PersonFlatDto(Person source) { /* generated */ }

    // Parameterless constructor
    public PersonFlatDto() { }

    // LINQ projection expression
    public static Expression<Func<Person, PersonFlatDto>> Projection { get; }
}
```

### Using the Generated DTO

There are three ways to use the generated DTO:

#### 1. Constructor-Based Mapping

```csharp
var person = await dbContext.People
    .Include(p => p.Address)
        .ThenInclude(a => a.Country)
    .Include(p => p.ContactInfo)
    .FirstAsync(p => p.Id == 1);

var dto = new PersonFlatDto(person);
```

#### 2. LINQ Projection (Recommended)

```csharp
// This runs entirely in the database - much more efficient!
var dto = await dbContext.People
    .Where(p => p.Id == 1)
    .Select(PersonFlatDto.Projection)
    .FirstAsync();
```

#### 3. Batch Projections

```csharp
var dtos = await dbContext.People
    .Where(p => p.IsActive)
    .OrderBy(p => p.LastName)
    .Select(PersonFlatDto.Projection)
    .ToListAsync();
```

## Advanced Features

### Excluding Properties

You can exclude specific properties or entire nested objects:

```csharp
// Exclude specific properties
[Flatten(typeof(Person), "DateOfBirth", "ContactInfo.Phone")]
public partial class PersonPublicDto { }

// Exclude entire nested object
[Flatten(typeof(Person), "ContactInfo")]
public partial class PersonWithoutContactDto { }
```

### Controlling Depth

By default, Flatten traverses up to 3 levels deep. You can customize this:

```csharp
// Only flatten 2 levels (Person -> Address, but not Address -> Country)
[Flatten(typeof(Person), MaxDepth = 2)]
public partial class PersonShallowDto { }

// Unlimited depth (use with caution!)
[Flatten(typeof(Person), MaxDepth = 0)]
public partial class PersonDeepDto { }
```

### Ignoring Nested IDs

Often, you don't want foreign keys and nested IDs in your API responses. The `IgnoreNestedIds` feature helps:

```csharp
public class Order
{
    public int Id { get; set; }
    public DateTime OrderDate { get; set; }
    public int CustomerId { get; set; }  // Foreign key
    public Customer Customer { get; set; }
}

public class Customer
{
    public int Id { get; set; }  // Nested ID
    public string Name { get; set; }
    public string Email { get; set; }
}

[Flatten(typeof(Order), IgnoreNestedIds = true)]
public partial class OrderDisplayDto { }
```

The generated DTO will include:
- `Id` (root level ID)
- `OrderDate`
- `CustomerName`
- `CustomerEmail`

But will exclude:
- `CustomerId` (foreign key)
- `Customer.Id` (nested ID)

This creates much cleaner API responses focused on display data rather than relational database implementation details.

### Ignoring Foreign Key Clashes

Entity Framework models often have both foreign key properties AND navigation properties, leading to duplicate data when flattened. The new `IgnoreForeignKeyClashes` feature solves this elegantly!

#### The Problem

Consider this common EF pattern:

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
```

Without `IgnoreForeignKeyClashes`, flattening creates duplicate ID data:

```csharp
[Flatten(typeof(Person))]
public partial class PersonFlatDto
{
    public int Id { get; set; }
    public string Name { get; set; }
    public int? AddressId { get; set; }   // FK property
    public int? AddressId2 { get; set; }  // Address.Id (collision!)
    public string AddressLine1 { get; set; }
    public string AddressCity { get; set; }
}
```

Notice `AddressId2` - this is `Address.Id` being flattened, but it represents the same data as `AddressId`!

#### The Solution

Enable `IgnoreForeignKeyClashes` to automatically detect and skip these duplicates:

```csharp
[Flatten(typeof(Person), IgnoreForeignKeyClashes = true)]
public partial class PersonFlatDto
{
    public int Id { get; set; }
    public string Name { get; set; }
    public int? AddressId { get; set; }  // FK kept
    public string AddressLine1 { get; set; }
    public string AddressCity { get; set; }
    // Address.Id is automatically skipped!
}
```

#### How It Works

The generator intelligently:
1. **Detects FK patterns**: Identifies properties ending with "Id" that have matching navigation properties
2. **Skips nested IDs**: When flattening a navigation property, skips its `Id` if it would clash with a FK
3. **Handles deep nesting**: Works at ALL depths, not just one level
4. **Preserves root FKs**: Root-level foreign keys are always included for reference

#### When to Use Each Feature

| Feature | Use When | Result |
|---------|----------|--------|
| `IgnoreNestedIds = true` | Pure display DTOs, no relationships needed | Removes ALL IDs except root |
| `IgnoreForeignKeyClashes = true` | Need FKs for relationships, avoid duplicates | Keeps root FKs, removes clashing nested IDs |
| Both together | Ultra-clean display DTOs | Same as IgnoreNestedIds alone |

### Naming Strategies

Choose how nested properties are named:

#### Prefix Strategy (Default)

Properties are prefixed with their full path:

```csharp
[Flatten(typeof(Person), NamingStrategy = FlattenNamingStrategy.Prefix)]
public partial class PersonFlatDto { }

// Generated properties:
// AddressStreet
// AddressCity
// AddressCountryName
// ContactInfoEmail
```

#### LeafOnly Strategy

Uses only the leaf property name (watch out for collisions!):

```csharp
[Flatten(typeof(Person), NamingStrategy = FlattenNamingStrategy.LeafOnly)]
public partial class PersonFlatDto { }

// Generated properties:
// Street
// City
// Name  (from Country.Name)
// Email
```

If there are collisions, numeric suffixes are added automatically (`Name`, `Name2`, `Name3`).

## Real-World Examples

### Example 1: E-Commerce Order API

```csharp
public class Order
{
    public int Id { get; set; }
    public DateTime OrderDate { get; set; }
    public decimal Total { get; set; }
    public string Status { get; set; }
    public int CustomerId { get; set; }
    public Customer Customer { get; set; }
    public Address ShippingAddress { get; set; }
}

// Create a clean DTO for the order list API
[Flatten(typeof(Order), IgnoreForeignKeyClashes = true)]
public partial class OrderListDto { }

// Usage in API controller
[HttpGet("orders")]
public async Task<ActionResult<List<OrderListDto>>> GetOrders()
{
    var orders = await _dbContext.Orders
        .Where(o => o.Status == "Active")
        .OrderByDescending(o => o.OrderDate)
        .Select(OrderListDto.Projection)
        .Take(50)
        .ToListAsync();

    return Ok(orders);
}
```

### Example 2: Employee Report Generation

```csharp
public class Employee
{
    public int Id { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public DateTime HireDate { get; set; }
    public decimal Salary { get; set; }
    public Department Department { get; set; }
    public Address Address { get; set; }
}

// Create a report DTO that excludes sensitive salary information
[Flatten(typeof(Employee), "Salary")]
public partial class EmployeeReportDto { }

// Generate Excel report
public async Task<byte[]> GenerateEmployeeReport()
{
    var employees = await _dbContext.Employees
        .Select(EmployeeReportDto.Projection)
        .ToListAsync();

    return ExcelGenerator.Create(employees);
}
```

### Example 3: Product Search Results

```csharp
public class Product
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public decimal Price { get; set; }
    public int CategoryId { get; set; }
    public Category Category { get; set; }
    public int ManufacturerId { get; set; }
    public Manufacturer Manufacturer { get; set; }
}

// Flatten for search results with FK clash prevention
[Flatten(typeof(Product), MaxDepth = 2, IgnoreForeignKeyClashes = true)]
public partial class ProductSearchDto { }

[HttpGet("products/search")]
public async Task<ActionResult<List<ProductSearchDto>>> SearchProducts(string query)
{
    var products = await _dbContext.Products
        .Where(p => p.Name.Contains(query) || p.Description.Contains(query))
        .Select(ProductSearchDto.Projection)
        .Take(20)
        .ToListAsync();

    return Ok(products);
}
```

## How It Works

The Flatten attribute uses C# source generators to analyze your domain models at compile time and generate optimized code. Here's what happens behind the scenes:

1. **Discovery**: The generator recursively walks your source type's property tree
2. **FK Detection**: When `IgnoreForeignKeyClashes` is enabled, it pre-scans to find all FK patterns at all depths
3. **Filtering**: Properties are filtered based on exclusion rules, depth limits, FK clashes, and type classifications
4. **Naming**: Property names are generated using your chosen naming strategy
5. **Code Generation**: Three things are generated:
   - Properties with XML documentation showing the source path
   - A constructor that uses null-conditional operators for safe access
   - A LINQ `Expression` for database projections

### Type Classification

The generator intelligently classifies types:

- **Leaf Types** (flattened as properties): primitives, strings, enums, DateTime, Guid, Decimal, simple value types
- **Complex Types** (recursed into): reference types with properties, complex value types
- **Collections** (completely ignored): Lists, Arrays, IEnumerable, etc.

### Safety Features

- **Null Safety**: All nested property access uses null-conditional operators (`?.`)
- **Recursion Protection**: Tracks visited types to prevent infinite loops
- **Depth Limiting**: Default max depth of 3, with a hard safety limit of 10
- **Collision Handling**: Automatically adds numeric suffixes when property names collide
- **FK Deduplication**: Prevents duplicate foreign key data

## Flatten vs. Facet with NestedFacets

You might wonder: how does Flatten differ from the existing `[Facet]` attribute with `NestedFacets`?

| Feature | [Flatten] | [Facet] with NestedFacets |
|---------|-----------|---------------------------|
| **Structure** | All properties at top level | Preserves nested structure |
| **Naming** | `AddressStreet` | Nested object with `Address.Street` |
| **BackTo Method** | No (one-way only) | Yes (bidirectional) |
| **Use Case** | Read-only projections, API responses | Full CRUD, bidirectional domain mapping |
| **Flexibility** | Automatic, less control | Explicit, more control |

**Key Takeaway**: Use `[Flatten]` for denormalized, read-only views (like API responses, reports, exports). Use `[Facet]` when you need bidirectional mapping with the ability to update source objects.

## Best Practices

### 1. Use Projections for Database Queries

Always prefer LINQ projections over constructor-based mapping when querying databases:

```csharp
// Good - runs in database
var dtos = await dbContext.People
    .Select(PersonFlatDto.Projection)
    .ToListAsync();

// Not optimal - loads entire object graphs into memory first
var people = await dbContext.People
    .Include(p => p.Address)
        .ThenInclude(a => a.Country)
    .ToListAsync();
var dtos = people.Select(p => new PersonFlatDto(p)).ToList();
```

### 2. Limit Depth Appropriately

Don't flatten too deep unless necessary:

```csharp
// Usually sufficient
[Flatten(typeof(Order), MaxDepth = 3)]
public partial class OrderDto { }
```

### 3. Exclude Sensitive Data

Always exclude sensitive information from API DTOs:

```csharp
[Flatten(typeof(Employee), "Salary", "SSN", "BankAccount")]
public partial class EmployeePublicDto { }
```

### 4. Use IgnoreForeignKeyClashes for EF Models

Clean up your API responses from duplicate FK data:

```csharp
[Flatten(typeof(Order), IgnoreForeignKeyClashes = true)]
public partial class OrderDisplayDto { }
```

### 5. Combine Features for Cleaner APIs

```csharp
// Ultra-clean public API
[Flatten(typeof(Product),
    IgnoreNestedIds = true,         // No FKs exposed
    exclude: new[] { "InternalNotes", "CostPrice" })]  // No internal data
public partial class ProductPublicDto { }

// Admin API with relationships
[Flatten(typeof(Product),
    IgnoreForeignKeyClashes = true,  // Keep FKs, avoid duplicates
    MaxDepth = 2)]                   // Limit depth
public partial class ProductAdminDto { }
```

## Troubleshooting

### Name Collisions

If you see properties like `Name2`, `Name3`, you have naming collisions. Consider:
- Using the Prefix naming strategy (default)
- Excluding one of the conflicting properties
- Using `UseFullName = true`

### Missing Properties

If properties aren't being generated:
- Check if they exceed your `MaxDepth` setting
- Verify they're not in your `Exclude` list
- Ensure the type isn't a collection
- Check if `IgnoreNestedIds` or `IgnoreForeignKeyClashes` is filtering them

### Circular References

The generator tracks visited types to prevent infinite loops. Circular references are handled automatically.

## Conclusion

The Flatten attribute is a powerful addition to the Facet library that eliminates the tedious work of creating flat DTOs. With the new `IgnoreForeignKeyClashes` feature, it's now even better at handling Entity Framework models with foreign key relationships.

Key benefits:
- **Zero Boilerplate**: No manual DTO property definitions or mapping code
- **Type Safe**: Compile-time code generation means no runtime reflection
- **Null Safe**: Generated code handles null nested objects automatically
- **Efficient**: LINQ projections enable optimal database queries
- **Smart FK Handling**: Eliminates duplicate foreign key data automatically
- **Flexible**: Extensive configuration options for every use case

Try it out in your next project and let me know what you think!

## Installation

```bash
dotnet add package Facet
```

## Resources

- [GitHub Repository](https://github.com/facet-tools/Facet)
- [Full Documentation](https://github.com/facet-tools/Facet/blob/master/docs/11_FlattenAttribute.md)
- [NuGet Package](https://www.nuget.org/packages/Facet)

Happy flattening!
