# Expression Mapping with Facet.Mapping.Expressions

Transform business logic expressions between entities and DTOs with `Facet.Mapping.Expressions`. This library enables you to define business rules, filters, and selectors once for your entities and seamlessly use them with your Facet DTOs.

## Overview

Expression mapping solves the common problem of duplicating business logic when working with both entities and their corresponding DTOs. Instead of rewriting predicates and selectors for each type, you can transform existing expressions to work with different but compatible types.

## Installation

```bash
dotnet add package Facet.Mapping.Expressions
```

## Core Concepts

### Predicate Mapping

Transform filter expressions from entity types to DTO types:

```csharp
using Facet.Mapping.Expressions;

// Business rule defined for entities
Expression<Func<User, bool>> activeUsers = u => u.IsActive && !u.IsDeleted;

// Transform to work with DTOs
Expression<Func<UserDto, bool>> activeDtoUsers = activeUsers.MapToFacet<UserDto>();

// Use with collections
var filteredDtos = dtoCollection.Where(activeDtoUsers.Compile()).ToList();
```

### Selector Mapping

Transform sorting and selection expressions:

```csharp
// Original selector for entity sorting
Expression<Func<User, string>> sortByLastName = u => u.LastName;

// Transform to work with DTO
Expression<Func<UserDto, string>> dtoSortByLastName = sortByLastName.MapToFacet<UserDto, string>();

// Use for sorting DTOs
var sortedDtos = dtoCollection.OrderBy(dtoSortByLastName.Compile()).ToList();
```

### Generic Expression Transformation

Handle complex expressions with anonymous objects and method calls:

```csharp
// Complex projection expression
Expression<Func<User, object>> complexProjection = u => new {
    FullName = u.FirstName + " " + u.LastName,
    IsEligible = u.Age > 21 && u.Email.Contains("@company.com"),
    Status = u.IsActive ? "Active" : "Inactive"
};

// Transform to DTO context
var dtoProjection = complexProjection.MapToFacetGeneric<UserDto>();
```

## Expression Composition

### Combining Predicates

Combine multiple conditions with logical operators:

```csharp
var isAdult = (Expression<Func<User, bool>>)(u => u.Age >= 18);
var isActive = (Expression<Func<User, bool>>)(u => u.IsActive);
var hasValidEmail = (Expression<Func<User, bool>>)(u => !string.IsNullOrEmpty(u.Email));

// Combine with AND
var validUserFilter = FacetExpressionExtensions.CombineWithAnd(isAdult, isActive, hasValidEmail);

// Combine with OR
var flexibleFilter = FacetExpressionExtensions.CombineWithOr(isAdult, hasValidEmail);

// Transform combined expressions
var dtoFilter = validUserFilter.MapToFacet<UserDto>();
```

### Negating Conditions

Create opposite conditions easily:

```csharp
var activeUsers = (Expression<Func<User, bool>>)(u => u.IsActive);
var inactiveUsers = activeUsers.Negate();

// Transform negated expression
var inactiveDtoUsers = inactiveUsers.MapToFacet<UserDto>();
```

## Real-World Examples

### Repository Pattern with Shared Business Logic

```csharp
public class UserService
{
    // Define business rules once
    private readonly Expression<Func<User, bool>> _activeUsersFilter = 
        u => u.IsActive && !u.IsDeleted && u.EmailVerified;
        
    private readonly Expression<Func<User, string>> _displayNameSelector =
        u => u.FirstName + " " + u.LastName;

    public IQueryable<User> GetActiveUsers(IQueryable<User> query)
    {
        return query.Where(_activeUsersFilter);
    }
    
    public IEnumerable<UserDto> FilterActiveDtos(IEnumerable<UserDto> dtos)
    {
        var dtoFilter = _activeUsersFilter.MapToFacet<UserDto>();
        return dtos.Where(dtoFilter.Compile());
    }
    
    public IEnumerable<UserDto> SortDtosByName(IEnumerable<UserDto> dtos)
    {
        var dtoSelector = _displayNameSelector.MapToFacet<UserDto, string>();
        return dtos.OrderBy(dtoSelector.Compile());
    }
}
```

### Dynamic Query Building

Build complex filters dynamically and apply to both entities and DTOs:

```csharp
public static class UserFilters
{
    public static Expression<Func<User, bool>> ByAgeRange(int minAge, int maxAge) =>
        u => u.Age >= minAge && u.Age <= maxAge;
        
    public static Expression<Func<User, bool>> ByStatus(bool isActive) =>
        u => u.IsActive == isActive;
        
    public static Expression<Func<User, bool>> ByEmailDomain(string domain) =>
        u => u.Email.EndsWith("@" + domain);
}

public class UserQueryBuilder
{
    public (Expression<Func<User, bool>>, Expression<Func<UserDto, bool>>) BuildFilters(
        int? minAge = null,
        int? maxAge = null,
        bool? activeOnly = null,
        string emailDomain = null)
    {
        var filters = new List<Expression<Func<User, bool>>>();

        if (minAge.HasValue && maxAge.HasValue)
            filters.Add(UserFilters.ByAgeRange(minAge.Value, maxAge.Value));
            
        if (activeOnly.HasValue)
            filters.Add(UserFilters.ByStatus(activeOnly.Value));
            
        if (!string.IsNullOrEmpty(emailDomain))
            filters.Add(UserFilters.ByEmailDomain(emailDomain));

        var entityFilter = FacetExpressionExtensions.CombineWithAnd(filters.ToArray());
        var dtoFilter = entityFilter.MapToFacet<UserDto>();
        
        return (entityFilter, dtoFilter);
    }
}
```

