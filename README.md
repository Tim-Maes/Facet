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
[![Discord](https://img.shields.io/discord/1443287393825329223?color=%237289da&label=Discord&logo=discord&logoColor=%237289da&style=flat-square)](https://discord.gg/yGDBhGuNMB)

</div>

---

**Facet** is a C# source generator that automatically creates DTOs, mappings, and LINQ projections from your domain models at compile time, eliminating boilerplate with zero runtime cost.

## :gem: What is a Facet?

Think of your domain model as a **gem with many facets**! Different views for different purposes:
- Public APIs need a facet without sensitive data
- Admin endpoints need a different facet with additional fields
- Database queries need efficient projections

Instead of manually creating each facet, **Facet** auto-generates them from a single source of truth.

## :clipboard: Documentation

- **[Documentation & Guides](docs/README.md)**
- **[Facet Dashboard](https://github.com/Tim-Maes/Facet/tree/master/src/Facet.Dashboard)**
- [What is being generated?](docs/07_WhatIsBeingGenerated.md)
- [Configure generated files output location](docs/12_GeneratedFilesOutput.md)
- [Comprehensive article about Facetting](https://tim-maes.com/blog/2025/09/28/facets-in-dotnet-(2)/)

## :star: Features

Click on a section to expand/collapse

<details>
  <summary>Code Generation</summary>
  
- Generate DTOs as classes, records, structs, or record structs
- Constructors & LINQ projection expressions
- Handle complex nested objects & collections automatically
- Preserve XML documentation
  
</details>

<details>
  <summary>Configuration & customization</summary>

- Include/exclude pattern with simple attributes
- Copy data validation attributes
- Reverse & custom mapping configurations (sync & async)
- Patch/update source with change tracking
- Expression transformation utilities for business logic reuse
- **Property mapping with `[MapFrom]`** for declarative property renaming
- **Conditional mapping with `[MapWhen]`** for status-dependent fields

</details>

<details>
  <summary>Additional Features</summary>

- **Flatten** nested objects into top-level properties
- **Wrapper** pattern for reference-based delegation (facades, decorators, ViewModels)
- **Auto-generate CRUD DTOs** (Create, Update, Response, Query, Upsert, Patch)
- **Source signature tracking** for detecting breaking changes when source entities change

</details> 

<details>
  <summary>Integration</summary>

- Full **Entity Framework Core** support with automatic navigation loading
- Works with any LINQ provider (via Facet.Extensions)
- Expression tree transformation for predicates & selectors
- Zero runtime cost and no reflection, everything happens at compile time
- Supports **.NET 8, .NET 9, and .NET 10** (LTS)

</details>

## :rocket: Quick Start

<details>
  <summary>Installation</summary>
  
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

For advanced EF Core custom mappers (with DI support):
```
dotnet add package Facet.Extensions.EFCore.Mapping
```

For expression transformation utilities:
```
dotnet add package Facet.Mapping.Expressions
```
  
</details>
 <details>
    <summary>Define Facets</summary>
   

  ```csharp
 // Example domain models:

  public class User
  {
      public int Id { get; set; }
      public string FirstName { get; set; }
      public string LastName { get; set; }
      public string Email { get; set; }
      public string PasswordHash { get; set; }
      public DateTime DateOfBirth { get; set; }
      public decimal Salary { get; set; }
      public string Department { get; set; }
      public bool IsActive { get; set; }
      public Address HomeAddress { get; set; }
      public Company Employer { get; set; }
      public List<Project> Projects { get; set; }
      public DateTime CreatedAt { get; set; }
      public string InternalNotes { get; set; }
  }

  public class Address
  {
      public string Street { get; set; }
      public string City { get; set; }
      public string State { get; set; }
      public string ZipCode { get; set; }
  }

  public class Company
  {
      public int Id { get; set; }
      public string Name { get; set; }
      public Address Headquarters { get; set; }
  }

  public class Project
  {
      public int Id { get; set; }
      public string Name { get; set; }
      public DateTime StartDate { get; set; }
  }
```

Create focused facets for different scenarios:

```csharp
  // 1. Public API - Exclude all sensitive data
  [Facet(typeof(User),
      exclude: [nameof(User.PasswordHash), nameof(User.Salary), nameof(User.InternalNotes)])]
  public partial record UserPublicDto;

  // 2. Contact Information - Include only specific properties
  [Facet(typeof(User),
      Include = [nameof(User.FirstName), nameof(User.LastName), nameof(User.Email), nameof(User.Department)])]
  public partial record UserContactDto;

  // 3. Query/Filter DTO - Make all properties nullable
  [Facet(typeof(User),
      Include = [nameof(User.FirstName), nameof(User.LastName), nameof(User.Email), nameof(User.Department), nameof(User.IsActive)],
      NullableProperties = true,
      GenerateToSource = false)]
  public partial record UserFilterDto;

  // 4. Validation-Aware DTO - Copy data annotations
  [Facet(typeof(User),
      Include = [nameof(User.FirstName), nameof(User.LastName), nameof(User.Email)],
      CopyAttributes = true)]
  public partial record UserRegistrationDto;

  // 5. Nested Objects - Single nested facet
  [Facet(typeof(Address))]
  public partial record AddressDto;

  [Facet(typeof(User),
      Include = [nameof(User.Id), nameof(User.FirstName), nameof(User.LastName), nameof(User.HomeAddress)],
      NestedFacets = [typeof(AddressDto)])]
  public partial record UserWithAddressDto;
  // Address -> AddressDto automatically
  // Type-safe nested mapping

  // 6. Complex Nested - Multiple nested facets
  [Facet(typeof(Company), NestedFacets = [typeof(AddressDto)])]
  public partial record CompanyDto;

  [Facet(typeof(User),
      exclude: [nameof(User.PasswordHash), nameof(User.Salary), nameof(User.InternalNotes)],
      NestedFacets = [typeof(AddressDto), typeof(CompanyDto)])]
  public partial record UserDetailDto;
  // Multi-level nesting supported

  // 7. Collections - Automatic collection mapping
  [Facet(typeof(Project))]
  public partial record ProjectDto;

  [Facet(typeof(User),
      Include = [nameof(User.Id), nameof(User.FirstName), nameof(User.LastName), nameof(User.Projects)],
      NestedFacets = [typeof(ProjectDto)])]
  public partial record UserWithProjectsDto;
  // List<Project> -> List<ProjectDto> automatically!
  // Arrays, ICollection<T>, IEnumerable<T> all supported

  // 8. Everything Combined
  [Facet(typeof(User),
      exclude: [nameof(User.PasswordHash), nameof(User.Salary), nameof(User.InternalNotes)],
      NestedFacets = [typeof(AddressDto), typeof(CompanyDto), typeof(ProjectDto)],
      CopyAttributes = true)]
  public partial record UserCompleteDto;
  // Excludes sensitive fields
  // Maps nested Address and Company objects
  // Maps Projects collection (List<Project> -> List<ProjectDto>)
  // Copies validation attributes
  // Ready for production APIs
```

</details>

<details>
<summary>Basic Projection of Facets</summary>

```csharp
[Facet(typeof(User))]
public partial class UserFacet { }

// Map your source to facet
var userFacet = user.ToFacet<UserFacet>();
var userFacet = user.ToFacet<User, UserFacet>(); //Much faster

// Map back to source
var user = userFacet.ToSource<User>();
var user = userFacet.ToSource<UserFacet, User>(); //Much faster

// Patch only changed properties back to source
user.ApplyFacet(userFacet);
user.ApplyFacet<User, UserFacet>(userFacet); // Much faster

// Patch with change tracking
bool hasChanges = userFacet.ApplyFacetWithChanges<user, userDto>(userFacet);

// LINQ queries
var users = users.SelectFacets<UserFacet>();
var users = users.SelectFacets<User, UserFacet>(); //Much faster
```
</details>

<details>
  <summary>Custom Sync Mapping</summary>
  
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
</details>

<details>
  <summary>Property Mapping with [MapFrom]</summary>

Rename properties declaratively without custom mapping configurations:

```csharp
public class User
{
    public int Id { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string Email { get; set; }
}

[Facet(typeof(User), GenerateToSource = true)]
public partial class UserDto
{
    // Type-safe property rename with reverse mapping
    [MapFrom(nameof(User.FirstName), Reversible = true)]
    public string Name { get; set; } = string.Empty;

    // Rename multiple properties
    [MapFrom(nameof(User.LastName), Reversible = true)]
    public string FamilyName { get; set; } = string.Empty;

    // Computed expression (not reversible)
    [MapFrom("FirstName + \" \" + LastName")]
    public string FullName { get; set; } = string.Empty;
}

// Usage
var user = new User { Id = 1, FirstName = "John", LastName = "Doe", Email = "john@example.com" };
var dto = new UserDto(user);
// dto.Name = "John" (mapped from FirstName)
// dto.FamilyName = "Doe" (mapped from LastName)
// dto.FullName = "John Doe" (computed expression)

// Reverse mapping works automatically
var entity = dto.ToSource();
// entity.FirstName = "John" (mapped from Name)
// entity.LastName = "Doe" (mapped from FamilyName)

// Projections also work
var dtos = users.SelectFacet<UserDto>().ToList();
```

#### Controlling Reversibility and Projection Inclusion

```csharp
[Facet(typeof(User), GenerateToSource = true)]
public partial class UserDto
{
    // Reversible mapping (included in ToSource) - opt-in
    [MapFrom(nameof(User.FirstName), Reversible = true)]
    public string Name { get; set; } = string.Empty;

    // Default: not reversible (one-way, source → DTO only)
    [MapFrom(nameof(User.LastName))]
    public string DisplayName { get; set; } = string.Empty;

    // Exclude from EF Core projection (for client-side computed values)
    [MapFrom("Name.ToUpper()", IncludeInProjection = false)]
    public string UpperName { get; set; } = string.Empty;
}
```

#### When to Use MapFrom vs Custom Configuration

| Use Case | MapFrom | Custom Config |
|----------|---------|---------------|
| Simple property rename | :white_check_mark: Best choice | Overkill |
| Multiple renames | :white_check_mark: Best choice | Overkill |
| Computed values (expressions) | :white_check_mark: Supported | Alternative |
| Async operations | :x: | :white_check_mark: Required |
| Complex transformations | :x: | :white_check_mark: Required |

**Note**: MapFrom and custom configurations can be combined. Auto-generated mappings (including MapFrom) are applied first, then the custom mapper is called.

</details>

<details>
  <summary>Conditional Mapping with [MapWhen]</summary>

Map properties only when specific conditions are met. Perfect for status-dependent fields, null checks, or role-based data exposure:

```csharp
public class Order
{
    public int Id { get; set; }
    public OrderStatus Status { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? TrackingNumber { get; set; }
    public bool IsActive { get; set; }
    public string? Email { get; set; }
}

[Facet(typeof(Order))]
public partial class OrderDto
{
    // Only map when status is Completed
    [MapWhen("Status == OrderStatus.Completed")]
    public DateTime? CompletedAt { get; set; }

    // Only map when not cancelled
    [MapWhen("Status != OrderStatus.Cancelled")]
    public string? TrackingNumber { get; set; }

    // Boolean condition
    [MapWhen("IsActive")]
    public string? Email { get; set; }
}

// Usage
var order = new Order
{
    Id = 1,
    Status = OrderStatus.Completed,
    CompletedAt = DateTime.Now,
    IsActive = true,
    Email = "user@example.com"
};

var dto = new OrderDto(order);
// dto.CompletedAt = DateTime.Now (condition true)
// dto.Email = "user@example.com" (IsActive is true)

var pendingOrder = new Order { Status = OrderStatus.Pending, Email = "test@example.com" };
var pendingDto = new OrderDto(pendingOrder);
// pendingDto.CompletedAt = null (condition false)
```

#### Multiple Conditions (AND Logic)

```csharp
[Facet(typeof(Order))]
public partial class SecureOrderDto
{
    // Both conditions must be true
    [MapWhen("IsActive")]
    [MapWhen("Status == OrderStatus.Completed")]
    public DateTime? CompletedAt { get; set; }
}
```

#### Supported Conditions

- **Boolean**: `[MapWhen("IsActive")]`
- **Equality**: `[MapWhen("Status == OrderStatus.Completed")]`
- **Inequality**: `[MapWhen("Status != OrderStatus.Cancelled")]`
- **Null checks**: `[MapWhen("Email != null")]`
- **Comparisons**: `[MapWhen("Age >= 18")]`
- **Negation**: `[MapWhen("!IsDeleted")]`

#### Works with EF Core Projections

```csharp
var orders = await dbContext.Orders
    .Where(o => o.IsActive)
    .SelectFacet<OrderDto>()  // Conditions included in SQL
    .ToListAsync();
```

</details>

<details>
  <summary>Async Mapping for I/O Operations</summary>
  
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
</details>

<details>
  <summary>Async Mapping with Dependency Injection</summary>
  
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
</details>

<details>
  <summary>EF Core Integration</summary>

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

#### Automatic Navigation Property Loading (No .Include() Required!)
```csharp
// Define nested facets
[Facet(typeof(Address))]
public partial record AddressDto;

[Facet(typeof(Company), NestedFacets = [typeof(AddressDto)])]
public partial record CompanyDto;

// Navigation properties are automatically loaded - no .Include() needed!
var companies = await dbContext.Companies
    .Where(c => c.IsActive)
    .SelectFacet<CompanyDto>()
    .ToListAsync();

// The HeadquartersAddress navigation property is automatically included!
// EF Core analyzes the projection expression and generates the necessary JOINs

// This also works with collections:
[Facet(typeof(OrderItem))]
public partial record OrderItemDto;

[Facet(typeof(Order), NestedFacets = [typeof(OrderItemDto), typeof(AddressDto)])]
public partial record OrderDto;

var orders = await dbContext.Orders
    .SelectFacet<OrderDto>()  // Automatically includes Items collection and ShippingAddress!
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

#### Advanced: Custom Mappers with EF Core (Facet.Extensions.EFCore.Mapping)

For complex mappings that cannot be expressed as SQL projections (e.g., external service calls, complex type conversions), use the advanced mapping package:

```csharp
// Install: dotnet add package Facet.Extensions.EFCore.Mapping
using Facet.Extensions.EFCore.Mapping;

// Example: Converting separate X, Y properties into a Vector2 type
[Facet(typeof(User), exclude: [nameof(User.X), nameof(User.Y)])]
public partial class UserDto
{
    public Vector2 Position { get; set; }
}

// Static mapper
public class UserMapper : IFacetMapConfigurationAsync<User, UserDto>
{
    public static async Task MapAsync(User source, UserDto target, CancellationToken cancellationToken = default)
    {
        target.Position = new Vector2(source.X, source.Y);
    }
}

// Usage with EF Core queries
var users = await dbContext.Users
    .Where(u => u.IsActive)
    .ToFacetsAsync<User, UserDto, UserMapper>();

// Or with dependency injection
public class UserMapper : IFacetMapConfigurationAsyncInstance<User, UserDto>
{
    private readonly ILocationService _locationService;

    public UserMapper(ILocationService locationService)
    {
        _locationService = locationService;
    }

    public async Task MapAsync(User source, UserDto target, CancellationToken cancellationToken = default)
    {
        target.Position = new Vector2(source.X, source.Y);
        target.Location = await _locationService.GetLocationAsync(source.LocationId);
    }
}

// Usage with DI
var users = await dbContext.Users
    .Where(u => u.IsActive)
    .ToFacetsAsync<User, UserDto>(userMapper);
```

**Note**: Custom mapper methods materialize the query first (execute SQL), then apply your custom logic. All matching properties are auto-mapped first.

</details>

<details>
  <summary>Automatic CRUD DTO Generation with [GenerateDtos]</summary>
  
Generate standard Create, Update, Response, Query, Upsert, and Patch DTOs automatically:

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

#### Entities with Smart Exclusions

```csharp
[GenerateAuditableDtos(
    Types = DtoTypes.Create | DtoTypes.Update | DtoTypes.Response,
    OutputType = OutputType.Record,
    ExcludeProperties = [nameof(Product.Password)])]
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
[GenerateDtos(Types = DtoTypes.Response, ExcludeProperties = [nameof(Schedule.Password), nameof(Schedule.InternalNotes)])]
[GenerateDtos(Types = DtoTypes.Upsert, ExcludeProperties = [nameof(Schedule.Password)])]
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
</details>

<details>
  <summary>Flatten nested objects with [Flatten]</summary>

Flatten nested object hierarchies into top-level properties automatically - perfect for API responses, reports, and denormalized views:

```csharp
// Domain models with nested structure
public class Person
{
    public int Id { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public Address Address { get; set; }
    public ContactInfo ContactInfo { get; set; }
}

public class Address
{
    public string Street { get; set; }
    public string City { get; set; }
    public string ZipCode { get; set; }
    public Country Country { get; set; }
}

public class Country
{
    public string Name { get; set; }
    public string Code { get; set; }
}

public class ContactInfo
{
    public string Email { get; set; }
    public string Phone { get; set; }
}

// Automatically flatten all nested properties
[Flatten(typeof(Person))]
public partial class PersonFlatDto
{
    // Auto-generates:
    // public int Id { get; set; }
    // public string FirstName { get; set; }
    // public string LastName { get; set; }
    // public string AddressStreet { get; set; }
    // public string AddressCity { get; set; }
    // public string AddressZipCode { get; set; }
    // public string AddressCountryName { get; set; }
    // public string AddressCountryCode { get; set; }
    // public string ContactInfoEmail { get; set; }
    // public string ContactInfoPhone { get; set; }
}

// Usage with constructor
var person = new Person
{
    FirstName = "John",
    Address = new Address
    {
        Street = "123 Main St",
        City = "Springfield",
        Country = new Country { Name = "USA", Code = "US" }
    },
    ContactInfo = new ContactInfo { Email = "john@example.com" }
};

var dto = new PersonFlatDto(person);
// dto.AddressStreet = "123 Main St"
// dto.AddressCountryName = "USA"

// Usage with Entity Framework projection
var flatDtos = await dbContext.People
    .Where(p => p.IsActive)
    .Select(PersonFlatDto.Projection)
    .ToListAsync();
```

#### Controlling Depth and Exclusions

```csharp
// Limit flattening depth
[Flatten(typeof(Person), MaxDepth = 2)]
public partial class PersonFlatDepth2Dto
{
    // Includes Address.Street and Address.City
    // Does NOT include Address.Country.* (beyond depth 2)
}

// Exclude specific paths
[Flatten(typeof(Person), nameof(Person.ContactInfo))]
public partial class PersonFlatWithoutContactDto
{
    // All properties except ContactInfo.*
}

[Flatten(typeof(Person), $"{nameof(Person.Address)}.{nameof(Address.Country)}")]
public partial class PersonFlatWithoutCountryDto
{
    // Includes Address.Street, Address.City
    // Excludes Address.Country.*
}
```

#### Naming Strategies

```csharp
// Prefix strategy (default): AddressStreet, AddressCity
[Flatten(typeof(Person), NamingStrategy = FlattenNamingStrategy.Prefix)]
public partial class PersonFlatPrefixDto { }

// Leaf-only strategy: Street, City (may cause collisions)
[Flatten(typeof(Person), NamingStrategy = FlattenNamingStrategy.LeafOnly)]
public partial class PersonFlatLeafDto { }
```

</details>

<details>
  <summary>Reference-based Wrappers with [Wrapper]</summary>

Generate wrapper classes that **delegate** to a source object instead of copying values. Unlike `[Facet]` which creates independent copies, wrappers maintain a reference to the source, so changes propagate:

```csharp
// Domain model
public class User
{
    public int Id { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string Email { get; set; }
    public string Password { get; set; }  // Sensitive!
    public decimal Salary { get; set; }   // Sensitive!
}

// Hide sensitive properties with a facade
[Wrapper(typeof(User), nameof(User.Password), nameof(User.Salary))]
public partial class PublicUserWrapper { }

// Usage - changes propagate to source!
var user = new User { Id = 1, FirstName = "John", Password = "secret" };
var wrapper = new PublicUserWrapper(user);

wrapper.FirstName = "Jane";
Console.WriteLine(user.FirstName);  // "Jane" - source is modified!

// Sensitive properties not accessible
// wrapper.Password;  // Compile error
// wrapper.Salary;    // Compile error
```

#### Read-Only Wrappers (Immutable Facades)

```csharp
// Prevent modifications with ReadOnly mode
[Wrapper(typeof(Product), ReadOnly = true)]
public partial class ReadOnlyProductView { }

var product = new Product { Name = "Laptop", Price = 1299.99m };
var view = new ReadOnlyProductView(product);

// Can read
Console.WriteLine(view.Name);

// Cannot write (compile error CS0200)
// view.Name = "Desktop";  // Property is read-only

// Still reflects source changes
product.Name = "Desktop";
Console.WriteLine(view.Name);  // "Desktop"
```

#### Use Cases

- **Facade Pattern**: Hide sensitive/internal properties from API consumers
- **ViewModel Pattern**: Expose domain model subset to UI with live binding
- **Decorator Pattern**: Add behavior without modifying domain models
- **Memory Efficiency**: Avoid duplicating large object graphs
- **Read-only Views**: Immutable facades

#### Wrapper vs Facet

| Aspect | Facet (Value Copy) | Wrapper (Reference) |
|--------|-------------------|---------------------|
| **Data Storage** | Independent copy | Reference to source |
| **Memory** | Duplicates data | No duplication |
| **Changes** | Independent | Synchronized to source |
| **Use Case** | DTOs, EF projections | Facades, ViewModels |

</details>

<details>
  <summary>Source Signature Tracking for Breaking Change Detection</summary>

Track changes to your source entities and get compile-time warnings when the structure changes. This helps prevent unintended API breaking changes when your EF Core models are modified.

#### Basic Usage

```csharp
// Without tracking (default behavior)
[Facet(typeof(User))]
public partial class UserDto;

// With change detection - add a SourceSignature
[Facet(typeof(User), SourceSignature = "a1b2c3d4")]
public partial class UserDto;
```

#### How It Works

1. The signature is an 8-character hash based on property names and types
2. When the source entity changes (properties added/removed/renamed), the hash changes
3. A compile-time warning (FAC022) alerts you to review and acknowledge the change

#### Example Warning

When someone adds a new property to the `User` entity:

```
warning FAC022: Source entity 'User' structure has changed.
Update SourceSignature to 'e5f6g7h8' to acknowledge this change.
```

#### IDE Code Fix

In Visual Studio or Rider, click the lightbulb to automatically update the signature:

**"Update SourceSignature to 'e5f6g7h8'"**

#### Benefits

| Benefit | Description |
|---------|-------------|
| **Prevents data exposure** | New sensitive fields don't silently appear in API responses |
| **Catches breaking changes** | Removed fields are detected before runtime errors |
| **Explicit acknowledgment** | Forces review of changes before they reach production |
| **Opt-in** | Only active when you set a SourceSignature |

#### Workflow

1. During development, use Facet without signatures for flexibility
2. Before release, add `SourceSignature` to lock the API contract
3. When source entities change, the warning reminds you to review
4. Update the signature to acknowledge intentional changes

</details>

## :earth_americas: The Facet Ecosystem

Facet is modular and consists of several NuGet packages:

- **[Facet](https://github.com/Tim-Maes/Facet/blob/master/README.md)**: The core source generator. Generates DTOs, projections, and mapping code.
- **[Facet.Extensions](https://github.com/Tim-Maes/Facet/blob/master/src/Facet.Extensions/README.md)**: Provider-agnostic extension methods for mapping, projecting and patch updates (works with any LINQ provider, no EF Core dependency).
- **[Facet.Mapping](https://github.com/Tim-Maes/Facet/tree/master/src/Facet.Mapping)**: Advanced static mapping configuration support with async capabilities and dependency injection for complex mapping scenarios.
- **[Facet.Mapping.Expressions](https://github.com/Tim-Maes/Facet/blob/master/src/Facet.Mapping.Expressions/README.md)**: Expression tree transformation utilities for transforming predicates, selectors, and business logic between source entities and their Facet projections.
- **[Facet.Extensions.EFCore](https://github.com/Tim-Maes/Facet/tree/master/src/Facet.Extensions.EFCore)**: Async extension methods for Entity Framework Core (requires EF Core 6+).
- **[Facet.Extensions.EFCore.Mapping](https://github.com/Tim-Maes/Facet/tree/master/src/Facet.Extensions.EFCore.Mapping)**: Advanced custom async mapper support for EF Core queries. Enables complex mappings that cannot be expressed as SQL projections 

## Comparison

| Feature | Facet | AutoMapper | Mapperly | Mapster |
|---------|-------|------------|----------|---------|
| **Generation Time** | Compile | Runtime | Compile | Runtime |
| **EF Core Projections** | :white_check_mark: Auto | :x: Manual | :warning: Manual | :warning: Manual |
| **Navigation Loading** | :white_check_mark: Auto | :x: Manual | :x: Manual | :x: Manual |
| **Flatten/Wrapper/CRUD** | :white_check_mark: Built-in | :x: | :x: | :warning: Limited |
| **Expression Transform** | :white_check_mark: | :x: | :x: | :x: |
| **Breaking Detection** | :white_check_mark: | :x: | :x: | :x: |
| **Conditional Mapping** | :white_check_mark: MapWhen | :warning: Custom | :warning: Custom | :warning: Custom |

**Facet is the only tool that combines compile-time generation with deep EF Core integration.**

## Performance Benchmarks

Note that these are perfomed by using the `<TSource, TDestination>` mapping method overloads wherever possible, as they are significantly faster than the `<TDestination>` versions.

**Simple mapping**

| Method           | Mean      | Ratio        | Allocated | Alloc Ratio |
|----------------- |----------:|-------------:|----------:|------------:|
| Facet            |  5.922 ns |     baseline |      40 B |             |
| Mapperly         |  6.227 ns | 1.05x slower |      40 B |  1.00x more |
| Mapster | 13.243 ns | 2.24x slower |      40 B |  1.00x more |
| AutoMapper       | 31.459 ns | 5.31x slower |      40 B |  1.00x more |

**Nested mapping**

| Method           | Mean      | Ratio        | Allocated | Alloc Ratio |
|----------------- |----------:|-------------:|----------:|------------:|
| Facet            |  5.497 ns |     baseline |      32 B |             |
| Mapperly         |  9.015 ns | 1.64x slower |      72 B |  2.25x more |
| Mapster | 17.743 ns | 3.23x slower |      72 B |  2.25x more |
| AutoMapper       | 36.794 ns | 6.69x slower |      72 B |  2.25x more |



## 💖 Contributors

Facet wouldn't be the same without these awesome contributors. Thank you!

<a href="https://github.com/Tim-Maes/Facet/graphs/contributors">
  <img src="https://contrib.rocks/image?repo=Tim-Maes/Facet" />
</a>

## 💖 Backers & supporters

<a href="https://github.com/pokeparadox">
  <img src="https://images.weserv.nl/?url=github.com/pokeparadox.png&w=64&h=64&mask=circle" width="64" height="64" />
</a>
