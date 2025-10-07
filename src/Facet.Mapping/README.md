# Facet.Mapping

**Facet.Mapping** enables advanced static mapping logic for the [Facet](https://www.nuget.org/packages/Facet) source generator.

This package defines strongly-typed interfaces that allow you to plug in custom mapping logic between source and generated Facet types with support for both synchronous and asynchronous operations, **dependency injection**, all at compile time with zero runtime reflection.

---

## What is this for?

`Facet` lets you define slim, redacted, or projected versions of classes using just attributes.  
With **Facet.Mapping**, you can go further and define custom logic like combining properties, renaming, transforming types, applying conditions, or performing async operations like database lookups and API calls.

---

## How it works

### Static Mappers (No Dependencies)
1. Implement the `IFacetMapConfiguration<TSource, TTarget>` interface.
2. Define a static `Map` method.
3. Point the `[Facet(...)]` attribute to the config class using `Configuration = typeof(...)`.

### Instance Mappers (With Dependency Injection)
1. Implement the `IFacetMapConfigurationAsyncInstance<TSource, TTarget>` interface.
2. Define an instance `MapAsync` method.
3. Use the new extension methods that accept mapper instances.

### Synchronous Mapping
1. Implement `IFacetMapConfiguration<TSource, TTarget>` (static) or `IFacetMapConfigurationInstance<TSource, TTarget>` (instance).
2. Define a `Map` method.

### Asynchronous Mapping
1. Implement `IFacetMapConfigurationAsync<TSource, TTarget>` (static) or `IFacetMapConfigurationAsyncInstance<TSource, TTarget>` (instance).
2. Define a `MapAsync` method.
3. Use the async extension methods to perform mapping operations.

### Hybrid Mapping
1. Implement `IFacetMapConfigurationHybrid<TSource, TTarget>` (static) or `IFacetMapConfigurationHybridInstance<TSource, TTarget>` (instance).
2. Define both `Map` and `MapAsync` methods for optimal performance.

---

## Install

```bash
dotnet add package Facet.Mapping
```

## Examples

### Basic Synchronous Mapping (Static)

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
        // Async database lookup (limited - no DI)
        target.ProfilePicture = await GetProfilePictureAsync(source.Id, cancellationToken);
        
        // Async API call (limited - no DI)
        target.ReputationScore = await CalculateReputationAsync(source.Email, cancellationToken);
    }
    
    private static async Task<string> GetProfilePictureAsync(int userId, CancellationToken cancellationToken)
    {
        // Simple implementation without DI
        await Task.Delay(100, cancellationToken);
        return $"https://api.example.com/users/{userId}/avatar.jpg";
    }
    
    private static async Task<decimal> CalculateReputationAsync(string email, CancellationToken cancellationToken)
    {
        await Task.Delay(50, cancellationToken);
        return Random.Shared.Next(1, 6) + (decimal)Random.Shared.NextDouble();
    }
}

// Usage
var userDto = await user.ToFacetAsync<User, UserDto, UserAsyncMapper>();
```

### Asynchronous Mapping with Dependency Injection

```csharp
// Define your services
public interface IProfilePictureService
{
    Task<string> GetProfilePictureAsync(int userId, CancellationToken cancellationToken = default);
}

public interface IReputationService
{
    Task<decimal> CalculateReputationAsync(string email, CancellationToken cancellationToken = default);
}

// Implement services with real dependencies (DbContext, HttpClient, etc.)
public class ProfilePictureService : IProfilePictureService
{
    private readonly IDbContext _dbContext;
    private readonly ILogger<ProfilePictureService> _logger;

    public ProfilePictureService(IDbContext dbContext, ILogger<ProfilePictureService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<string> GetProfilePictureAsync(int userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var user = await _dbContext.Users.FindAsync(userId, cancellationToken);
            return user?.ProfilePictureUrl ?? "/images/default-avatar.png";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load profile picture for user {UserId}", userId);
            return "/images/default-avatar.png";
        }
    }
}

// Instance mapper with dependency injection
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
        // Use injected services for real implementations
        target.ProfilePicture = await _profilePictureService.GetProfilePictureAsync(source.Id, cancellationToken);
        target.ReputationScore = await _reputationService.CalculateReputationAsync(source.Email, cancellationToken);
    }
}

// Register in DI container
services.AddScoped<IProfilePictureService, ProfilePictureService>();
services.AddScoped<IReputationService, ReputationService>();
services.AddScoped<UserAsyncMapperWithDI>();

// Usage with DI
public class UserController : ControllerBase
{
    private readonly UserAsyncMapperWithDI _userMapper;
    
    public UserController(UserAsyncMapperWithDI userMapper)
    {
        _userMapper = userMapper;
    }
    
    [HttpGet("{id}")]
    public async Task<UserDto> GetUser(int id)
    {
        var user = await GetUserFromDatabase(id);
        
        // NEW: Use instance mapper with injected dependencies
        return await user.ToFacetAsync(_userMapper);
    }
    
    [HttpGet]
    public async Task<List<UserDto>> GetUsers()
    {
        var users = await GetUsersFromDatabase();
        
        // NEW: Collection mapping with DI support
        return await users.ToFacetsParallelAsync(_userMapper, maxDegreeOfParallelism: 4);
    }
}
```

### Hybrid Mapping with Dependency Injection

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
        target.Email = source.Email.ToLower();
    }

    // Expensive asynchronous operations with injected services
    public async Task MapAsync(User source, UserDto target, CancellationToken cancellationToken = default)
    {
        target.ProfilePicture = await _profilePictureService.GetProfilePictureAsync(source.Id, cancellationToken);
        target.ReputationScore = await _reputationService.CalculateReputationAsync(source.Email, cancellationToken);
    }
}

// Usage
var userDto = await user.ToFacetHybridAsync(hybridMapperWithDI);
```
