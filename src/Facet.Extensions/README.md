# Facet.Extensions

Provider-agnostic extension methods for the Facet library, enabling one-line mapping between your domain entities and generated facet types.

## Key Features

- **Forward Mapping**: Source > Facet
  - Constructor-based mapping `(ToFacet<>)` for any object graph
  - Enumerable mapping `(SelectFacets<>)` via LINQ
  - IQueryable projection `(SelectFacet<>)` using the generated Projection expression

- **Reverse Mapping**: Facet > Source
  - Generate source from facet: `ToSource<TFacetSource>()`

- **Patch/update source**: Facet > Source
  - Selective source updates: `ApplyFacet<TSource, TFacet>()`
  - Update with change tracking: `ApplyFacetWithChanges<TSource, TFacet>()`
 
All methods are zero-boilerplate and leverage your already generated ctor or Projection property.

## Getting Started

### 1. Install packages

# Core Facet generator + DTOs

```bash
dotnet add package Facet
```

# Provider-agnostic mapping helpers

```bash
dotnet add package Facet.Extensions
```

> Note: For EF Core async methods, see [Facet.Extensions.EFCore](https://www.nuget.org/packages/Facet.Extensions.EFCore).

### 2. Import namespaces

```csharp
using Facet;              // for [Facet] and generated types
using Facet.Extensions;   // for mapping extension methods
```

### 3. Define your facet types

```csharp
using Facet;

// emits ctor + Projection by default
[Facet(typeof(Person))]
public partial class PersonDto { }
```

### 4. Map to and from facets

```csharp
// Forward mapping: Source -> Facet
var dto = person.ToFacet<PersonDto>();

// Enumerable mapping (in-memory)
var dtos = people.SelectFacets<PersonDto>().ToList();

// IQueryable projection (deferred)
var query = dbContext.People.SelectFacet<PersonDto>();
var list  = query.ToList();

// Reverse mapping: Facet -> Source (apply changes back to source)
var updatedDto = new PersonDto { Name = "Jane", Email = "jane@example.com" };
person.ApplyFacet(updatedDto);  // Only updates changed properties

// Track changes for auditing
var result = person.ApplyFacetWithChanges<Person, PersonDto>(updatedDto);
if (result.HasChanges)
{
    Console.WriteLine($"Changed: {string.Join(", ", result.ChangedProperties)}");
}
```

## Forward Mapping (Source -> Facet)

### Single Object Mapping

```csharp
var person = new Person { Id = 1, Name = "John", Email = "john@example.com" };
var dto = person.ToFacet<PersonDto>();
```

### Enumerable Mapping

```csharp
var people = GetPeople();
var dtos = people.SelectFacets<PersonDto>().ToList();
```

### IQueryable Projection

```csharp
var query = dbContext.People.SelectFacet<PersonDto>();
var list = query.ToList();
```

## Reverse Mapping (Facet -> Source)

Apply changes from a facet DTO back to the source object. Only properties that exist in both types and have different values will be updated.

### Basic Usage

```csharp
var person = new Person { Id = 1, Name = "John", Email = "john@example.com" };
var dto = new PersonDto { Name = "Jane", Email = "jane@example.com" };

// Apply changes from facet to source
person.ApplyFacet(dto);
// person.Name is now "Jane", Email is "jane@example.com"

// Fluent API support
var updatedPerson = person.ApplyFacet(dto);
```

### Change Tracking

Track which properties were changed for auditing or logging:

```csharp
var result = person.ApplyFacetWithChanges<Person, PersonDto>(dto);

if (result.HasChanges)
{
    Console.WriteLine($"Updated properties: {string.Join(", ", result.ChangedProperties)}");
    // Output: "Updated properties: Name, Email"
}

// Access the updated source
var updatedPerson = result.Source;
```

### Common Scenarios

```csharp
// API scenario: Apply user updates
[HttpPut("{id}")]
public IActionResult UpdatePerson(int id, PersonDto dto)
{
    var person = repository.GetById(id);
    if (person == null) return NotFound();

    var result = person.ApplyFacetWithChanges<Person, PersonDto>(dto);

    if (result.HasChanges)
    {
        repository.Save(person);
        logger.LogInformation("Person {Id} updated: {Changes}",
            id, string.Join(", ", result.ChangedProperties));
    }

    return NoContent();
}

// Partial updates: Only defined properties in the DTO are updated
public partial class UpdatePersonDto  // Might exclude sensitive fields
{
    public string Name { get; set; }
    public string Email { get; set; }
    // Password, CreatedAt, etc. not included = won't be updated
}

var person = repository.GetById(1);
var updateDto = new UpdatePersonDto { Name = "Jane" };
person.ApplyFacet(updateDto);  // Only Name is updated
```

## API Reference

### Forward Mapping

| Method |  Description    |
| ------- |------
| `ToFacet<TTarget>()`    |   Map one instance via generated constructor  |
| `ToFacet<TSource,TTarget>()`    |   Map one instance via generated constructor  |
| `SelectFacets<TTarget>()`     |  Map an `IEnumerable<TSource>` via constructor   |
| `SelectFacets<TSource,TTarget>()`     |  Map an `IEnumerable<TSource>` via constructor   |
| `SelectFacet<TTarget>()`    |  Project `IQueryable<TSource>` to `IQueryable<TTarget>`   |
| `SelectFacet<TSource,TTarget>()`    |  Project `IQueryable<TSource>` to `IQueryable<TTarget>`   |
| `ToSource<TFacetSource>()`    |  Map facet back to source via generated ToSource method   |
| `ToSource<TFacet,TFacetSource>()`    |  Map facet back to source via generated ToSource method   |
| `SelectFacetSources<TFacetSource>()`     |  Map facets back to sources   |
| `SelectFacetSources<TFacet,TFacetSource>()`     |  Map facets back to sources   |

### Reverse Mapping (Patch/Update)

| Method |  Description    | Use Case |
| ------- |------ |------
| `ApplyFacet<TSource, TFacet>()`    |   Apply changed properties from facet to source  | Updates, PATCH endpoints |
| `ApplyFacet<TFacet>()`    |   Apply changed properties (type inferred)  | Updates with type inference |
| `ApplyFacetWithChanges<TSource, TFacet>()`    |   Apply changes and return `FacetApplyResult` with changed property names  | Auditing, logging |

## Performance Considerations

The `ApplyFacet` methods use reflection to discover and update properties. For most scenarios, the performance overhead is negligible. The methods are optimized to:

- Only enumerate properties once per call
- Only update properties that have different values
- Skip properties that don't exist in both types

## Requirements

- Facet v1.6.0+
- .NET Standard 2.0+ (sync methods)

---

## Related Packages

- For EF Core async support and `DbContext`-aware updates, see [Facet.Extensions.EFCore](https://www.nuget.org/packages/Facet.Extensions.EFCore)
  - `UpdateFromFacet()` - Similar to `ApplyFacet()` but with EF Core change tracking
  - `UpdateFromFacetAsync()` - Async version
  - `UpdateFromFacetWithChanges()` - With change tracking
