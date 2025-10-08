# Facet.Mapping.Expressions

Expression tree transformation and mapping utilities for Facet DTOs. Transform predicates, selectors, and other expressions between source entities and their Facet projections.

## Features

- **Predicate Mapping**: Transform filter expressions from entity types to DTO types
- **Selector Mapping**: Transform sorting and selection expressions  
- **Generic Expression Transformation**: Handle any lambda expression pattern
- **Expression Composition**: Combine predicates with AND/OR logic, negation
- **Performance Optimized**: Caches reflection results and property mappings
- **Type Safe**: Compile-time checking with full IntelliSense support

## Installation

```bash
dotnet add package Facet.Mapping.Expressions
```

## Quick Start

### Basic Predicate Mapping

Transform business logic from entities to DTOs:

```csharp
using Facet.Mapping.Expressions;

// Original predicate for entity
Expression<Func<User, bool>> entityFilter = u => u.IsActive && u.Age > 18;

// Transform to work with DTO
Expression<Func<UserDto, bool>> dtoFilter = entityFilter.MapToFacet<UserDto>();

// Use with LINQ queries
var results = dtoCollection.Where(dtoFilter.Compile()).ToList();
```

### Selector Expression Mapping

Transform sorting and selection logic:

```csharp
// Original selector for entity
Expression<Func<User, string>> entitySelector = u => u.LastName;

// Transform to work with DTO  
Expression<Func<UserDto, string>> dtoSelector = entitySelector.MapToFacet<UserDto, string>();

// Use for sorting
var sorted = dtoCollection.OrderBy(dtoSelector.Compile()).ToList();
```

### Expression Composition

Combine multiple conditions:

```csharp
var isAdult = (Expression<Func<User, bool>>)(u => u.Age >= 18);
var isActive = (Expression<Func<User, bool>>)(u => u.IsActive);
var isVip = (Expression<Func<User, bool>>)(u => u.IsVip);

// Combine with AND
var adultAndActive = FacetExpressionExtensions.CombineWithAnd(isAdult, isActive);

// Combine with OR  
var vipOrAdult = FacetExpressionExtensions.CombineWithOr(isVip, isAdult);

// Negate a condition
var inactive = isActive.Negate();

// Transform composed expressions to DTOs
var dtoFilter = adultAndActive.MapToFacet<UserDto>();
```

## Advanced Usage

### Generic Expression Transformation

Handle complex expressions with anonymous objects, method calls, etc.:

```csharp
// Complex expression with method calls and projections
Expression<Func<User, object>> complexExpr = u => new {
    FullName = u.FirstName + " " + u.LastName,
    IsEligible = u.Age > 21 && u.Email.Contains("@company.com"),
    DisplayAge = u.Age.ToString()
};

// Transform to DTO context
var dtoExpr = complexExpr.MapToFacetGeneric<UserDto>();
```

### Working with Repository Patterns

Common pattern for reusable business logic:

```csharp
public class UserRepository 
{
    private readonly Expression<Func<User, bool>> _activeUsersFilter = 
        u => u.IsActive && !u.IsDeleted;
        
    public IQueryable<User> GetActiveUsers(IQueryable<User> query)
    {
        return query.Where(_activeUsersFilter);
    }
    
    public IEnumerable<UserDto> GetActiveUserDtos(IEnumerable<UserDto> dtos)
    {
        var dtoFilter = _activeUsersFilter.MapToFacet<UserDto>();
        return dtos.Where(dtoFilter.Compile());
    }
}
```

### Dynamic Query Building

Build complex queries dynamically:

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

// Combine filters dynamically
var filters = new List<Expression<Func<User, bool>>>();

if (ageFilter.HasValue)
    filters.Add(UserFilters.ByAgeRange(ageFilter.Value.Min, ageFilter.Value.Max));
    
if (activeOnly)
    filters.Add(UserFilters.ByStatus(true));
    
if (!string.IsNullOrEmpty(emailDomain))
    filters.Add(UserFilters.ByEmailDomain(emailDomain));

// Combine all filters
var combinedFilter = FacetExpressionExtensions.CombineWithAnd(filters.ToArray());

// Apply to both entities and DTOs
var entityResults = entityQuery.Where(combinedFilter);
var dtoFilter = combinedFilter.MapToFacet<UserDto>();
var dtoResults = dtoCollection.Where(dtoFilter.Compile());
```

## How It Works

The library uses expression tree visitors to transform expressions from source types to target (Facet) types:

1. **Property Mapping**: Maps properties between source and target types by name and type compatibility
2. **Parameter Replacement**: Replaces lambda parameters with the appropriate target type parameters  
3. **Expression Transformation**: Recursively transforms all parts of the expression tree
4. **Type Safety**: Maintains compile-time type checking throughout the transformation

## Supported Expression Types

- **Binary expressions**: Comparisons (`==`, `!=`, `>`, `<`, etc.), logical operations (`&&`, `||`)
- **Unary expressions**: Negation (`!`), conversions
- **Member access**: Property and field access (`u.Name`, `u.Age`)
- **Method calls**: Instance and static method calls (with compatible signatures)
- **Constants and literals**: Preserved as-is
- **Complex projections**: Anonymous objects, new expressions

## Performance Considerations

- Property mappings are cached per type pair
- Reflection results are cached for better performance
- Expression compilation is lazy - only when needed
- Thread-safe caching mechanisms

## Integration with Facet

This library works seamlessly with all Facet-generated types:

- Standard Facets with `[Facet]` attribute
- Record-based Facets (using `record` keyword)
- Struct-based Facets (using `struct` keyword)
- Custom mapping configurations
- Async mapping scenarios (when combined with `Facet.Mapping`)
