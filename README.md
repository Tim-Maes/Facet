# Facet

> "One part of a subject, situation, object that has many parts."

**Facet** is a C# source generator that lets you define **lightweight projections** (DTOs, API models, etc.) directly from your domain models — without writing boilerplate.

It generates partial classes, records, structs, or record structs with constructors, optional LINQ projections, and even supports custom mappings — all at compile time, with zero runtime cost.

---

## What is Facetting?

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

## Key Features

- :white_check_mark: Generate classes, records, structs, or record structs from existing types
- :white_check_mark: Exclude fields/properties you don't want (create a Facetted view of your model)
- :white_check_mark: Include/redact public fields
- :white_check_mark: Auto-generate constructors for fast mapping
- :white_check_mark: LINQ projection expressions `(Expression<Func<TSource,TTarget>>)`
- :white_check_mark: Custom mapping via `IFacetMapConfiguration`
- :white_check_mark: **Async mapping support** via `IFacetMapConfigurationAsync`
- :white_check_mark: **Hybrid sync/async mapping** for optimal performance
- :white_check_mark: **Collection mapping** with sequential and parallel processing
- :white_check_mark: **Full cancellation token support** for async operations

## Documentation

- **[Documentation & Guides](docs/README.md)**
- [What is being generated?](docs/07_WhatIsBeingGenerated.md)

## The Facet Ecosystem

Facet is modular and consists of several NuGet packages:

- **Facet**: The core source generator. Generates DTOs, projections, and mapping code.

- **Facet.Extensions**: Provider-agnostic extension methods for mapping and projecting (works with any LINQ provider, no EF Core dependency).

- **Facet.Extensions.EFCore**: Async extension methods for Entity Framework Core (requires EF Core 6+).

- **Facet.Mapping**: Advanced static mapping configuration support with async capabilities for complex mapping scenarios.

## Quick Examples

### Basic Projection
```csharp
[Facet(typeof(User))]
public partial class UserDto { }

// Auto-generates constructor, properties, and LINQ projection
var userDto = user.ToFacet<User, UserDto>();
var userDtos = users.SelectFacets<User, UserDto>();
```

### Custom Sync Mapping
```csharp
public class UserMapper : IFacetMapConfiguration<User, UserDto>
{
    public static void Map(User source, UserDto target)
    {
        target.FullName = $"{source.FirstName} {source.LastName}";
    }
}

[Facet(typeof(User), Configuration = typeof(UserMapper))]
public partial class UserDto 
{
    public string FullName { get; set; }
}
```

### Async Mapping for I/O Operations
```csharp
public class UserAsyncMapper : IFacetMapConfigurationAsync<User, UserDto>
{
    public static async Task MapAsync(User source, UserDto target, CancellationToken cancellationToken = default)
    {
        // Async database lookup
        target.ProfilePicture = await GetProfilePictureAsync(source.Id, cancellationToken);
        
        // Async API call
        target.ReputationScore = await CalculateReputationAsync(source.Email, cancellationToken);
    }
}

// Usage
var userDto = await user.ToFacetAsync<User, UserDto, UserAsyncMapper>();
var userDtos = await users.ToFacetsParallelAsync<User, UserDto, UserAsyncMapper>();
```

### EF Core Integration
```csharp
// Async projection directly in EF Core queries
var userDtos = await dbContext.Users
    .Where(u => u.IsActive)
    .ToFacetsAsync<User, UserDto>();
```

---

**Facet** — Define less, project more.
