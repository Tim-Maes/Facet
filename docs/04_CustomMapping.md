# Custom Mapping with IFacetMapConfiguration

Facet supports custom mapping logic for advanced scenarios via multiple interfaces that handle different mapping requirements.

## Available Mapping Interfaces

| Interface | Purpose | Use Case |
|-----------|---------|----------|
| `IFacetMapConfiguration<TSource, TTarget>` | Synchronous mapping | Fast, in-memory operations |
| `IFacetMapConfigurationAsync<TSource, TTarget>` | Asynchronous mapping | I/O operations, database calls, API calls |
| `IFacetMapConfigurationHybrid<TSource, TTarget>` | Combined sync/async | Optimal performance with mixed operations |

## When to Use Custom Mapping

### Synchronous Mapping
- You need to compute derived properties
- You want to format or transform values
- You need to inject additional logic during mapping
- Operations are fast and CPU-bound

### Asynchronous Mapping
- You need to perform database lookups
- You want to call external APIs
- You need to read files or perform I/O operations
- Operations are slow and I/O-bound

## Synchronous Mapping

### 1. Implement the Interface

```csharp
using Facet.Mapping;

public class UserMapConfig : IFacetMapConfiguration<User, UserDto>
{
    public static void Map(User source, UserDto target)
    {
        target.FullName = $"{source.FirstName} {source.LastName}";
        target.DisplayEmail = source.Email.ToLower();
    }
}
```

### 2. Reference in the Facet Attribute

```csharp
[Facet(typeof(User), Configuration = typeof(UserMapConfig))]
public partial class UserDto 
{ 
    public string FullName { get; set; }
    public string DisplayEmail { get; set; }
}
```

The generated constructor will call your `Map` method after copying properties.

## Asynchronous Mapping

### 1. Implement the Async Interface

```csharp
using Facet.Mapping;

public class UserAsyncMapper : IFacetMapConfigurationAsync<User, UserDto>
{
    public static async Task MapAsync(User source, UserDto target, CancellationToken cancellationToken = default)
    {
        // Async database lookup
        target.ProfilePictureUrl = await GetProfilePictureAsync(source.Id, cancellationToken);
        
        // Async API call
        target.ReputationScore = await CalculateReputationAsync(source.Email, cancellationToken);
        
        // Async file operation
        target.Preferences = await LoadUserPreferencesAsync(source.Id, cancellationToken);
    }
    
    private static async Task<string> GetProfilePictureAsync(int userId, CancellationToken cancellationToken)
    {
        // Database query example
        using var httpClient = new HttpClient();
        var response = await httpClient.GetStringAsync($"https://api.example.com/users/{userId}/avatar", cancellationToken);
        return response;
    }
    
    private static async Task<decimal> CalculateReputationAsync(string email, CancellationToken cancellationToken)
    {
        // External API call example
        await Task.Delay(100, cancellationToken); // Simulated API delay
        return Random.Shared.Next(1, 6) + (decimal)Random.Shared.NextDouble();
    }
    
    private static async Task<UserPreferences> LoadUserPreferencesAsync(int userId, CancellationToken cancellationToken)
    {
        // File I/O example
        var filePath = $"preferences/{userId}.json";
        if (File.Exists(filePath))
        {
            var json = await File.ReadAllTextAsync(filePath, cancellationToken);
            return JsonSerializer.Deserialize<UserPreferences>(json) ?? new UserPreferences();
        }
        return new UserPreferences();
    }
}
```

### 2. Define Your DTO

```csharp
[Facet(typeof(User))]
public partial class UserDto 
{ 
    public string ProfilePictureUrl { get; set; } = "";
    public decimal ReputationScore { get; set; }
    public UserPreferences Preferences { get; set; } = new();
}
```

### 3. Use Async Extension Methods

