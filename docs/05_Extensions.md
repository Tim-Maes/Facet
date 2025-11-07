# Extension Methods (LINQ, EF Core, etc.)

Facet.Extensions provides a set of provider-agnostic extension methods for mapping and projecting between your domain entities and generated facet types.
For async EF Core support, see the separate Facet.Extensions.EFCore package.

## Methods (Facet.Extensions)

| Method                              | Description                                                      |
|------------------------------------- |------------------------------------------------------------------|
| `ToFacet<TSource, TTarget>()`        | Map a single object with explicit source type (compile-time).   |
| `ToFacet<TTarget>()`                 | Map a single object with inferred source type (runtime).        |
| `SelectFacets<TSource, TTarget>()`   | Map an `IEnumerable<TSource>` with explicit types.              |
| `SelectFacets<TTarget>()`            | Map an `IEnumerable` with inferred source type.                 |
| `SelectFacet<TSource, TTarget>()`    | Project an `IQueryable<TSource>` with explicit types.           |
| `SelectFacet<TTarget>()`             | Project an `IQueryable` with inferred source type.              |

## Methods (Facet.Extensions.EFCore)

| Method                              | Description                                                      |
|------------------------------------- |------------------------------------------------------------------|
| `ToFacetsAsync<TSource, TTarget>()`  | Async projection to `List<TTarget>` with explicit source type.    |
| `ToFacetsAsync<TTarget>()`           | Async projection to `List<TTarget>` with inferred source type.    |
| `FirstFacetAsync<TSource, TTarget>()`| Async projection to first/default with explicit source type.      |
| `FirstFacetAsync<TTarget>()`         | Async projection to first/default with inferred source type.      |
| `SingleFacetAsync<TSource, TTarget>()`| Async projection to single with explicit source type.            |
| `SingleFacetAsync<TTarget>()`        | Async projection to single with inferred source type.            |
| `UpdateFromFacet<TEntity, TFacet>()` | Update entity with changed properties from facet DTO.            |
| `UpdateFromFacetAsync<TEntity, TFacet>()`| Async update entity with changed properties from facet DTO.  |
| `UpdateFromFacetWithChanges<TEntity, TFacet>()`| Update entity and return information about changed properties. |

## Methods (Facet.Extensions.EFCore.Mapping)

For advanced custom async mapper support, install the separate package:

```bash
dotnet add package Facet.Extensions.EFCore.Mapping
```

| Method                              | Description                                                      |
|------------------------------------- |------------------------------------------------------------------|
| `ToFacetsAsync<TSource, TTarget>(mapper)` | Async projection with custom instance mapper (DI support).    |
| `ToFacetsAsync<TSource, TTarget, TAsyncMapper>()` | Async projection with static async mapper.              |
| `FirstFacetAsync<TSource, TTarget>(mapper)` | Get first with custom instance mapper (DI support).        |
| `FirstFacetAsync<TSource, TTarget, TAsyncMapper>()` | Get first with static async mapper.                   |
| `SingleFacetAsync<TSource, TTarget>(mapper)` | Get single with custom instance mapper (DI support).      |
| `SingleFacetAsync<TSource, TTarget, TAsyncMapper>()` | Get single with static async mapper.                 |

## Usage Examples

### Extensions

```bash
dotnet add package Facet.Extensions
```

```csharp
using Facet.Extensions;

// provider-agnostic
// Single object
var dto = person.ToFacet<PersonDto>();

// Enumerable
var dtos = people.SelectFacets<PersonDto>();
```

### EF Core Extensions

```bash
dotnet add package Facet.Extensions.EFCore
```

```csharp
// IQueryable (LINQ/EF Core)

using Facet.Extensions.EFCore;

var query = dbContext.People.SelectFacet<PersonDto>();

// Async (EF Core)
var dtosAsync = await dbContext.People.ToFacetsAsync<PersonDto>();
var dtosInferred = await dbContext.People.ToFacetsAsync<PersonDto>();

var firstDto = await dbContext.People.FirstFacetAsync<Person, PersonDto>();
var firstInferred = await dbContext.People.FirstFacetAsync<PersonDto>();

var singleDto = await dbContext.People.SingleFacetAsync<Person, PersonDto>();
var singleInferred = await dbContext.People.SingleFacetAsync<PersonDto>();
```

#### Automatic Navigation Property Loading (No `.Include()` Required!)

When using nested facets, EF Core automatically loads navigation properties without requiring explicit `.Include()` calls:

