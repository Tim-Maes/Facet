# Facet.Extensions.EFCore

EF Core async extension methods for the Facet library, enabling one-line async mapping and projection between your domain entities and generated facet types.

## Key Features

- **Forward Mapping**: Entity ? Facet DTO
  - Async projection to `List<TTarget>`: `ToFacetsAsync<TSource,TTarget>()`
  - Async projection to first or default: `FirstFacetAsync<TSource,TTarget>()`
  - Async projection to single: `SingleFacetAsync<TSource,TTarget>()`

- **Reverse Mapping**: Facet DTO ? Entity (NEW!)
  - Selective entity updates: `UpdateFromFacet<TEntity,TFacet>()`
  - Async entity updates: `UpdateFromFacetAsync<TEntity,TFacet>()`
  - Update with change tracking: `UpdateFromFacetWithChanges<TEntity,TFacet>()`

All methods leverage your already generated ctor or Projection property and require EF Core 6+.

## Getting Started

### 1. Install packages

```bash
dotnet add package Facet.Extensions.EFCore
```

### 2. Import namespaces

```csharp
using Facet.Extensions.EFCore; // for async EF Core extension methods
```

## Forward Mapping (Entity ? DTO)

### 3. Use async mapping in EF Core

```csharp
// Async projection to list
var dtos = await dbContext.People.ToFacetsAsync<Person, PersonDto>();

// Async projection to first or default
var firstDto = await dbContext.People.FirstFacetAsync<Person, PersonDto>();

// Async projection to single
var singleDto = await dbContext.People.SingleFacetAsync<Person, PersonDto>();
```

## Reverse Mapping (DTO ? Entity)

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

// Async version (for future extensibility)
await user.UpdateFromFacetAsync(dto, context);
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
    
    // GET: Forward mapping (Entity ? DTO)
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ProductDto>>> GetProducts()
    {
        return await _context.Products
            .Where(p => p.IsActive)
            .ToFacetsAsync<Product, ProductDto>();
    }
    
    [HttpGet("{id}")]
    public async Task<ActionResult<ProductDto>> GetProduct(int id)
    {
        var product = await _context.Products
            .FirstFacetAsync<Product, ProductDto>(p => p.Id == id);
            
        return product == null ? NotFound() : product;
    }
    
    // PUT: Reverse mapping (DTO ? Entity)
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

## Performance Benefits

### Selective Updates
- **Efficient SQL**: Only updates columns that actually changed
- **Reduced Conflicts**: Minimizes optimistic concurrency conflicts
- **Better Performance**: Smaller UPDATE statements
- **Cleaner Logs**: Audit trails only show actual changes

### Generated SQL Comparison

**Traditional approach** (updates all properties):
```sql
UPDATE Products SET 
    Name = @p0,         -- Even if unchanged
    Description = @p1,  -- Even if unchanged
    Price = @p2         -- Even if unchanged
WHERE Id = @p3
```

**UpdateFromFacet** (selective updates):
```sql
UPDATE Products SET 
    Price = @p0         -- Only changed property
WHERE Id = @p1
```

## API Reference

| Method | Description | Use Case |
|--------|-------------|----------|
| `ToFacetsAsync<TSource, TTarget>()` | Project query to DTO list | GET endpoints |
| `FirstFacetAsync<TSource, TTarget>()` | Project first result to DTO | GET single item |
| `SingleFacetAsync<TSource, TTarget>()` | Project single result to DTO | GET single item (strict) |
| `UpdateFromFacet<TEntity, TFacet>()` | Update entity from DTO | PUT/PATCH endpoints |
| `UpdateFromFacetAsync<TEntity, TFacet>()` | Async update entity from DTO | PUT/PATCH with async logic |
| `UpdateFromFacetWithChanges<TEntity, TFacet>()` | Update with change tracking | Auditing/logging scenarios |

## Requirements

- Facet.Extensions
- .NET 6.0+
- Microsoft.EntityFrameworkCore 6.0+

---

For provider-agnostic sync and LINQ methods, see [Facet.Extensions](https://www.nuget.org/packages/Facet.Extensions).
