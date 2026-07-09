# Facet.Extensions.EFCore

EF Core async extension methods for the Facet library, enabling one-line async mapping and projection between your domain entities and generated facet types.

## Key Features

- **Forward Mapping**: Entity -> Facet DTO
  - Async projection to `List<TTarget>`: `ToFacetsAsync<TSource,TTarget>()` or `ToFacetsAsync<TTarget>()`
  - Async projection to first or default: `FirstFacetAsync<TSource,TTarget>()` or `FirstFacetAsync<TTarget>()`
  - Async projection to single: `SingleFacetAsync<TSource,TTarget>()` or `SingleFacetAsync<TTarget>()`
  - **Automatic Navigation Property Loading**: No `.Include()` required for nested facets!

- **Reverse Mapping**: Facet DTO -> Entity
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

### 4. Automatic Navigation Property Loading (No `.Include()` Required!)

```csharp
// Define nested facets
[Facet(typeof(Address))]
public partial record AddressDto;

[Facet(typeof(Company), NestedFacets = [typeof(AddressDto)])]
public partial record CompanyDto;

// Navigation properties are automatically loaded - no .Include() needed!
var companies = await dbContext.Companies
    .Where(c => c.IsActive)
    .ToFacetsAsync<CompanyDto>();

// The HeadquartersAddress navigation property is automatically included!
// EF Core analyzes the projection expression and generates the necessary JOINs

// This also works with collections:
[Facet(typeof(OrderItem))]
public partial record OrderItemDto;

[Facet(typeof(Order), NestedFacets = [typeof(OrderItemDto), typeof(AddressDto)])]
public partial record OrderDto;

var orders = await dbContext.Orders
    .ToFacetsAsync<OrderDto>();  // Automatically includes Items collection and ShippingAddress!

// All these methods support auto-include:
await dbContext.Companies.ToFacetsAsync<CompanyDto>();
await dbContext.Companies.FirstFacetAsync<CompanyDto>();
await dbContext.Companies.SingleFacetAsync<CompanyDto>();
await dbContext.Companies.SelectFacet<CompanyDto>().ToListAsync();
```

## Streaming with AsAsyncEnumerable

**Facet fully supports EF Core's streaming patterns using `AsAsyncEnumerable()`** for memory-efficient processing of large result sets:

```csharp
// Stream results one at a time instead of loading all into memory
await foreach (var userDto in dbContext.Users
    .Where(u => u.IsActive)
    .SelectFacet<UserDto>()        // Apply facet projection
    .AsAsyncEnumerable())           // Stream results
{
    // Process each item as it's retrieved from the database
    await ProcessUserAsync(userDto);
}

// Works with complex queries
await foreach (var companyDto in dbContext.Companies
    .Where(c => c.Revenue > 1000000)
    .OrderBy(c => c.Name)
    .SelectFacet<CompanyDto>()      // Nested facets are automatically loaded
    .AsAsyncEnumerable())
{
    Console.WriteLine($"{companyDto.Name}: {companyDto.HeadquartersAddress?.City}");
}

// Memory-efficient pagination
await foreach (var productDto in dbContext.Products
    .OrderBy(p => p.Id)
    .Skip(page * pageSize)
    .Take(pageSize)
    .SelectFacet<ProductDto>()
    .AsAsyncEnumerable())
{
    yield return productDto;
}
```

**Important:** Always call `SelectFacet()` **before** `AsAsyncEnumerable()`:
- **Correct:** `.SelectFacet<Dto>().AsAsyncEnumerable()` - Projection happens in SQL
- **Incorrect:** `.AsAsyncEnumerable().Select(x => x.ToFacet<Dto>())` - Loads full entities into memory first

The correct order ensures that:
1. The projection is translated to SQL (efficient database query)
2. Only the projected columns are retrieved from the database
3. Results are streamed without loading everything into memory

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

## Design-Time Services: the EF Model Manifest