```csharp
// Define nested facets
[Facet(typeof(Address))]
public partial record AddressDto;

[Facet(typeof(Company), NestedFacets = [typeof(AddressDto)])]
public partial record CompanyDto;

// Navigation properties are automatically loaded!
var companies = await dbContext.Companies
    .Where(c => c.IsActive)
    .ToFacetsAsync<CompanyDto>();

// The HeadquartersAddress navigation property is automatically included
// EF Core sees the property access in the projection and generates JOINs

// Works with all projection methods:
await dbContext.Companies.ToFacetsAsync<CompanyDto>();       
await dbContext.Companies.FirstFacetAsync<CompanyDto>();    
await dbContext.Companies.SelectFacet<CompanyDto>().ToListAsync();

// Also works with collecstions:
[Facet(typeof(OrderItem))]
public partial record OrderItemDto;

[Facet(typeof(Order), NestedFacets = [typeof(OrderItemDto), typeof(AddressDto)])]
public partial record OrderDto;

var orders = await dbContext.Orders.ToFacetsAsync<OrderDto>();
// Automatically includes Items collection and ShippingAddress!
```

### EF Core Reverse Mapping (UpdateFromFacet)

```csharp
using Facet.Extensions.EFCore;

[HttpPut("{id}")]
public async Task<IActionResult> UpdateUser(int id, [FromBody] UpdateUserDto dto)
{
    var user = await context.Users.FindAsync(id);
    if (user == null) return NotFound();
    
    // Only updates properties that actually changed - selective update
    user.UpdateFromFacet(dto, context);
    
    await context.SaveChangesAsync();
    return Ok();
}

// With change tracking for auditing
var result = user.UpdateFromFacetWithChanges(dto, context);
if (result.HasChanges)
{
    logger.LogInformation("User {UserId} updated. Changed: {Properties}", 
        user.Id, string.Join(", ", result.ChangedProperties));
}

// Async version
await user.UpdateFromFacetAsync(dto, context);
```

### Complete API Example

```csharp
// Domain model
public class User
{
    public int Id { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string Email { get; set; }
    public string Password { get; set; }  // Sensitive
    public DateTime CreatedAt { get; set; }  // Immutable
}

// Update DTO - excludes sensitive/immutable properties
[Facet(typeof(User), "Password", "CreatedAt")]
public partial class UpdateUserDto { }

// API Controller
[ApiController]
public class UsersController : ControllerBase
{
    // GET: Entity -> Facet
    [HttpGet("{id}")]
    public async Task<ActionResult<UserDto>> GetUser(int id)
    {
        var user = await context.Users.FindAsync(id);
        if (user == null) return NotFound();
        
        return user.ToFacet<UserDto>();  // Forward mapping
    }
    
    // PUT: Facet -> Entity (selective update)
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateUser(int id, UpdateUserDto dto)
    {
        var user = await context.Users.FindAsync(id);
        if (user == null) return NotFound();
        
        user.UpdateFromFacet(dto, context);  // Reverse mapping
        await context.SaveChangesAsync();
        
        return NoContent();
    }
}
```

### EF Core Custom Mappers (Advanced)

For complex mappings that cannot be expressed as SQL projections (e.g., calling external services, complex type conversions like Vector2, or async operations), install the advanced mapping package:

```bash
dotnet add package Facet.Extensions.EFCore.Mapping
```

```csharp
using Facet.Extensions.EFCore.Mapping;  // Advanced mappers
using Facet.Mapping;

// Define your DTO with excluded properties
[Facet(typeof(User), exclude: ["X", "Y"])]
public partial class UserDto
{
    public Vector2 Position { get; set; }
}

// Option 1: Static mapper (no DI)
public class UserMapper : IFacetMapConfigurationAsync<User, UserDto>
{
    public static async Task MapAsync(User source, UserDto target, CancellationToken cancellationToken = default)
    {
        target.Position = new Vector2(source.X, source.Y);
    }
}

// Option 2: Instance mapper with dependency injection
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

// Usage with static mapper
var users = await dbContext.Users
    .Where(u => u.IsActive)
    .ToFacetsAsync<User, UserDto, UserMapper>();

// Usage with instance mapper (DI)
var users = await dbContext.Users
    .Where(u => u.IsActive)
    .ToFacetsAsync<User, UserDto>(userMapper);
```

**Note:** Custom mapper methods materialize the query first (execute SQL), then apply your custom logic. All matching properties are auto-mapped first.

See the [Facet.Extensions.EFCore.Mapping](https://www.nuget.org/packages/Facet.Extensions.EFCore.Mapping) package for more details.

---

See [Quick Start](02_QuickStart.md) for setup, [Facet.Extensions.EFCore](https://www.nuget.org/packages/Facet.Extensions.EFCore) for async EF Core support, and [Facet.Extensions.EFCore.Mapping](https://www.nuget.org/packages/Facet.Extensions.EFCore.Mapping) for advanced custom async mappers.
