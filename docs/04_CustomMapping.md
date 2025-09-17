# Custom Mapping with IFacetMapConfiguration

Facet supports custom mapping logic for advanced scenarios via multiple interfaces that handle different mapping requirements. Starting with the latest version, Facet now supports **both static mappers and instance-based mappers with dependency injection**.

## Available Mapping Interfaces

### Static Mappers (No Dependency Injection)
| Interface | Purpose | Use Case |
|-----------|---------|----------|
| `IFacetMapConfiguration<TSource, TTarget>` | Synchronous mapping | Fast, in-memory operations |
| `IFacetMapConfigurationAsync<TSource, TTarget>` | Asynchronous mapping | I/O operations, database calls, API calls |
| `IFacetMapConfigurationHybrid<TSource, TTarget>` | Combined sync/async | Optimal performance with mixed operations |

### Instance Mappers (With Dependency Injection Support)
| Interface | Purpose | Use Case |
|-----------|---------|----------|
| `IFacetMapConfigurationInstance<TSource, TTarget>` | Synchronous mapping | Fast operations with injected services |
| `IFacetMapConfigurationAsyncInstance<TSource, TTarget>` | Asynchronous mapping | I/O operations with injected services |
| `IFacetMapConfigurationHybridInstance<TSource, TTarget>` | Combined sync/async | Mixed operations with injected services |

## When to Use Each Approach

### Static Mappers
- **Best for**: Simple transformations, computed properties, formatting
- **Benefits**: Zero overhead, compile-time optimization, no DI container required
- **Limitations**: Cannot inject services, no access to external dependencies

### Instance Mappers  
- **Best for**: Complex scenarios requiring external services (databases, APIs, file systems)
- **Benefits**: Full dependency injection support, easier testing, better separation of concerns
- **Usage**: Pass mapper instances with injected dependencies

## Facet.Mapping Extension Methods

The following extension methods are available for async mapping scenarios:

| Method | Description |
|--------|-------------|
| `ToFacetAsync<TSource, TTarget, TMapper>()` | Map single instance with explicit type parameters (compile-time) |
| `ToFacetAsync<TTarget, TMapper>()` | Map single instance with inferred source type (runtime reflection) |
| `ToFacetsAsync<TSource, TTarget, TMapper>()` | Map collection sequentially with explicit types |
| `ToFacetsAsync<TTarget, TMapper>()` | Map collection sequentially with inferred source type |
| `ToFacetsParallelAsync<TSource, TTarget, TMapper>()` | Map collection in parallel with explicit types |
| `ToFacetsParallelAsync<TTarget, TMapper>()` | Map collection in parallel with inferred source type |
| `ToFacetHybridAsync<TSource, TTarget, TMapper>()` | Hybrid sync/async mapping with explicit types |
| `ToFacetHybridAsync<TTarget, TMapper>()` | Hybrid sync/async mapping with inferred source type |

> **Performance Note**: Methods with explicit type parameters (`<TSource, TTarget, TMapper>`) offer better compile-time performance, while simplified methods (`<TTarget, TMapper>`) use runtime reflection for improved developer experience.

## Static Mapping (No DI)

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

## Instance Mapping (With Dependency Injection)

### 1. Define Your Services

```csharp
public interface IProfilePictureService
{
    Task<string> GetProfilePictureAsync(int userId, CancellationToken cancellationToken = default);
}

public interface IReputationService
{
    Task<decimal> CalculateReputationAsync(string email, CancellationToken cancellationToken = default);
}

public class ProfilePictureService : IProfilePictureService
{
    private readonly IDbContext _dbContext;
    
    public ProfilePictureService(IDbContext dbContext)
    {
        _dbContext = dbContext;
    }
    
    public async Task<string> GetProfilePictureAsync(int userId, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users.FindAsync(userId, cancellationToken);
        return user?.ProfilePictureUrl ?? "/images/default-avatar.png";
    }
}
```

### 2. Implement the Instance Interface

```csharp
using Facet.Mapping;

public class UserAsyncMapperWithDI : IFacetMapConfigurationAsyncInstance<User, UserDto>
{
    private readonly IProfilePictureService _profilePictureService;
    private readonly IReputationService _reputationService;

    public UserAsyncMapperWithDI(IProfilePictureService profilePictureService, IReputationService reputationService)
    {
        _profilePictureService = profilePictureService;
        _reputationService = reputationService;
    }

    public async Task MapAsync(User source, UserDto target, CancellationToken cancellationToken = default)
    {
        // Use injected services for async operations
        target.ProfilePictureUrl = await _profilePictureService.GetProfilePictureAsync(source.Id, cancellationToken);
        target.ReputationScore = await _reputationService.CalculateReputationAsync(source.Email, cancellationToken);
        
        // Set computed property
        target.FullName = $"{source.FirstName} {source.LastName}";
    }
}
```

