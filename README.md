
<div align="center">
  <img
    src="https://raw.githubusercontent.com/Tim-Maes/Facet/master/assets/Facet.png"
    alt="Facet logo"
    width="400">
</div>

<div align="center">
"One part of a subject, situation, object that has many parts."
</div>

---

**Facet** is a C# source generator that lets you define **lightweight projections** (DTOs, API models, etc.) directly from your domain models, without writing boilerplate.

It generates partial classes, records, structs, or record structs with constructors, optional LINQ projections, and even supports custom mappings, all at compile time, with zero runtime cost.

## :gem: What is Facetting?

Facetting is the process of defining **focused views** of a larger model at compile time.

Instead of manually writing separate DTOs, mappers, and projections, **Facet** allows you to declare what you want to keep, and generates everything else.

You can think of it like **carving out a specific facet** of a gem:

- The part you care about
- Leaving the rest behind.

## :grey_question: Why Facetting?

- Reduce duplication across DTOs, projections, and ViewModels
- Maintain strong typing with no runtime cost
- Stay DRY (Don't Repeat Yourself) without sacrificing performance
- Works seamlessly with LINQ providers like Entity Framework

## :clipboard: Documentation

- **[Documentation & Guides](docs/README.md)**
- [What is being generated?](docs/07_WhatIsBeingGenerated.md)

## :star: Key Features

- :white_check_mark: Generate classes, records, structs, or record structs from existing types
- :white_check_mark: Exclude fields/properties you don't want (create a Facetted view of your model)
- :white_check_mark: Include/redact public fields
- :white_check_mark: Auto-generate constructors for fast mapping
- :white_check_mark: LINQ projection expressions `(Expression<Func<TSource,TTarget>>)`
- :white_check_mark: Full mapping support with custom mapping configurations

## :earth_americas: The Facet Ecosystem

Facet is modular and consists of several NuGet packages:

- **Facet**: The core source generator. Generates DTOs, projections, and mapping code.

- **Facet.Extensions**: Provider-agnostic extension methods for mapping and projecting (works with any LINQ provider, no EF Core dependency).

- **Facet.Extensions.EFCore**: Async extension methods for Entity Framework Core (requires EF Core 6+).

- **Facet.Mapping**: Advanced static mapping configuration support with async capabilities and dependency injection for complex mapping scenarios.

## :rocket: Quick start 

### Install the NuGet Package

```
dotnet add package Facet
```

For LINQ helpers:
```
dotnet add package Facet.Extensions
```

For EF Core support:
```
dotnet add package Facet.Extensions.EFCore
```


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

### Async Mapping with Dependency Injection
```csharp
public class UserAsyncMapperWithDI : IFacetMapConfigurationAsyncInstance<User, UserDto>
{
    private readonly IProfilePictureService _profileService;
    private readonly IReputationService _reputationService;

    public UserAsyncMapperWithDI(IProfilePictureService profileService, IReputationService reputationService)
    {
        _profileService = profileService;
        _reputationService = reputationService;
    }

    public async Task MapAsync(User source, UserDto target, CancellationToken cancellationToken = default)
    {
        // Use injected services
        target.ProfilePicture = await _profileService.GetProfilePictureAsync(source.Id, cancellationToken);
        target.ReputationScore = await _reputationService.CalculateReputationAsync(source.Email, cancellationToken);
    }
}

// Usage with DI
var mapper = new UserAsyncMapperWithDI(profileService, reputationService);
var userDto = await user.ToFacetAsync(mapper);
var userDtos = await users.ToFacetsParallelAsync(mapper);
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
