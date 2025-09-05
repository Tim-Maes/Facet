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
- :white_check_mark: LINQ projection expressions
- :white_check_mark: Full mapping support with custom mapping configurations
- :white_check_mark: Preserves member and type XML documentation

## :earth_americas: The Facet Ecosystem

Facet is modular and consists of several NuGet packages:

- **Facet**: The core source generator. Generates DTOs, projections, and mapping code.

- **Facet.Extensions**: Provider-agnostic extension methods for mapping and projecting (works with any LINQ provider, no EF Core dependency).

- **Facet.Mapping**: Advanced static mapping configuration support with async capabilities and dependency injection for complex mapping scenarios.

- **Facet.Extensions.EFCore**: Async extension methods for Entity Framework Core (requires EF Core 6+).

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
public partial class UserFacet { }

// Auto-generates constructor, properties, and LINQ projection
var user = user.ToFacet<UserFacet>();
var users = users.SelectFacets<UserFacet>();
```

### Property Exclusion & Field Inclusion
```csharp
// Exclude sensitive properties
string[] excludeFields = { "Password", "Email" };

[Facet(typeof(User), exclude: excludeFields)]
public partial class UserWithoutEmail { }

// Include public fields
[Facet(typeof(Entity), IncludeFields = true)]
public partial class EntityDto { }
```

### Different Type Kinds
```csharp
// Generate as record (immutable by default)
[Facet(typeof(Product))]
public partial record ProductDto;

// Generate as struct (value type)
[Facet(typeof(Point))]
public partial struct PointDto;

// Generate as record struct (immutable value type)
[Facet(typeof(Coordinates))]
public partial record struct CoordinatesDto; // Preserves required/init-only
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

#### Interface Contracts
Generate DTOs that implement specific interfaces for better type safety:

```csharp
// Generate DTOs that implement interfaces
[GenerateDtos(
    Types = DtoTypes.Create, 
    InterfaceContracts = new[] { typeof(ICreateSchedulePayload) })]
public class Schedule
{
    public int Id { get; set; }
    public string Name { get; set; }
    public DateTime StartTime { get; set; }
}

// Generates: CreateScheduleRequest : ICreateSchedulePayload
```

#### Auditable Entities with Smart Exclusions
```csharp
[GenerateAuditableDtos(
    Types = DtoTypes.Create | DtoTypes.Update | DtoTypes.Response,
    OutputType = OutputType.Record,
    ExcludeProperties = new[] { "Password" },
    InterfaceContracts = new[] { typeof(IProductDto) })]
public class Product
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Password { get; set; } // Excluded
    public DateTime CreatedAt { get; set; } // Auto-excluded (audit)
    public string CreatedBy { get; set; } // Auto-excluded (audit)
}

// Auto-excludes audit fields: CreatedAt, UpdatedAt, CreatedBy, UpdatedBy
// All generated DTOs implement IProductDto interface
```

#### Excluding Members from Base Types
```csharp
public abstract class BaseEntity
{
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string CreatedBy { get; set; }
}

public interface IAuditable 
{
    DateTime LastModified { get; set; }
    string ModifiedBy { get; set; }
}

// Exclude all members from base class and interface
[GenerateDtos(
    Types = DtoTypes.Create,
    ExcludeMembersFromType = new[] { typeof(BaseEntity), typeof(IAuditable) })]
public class Product : BaseEntity, IAuditable
{
    public int Id { get; set; }
    public string Name { get; set; }
    public decimal Price { get; set; }
    
    // From BaseEntity - will be excluded
    public DateTime LastModified { get; set; }
    public string ModifiedBy { get; set; }
}

// Generates: CreateProductRequest with only Id, Name, Price
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

#### Perfect for RESTful APIs
```csharp
[HttpPost]
public async Task<ActionResult<ScheduleResponse>> CreateSchedule(CreateScheduleRequest request)
{
    var schedule = new Schedule
    {
        Name = request.Name,
        // Map other properties;;;
    };

    context.Schedules.Add(schedule);
    await context.SaveChangesAsync();
    return schedule.ToFacet<ScheduleResponse>();
}

[HttpPut("{id}")]
public async Task<ActionResult<ScheduleResponse>> UpsertSchedule(int id, UpsertScheduleRequest body)
{
    var schedule = context.GetScheduleById(id);
    if (schedule == null) return NotFound();
    
    // Ensure the body ID matches the route ID  
    body = body with { Id = id };
    
    schedule.UpdateFromFacet(body, context);
    await context.SaveChangesAsync();
    return schedule.ToFacet<ScheduleResponse>();
}
```

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

**Insights:**
> - **Single mapping**: All three libraries perform similarly with sub-nanosecond differences
> - **Collection mapping**: Mapster has a slight edge for bulk operations, while Facet and Mapperly are very close
> - **Memory efficiency**: All libraries are within ~10% of each other for memory allocation
> - **Compile-time generation**: Both Facet and Mapperly benefit from zero-runtime-cost source generation