```csharp
// Single instance async mapping
var userDto = await user.ToFacetAsync<User, UserDto, UserAsyncMapper>();

// Collection async mapping (sequential)
var userDtos = await users.ToFacetsAsync<User, UserDto, UserAsyncMapper>();

// Collection async mapping (parallel for better performance)
var userDtosParallel = await users.ToFacetsParallelAsync<User, UserDto, UserAsyncMapper>(
    maxDegreeOfParallelism: 4);
```

## Hybrid Mapping (Best Performance)

For optimal performance, combine fast synchronous operations with async I/O operations:

### 1. Implement the Hybrid Interface

```csharp
public class UserHybridMapper : IFacetMapConfigurationHybrid<User, UserDto>
{
    // Fast synchronous operations
    public static void Map(User source, UserDto target)
    {
        target.FullName = $"{source.FirstName} {source.LastName}";
        target.DisplayEmail = source.Email.ToLower();
        target.AgeCategory = CalculateAgeCategory(source.BirthDate);
    }

    // Expensive asynchronous operations
    public static async Task MapAsync(User source, UserDto target, CancellationToken cancellationToken = default)
    {
        target.ProfilePictureUrl = await GetProfilePictureAsync(source.Id, cancellationToken);
        target.ReputationScore = await CalculateReputationAsync(source.Email, cancellationToken);
    }
    
    private static string CalculateAgeCategory(DateTime birthDate)
    {
        var age = DateTime.Now.Year - birthDate.Year;
        return age switch
        {
            < 18 => "Minor",
            < 65 => "Adult",
            _ => "Senior"
        };
    }
    
    // ... async method implementations
}
```

### 2. Use Hybrid Mapping

```csharp
// Applies both sync and async mapping
var userDto = await user.ToFacetHybridAsync<User, UserDto, UserHybridMapper>();
```

## Error Handling

### Async Mapping with Error Handling

```csharp
public class SafeUserAsyncMapper : IFacetMapConfigurationAsync<User, UserDto>
{
    public static async Task MapAsync(User source, UserDto target, CancellationToken cancellationToken = default)
    {
        try
        {
            target.ProfilePictureUrl = await GetProfilePictureAsync(source.Id, cancellationToken);
        }
        catch (HttpRequestException)
        {
            target.ProfilePictureUrl = "/images/default-avatar.png";
        }
        
        try
        {
            target.ReputationScore = await CalculateReputationAsync(source.Email, cancellationToken);
        }
        catch (Exception)
        {
            target.ReputationScore = 0m; // Default value on error
        }
    }
}
```

## Performance Considerations

### When to Use Each Approach

| Scenario | Recommended Interface | Reason |
|----------|----------------------|---------|
| Simple property transformations | `IFacetMapConfiguration` | Zero overhead, compile-time optimized |
| Database lookups | `IFacetMapConfigurationAsync` | Proper async/await handling |
| API calls | `IFacetMapConfigurationAsync` | Non-blocking I/O operations |
| Mixed fast/slow operations | `IFacetMapConfigurationHybrid` | Best of both worlds |
| Large collections | Parallel async methods | Improved throughput |

### Parallel Processing Guidelines

```csharp
// For small collections (< 100 items)
var results = await items.ToFacetsAsync<Source, Target, AsyncMapper>();

// For large collections with I/O operations
var results = await items.ToFacetsParallelAsync<Source, Target, AsyncMapper>(
    maxDegreeOfParallelism: Environment.ProcessorCount);

// For database-heavy operations (avoid overwhelming the database)
var results = await items.ToFacetsParallelAsync<Source, Target, AsyncMapper>(
    maxDegreeOfParallelism: 2);
```

## Notes

- All mapping methods must be `public static` and match the exact signature
- Async mapping methods support `CancellationToken` for proper cancellation
- The source generator will call sync mapping first, then async mapping for hybrid scenarios
- Use parallel processing judiciously to avoid overwhelming external services
- Always handle exceptions in async mappers to provide fallback values

---
