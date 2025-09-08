# Facet.Extensions.EFCore

EF Core async extension methods and fluent navigation builders for the Facet library, enabling one-line async mapping/projection and compile‑time guided navigation between your entities and generated shapes/DTOs.

## Key Features

- **Forward Mapping**: Entity -> Facet DTO
  - Async projection to `List<TTarget>`: `ToFacetsAsync<TSource,TTarget>()` or `ToFacetsAsync<TTarget>()`
  - Async projection to first or default: `FirstFacetAsync<TSource,TTarget>()` or `FirstFacetAsync<TTarget>()`
  - Async projection to single: `SingleFacetAsync<TSource,TTarget>()` or `SingleFacetAsync<TTarget>()`

- **Reverse Mapping**: Facet DTO -> Entity (NEW!)
  - Selective entity updates: `UpdateFromFacet<TEntity,TFacet>()`
  - Async entity updates: `UpdateFromFacetAsync<TEntity,TFacet>()`
  - Update with change tracking: `UpdateFromFacetWithChanges<TEntity,TFacet>()`
  - Advanced filtering with skip flags, exclusions, and predicates
  - Property-level ignore attribute: `FacetUpdateIgnoreAttribute`

- **Fluent Navigation (NEW!)**
  - Generated `DbContext` entry points per entity: `FacetUser()`, `FacetOrder()`, etc.
  - Chain navigations with `WithXxx()` methods (e.g., `WithOrders()`, `WithUser()`).
  - Terminal methods: `ToListAsync()`, `FirstOrDefaultAsync()`, `GetByIdAsync(id)`.
  - Strongly-typed shape/capability interfaces (e.g., `IUserShape`, `IUserWithOrders<IOrderShape>`).
  - Generic entry: `DbContext.Facet<TEntity, TDto>()` for validation and query base.

All methods leverage your already generated ctor or Projection property and require EF Core 6+.

## Getting Started

### 1. Install packages

```bash
dotnet add package Facet.Extensions.EFCore
```

### 2. Import namespaces

```csharp
using Facet.Extensions.EFCore; // for async EF Core extension methods

// Fluent navigation entry points live in your DbContext's namespace
// once you reference Facet.Extensions.EFCore and build (source-generated).
```

## Fluent Navigation

Start fluent, shape-safe queries from generated `DbContext` entry points. Builders expose `WithXxx()` navigation methods that refine the returned shape, and terminal async methods to execute.

```csharp
// Simple listing (scalar shape)
var users = await db.FacetUser().ToListAsync(); // returns List<IUserShape>

// Include a collection navigation
var usersWithOrders = await db
    .FacetUser()
    .WithOrders()                       // shape becomes IUserWithOrders<IOrderShape>
    .ToListAsync();

// Single entity with a reference navigation
var order = await db
    .FacetOrder()
    .WithUser()                         // shape becomes IOrderWithUser<IUserShape>
    .FirstOrDefaultAsync();

// Fetch by key
var user = await db.FacetUser().GetByIdAsync(id);

// Advanced (generic) entry point – validates TDto has Projection and returns an IQueryable<TEntity>
var query = db.Facet<User, UserResponse>();
var results = await query.SelectFacet<UserResponse>().ToListAsync();
```

Notes
- Shapes are interfaces generated for each entity (e.g., `IUserShape`, `IOrderShape`).
- When you call `WithOrders()`, the builder’s type changes to a capability interface (e.g., `IUserWithOrders<IOrderShape>`) that adds a typed `Orders` property to the shape.
- Nested navigation configuration (`WithOrders<TNestedShape>(...)`) is planned; the signature may be present but can be not yet implemented.

## Forward Mapping (Entity -> DTO)

### 3. Use async mapping in EF Core

```csharp
// Async projection to list (source type inferred)
var dtos = await dbContext.People.ToFacetsAsync<PersonDto>();

// Async projection to first or default (source type inferred)
var firstDto = await dbContext.People.FirstFacetAsync<PersonDto>();

// Async projection to single (source type inferred)
var singleDto = await dbContext.People.SingleFacetAsync<PersonDto>();

// Legacy explicit syntax still supported
var dtosExplicit = await dbContext.People.ToFacetsAsync<Person, PersonDto>();
```

## Reverse Mapping (DTO -> Entity)

### 4. Use selective entity updates

```csharp
// Define update DTO (excludes sensitive/immutable properties)
[Facet(typeof(User), "Password", "CreatedAt")]
public partial class UpdateUserDto { }

// API Controller
[HttpPut("{id}")]
public async Task<IActionResult> UpdateUser(int id, UpdateUserDto dto)
{
    var user = await context.Users.FindAsync(id);
    if (user == null) return NotFound();
    
    // Only updates properties that actually changed
    user.UpdateFromFacet(dto, context);
    
    await context.SaveChangesAsync();
    return NoContent();
}
```

### 5. Advanced scenarios

```csharp
// With change tracking for auditing
var result = user.UpdateFromFacetWithChanges(dto, context);
if (result.HasChanges)
{
    logger.LogInformation("User {UserId} updated. Changed: {Properties}", 
        user.Id, string.Join(", ", result.ChangedProperties));
}

// Advanced filtering with skip flags
user.UpdateFromFacet(dto, context, 
    skipKeys: true,           // Skip primary keys (default)
    skipConcurrency: true,    // Skip concurrency tokens (default)  
    skipNavigations: true,    // Skip navigation properties (default)
    excludedProperties: new[] { "CreatedBy", "LastModified" },
    propertyPredicate: prop => prop.Name.StartsWith("Public"));

// Using FacetUpdateIgnore attribute
public class UserDto
{
    public string Name { get; set; }
    
    [FacetUpdateIgnore]  // This property will be ignored during updates
    public string InternalNotes { get; set; }
}

// Async version (for future extensibility)
await user.UpdateFromFacetAsync(dto, context, 
    excludedProperties: new[] { "Password" });
```

