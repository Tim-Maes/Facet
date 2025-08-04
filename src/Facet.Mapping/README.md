# Facet.Mapping

**Facet.Mapping** enables advanced static mapping logic for the [Facet](https://www.nuget.org/packages/Facet) source generator.

This package defines strongly-typed interfaces that allow you to plug in custom mapping logic between source and generated Facet types — with support for both synchronous and asynchronous operations, all at compile time with zero runtime reflection.

---

## What is this for?

`Facet` lets you define slim, redacted, or projected versions of classes using just attributes.  
With **Facet.Mapping**, you can go further — and define custom logic like combining properties, renaming, transforming types, applying conditions, or performing async operations like database lookups and API calls.

---

## How it works

### Synchronous Mapping
1. Implement the `IFacetMapConfiguration<TSource, TTarget>` interface.
2. Define a static `Map` method.
3. Point the `[Facet(...)]` attribute to the config class using `Configuration = typeof(...)`.

### Asynchronous Mapping
1. Implement the `IFacetMapConfigurationAsync<TSource, TTarget>` interface.
2. Define a static `MapAsync` method.
3. Use the async extension methods to perform mapping operations.

### Hybrid Mapping
1. Implement the `IFacetMapConfigurationHybrid<TSource, TTarget>` interface.
2. Define both `Map` and `MapAsync` methods for optimal performance.

---

## Install

```bash
dotnet add package Facet.Mapping
```

## Examples

### Basic Synchronous Mapping

```csharp
public class User
{
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public int Id { get; set; }
}

[Facet(typeof(User), GenerateConstructor = true, Configuration = typeof(UserMapper))]
public partial class UserDto
{
    public string FullName { get; set; }
    public int Id { get; set; }
}

public class UserMapper : IFacetMapConfiguration<User, UserDto>
{
    public static void Map(User source, UserDto target)
    {
        target.FullName = $"{source.FirstName} {source.LastName}";
    }
}
```

### Asynchronous Mapping

```csharp
public class User
{
    public string Email { get; set; }
    public int Id { get; set; }
}

[Facet(typeof(User))]
public partial class UserDto
{
    public string Email { get; set; }
    public int Id { get; set; }
    public string ProfilePicture { get; set; }
    public decimal ReputationScore { get; set; }
}

public class UserAsyncMapper : IFacetMapConfigurationAsync<User, UserDto>
{
    public static async Task MapAsync(User source, UserDto target, CancellationToken cancellationToken = default)
    {
        // Async database lookup
        target.ProfilePicture = await GetProfilePictureAsync(source.Id, cancellationToken);
        
        // Async API call
        target.ReputationScore = await CalculateReputationAsync(source.Email, cancellationToken);
    }
    
    private static async Task<string> GetProfilePictureAsync(int userId, CancellationToken cancellationToken)
    {
        // Implementation details...
        await Task.Delay(100, cancellationToken); // Simulated async operation
        return $"https://api.example.com/users/{userId}/avatar";
    }
    
    private static async Task<decimal> CalculateReputationAsync(string email, CancellationToken cancellationToken)
    {
        // Implementation details...
        await Task.Delay(50, cancellationToken); // Simulated async operation
        return 4.5m;
    }
}

// Usage
var user = new User { Id = 1, Email = "john@example.com" };
var userDto = await user.ToFacetAsync<User, UserDto, UserAsyncMapper>();
```

### Hybrid Mapping (Sync + Async)

```csharp
public class ProductHybridMapper : IFacetMapConfigurationHybrid<Product, ProductDto>
{
    // Fast synchronous operations
    public static void Map(Product source, ProductDto target)
    {
        target.DisplayName = $"{source.Name} - {source.Category}";
        target.FormattedPrice = $"${source.Price:F2}";
    }

    // Expensive asynchronous operations
    public static async Task MapAsync(Product source, ProductDto target, CancellationToken cancellationToken = default)
    {
        target.AverageRating = await GetAverageRatingAsync(source.Id, cancellationToken);
        target.StockLevel = await CheckStockLevelAsync(source.Id, cancellationToken);
    }
    
    private static async Task<decimal> GetAverageRatingAsync(int productId, CancellationToken cancellationToken)
    {
        // Simulated database query
        await Task.Delay(100, cancellationToken);
        return 4.2m;
    }
    
    private static async Task<int> CheckStockLevelAsync(int productId, CancellationToken cancellationToken)
    {
        // Simulated inventory service call
        await Task.Delay(75, cancellationToken);
        return 25;
    }
}

// Usage - applies both sync and async mapping
var product = new Product { Id = 1, Name = "Laptop", Category = "Electronics", Price = 999.99m };
var productDto = await product.ToFacetHybridAsync<Product, ProductDto, ProductHybridMapper>();
```

### Collection Mapping

```csharp
// Map collections asynchronously
var users = new List<User> { /* ... */ };

// Sequential async mapping
var userDtos = await users.ToFacetsAsync<User, UserDto, UserAsyncMapper>();

// Parallel async mapping for better performance
var userDtosParallel = await users.ToFacetsParallelAsync<User, UserDto, UserAsyncMapper>(
    maxDegreeOfParallelism: 4);
```

## API Reference

### Interfaces

| Interface | Description |
|-----------|-------------|
| `IFacetMapConfiguration<TSource, TTarget>` | Synchronous mapping configuration |
| `IFacetMapConfigurationAsync<TSource, TTarget>` | Asynchronous mapping configuration |
| `IFacetMapConfigurationHybrid<TSource, TTarget>` | Combined sync/async mapping configuration |

### Extension Methods

| Method | Description |
|--------|-------------|
| `ToFacetAsync<TSource, TTarget, TAsyncMapper>()` | Maps single instance with async configuration |
| `ToFacetWithConstructorAsync<TSource, TTarget, TAsyncMapper>()` | Uses generated constructor + async mapping |
| `ToFacetsAsync<TSource, TTarget, TAsyncMapper>()` | Maps collection sequentially with async configuration |
| `ToFacetsParallelAsync<TSource, TTarget, TAsyncMapper>()` | Maps collection in parallel with async configuration |
| `ToFacetHybridAsync<TSource, TTarget, THybridMapper>()` | Maps with hybrid sync/async configuration |

## Requirements

- .NET 8.0+
- Facet v1.9.0+

## Performance Considerations

- **Sync mapping**: Zero overhead, compile-time optimized
- **Async mapping**: Use for I/O-bound operations (database, API calls)
- **Hybrid mapping**: Combine both for optimal performance
- **Parallel mapping**: Use for independent operations that can be parallelized

---

**Facet.Mapping** — Define less, map more efficiently.