`[GenerateDtos(ExcludeNavigationProperties = true)]` normally decides what a "navigation" is
with a type-shape heuristic. This package can replace the guess with the EF model's own
designation: its design-time services write a **model manifest** (`{ContextName}.facetmodel.json`)
beside the migrations model snapshot every time you run
`dotnet ef migrations add`/`remove`, recording per entity exactly which properties EF maps as
data (scalar columns, complex properties, primitive collections) and which are navigations,
owned references, or skip navigations.

### Three projects, three roles

In a layered solution these are usually **three different projects**, and each piece lives in
a specific one. The manifest is written next to the snapshot (the migrations project), but the
generator that reads it runs in the project that declares your `[GenerateDtos]` attributes —
so the `<AdditionalFiles>` glob usually reaches *across* projects.

| Role | `dotnet ef` flag | What goes here | Example (a typical layered app) |
|------|------------------|----------------|--------------------------------|
| **Startup project** | `--startup-project` (or current) | the `DesignTimeServicesReference` attribute | `MyApp.Web` |
| **Migrations project** | `--project` (or current) | the snapshot **and the generated `{Context}.facetmodel.json`** | `MyApp.Persistence` |
| **DTO project** | *(not an ef flag)* | the `[GenerateDtos]` attributes and the `<AdditionalFiles>` glob | `MyApp.Domain` |

If all three happen to be one project, every path below is local; the cross-project relative
path in step 2 is what changes.

### 1. Register the design-time services (startup project)

