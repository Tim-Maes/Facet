# Extension Methods (LINQ, EF Core, etc.)

Facet.Extensions provides a set of provider-agnostic extension methods for mapping and projecting between your domain entities and generated facet types.
For async EF Core support, see the separate Facet.Extensions.EFCore package.

## Methods (Facet.Extensions)

| Method                              | Description                                                      |
|------------------------------------- |------------------------------------------------------------------|
| `ToFacet<TTarget>()`        | Map a single object using the generated constructor.              |
| `ToFacet<TSource, TTarget>()`        | Map a single object using the generated constructor.              |
| `SelectFacets<TTarget>()`   | Map an `IEnumerable<TSource>` to `IEnumerable<TTarget>`.          |
| `SelectFacets<TSource, TTarget>()`   | Map an `IEnumerable<TSource>` to `IEnumerable<TTarget>`.          |
| `SelectFacet<TTarget>()`    | Project an `IQueryable<TSource>` to `IQueryable<TTarget>`.        |
| `SelectFacet<TSource, TTarget>()`    | Project an `IQueryable<TSource>` to `IQueryable<TTarget>`.        |

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
var query = dbContext.People.SelectFacet<Person, PersonDto>();

// Async (EF Core) 
var dtosAsync = await dbContext.People.ToFacetsAsync<Person, PersonDto>();
var dtosInferred = await dbContext.People.ToFacetsAsync<PersonDto>();

var firstDto = await dbContext.People.FirstFacetAsync<Person, PersonDto>();
var firstInferred = await dbContext.People.FirstFacetAsync<PersonDto>();

var singleDto = await dbContext.People.SingleFacetAsync<Person, PersonDto>();
var singleInferred = await dbContext.People.SingleFacetAsync<PersonDto>();
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

---

See [Quick Start](02_QuickStart.md) for setup and [Facet.Extensions.EFCore](https://www.nuget.org/packages/Facet.Extensions.EFCore) for async EF Core support.
