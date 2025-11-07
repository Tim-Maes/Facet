# Facet.Extensions.EFCore.Mapping

Advanced custom async mapper support for Facet with EF Core queries. This package enables complex mappings that cannot be expressed as SQL projections, such as calling external services, complex type conversions (like Vector2), or async operations.

## When to Use This Package

Use this package when you need to:
- Call external services during mapping (e.g., geocoding, user lookup)
- Perform complex type conversions that can't be translated to SQL
- Execute async operations as part of the mapping process
- Apply custom business logic that requires dependency injection

For simple projection-based mappings, use [Facet.Extensions.EFCore](https://www.nuget.org/packages/Facet.Extensions.EFCore) instead.

## Getting Started

### 1. Install packages

```bash
dotnet add package Facet.Extensions.EFCore.Mapping
```

This package automatically includes:
- `Facet.Extensions.EFCore` - Core EF Core extensions
- `Facet.Mapping` - Custom mapper interfaces

### 2. Import namespaces

```csharp
using Facet.Extensions.EFCore.Mapping;  // For custom mapper methods
using Facet.Mapping;                     // For mapper interfaces
```

## Usage Examples

### Example 1: Simple Type Conversion (Vector2)

This addresses the GitHub issue #134 - converting separate X, Y properties into a Vector2 type.

```csharp
// Domain entity
public class User
{
    public int Id { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public decimal X { get; set; }
    public decimal Y { get; set; }
}

// DTO with custom mapping
[Facet(typeof(User), exclude: ["X", "Y"])]
public partial class UserDto
{
    public Vector2 Position { get; set; }
}

// Static mapper (no DI needed)
public class UserMapper : IFacetMapConfigurationAsync<User, UserDto>
{
    public static async Task MapAsync(User source, UserDto target, CancellationToken cancellationToken = default)
    {
        target.Position = new Vector2(source.X, source.Y);
        await Task.CompletedTask; // Or actual async work
    }
}

// Usage
var users = await dbContext.Users
    .Where(u => u.IsActive)
    .ToFacetsAsync<User, UserDto, UserMapper>();

var user = await dbContext.Users
    .Where(u => u.Id == userId)
    .FirstFacetAsync<User, UserDto, UserMapper>();
```

### Example 2: Dependency Injection with External Services

```csharp
// DTO
[Facet(typeof(User), exclude: ["LocationId"])]
public partial class UserDto
{
    public string Location { get; set; }
}

// Instance mapper with DI
public class UserMapper : IFacetMapConfigurationAsyncInstance<User, UserDto>
{
    private readonly ILocationService _locationService;

    public UserMapper(ILocationService locationService)
    {
        _locationService = locationService;
    }

    public async Task MapAsync(User source, UserDto target, CancellationToken cancellationToken = default)
    {
        // Call external service to resolve location name
        target.Location = await _locationService.GetLocationAsync(source.LocationId, cancellationToken);
    }
}

// Register in DI container
services.AddScoped<UserMapper>();

// Usage in controller
public class UsersController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly UserMapper _userMapper;

    public UsersController(ApplicationDbContext context, UserMapper userMapper)
    {
        _context = context;
        _userMapper = userMapper;
    }

    [HttpGet]
    public async Task<ActionResult<List<UserDto>>> GetUsers()
    {
        return await _context.Users
            .Where(u => u.IsActive)
            .ToFacetsAsync<User, UserDto>(_userMapper);
    }
}
```

### Example 3: Complex Business Logic

```csharp
// DTO
[Facet(typeof(Order))]
public partial class OrderDto
{
    public string ShippingEstimate { get; set; }
    public decimal TotalWithTax { get; set; }
}

// Mapper with multiple services
public class OrderMapper : IFacetMapConfigurationAsyncInstance<Order, OrderDto>
{
    private readonly IShippingService _shippingService;
    private readonly ITaxCalculator _taxCalculator;

    public OrderMapper(IShippingService shippingService, ITaxCalculator taxCalculator)
    {
        _shippingService = shippingService;
        _taxCalculator = taxCalculator;
    }

    public async Task MapAsync(Order source, OrderDto target, CancellationToken cancellationToken = default)
    {
        // Calculate shipping estimate
        var shippingDays = await _shippingService.EstimateDeliveryAsync(
            source.ShippingAddress,
            cancellationToken);
        target.ShippingEstimate = $"{shippingDays} business days";

        // Calculate tax
        var tax = await _taxCalculator.CalculateTaxAsync(
            source.Total,
            source.ShippingAddress.State,
            cancellationToken);
        target.TotalWithTax = source.Total + tax;
    }
}
```

## API Reference

| Method | Description |
|--------|-------------|
| `ToFacetsAsync<TSource, TTarget>(mapper)` | Project query to list with instance mapper (DI support) |
| `ToFacetsAsync<TSource, TTarget, TAsyncMapper>()` | Project query to list with static async mapper |
| `FirstFacetAsync<TSource, TTarget>(mapper)` | Get first with instance mapper (DI support) |
| `FirstFacetAsync<TSource, TTarget, TAsyncMapper>()` | Get first with static async mapper |
| `SingleFacetAsync<TSource, TTarget>(mapper)` | Get single with instance mapper (DI support) |
| `SingleFacetAsync<TSource, TTarget, TAsyncMapper>()` | Get single with static async mapper |

## How It Works

1. **Query Execution**: The EF Core query is executed first, materializing entities from the database
2. **Auto-Mapping**: All matching properties are automatically mapped (like the base Facet behavior)
3. **Custom Mapping**: Your custom mapper is called to handle additional logic

**Important**: These methods execute the SQL query first, then apply custom mapping. This means:
- ✅ You can use async operations, external services, and complex logic
- ⚠️ The query is materialized before custom mapping (can't be translated to SQL)
- 💡 Use standard `ToFacetsAsync` from `Facet.Extensions.EFCore` if you only need SQL projections

## Mapper Interfaces

### Static Mapper (No DI)
```csharp
public class MyMapper : IFacetMapConfigurationAsync<TSource, TTarget>
{
    public static async Task MapAsync(TSource source, TTarget target, CancellationToken cancellationToken = default)
    {
        // Custom mapping logic
    }
}
```

### Instance Mapper (With DI)
```csharp
public class MyMapper : IFacetMapConfigurationAsyncInstance<TSource, TTarget>
{
    private readonly IMyService _service;

    public MyMapper(IMyService service)
    {
        _service = service;
    }

    public async Task MapAsync(TSource source, TTarget target, CancellationToken cancellationToken = default)
    {
        // Custom mapping logic with injected services
    }
}
```

## Requirements

- Facet v1.6.0+
- Facet.Extensions.EFCore v1.0.0+
- Facet.Mapping v1.0.0+
- Entity Framework Core 6+
- .NET 6+

## Related Packages

- [Facet](https://www.nuget.org/packages/Facet) - Core source generator
- [Facet.Extensions](https://www.nuget.org/packages/Facet.Extensions) - Provider-agnostic extensions
- [Facet.Extensions.EFCore](https://www.nuget.org/packages/Facet.Extensions.EFCore) - Basic EF Core async extensions
- [Facet.Mapping](https://www.nuget.org/packages/Facet.Mapping) - Custom mapping interfaces