### 3. Use with Dependency Injection

```csharp
// Register services in your DI container
services.AddScoped<IProfilePictureService, ProfilePictureService>();
services.AddScoped<IReputationService, ReputationService>();
services.AddScoped<UserAsyncMapperWithDI>();

// Usage in your application
public class UserController : ControllerBase
{
    private readonly UserAsyncMapperWithDI _userMapper;
    
    public UserController(UserAsyncMapperWithDI userMapper)
    {
        _userMapper = userMapper;
    }
    
    public async Task<UserDto> GetUser(int id)
    {
        var user = await GetUserFromDatabase(id);
        
        // NEW: Pass mapper instance with injected dependencies
        return await user.ToFacetAsync(_userMapper);
    }
    
    public async Task<List<UserDto>> GetUsers()
    {
        var users = await GetUsersFromDatabase();
        
        // NEW: Collection mapping with DI
        return await users.ToFacetsAsync(_userMapper);
    }
}
```

## Asynchronous Mapping (Static - Original Approach)

### 1. Implement the Async Interface

```csharp
using Facet.Mapping;

public class UserAsyncMapper : IFacetMapConfigurationAsync<User, UserDto>
{
    public static async Task MapAsync(User source, UserDto target, CancellationToken cancellationToken = default)
    {
        // Async database lookup (without DI)
        target.ProfilePictureUrl = await GetProfilePictureAsync(source.Id, cancellationToken);
        
        // Async API call (without DI)
        target.ReputationScore = await CalculateReputationAsync(source.Email, cancellationToken);
        
        // Async file operation
        target.Preferences = await LoadUserPreferencesAsync(source.Id, cancellationToken);
    }
    
    private static async Task<string> GetProfilePictureAsync(int userId, CancellationToken cancellationToken)
    {
        // Database query example (without DI - not recommended for complex scenarios)
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

### 3. Use Static Async Extension Methods

```csharp
// Single instance async mapping (static)
var userDto = await user.ToFacetAsync<UserDto, UserAsyncMapper>();

// Collection async mapping (sequential, static)
var userDtos = await users.ToFacetsAsync<UserDto, UserAsyncMapper>();

// Collection async mapping (parallel for better performance, static)
var userDtosParallel = await users.ToFacetsParallelAsync<UserDto, UserAsyncMapper>(
    maxDegreeOfParallelism: 4);
```

## Hybrid Mapping (Best Performance)

### Static Hybrid Mapper

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

// Usage
var userDto = await user.ToFacetHybridAsync<UserDto, UserHybridMapper>();
```

### Instance Hybrid Mapper (With DI)

```csharp
public class UserHybridMapperWithDI : IFacetMapConfigurationHybridInstance<User, UserDto>
{
    private readonly IProfilePictureService _profilePictureService;
    private readonly IReputationService _reputationService;

    public UserHybridMapperWithDI(IProfilePictureService profilePictureService, IReputationService reputationService)
    {
        _profilePictureService = profilePictureService;
        _reputationService = reputationService;
    }

    // Fast synchronous operations
    public void Map(User source, UserDto target)
    {
        target.FullName = $"{source.FirstName} {source.LastName}";
        target.DisplayEmail = source.Email.ToLower();
        target.AgeCategory = CalculateAgeCategory(source.BirthDate);
    }

    // Expensive asynchronous operations with injected services
    public async Task MapAsync(User source, UserDto target, CancellationToken cancellationToken = default)
    {
        target.ProfilePictureUrl = await _profilePictureService.GetProfilePictureAsync(source.Id, cancellationToken);
        target.ReputationScore = await _reputationService.CalculateReputationAsync(source.Email, cancellationToken);
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
}

// Usage with DI
var userDto = await user.ToFacetHybridAsync(hybridMapperWithDI);
```

## API Comparison

### Before (Static Only)
```csharp
// Limited to static methods, no DI support
var userDto = await user.ToFacetAsync<UserDto, UserAsyncMapper>();
```

### After (Both Static and Instance Support)
```csharp
// Option 1: Static approach (existing, unchanged)
var userDto1 = await user.ToFacetAsync<UserDto, UserAsyncMapper>();

// Option 2: Instance approach (NEW, with DI support)
var mapper = new UserAsyncMapperWithDI(profilePictureService, reputationService);
var userDto2 = await user.ToFacetAsync(mapper);
```

