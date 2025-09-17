# Asynchronous Mapping with Facet.Mapping

This guide covers advanced asynchronous mapping scenarios with Facet.Mapping, including real-world examples and best practices.

## Overview

Facet.Mapping v2.0+ provides comprehensive async mapping support for scenarios where your mapping logic needs to perform I/O operations like:

- Database queries
- External API calls  
- File system operations
- Network requests
- Any other async operations

## Core Async Interfaces

### IFacetMapConfigurationAsync<TSource, TTarget>

```csharp
public interface IFacetMapConfigurationAsync<TSource, TTarget>
{
    static abstract Task MapAsync(TSource source, TTarget target, CancellationToken cancellationToken = default);
}
```

### IFacetMapConfigurationHybrid<TSource, TTarget>

```csharp
public interface IFacetMapConfigurationHybrid<TSource, TTarget> : 
    IFacetMapConfiguration<TSource, TTarget>,
    IFacetMapConfigurationAsync<TSource, TTarget>
{
    // Inherits both Map() and MapAsync() methods
}
```

## Real-World Examples

### Example 1: User Profile with External Data

```csharp
public class User
{
    public int Id { get; set; }
    public string Email { get; set; } = "";
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
}

[Facet(typeof(User))]
public partial class UserProfileDto
{
    public int Id { get; set; }
    public string Email { get; set; } = "";
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    
    // Async-populated properties
    public string AvatarUrl { get; set; } = "";
    public decimal ReputationScore { get; set; }
    public List<string> BadgeNames { get; set; } = new();
    public UserStatistics Statistics { get; set; } = new();
}

public class UserProfileAsyncMapper : IFacetMapConfigurationAsync<User, UserProfileDto>
{
    private static readonly HttpClient _httpClient = new();
    private static readonly IMemoryCache _cache = new MemoryCache(new MemoryCacheOptions());

    public static async Task MapAsync(User source, UserProfileDto target, CancellationToken cancellationToken = default)
    {
        // Parallel async operations for better performance
        var tasks = new[]
        {
            LoadAvatarAsync(source.Id, target, cancellationToken),
            LoadReputationAsync(source.Email, target, cancellationToken),
            LoadBadgesAsync(source.Id, target, cancellationToken),
            LoadStatisticsAsync(source.Id, target, cancellationToken)
        };

        await Task.WhenAll(tasks);
    }

    private static async Task LoadAvatarAsync(int userId, UserProfileDto target, CancellationToken cancellationToken)
    {
        var cacheKey = $"avatar:{userId}";
        if (_cache.TryGetValue(cacheKey, out string? cachedAvatar))
        {
            target.AvatarUrl = cachedAvatar!;
            return;
        }

        try
        {
            var response = await _httpClient.GetStringAsync($"https://api.example.com/users/{userId}/avatar", cancellationToken);
            target.AvatarUrl = response;
            _cache.Set(cacheKey, response, TimeSpan.FromMinutes(30));
        }
        catch (Exception)
        {
            target.AvatarUrl = "/images/default-avatar.png";
        }
    }

    private static async Task LoadReputationAsync(string email, UserProfileDto target, CancellationToken cancellationToken)
    {
        try
        {
            using var client = new HttpClient();
            var response = await client.GetFromJsonAsync<ReputationResponse>(
                $"https://reputation-service.example.com/score?email={Uri.EscapeDataString(email)}", 
                cancellationToken);
            target.ReputationScore = response?.Score ?? 0m;
        }
        catch (Exception)
        {
            target.ReputationScore = 0m;
        }
    }

    private static async Task LoadBadgesAsync(int userId, UserProfileDto target, CancellationToken cancellationToken)
    {
        try
        {
            // Simulated database query
            await using var connection = new SqlConnection("connection_string");
            await connection.OpenAsync(cancellationToken);
            
            using var command = new SqlCommand("SELECT BadgeName FROM UserBadges WHERE UserId = @UserId", connection);
            command.Parameters.AddWithValue("@UserId", userId);
            
            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var badges = new List<string>();
            
            while (await reader.ReadAsync(cancellationToken))
            {
                badges.Add(reader.GetString("BadgeName"));
            }
            
            target.BadgeNames = badges;
        }
        catch (Exception)
        {
            target.BadgeNames = new List<string>();
        }
    }

    private static async Task LoadStatisticsAsync(int userId, UserProfileDto target, CancellationToken cancellationToken)
    {
        try
        {
            var filePath = $"statistics/{userId}.json";
            if (File.Exists(filePath))
            {
                var json = await File.ReadAllTextAsync(filePath, cancellationToken);
                target.Statistics = JsonSerializer.Deserialize<UserStatistics>(json) ?? new UserStatistics();
            }
        }
        catch (Exception)
        {
            target.Statistics = new UserStatistics();
        }
    }
}

// Usage
var user = await dbContext.Users.FindAsync(userId);
var userProfile = await user.ToFacetAsync<UserProfileDto, UserProfileAsyncMapper>();
```