## Shapes, Selectors, and Projections

Facet generates shape interfaces and internal selector classes that power fluent navigation and projection:

```csharp
// Scalar shape
public interface IUserShape
{
    int Id { get; }
    string FirstName { get; }
    string LastName { get; }
    string Email { get; }
    bool IsActive { get; }
    DateTime CreatedAt { get; }
}

// Capability interface when Orders are included via WithOrders()
public interface IUserWithOrders<TOrder> : IUserShape
{
    IReadOnlyList<TOrder> Orders { get; }
}

// Internal selectors provide expressions used by SelectFacet<>()
internal static class UserSelectors
{
    public static Expression<Func<User, IUserShape>> BaseShape { get; }
}
```

## Complete Example

```csharp
// Domain entity
public class Product
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public decimal Price { get; set; }
    public DateTime CreatedAt { get; set; }  // Immutable
    public string InternalNotes { get; set; }  // Sensitive
}

// Read DTO (for GET operations)
[Facet(typeof(Product), "InternalNotes")]
public partial class ProductDto { }

// Update DTO (for PUT operations - excludes immutable/sensitive fields)
[Facet(typeof(Product), "Id", "CreatedAt", "InternalNotes")]
public partial class UpdateProductDto { }

// API Controller
[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    
    public ProductsController(ApplicationDbContext context)
    {
        _context = context;
    }
    
    // GET: Forward mapping (Entity -> DTO)
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ProductDto>>> GetProducts()
    {
        return await _context.Products
            .Where(p => p.IsActive)
            .ToFacetsAsync<ProductDto>();  // Source type inferred
    }
    
    [HttpGet("{id}")]
    public async Task<ActionResult<ProductDto>> GetProduct(int id)
    {
        var product = await _context.Products
            .Where(p => p.Id == id)
            .FirstFacetAsync<ProductDto>();  // Source type inferred
            
        return product == null ? NotFound() : product;
    }
    
    // PUT: Reverse mapping (DTO -> Entity)
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateProduct(int id, UpdateProductDto dto)
    {
        var product = await _context.Products.FindAsync(id);
        if (product == null) return NotFound();
        
        // Selective update - only changed properties
        var result = product.UpdateFromFacetWithChanges(dto, _context);
        
        if (result.HasChanges)
        {
            await _context.SaveChangesAsync();
            
            // Optional: Log what changed
            logger.LogInformation("Product {ProductId} updated. Changed: {Properties}", 
                id, string.Join(", ", result.ChangedProperties));
        }
        
        return NoContent();
    }
}
```

## API Reference

| Method | Description | Use Case |
|--------|-------------|----------|
| `ToFacetsAsync<TTarget>()` | Project query to DTO list (source inferred) | GET endpoints |
| `ToFacetsAsync<TSource, TTarget>()` | Project query to DTO list (explicit types) | Legacy/explicit typing |
| `FirstFacetAsync<TTarget>()` | Get first DTO or null (source inferred) | GET single item |
| `FirstFacetAsync<TSource, TTarget>()` | Get first DTO or null (explicit types) | Legacy/explicit typing |
| `SingleFacetAsync<TTarget>()` | Get single DTO (source inferred) | GET unique item |
| `SingleFacetAsync<TSource, TTarget>()` | Get single DTO (explicit types) | Legacy/explicit typing |
| `UpdateFromFacet<TEntity, TFacet>()` | Selective entity update with advanced filtering | PUT/PATCH endpoints |
| `UpdateFromFacetWithChanges<TEntity, TFacet>()` | Update with change tracking and advanced filtering | Auditing scenarios |
| `UpdateFromFacetAsync<TEntity, TFacet>()` | Async selective update with advanced filtering | Future extensibility |

Fluent Navigation Builders

- `DbContext.Facet{EntityName}()` → returns `Facet{EntityName}Builder<I{EntityName}Shape>`
- `WithXxx()` → include navigation and refine shape (e.g., `IUserWithOrders<IOrderShape>`)
- `ToListAsync()` → execute and return `List<TShape>`
- `FirstOrDefaultAsync()` → execute and return `TShape?`
- `GetByIdAsync(id)` → execute a keyed lookup and return `TShape?`

Generic Entry Point

- `DbContext.Facet<TEntity, TDto>()` → returns `IQueryable<TEntity>` and validates `TDto.Projection` exists

### Advanced Update Parameters

All `UpdateFromFacet*` methods support these optional parameters:

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `skipKeys` | `bool` | `true` | Skip primary key properties |
| `skipConcurrency` | `bool` | `true` | Skip concurrency token properties |
| `skipNavigations` | `bool` | `true` | Skip navigation properties |
| `excludedProperties` | `IEnumerable<string>?` | `null` | Specific property names to ignore |
| `propertyPredicate` | `Func<PropertyInfo, bool>?` | `null` | Custom predicate for filtering properties |

### Property-Level Control

Use `FacetUpdateIgnoreAttribute` to mark specific DTO properties that should never be updated:

```csharp
using Facet.Extensions.EFCore;

public class UpdateUserDto  
{
    public string FirstName { get; set; }
    
    [FacetUpdateIgnore]
    public string InternalNotes { get; set; } // Will be ignored during updates
}
```

## Requirements

- Facet v1.6.0+
- Entity Framework Core 6+
- .NET 6+