### API Controller with Consistent Filtering

```csharp
[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly UserService _userService;
    
    // Shared business logic
    private readonly Expression<Func<User, bool>> _publicUsersFilter = 
        u => u.IsActive && u.IsPublic && !u.IsDeleted;

    [HttpGet("entities")]
    public async Task<ActionResult<IEnumerable<User>>> GetEntities()
    {
        var users = await _userService.GetUsers()
            .Where(_publicUsersFilter)
            .ToListAsync();
            
        return Ok(users);
    }
    
    [HttpGet("dtos")]
    public async Task<ActionResult<IEnumerable<UserDto>>> GetDtos()
    {
        var users = await _userService.GetUsers().ToListAsync();
        var dtos = users.Select(u => u.ToFacet<UserDto>()).ToList();
        
        var dtoFilter = _publicUsersFilter.MapToFacet<UserDto>();
        var filteredDtos = dtos.Where(dtoFilter.Compile()).ToList();
        
        return Ok(filteredDtos);
    }
}
```

## Advanced Scenarios

### Nested Property Access

Handle complex object hierarchies:

```csharp
// Works with nested properties
Expression<Func<Order, bool>> recentOrders = o => 
    o.Customer.IsActive && 
    o.OrderDate > DateTime.Now.AddMonths(-1) &&
    o.Items.Any(i => i.Price > 100);

// Transform to DTO (assuming OrderDto has matching nested structure)
var dtoFilter = recentOrders.MapToFacet<OrderDto>();
```

### Method Call Transformations

Support common string and numeric operations:

```csharp
// Method calls are preserved in transformation
Expression<Func<User, bool>> emailDomainFilter = u => 
    u.Email.ToLower().EndsWith("@company.com") &&
    u.FirstName.StartsWith("John") &&
    u.Age.ToString().Length == 2;

var dtoFilter = emailDomainFilter.MapToFacet<UserDto>();
```

## Performance Considerations

- **Caching**: Property mappings are cached per type pair for optimal performance
- **Reflection Optimization**: Reflection results are cached and reused
- **Lazy Compilation**: Expression compilation happens only when needed
- **Thread Safety**: All caching mechanisms are thread-safe

## Best Practices

### 1. Define Business Rules Centrally
```csharp
public static class BusinessRules
{
    public static Expression<Func<User, bool>> ActiveUser => 
        u => u.IsActive && !u.IsDeleted;
        
    public static Expression<Func<User, bool>> EligibleForPromotion => 
        u => u.IsActive && u.LastLoginDate > DateTime.Now.AddMonths(-1);
}
```

### 2. Use Composition for Complex Logic
```csharp
var eligibleActiveUser = FacetExpressionExtensions.CombineWithAnd(
    BusinessRules.ActiveUser,
    BusinessRules.EligibleForPromotion
);
```

### 3. Cache Transformed Expressions
```csharp
public class UserFilterCache
{
    private static readonly Lazy<Expression<Func<UserDto, bool>>> _activeDtoFilter = 
        new(() => BusinessRules.ActiveUser.MapToFacet<UserDto>());
        
    public static Expression<Func<UserDto, bool>> ActiveDtoFilter => 
        _activeDtoFilter.Value;
}
```

## Supported Expression Types

The expression mapping library supports:

- **Binary expressions**: Comparisons (`==`, `!=`, `>`, `<`, `>=`, `<=`), logical operations (`&&`, `||`)
- **Unary expressions**: Negation (`!`), conversions, increment/decrement
- **Member access**: Property and field access (`u.Name`, `u.Profile.Email`)
- **Method calls**: Instance methods (`u.Name.StartsWith("A")`), static methods
- **Constants and literals**: Preserved as-is during transformation
- **Lambda expressions**: Parameter replacement and body transformation
- **New expressions**: Object creation and anonymous types
- **Conditional expressions**: Ternary operators (`condition ? true : false`)

## Troubleshooting

### Property Not Found Errors
Ensure that properties exist in both source and target types with compatible types:

```csharp
// This will fail if UserDto doesn't have an Age property
Expression<Func<User, bool>> ageFilter = u => u.Age > 18;
var dtoFilter = ageFilter.MapToFacet<UserDto>(); // May throw if Age doesn't exist
```

### Type Compatibility Issues
Property types must be compatible between source and target:

```csharp
// Works: both User.Id and UserDto.Id are int
Expression<Func<User, bool>> idFilter = u => u.Id > 0;

// May fail: if types don't match between User.CreatedDate and UserDto.CreatedDate
Expression<Func<User, bool>> dateFilter = u => u.CreatedDate > DateTime.Now;
```

## Integration with Other Facet Libraries

Expression mapping works seamlessly with:

- **Facet**: All Facet-generated types are supported
- **Facet.Extensions**: Use with LINQ extension methods
- **Facet.Mapping**: Combine with custom mapping configurations
- **Facet.Extensions.EFCore**: Transform expressions before EF Core queries

```csharp
// Combined usage example
var entityFilter = BusinessRules.ActiveUser;
var users = await dbContext.Users
    .Where(entityFilter)
    .ToFacetsAsync<UserDto>();

var dtoFilter = entityFilter.MapToFacet<UserDto>();
var additionalFiltering = users.Where(dtoFilter.Compile()).ToList();
```