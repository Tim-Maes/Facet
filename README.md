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

**Facet** is a C# source generator that automatically creates DTOs, mappings, and LINQ projections from your domain models at compile time, eliminating boilerplate with zero runtime cost.

## :gem: What is a Facet?

Think of your domain model as a **gem with many facets**! Different views for different purposes:
- Public APIs need a facet without sensitive data
- Admin endpoints need a different facet with additional fields
- Database queries need efficient projections

Instead of manually creating each facet, **Facet** auto-generates them from a single source of truth.

## :clipboard: Documentation

- **[Documentation & Guides](docs/README.md)**
- [What is being generated?](docs/07_WhatIsBeingGenerated.md)
- [Configure generated files output location](docs/12_GeneratedFilesOutput.md)
- [Comprehensive article about Facetting](https://tim-maes.com/blog/2025/09/28/facets-in-dotnet-(2)/)

## :star: Features

**Code Generation**
- Generate DTOs as classes, records, structs, or record structs
- Constructors & LINQ projection expressions
- Handle complex nested objects & collections automatically
- Preserve XML documentation

**Configuration & Customization**
- Include/exclude properties with simple attributes
- Copy data validation attributes
- Custom mapping configurations (sync & async)
- Expression transformation utilities for business logic reuse

**Additional Patterns**
- **Flatten** nested objects into top-level properties
- **Wrapper** pattern for reference-based delegation (facades, decorators, ViewModels)
- **Auto-generate CRUD DTOs** (Create, Update, Response, Query, Upsert)

**Integration**
- Full **Entity Framework Core** support with automatic navigation loading
- Works with any LINQ provider (via Facet.Extensions)
- Expression tree transformation for predicates & selectors
- Zero runtime cost and no reflection, everything happens at compile time

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
    <summary>Facets usage</summary>
   

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
      exclude: ["PasswordHash", "Salary", "InternalNotes"])]
  public partial record UserPublicDto;

  // 2. Contact Information - Include only specific properties
  [Facet(typeof(User),
      Include = ["FirstName", "LastName", "Email", "Department"])]
  public partial record UserContactDto;

  // 3. Query/Filter DTO - Make all properties nullable
  [Facet(typeof(User),
      Include = ["FirstName", "LastName", "Email", "Department", "IsActive"],
      NullableProperties = true,
      GenerateToSource = false)]
  public partial record UserFilterDto;

  // 4. Validation-Aware DTO - Copy data annotations
  [Facet(typeof(User),
      Include = ["FirstName", "LastName", "Email"],
      CopyAttributes = true)]
  public partial record UserRegistrationDto;

  // 5. Nested Objects - Single nested facet
  [Facet(typeof(Address))]
  public partial record AddressDto;

  [Facet(typeof(User),
      Include = ["Id", "FirstName", "LastName", "HomeAddress"],
      NestedFacets = [typeof(AddressDto)])]
  public partial record UserWithAddressDto;
  // Address -> AddressDto automatically
  // Type-safe nested mapping

  // 6. Complex Nested - Multiple nested facets
  [Facet(typeof(Company), NestedFacets = [typeof(AddressDto)])]
  public partial record CompanyDto;

  [Facet(typeof(User),
      exclude: ["PasswordHash", "Salary", "InternalNotes"],
      NestedFacets = [typeof(AddressDto), typeof(CompanyDto)])]
  public partial record UserDetailDto;
  // Multi-level nesting supported

  // 7. Collections - Automatic collection mapping
  [Facet(typeof(Project))]
  public partial record ProjectDto;

  [Facet(typeof(User),
      Include = ["Id", "FirstName", "LastName", "Projects"],
      NestedFacets = [typeof(ProjectDto)])]
  public partial record UserWithProjectsDto;
  // List<Project> -> List<ProjectDto> automatically!
  // Arrays, ICollection<T>, IEnumerable<T> all supported

  // 8. Everything Combined
  [Facet(typeof(User),
      exclude: ["PasswordHash", "Salary", "InternalNotes"],
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

// Auto-generates constructor, properties, and LINQ projection
var userFacet = user.ToFacet<UserFacet>();
var userFacet = user.ToFacet<User, UserFacet>(); //Much faster

var user = userFacet.ToSource<User>();
var user = userFacet.ToSource<UserFacet, User>(); //Much faster

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
[Facet(typeof(User), exclude: ["X", "Y"])]
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

#### Entities with Smart Exclusions

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
[Flatten(typeof(Person), "ContactInfo")]
public partial class PersonFlatWithoutContactDto
{
    // All properties except ContactInfo.*
}

[Flatten(typeof(Person), "Address.Country")]
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
[Wrapper(typeof(User), "Password", "Salary")]
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

## :earth_americas: The Facet Ecosystem

Facet is modular and consists of several NuGet packages:

- **[Facet](https://github.com/Tim-Maes/Facet/blob/master/README.md)**: The core source generator. Generates DTOs, projections, and mapping code.
- **[Facet.Extensions](https://github.com/Tim-Maes/Facet/blob/master/src/Facet.Extensions/README.md)**: Provider-agnostic extension methods for mapping and projecting (works with any LINQ provider, no EF Core dependency).
- **[Facet.Mapping](https://github.com/Tim-Maes/Facet/tree/master/src/Facet.Mapping)**: Advanced static mapping configuration support with async capabilities and dependency injection for complex mapping scenarios.
- **[Facet.Mapping.Expressions](https://github.com/Tim-Maes/Facet/blob/master/src/Facet.Mapping.Expressions/README.md)**: Expression tree transformation utilities for transforming predicates, selectors, and business logic between source entities and their Facet projections.
- **[Facet.Extensions.EFCore](https://github.com/Tim-Maes/Facet/tree/master/src/Facet.Extensions.EFCore)**: Async extension methods for Entity Framework Core (requires EF Core 6+).
- **[Facet.Extensions.EFCore.Mapping](https://github.com/Tim-Maes/Facet/tree/master/src/Facet.Extensions.EFCore.Mapping)**: Advanced custom async mapper support for EF Core queries. Enables complex mappings that cannot be expressed as SQL projections 

## Comparison

| Facet | AutoMapper | Mapperly | Mapster |
|-------|------------|----------|---------|
| :white_check_mark: Compile-time generation | :x: Runtime reflection | :white_check_mark: Source generation | :warning: Runtime codegen |
| :white_check_mark: EF Core LINQ projections | :x: Manual Select() | :white_check_mark: Manual setup | :warning: Manual setup |
| :white_check_mark: Auto navigation loading | :x: Manual .Include() | :x: Manual .Include() | :x: Manual .Include() |
| :white_check_mark: Flatten, Wrapper, CRUD gen | :x: No | :x: No | :warning: Limited |
| :white_check_mark: Expression transformation | :x: No | :x: No | :x: No |

**Facet is the only tool that combines compile-time generation with deep EF Core integration.**

## Contributors

<a href="https://github.com/Tim-Maes/Facet/graphs/contributors">
  <img src="https://contrib.rocks/image?repo=Tim-Maes/Facet" />
</a>
