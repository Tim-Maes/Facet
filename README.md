<div align="center">
  <img
    src="https://raw.githubusercontent.com/Tim-Maes/Facet/master/assets/Facet.png"
    alt="Facet logo"
    width="400">
</div>

<div align="center">
"One part of a subject, situation, object that has many parts."
</div>

<br>

<div align="center">
  
[![CI](https://github.com/Tim-Maes/Facet/actions/workflows/build.yml/badge.svg)](https://github.com/Tim-Maes/Facet/actions/workflows/build.yml)
[![Test](https://github.com/Tim-Maes/Facet/actions/workflows/test.yml/badge.svg)](https://github.com/Tim-Maes/Facet/actions/workflows/test.yml)
[![CD](https://github.com/Tim-Maes/Facet/actions/workflows/release.yml/badge.svg)](https://github.com/Tim-Maes/Facet/actions/workflows/release.yml)
[![NuGet](https://img.shields.io/nuget/v/Facet.svg)](https://www.nuget.org/packages/Facet)
[![Downloads](https://img.shields.io/nuget/dt/Facet.svg)](https://www.nuget.org/packages/Facet)
[![GitHub](https://img.shields.io/github/license/Tim-Maes/Facet.svg)](https://github.com/Tim-Maes/Facet/blob/main/LICENSE.txt)

</div>

---

**Facet** is a C# source generator that lets you define **projections** (DTOs, API models, etc.) directly from your domain models, without writing boilerplate.

## :gem: What is Facetting?

Facetting is the process of defining **focused views** of a larger model at compile time.

Instead of manually writing separate DTOs, mappers, and projections, **Facet** allows you to declare what you want, and generates everything else.
Generate classes, records, structs, or record structs with constructors, LINQ projections, and even supports custom mappings, all at compile time, with zero runtime cost.

You can think of it like **carving out a specific facet** of a gem:

- The part you care about
- Leave the rest behind.

## :clipboard: Documentation

- **[Documentation & Guides](docs/README.md)**
- [What is being generated?](docs/07_WhatIsBeingGenerated.md)
- [Comprehensive article about Facetting](https://tim-maes.com/facets-in-dotnet.html)

## :star: Key Features

- :white_check_mark: Generate classes, records, structs, or record structs from existing types
- :white_check_mark: Define what to include, or exclude fields/properties
- :white_check_mark: Include/redact public fields
- :white_check_mark: Constructors & LINQ projection expressions
- :white_check_mark: Handle **complex and nested objects**
- :white_check_mark: **Copy data validation attributes**
- :white_check_mark: Full mapping support with custom mapping configurations
- :white_check_mark: **Expression transformation and mapping utilities** for reusing business logic across entities and DTOs
- :white_check_mark: Preserve member and type XML documentation
- :white_check_mark: Auto-generate complete CRUD DTO sets

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

For expression transformation utilities:
```
dotnet add package Facet.Mapping.Expressions
```

### Define Facets

... and specify what you need.

```csharp
// Exclude sensitive properties
string[] excludeFields = { "Password", "Email" };

[Facet(typeof(User), exclude: excludeFields)]
public partial record UserWithoutEmail;

// Include only specific properties
[Facet(typeof(User), Include = new[] { "FirstName", "LastName", "Email" })]
public partial record UserContactDto;

// Include public fields
[Facet(typeof(Entity), IncludeFields = true)]
public partial record EntityDto;

// Include specific fields and properties
[Facet(typeof(Entity), Include = new[] { "Name", "Status" }, IncludeFields = true)]
public partial record EntitySummaryDto;

// Make all properties nullable for query/filter scenarios
[Facet(typeof(Product), "InternalNotes", NullableProperties = true, GenerateBackTo = false)]
public partial record ProductQueryDto;

// Copy data validation attributes from source type
[Facet(typeof(User), CopyAttributes = true)]
public partial class UserDto; // Copies [Required], [StringLength], [EmailAddress], etc.

// Automatically map nested types with NestedFacets
[Facet(typeof(Address))]
public partial record AddressDto;

[Facet(typeof(Company), NestedFacets = [typeof(AddressDto)])]
public partial record CompanyDto; // Automatically includes AddressDto for nested Address property

[Facet(typeof(Employee),
    exclude: ["PasswordHash", "Salary"],
    NestedFacets = [typeof(CompanyDto), typeof(AddressDto)])]
public partial record EmployeeDto; // Handles multiple nested facets automatically
```

### Basic Projection of Facets
```csharp
[Facet(typeof(User))]
public partial class UserFacet { }

// Auto-generates constructor, properties, and LINQ projection
var userFacet = user.ToFacet<UserFacet>();
var userFacet = user.ToFacet<User, UserFacet>(); //Much faster

var user = userFacet.BackTo<User>();
var user = userFacet.BackTo<UserFacet, User>(); //Much faster

var users = users.SelectFacets<UserFacet>();
var users = users.SelectFacets<User, UserFacet>(); //Much faster
```

### Custom Sync Mapping
```csharp
public class UserMapper : IFacetMapConfiguration<User, UserDto>
{
    public static void Map(User source, UserDto target)
    {
        target.FullName = $"{source.FirstName} {source.LastName}";
        target.Age = CalculateAge(source.DateOfBirth);
    }
}

[Facet(typeof(User), Configuration = typeof(UserMapper))]
public partial class UserDto 
{
    public string FullName { get; set; }
    public int Age { get; set; }
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

#### Forward Mapping (Entity -> Facet)
```csharp
// Async projection directly in EF Core queries
var userDtos = await dbContext.Users
    .Where(u => u.IsActive)
    .ToFacetsAsync<UserDto>();

// LINQ projection for complex queries
var results = await dbContext.Products
    .Where(p => p.IsAvailable)
    .SelectFacet<ProductDto>()
    .OrderBy(dto => dto.Name)
    .ToListAsync();
```

#### Reverse Mapping (Facet -> Entity)
```csharp
[Facet(typeof(User)]
public partial class UpdateUserDto { }

[HttpPut("{id}")]
public async Task<IActionResult> UpdateUser(int id, UpdateUserDto dto)
{
    var user = await context.Users.FindAsync(id);
    if (user == null) return NotFound();
    
    // Only updates properties that mutated
    user.UpdateFromFacet(dto, context);
    
    await context.SaveChangesAsync();
    return NoContent();
}

// With change tracking for auditing
var result = user.UpdateFromFacetWithChanges(dto, context);
if (result.HasChanges)
{
    logger.LogInformation("User {UserId} updated. Changed: {Properties}", 
        user.Id, string.Join(", ", result.ChangedProperties));
}
```

### Automatic CRUD DTO Generation

Generate standard Create, Update, Response, Query, and Upsert DTOs automatically:

```csharp
// Generate all standard CRUD DTOs
[GenerateDtos(Types = DtoTypes.All, OutputType = OutputType.Record)]
public class User
{
    public int Id { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string Email { get; set; }
    public DateTime CreatedAt { get; set; }
}

// Auto-generates:
// - CreateUserRequest (excludes Id)
// - UpdateUserRequest (includes Id)  
// - UserResponse (includes all)
// - UserQuery (all properties nullable)
// - UpsertUserRequest (includes Id, for create/update operations)
```

#### 
Entities with Smart Exclusions
```csharp
[GenerateAuditableDtos(
    Types = DtoTypes.Create | DtoTypes.Update | DtoTypes.Response,
    OutputType = OutputType.Record,
    ExcludeProperties = new[] { "Password" })]
public class Product
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Password { get; set; } // Excluded
    public DateTime CreatedAt { get; set; } // Auto-excluded (audit)
    public string CreatedBy { get; set; } // Auto-excluded (audit)
}

// Auto-excludes audit fields: CreatedAt, UpdatedAt, CreatedBy, UpdatedBy
```

#### Multiple Configurations for Fine-Grained Control
```csharp
// Different exclusions for different DTO types
[GenerateDtos(Types = DtoTypes.Response, ExcludeProperties = new[] { "Password", "InternalNotes" })]
[GenerateDtos(Types = DtoTypes.Upsert, ExcludeProperties = new[] { "Password" })]
public class Schedule
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Password { get; set; } // Excluded from both
    public string InternalNotes { get; set; } // Only excluded from Response
}

// Generates:
// - ScheduleResponse (excludes Password, InternalNotes) 
// - UpsertScheduleRequest (excludes Password, includes InternalNotes)
```

## :earth_americas: The Facet Ecosystem

Facet is modular and consists of several NuGet packages:

- **[Facet](https://github.com/Tim-Maes/Facet/blob/master/README.md)**: The core source generator. Generates DTOs, projections, and mapping code.

- **[Facet.Extensions](https://github.com/Tim-Maes/Facet/blob/master/src/Facet.Extensions/README.md)**: Provider-agnostic extension methods for mapping and projecting (works with any LINQ provider, no EF Core dependency).

- **[Facet.Mapping](https://github.com/Tim-Maes/Facet/tree/master/src/Facet.Mapping)**: Advanced static mapping configuration support with async capabilities and dependency injection for complex mapping scenarios.

- **[Facet.Mapping.Expressions](https://github.com/Tim-Maes/Facet/blob/master/src/Facet.Mapping.Expressions/README.md)**: Expression tree transformation utilities for transforming predicates, selectors, and business logic between source entities and their Facet projections.

- **[Facet.Extensions.EFCore](https://github.com/Tim-Maes/Facet/tree/master/src/Facet.Extensions.EFCore)**: Async extension methods for Entity Framework Core (requires EF Core 6+).

## :chart_with_upwards_trend: Performance Benchmarks

Facet delivers competitive performance across different mapping scenarios. Here's how it compares to popular alternatives:

### Single Mapping

| Library  | Mean Time | Memory Allocated | Performance vs Facet |
|----------|-----------|------------------|---------------------|
| **Facet** | 15.93 ns | 136 B | **Baseline** |
| Mapperly | 15.09 ns | 128 B | 5% faster, 6% less memory |
| Mapster  | 21.90 ns | 128 B | 38% slower, 6% less memory |

### Collection Mapping (10 items)

| Library  | Mean Time | Memory Allocated | Performance vs Facet |
|----------|-----------|------------------|---------------------|
| Mapster  | 192.55 ns | 1,416 B | **10% faster, 10% less memory** |
| **Facet** | 207.32 ns | 1,568 B | **Baseline** |
| Mapperly | 222.50 ns | 1,552 B | 7% slower, 1% less memory |

For this benchmark we used the `<TSource, TTarget>` methods. 

**Insights:**
> - **Single mapping**: All three libraries perform similarly with sub-nanosecond differences
> - **Collection mapping**: Mapster has a slight edge for bulk operations, while Facet and Mapperly are very close
> - **Memory efficiency**: All libraries are within ~10% of each other for memory allocation
> - **Compile-time generation**: Both Facet and Mapperly benefit from zero-runtime-cost source generation