### Example 2: E-commerce Product with Pricing and Inventory

```csharp
public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public decimal BasePrice { get; set; }
}

[Facet(typeof(Product))]
public partial class ProductDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public decimal BasePrice { get; set; }
    
    // Async-populated properties
    public decimal CurrentPrice { get; set; }
    public int StockLevel { get; set; }
    public decimal AverageRating { get; set; }
    public int ReviewCount { get; set; }
    public List<string> ImageUrls { get; set; } = new();
    public bool IsOnSale { get; set; }
}

public class ProductHybridMapper : IFacetMapConfigurationHybrid<Product, ProductDto>
{
    // Fast synchronous operations
    public static void Map(Product source, ProductDto target)
    {
        // These operations are fast and don't require async
        target.IsOnSale = DateTime.Now.DayOfWeek == DayOfWeek.Friday; // Example business logic
    }

    // Expensive asynchronous operations
    public static async Task MapAsync(Product source, ProductDto target, CancellationToken cancellationToken = default)
    {
        // Run independent async operations in parallel
        var pricingTask = LoadPricingDataAsync(source.Id, target, cancellationToken);
        var inventoryTask = LoadInventoryDataAsync(source.Id, target, cancellationToken);
        var reviewsTask = LoadReviewDataAsync(source.Id, target, cancellationToken);
        var imagesTask = LoadProductImagesAsync(source.Id, target, cancellationToken);

        await Task.WhenAll(pricingTask, inventoryTask, reviewsTask, imagesTask);
    }

    private static async Task LoadPricingDataAsync(int productId, ProductDto target, CancellationToken cancellationToken)
    {
        // Call pricing microservice
        using var client = new HttpClient();
        var pricing = await client.GetFromJsonAsync<PricingData>(
            $"https://pricing-service.example.com/products/{productId}", 
            cancellationToken);
        
        target.CurrentPrice = pricing?.CurrentPrice ?? target.BasePrice;
    }

    private static async Task LoadInventoryDataAsync(int productId, ProductDto target, CancellationToken cancellationToken)
    {
        // Query inventory database
        await using var connection = new SqlConnection("inventory_connection_string");
        await connection.OpenAsync(cancellationToken);
        
        using var command = new SqlCommand("SELECT StockLevel FROM Inventory WHERE ProductId = @ProductId", connection);
        command.Parameters.AddWithValue("@ProductId", productId);
        
        var result = await command.ExecuteScalarAsync(cancellationToken);
        target.StockLevel = result as int? ?? 0;
    }

    private static async Task LoadReviewDataAsync(int productId, ProductDto target, CancellationToken cancellationToken)
    {
        using var client = new HttpClient();
        var reviews = await client.GetFromJsonAsync<ReviewSummary>(
            $"https://reviews-service.example.com/products/{productId}/summary", 
            cancellationToken);
        
        target.AverageRating = reviews?.AverageRating ?? 0m;
        target.ReviewCount = reviews?.Count ?? 0;
    }

    private static async Task LoadProductImagesAsync(int productId, ProductDto target, CancellationToken cancellationToken)
    {
        // Load from file system or CDN
        var imageDirectory = $"product-images/{productId}";
        if (Directory.Exists(imageDirectory))
        {
            var imageFiles = await Task.Run(() => 
                Directory.GetFiles(imageDirectory, "*.jpg")
                         .Select(Path.GetFileName)
                         .ToList(), cancellationToken);
            
            target.ImageUrls = imageFiles.Select(f => $"https://cdn.example.com/products/{productId}/{f}").ToList();
        }
    }
}

// Usage examples
var product = await dbContext.Products.FindAsync(productId);

// Single product with full async mapping
var productDto = await product.ToFacetHybridAsync<ProductDto, ProductHybridMapper>();

// Multiple products with parallel processing
var products = await dbContext.Products.Take(20).ToListAsync();
var productDtos = await products.ToFacetsParallelAsync<ProductDto, ProductHybridMapper>(
    maxDegreeOfParallelism: 4);
```

## Collection Processing Strategies

### Sequential Processing
```csharp
// Best for: Small collections, operations that might conflict if run in parallel
var results = await items.ToFacetsAsync<Target, AsyncMapper>();
```

### Parallel Processing
```csharp
// Best for: Large collections, independent operations
var results = await items.ToFacetsParallelAsync<Target, AsyncMapper>(
    maxDegreeOfParallelism: Environment.ProcessorCount);
```

### Batched Processing
```csharp
// Custom batched processing for very large collections
public static async Task<List<ProductDto>> ProcessProductsBatched(
    IEnumerable<Product> products, 
    int batchSize = 50)
{
    var results = new List<ProductDto>();
    var batches = products.Chunk(batchSize);
    
    foreach (var batch in batches)
    {
        var batchResults = await batch.ToFacetsParallelAsync<ProductDto, ProductHybridMapper>(
            maxDegreeOfParallelism: 4);
        results.AddRange(batchResults);
        
        // Optional: Add delay between batches to avoid overwhelming services
        await Task.Delay(100);
    }
    
    return results;
}
```