## Error Handling

### Instance Mapper with Error Handling

```csharp
public class SafeUserAsyncMapperWithDI : IFacetMapConfigurationAsyncInstance<User, UserDto>
{
    private readonly IProfilePictureService _profilePictureService;
    private readonly IReputationService _reputationService;
    private readonly ILogger<SafeUserAsyncMapperWithDI> _logger;

    public SafeUserAsyncMapperWithDI(
        IProfilePictureService profilePictureService, 
        IReputationService reputationService,
        ILogger<SafeUserAsyncMapperWithDI> logger)
    {
        _profilePictureService = profilePictureService;
        _reputationService = reputationService;
        _logger = logger;
    }

    public async Task MapAsync(User source, UserDto target, CancellationToken cancellationToken = default)
    {
        try
        {
            target.ProfilePictureUrl = await _profilePictureService.GetProfilePictureAsync(source.Id, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load profile picture for user {UserId}", source.Id);
            target.ProfilePictureUrl = "/images/default-avatar.png";
        }
        
        try
        {
            target.ReputationScore = await _reputationService.CalculateReputationAsync(source.Email, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to calculate reputation for user {Email}", source.Email);
            target.ReputationScore = 0m; // Default value on error
        }
    }
}
```

## Performance Considerations

### When to Use Each Approach

| Scenario | Recommended Interface | Reason |
|----------|----------------------|---------|
| Simple property transformations | `IFacetMapConfiguration` (static) | Zero overhead, compile-time optimized |
| Complex transformations needing services | `IFacetMapConfigurationInstance` | Full DI support, easier testing |
| Database lookups | `IFacetMapConfigurationAsyncInstance` | Proper async/await with injected DbContext |
| API calls | `IFacetMapConfigurationAsyncInstance` | Non-blocking I/O with injected HttpClient |
| Mixed fast/slow operations | `IFacetMapConfigurationHybridInstance` | Best of both worlds with DI |
| Large collections | Parallel async methods with instances | Improved throughput with shared services |

### Collection Processing Guidelines

```csharp
// For small collections (< 100 items) with DI
var mapper = serviceProvider.GetRequiredService<UserAsyncMapperWithDI>();
var results = await items.ToFacetsAsync(mapper);

// For large collections with I/O operations and DI
var results = await items.ToFacetsParallelAsync(mapper, maxDegreeOfParallelism: Environment.ProcessorCount);

// For database-heavy operations (avoid overwhelming the database)
var results = await items.ToFacetsParallelAsync(mapper, maxDegreeOfParallelism: 2);
```

## Migration Guide

### Existing Static Mappers
Your existing static mappers continue to work unchanged:

```csharp
// This continues to work exactly as before
var result = await user.ToFacetAsync<UserDto, ExistingAsyncMapper>();
```

### Adding DI Support
To add dependency injection support to existing scenarios:

```csharp
// 1. Create new instance-based mapper
public class ExistingAsyncMapperWithDI : IFacetMapConfigurationAsyncInstance<User, UserDto>
{
    private readonly ISomeService _service;
    
    public ExistingAsyncMapperWithDI(ISomeService service)
    {
        _service = service;
    }
    
    public async Task MapAsync(User source, UserDto target, CancellationToken cancellationToken = default)
    {
        // Use _service instead of static calls
        target.SomeProperty = await _service.GetDataAsync(source.Id, cancellationToken);
    }
}

// 2. Register in DI container
services.AddScoped<ExistingAsyncMapperWithDI>();

// 3. Use new approach
var mapper = serviceProvider.GetRequiredService<ExistingAsyncMapperWithDI>();
var result = await user.ToFacetAsync(mapper);
```

## Notes

- **Backward Compatibility**: All existing static mapper interfaces and extension methods continue to work unchanged
- **Instance mappers**: All mapping methods must be `public` (not static) and match the interface signature
- **Dependency Injection**: Instance mappers fully support constructor injection and can be registered in any DI container
- **Testing**: Instance mappers are much easier to unit test since dependencies can be mocked
- **Performance**: Instance mappers have minimal overhead compared to static mappers
- **Thread Safety**: Each instance should handle its own thread-safety requirements for injected services
- **Cancellation**: All async methods support `CancellationToken` for proper cancellation
- **Error Handling**: Instance mappers can inject loggers and other services for better error handling

---