The attribute goes in the **startup project** — the one you pass to `dotnet ef` via
`--startup-project` (or the current project if you don't pass one). It's an assembly-level
attribute, so any compiled `.cs` file works; conventionally `Properties/AssemblyInfo.cs` or a
small dedicated file:

```csharp
[assembly: Microsoft.EntityFrameworkCore.Design.DesignTimeServicesReference(
    "Facet.Extensions.EFCore.Design.FacetDesignTimeServices, Facet.Extensions.EFCore")]
```

Or, with no new file, generate it from the startup project's `.csproj`:

```xml
<ItemGroup>
  <AssemblyAttribute Include="Microsoft.EntityFrameworkCore.Design.DesignTimeServicesReference">
    <_Parameter1>Facet.Extensions.EFCore.Design.FacetDesignTimeServices, Facet.Extensions.EFCore</_Parameter1>
  </AssemblyAttribute>
</ItemGroup>
```

The startup project must reference `Facet.Extensions.EFCore` (directly or transitively) so
the assembly named in the attribute is present in its output for `dotnet ef` to load.

Now run a migration once to produce the manifest — **this is the only thing that writes it**:

```bash
dotnet ef migrations add InitFacetManifest --project ../MyApp.Persistence --startup-project .
```

Nothing else regenerates it — not `build`, not `database update`, not `dbcontext info`. Until
that first migration, there is no manifest and `ExcludeNavigationProperties` silently uses the
heuristic. Each `DbContext` writes its own `{Context}.facetmodel.json`; if you have several,
the generator merges them (a property mapped as data in any context is kept).

### 2. Expose the manifest to the generator (DTO project — the one with `[GenerateDtos]`)

Point `<AdditionalFiles>` at wherever the migrations project keeps the manifest. That is
almost always a **relative path into the migrations project**, not a local folder:

```xml
<ItemGroup>
  <!-- from MyApp.Domain, reaching into MyApp.Persistence -->
  <AdditionalFiles Include="..\MyApp.Persistence\Migrations\*.facetmodel.json" />
</ItemGroup>
```

> A glob that matches nothing is silently empty — the compiler simply receives no file, and
> the generator cannot tell an empty glob from a missing one. If the path is wrong you get the
> heuristic with no error. Turn on `Facet_RequireEfModelManifest` (below) to make that a build
> failure, or sanity-check that the generated DTOs shrank as expected after wiring it up.

Commit the manifest like you commit the snapshot. For every entity listed in it,
`ExcludeNavigationProperties` keeps exactly the mapped data properties — value-converted
columns survive, EF-ignored (`[NotMapped]`) properties drop — and types not listed anywhere
keep the heuristic behavior. `IncludeProperties` still wins for aggregate children you want
in the DTO.

### Make drift a build failure (recommended)

Because the manifest is regenerated only when migrations are, committing it turns your build
into a schema-drift guard: add a mapped property without a migration and the generator emits
**FAC106** (the property is unknown to the model). Two knobs make that guarantee strict:

```xml
<PropertyGroup>
  <!-- In the DTO project. Turns off the heuristic: an ExcludeNavigationProperties type with
       no manifest coverage — including a mis-wired glob that found nothing — is FAC107, an
       error, instead of a silent fallback. -->
  <Facet_RequireEfModelManifest>true</Facet_RequireEfModelManifest>
  <!-- Escalate the drift warnings to errors so CI rejects a stale manifest. -->
  <WarningsAsErrors>$(WarningsAsErrors);FAC105;FAC106</WarningsAsErrors>
</PropertyGroup>
```

With both set, a PR that changes the model without regenerating the manifest cannot merge
green — your DTO contracts can't silently drift from your schema.

### Programmatic generation

The manifest can also be produced from a custom tool via
`FacetEfModelManifest.Build(model)` / `FacetEfModelManifest.Write(model, directory, contextName)`
instead of the migrations workflow. **Pass the design-time model**, not `DbContext.Model`:

```csharp
var model = context.GetService<IDesignTimeModel>().Model;   // NOT context.Model
FacetEfModelManifest.Write(model, migrationsDir, nameof(MyDbContext));
```

The runtime model has no convention metadata, so `DbContext.Model` reports zero explicitly
ignored (`[NotMapped]`/`Ignore(...)`) members — which would turn every one of them into a
spurious FAC106. The migrations scaffolder hook already uses the design-time model, so the
`dotnet ef` path in step 1 gets this right for free.

## Advanced: Custom Mapping Support

For complex mappings that cannot be expressed as SQL projections (e.g., calling external services, complex type conversions like Vector2, or async operations), see the **[Facet.Extensions.EFCore.Mapping](https://www.nuget.org/packages/Facet.Extensions.EFCore.Mapping)** package.

```bash
dotnet add package Facet.Extensions.EFCore.Mapping
```

This optional package provides custom async mapper support with dependency injection for advanced scenarios. See the [Facet.Extensions.EFCore.Mapping README](../Facet.Extensions.EFCore.Mapping/README.md) for details.

## API Reference

| Method | Description | Use Case |
|--------|-------------|----------|
| `ToFacetsAsync<TTarget>()` | Project query to DTO list (source inferred) | GET endpoints |
| `ToFacetsAsync<TSource, TTarget>()` | Project query to DTO list (explicit types) | Legacy/explicit typing |
| `FirstFacetAsync<TTarget>()` | Get first DTO or null (source inferred) | GET single item |
| `FirstFacetAsync<TSource, TTarget>()` | Get first DTO or null (explicit types) | Legacy/explicit typing |
| `SingleFacetAsync<TTarget>()` | Get single DTO (source inferred) | GET unique item |
| `SingleFacetAsync<TSource, TTarget>()` | Get single DTO (explicit types) | Legacy/explicit typing |
| `SelectFacet<TTarget>().AsAsyncEnumerable()` | Stream projected results | Memory-efficient large result sets |
| `SelectFacet<TSource, TTarget>().AsAsyncEnumerable()` | Stream projected results (explicit) | Memory-efficient large result sets |
| `UpdateFromFacet<TEntity, TFacet>()` | Selective entity update | PUT/PATCH endpoints |
| `UpdateFromFacetWithChanges<TEntity, TFacet>()` | Update with change tracking | Auditing scenarios |
| `UpdateFromFacetAsync<TEntity, TFacet>()` | Async selective update | Future extensibility |

For custom async mapper overloads, see [Facet.Extensions.EFCore.Mapping](https://www.nuget.org/packages/Facet.Extensions.EFCore.Mapping).

## Requirements

- Facet v1.6.0+
- Entity Framework Core 6+
- .NET 6+