## Error Handling and Resilience

### Retry Logic with Polly

```csharp
public class ResilientUserMapper : IFacetMapConfigurationAsync<User, UserDto>
{
    private static readonly RetryPolicy _retryPolicy = Policy
        .Handle<HttpRequestException>()
        .Or<TaskCanceledException>()
        .WaitAndRetryAsync(3, retryAttempt => 
            TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));

    public static async Task MapAsync(User source, UserDto target, CancellationToken cancellationToken = default)
    {
        try
        {
            target.ExternalData = await _retryPolicy.ExecuteAsync(async () =>
            {
                using var client = new HttpClient();
                return await client.GetStringAsync($"https://api.example.com/users/{source.Id}", cancellationToken);
            });
        }
        catch (Exception ex)
        {
            // Log the error and provide fallback
            Console.WriteLine($"Failed to load external data for user {source.Id}: {ex.Message}");
            target.ExternalData = "Unavailable";
        }
    }
}
```

### Circuit Breaker Pattern

```csharp
public class CircuitBreakerUserMapper : IFacetMapConfigurationAsync<User, UserDto>
{
    private static readonly CircuitBreakerPolicy _circuitBreaker = Policy
        .Handle<HttpRequestException>()
        .CircuitBreakerAsync(3, TimeSpan.FromMinutes(1));

    public static async Task MapAsync(User source, UserDto target, CancellationToken cancellationToken = default)
    {
        try
        {
            target.ExternalData = await _circuitBreaker.ExecuteAsync(async () =>
            {
                using var client = new HttpClient();
                return await client.GetStringAsync($"https://api.example.com/users/{source.Id}", cancellationToken);
            });
        }
        catch (CircuitBreakerOpenException)
        {
            // Circuit breaker is open, use cached data or default
            target.ExternalData = GetCachedDataOrDefault(source.Id);
        }
    }

    private static string GetCachedDataOrDefault(int userId)
    {
        // Implementation for fallback data
        return "Service temporarily unavailable";
    }
}
```

## Performance Optimization

### Caching Strategy

```csharp
public class CachedAsyncMapper : IFacetMapConfigurationAsync<User, UserDto>
{
    private static readonly IMemoryCache _cache = new MemoryCache(new MemoryCacheOptions
    {
        SizeLimit = 1000
    });

    public static async Task MapAsync(User source, UserDto target, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"user_external_data:{source.Id}";
        
        if (_cache.TryGetValue(cacheKey, out ExternalUserData? cachedData))
        {
            target.ExternalData = cachedData!.Data;
            return;
        }

        try
        {
            var data = await FetchExternalDataAsync(source.Id, cancellationToken);
            target.ExternalData = data.Data;
            
            _cache.Set(cacheKey, data, TimeSpan.FromMinutes(15));
        }
        catch (Exception)
        {
            target.ExternalData = "Error loading data";
        }
    }

    private static async Task<ExternalUserData> FetchExternalDataAsync(int userId, CancellationToken cancellationToken)
    {
        using var client = new HttpClient();
        return await client.GetFromJsonAsync<ExternalUserData>(
            $"https://api.example.com/users/{userId}", cancellationToken) ?? new ExternalUserData();
    }
}
```

## Best Practices

### 1. Use Appropriate Concurrency Levels

```csharp
// For database operations: Low concurrency to avoid overwhelming the DB
var dbResults = await items.ToFacetsParallelAsync<Target, DbMapper>(maxDegreeOfParallelism: 2);

// For HTTP APIs: Medium concurrency
var apiResults = await items.ToFacetsParallelAsync<Target, ApiMapper>(maxDegreeOfParallelism: 4);

// For CPU-bound operations: High concurrency
var cpuResults = await items.ToFacetsParallelAsync<Target, CpuMapper>(maxDegreeOfParallelism: Environment.ProcessorCount);
```

### 2. Always Handle Cancellation

```csharp
public static async Task MapAsync(User source, UserDto target, CancellationToken cancellationToken = default)
{
    cancellationToken.ThrowIfCancellationRequested();
    
    // Your async operations here...
    await SomeAsyncOperation(cancellationToken);
    
    cancellationToken.ThrowIfCancellationRequested();
}
```

### 3. Use ConfigureAwait(false) for Library Code

```csharp
public static async Task MapAsync(User source, UserDto target, CancellationToken cancellationToken = default)
{
    var data = await FetchDataAsync(source.Id, cancellationToken).ConfigureAwait(false);
    target.ExternalData = data;
}
```

### 4. Dispose Resources Properly

```csharp
public static async Task MapAsync(User source, UserDto target, CancellationToken cancellationToken = default)
{
    using var client = new HttpClient();
    await using var connection = new SqlConnection("connection_string");
    
    // Use the resources...
} // Automatic disposal
```

---

This comprehensive async mapping guide provides the foundation for building robust, scalable mapping solutions with Facet.Mapping.