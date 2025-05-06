_This is a new project, any help, feedback and contributions are highy appreciated._

# Facet

```
"One part of a subject, situation, object that has many parts."
```

**Facet** is a C# source generator that lets you define **lightweight projections** (DTOs, API models, etc.) directly from your domain models — without writing boilerplate.

It generates partial classes, records structs or record structs with constructors, optional LINQ projections, and even supports custom mappings — all at compile time, with zero runtime cost.

---

- :white_check_mark: Generate classes, records, structs or record structs from existing types
- :white_check_mark: Exclude fields/properties you don't want (create a Facetted view of your model))
- :white_check_mark: Include public fields (optional)
- :white_check_mark: Auto-generate constructors for fast mapping
- :white_check_mark: LINQ projection expressions `(Expression<Func<TSource,TTarget>>)`
- :white_check_mark: Custom mapping via `IFacetMapConfiguration`

## Quick Start

### 1. Install

```bash
dotnet add package Facet
```

### 2. Define  facet

```csharp
using Facet;

public class Person
{
    public string Name { get; set; }
    public string Email { get; set; }
    public int Age { get; set; }
}

// Generate a DTO that excludes Email
[Facet(typeof(Person), exclude: nameof(Person.Email))]
public partial class PersonDto { }
```

This generates a PersonDto with only Name and Age, and a constructor that copies values.

### 3. Use your generated facets

```csharp
var person = new Person { Name = "Alice", Email = "a@b.com", Age = 30 };
var dto = new PersonDto(person);
```

LINQ projection support:

```csharp
var query = dbContext.People
    .Select(PersonDto.Projection)
    .ToList();
```

## Advanced scenarios

### Records


```csharp
[Facet(typeof(Person), Kind = FacetKind.Record)]
public partial record PersonRecord;
```

### Struct & Record Struct

```csharp
[Facet(typeof(MyStruct), Kind = FacetKind.Struct, IncludeFields = true)]
public partial struct MyStructDto;

[Facet(typeof(MyRecordStruct), Kind = FacetKind.RecordStruct)]
public partial record struct MyRecordStructDto;
```

### Custom mapping

Eg. you need to compile extra fields

```csharp
public class UserMapConfig : IFacetMapConfiguration<User, UserDto>
{
    public static void Map(User source, UserDto target)
    {
        target.FullName = $"{source.FirstName} {source.LastName}";
    }
}

[Facet(typeof(User), Configuration = typeof(UserMapConfig))]
public partial class UserDto;
```
This lets you add derived properties, formatting, etc.

## Facet.Extensions for LINQ/EF Core async

Install Facet.Extensions for one-line mapping helpers:

```bash
dotnet add package Facet.Extensions
```

Then:

```csharp
using Facet.Extensions;

// Single object
var dto = person.ToFacet<Person, PersonDto>();

// Lists
var dtos = people.SelectFacets<Person, PersonDto>();

// IQueryable deferred projection
var query = dbContext.People.SelectFacet<Person, PersonDto>();
```

And with EF Core:

```csharp
var dtos = await dbContext.People.ToFacetsAsync<Person, PersonDto>();

var single = await dbContext.People.FirstFacetAsync<Person, PersonDto>();
```

---

# What is Facetting?

Facetting is the process of defining **focused views** of a larger model at compile time.

Instead of manually writing separate DTOs, mappers, and projections, **Facet** allows you to declare what you want to keep — and generates everything else.

You can think of it like **carving out a specific facet** of a gem:  

- The part you care about  
- Leaving the rest behind.

## Why Facetting?

- Reduce duplication across DTOs, projections, and ViewModels
- Maintain strong typing with no runtime cost
- Stay DRY (Don't Repeat Yourself) without sacrificing performance
- Works seamlessly with LINQ providers like Entity Framework

---

**Facet** - Define less, project more.
