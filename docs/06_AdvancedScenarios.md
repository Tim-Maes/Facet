# Advanced Scenarios

This section covers advanced use cases and configuration options for Facet.

## Multiple Facets from One Source

You can create multiple facets from the same source type:

```csharp
public class User
{
    public int Id { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string Email { get; set; }
    public DateTime DateOfBirth { get; set; }
    public string Password { get; set; }
    public decimal Salary { get; set; }
    public string Department { get; set; }
}

// Public profile (exclude sensitive data)
[Facet(typeof(User), nameof(User.Password), nameof(User.Salary))]
public partial class UserPublicDto { }

// Contact information only (include specific fields)
[Facet(typeof(User), Include = new[] { "FirstName", "LastName", "Email" })]
public partial class UserContactDto { }

// Summary for lists (include minimal data)
[Facet(typeof(User), Include = new[] { "Id", "FirstName", "LastName" })]
public partial class UserSummaryDto { }

// HR view (exclude password but include salary)
[Facet(typeof(User), nameof(User.Password))]
public partial class UserHRDto { }
```

## Include vs Exclude Patterns

### Include Pattern - Building Focused DTOs

Use the `Include` pattern when you want facets with only specific properties:

```csharp
public class Product
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public decimal Price { get; set; }
    public int CategoryId { get; set; }
    public bool IsAvailable { get; set; }
    public DateTime CreatedAt { get; set; }
    public string InternalNotes { get; set; }
    public decimal Cost { get; set; }
    public string SKU { get; set; }
}

// API response with only customer-facing data
[Facet(typeof(Product), Include = new[] { "Id", "Name", "Description", "Price", "IsAvailable" })]
public partial record ProductApiDto;

// Search results with minimal data
[Facet(typeof(Product), Include = new[] { "Id", "Name", "Price" })]
public partial record ProductSearchDto;

// Internal admin view with cost data
[Facet(typeof(Product), Include = new[] { "Id", "Name", "Price", "Cost", "SKU", "InternalNotes" })]
public partial class ProductAdminDto;
```

### Exclude Pattern - Hiding Sensitive Data

Use the `Exclude` pattern when you want most properties but need to hide specific ones:

```csharp
// Exclude only sensitive information
[Facet(typeof(User), nameof(User.Password))]
public partial class UserDto { }

// Exclude multiple sensitive fields
[Facet(typeof(Employee), nameof(Employee.Salary), nameof(Employee.SSN))]
public partial class EmployeePublicDto { }
```

## Working with Fields

### Include Fields Example

```csharp
public class LegacyEntity
{
    public int Id;
    public string Name;
    public DateTime CreatedDate;
    public string Status { get; set; }
    public string Notes { get; set; }
}

// Include specific fields and properties
[Facet(typeof(LegacyEntity), Include = new[] { "Name", "Status" }, IncludeFields = true)]
public partial class LegacyEntityDto;

// Only include properties (fields ignored even if listed)
[Facet(typeof(LegacyEntity), Include = new[] { "Status", "Notes", "Name" }, IncludeFields = false)]
public partial class LegacyEntityPropsOnlyDto;
```

## Nested Types and Inheritance

### Including Properties from Base Classes

Include mode works seamlessly with inheritance:

```csharp
public class BaseEntity
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; }
}

public class Product : BaseEntity
{
    public string Name { get; set; }
    public decimal Price { get; set; }
    public string Category { get; set; }
}

// Include properties from both base and derived class
[Facet(typeof(Product), Include = new[] { "Id", "Name", "Price" })]
public partial class ProductSummaryDto;

// Include only derived class properties
[Facet(typeof(Product), Include = new[] { "Name", "Category" })]
public partial class ProductInfoDto;
```

### Nested Classes

Both include and exclude work with nested classes:

```csharp
public class OuterClass
{
    [Facet(typeof(User), Include = new[] { "FirstName", "LastName" })]
    public partial class NestedUserDto { }
}
```

## Custom Mapping with Include

You can combine Include mode with custom mapping:

```csharp
public class UserIncludeMapper : IFacetMapConfiguration<User, UserFormattedDto>
{
    public static void Map(User source, UserFormattedDto target)
    {
        target.DisplayName = $"{source.FirstName} {source.LastName}".ToUpper();
    }
}

[Facet(typeof(User), Include = new[] { "FirstName", "LastName" }, Configuration = typeof(UserIncludeMapper))]
public partial class UserFormattedDto
{
    public string DisplayName { get; set; } = string.Empty;
}
```

## Nullable Properties for Query and Patch Models

The `NullableProperties` parameter makes all non-nullable properties nullable in the generated facet, which is extremely useful for query DTOs and partial update scenarios.

### Query/Filter DTOs

```csharp
public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int CategoryId { get; set; }
    public bool IsAvailable { get; set; }
    public DateTime CreatedAt { get; set; }
}

// All properties become nullable for flexible querying
[Facet(typeof(Product), "CreatedAt", NullableProperties = true, GenerateBackTo = false)]
public partial class ProductQueryDto;

// Usage: Only specify the fields you want to filter on
var query = new ProductQueryDto
{
    Name = "Widget",           // Filter by name
    Price = 50.00m,            // Filter by price
    IsAvailable = true         // Filter by availability
    // Id, CategoryId remain null (not part of filter)
};

// Use in LINQ queries
var results = products.Where(p =>
    (query.Name == null || p.Name.Contains(query.Name)) &&
    (query.Price == null || p.Price == query.Price) &&
    (query.IsAvailable == null || p.IsAvailable == query.IsAvailable)
).ToList();
```

### Patch/Update Models

```csharp
// Create a patch model where only non-null fields are updated
[Facet(typeof(User), "Id", "CreatedAt", NullableProperties = true, GenerateBackTo = false)]
public partial class UserPatchDto;

// Usage: Only update specific fields
var patch = new UserPatchDto
{
    Email = "newemail@example.com",  // Update email
    IsActive = false                 // Update active status
    // Other properties remain null (won't be updated)
};

// Apply the patch
void ApplyPatch(User user, UserPatchDto patch)
{
    if (patch.FirstName != null) user.FirstName = patch.FirstName;
    if (patch.LastName != null) user.LastName = patch.LastName;
    if (patch.Email != null) user.Email = patch.Email;
    if (patch.IsActive != null) user.IsActive = patch.IsActive.Value;
    // ... etc
}
```

### How NullableProperties Works

- **Value Types**: Become nullable (`int` → `int?`, `bool` → `bool?`, `DateTime` → `DateTime?`, enums → `EnumType?`)
- **Reference Types**: Remain reference types but are marked as nullable (`string` → `string`)
- **Already Nullable Types**: Stay nullable (`DateTime?` remains `DateTime?`)

### Important Considerations

1. **Disable GenerateBackTo**: When using `NullableProperties = true`, set `GenerateBackTo = false` since mapping nullable properties back to non-nullable source properties is not logically sound.

2. **Constructor Behavior**: The generated constructor will still map from source to nullable properties correctly.

3. **Comparison with GenerateDtos Query**: This provides the same functionality as the Query DTOs in `GenerateDtos`, but gives you more control with the `Facet` attribute.

```csharp
// Similar to GenerateDtos Query DTO
[Facet(typeof(Product), NullableProperties = true, GenerateBackTo = false, Kind = FacetKind.Record)]
public partial record ProductQueryRecord;
```

## Mixed Usage Patterns

### API Layer Pattern

```csharp
// Controller uses different facets for different endpoints
[ApiController]
public class UsersController : ControllerBase
{
    [HttpGet]
    public List<UserSummaryDto> GetUsers()
    {
        return users.SelectFacets<User, UserSummaryDto>().ToList();
    }
    
    [HttpGet("{id}")]
    public UserDetailDto GetUser(int id)
    {
        return user.ToFacet<User, UserDetailDto>();
    }
    
    [HttpPost]
    public IActionResult CreateUser(UserCreateDto dto)
    {
        var user = dto.BackTo();
        // Save user...
    }
}

// Different DTOs for different use cases
[Facet(typeof(User), Include = new[] { "Id", "FirstName", "LastName" })]
public partial record UserSummaryDto;

[Facet(typeof(User), nameof(User.Password))] // Exclude password but include everything else
public partial class UserDetailDto;

[Facet(typeof(User), Include = new[] { "FirstName", "LastName", "Email", "Department" })]
public partial class UserCreateDto;
```

## Record Types with Include

Include mode works perfectly with modern C# records:

```csharp
public record ModernUser
{
    public required string Id { get; init; }
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public string? Email { get; set; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public string? Bio { get; set; }
}

// Generate record with only specific properties
[Facet(typeof(ModernUser), Include = new[] { "FirstName", "LastName", "Email" }, Kind = FacetKind.Record)]
public partial record ModernUserContactRecord;

// Include with init-only preservation
[Facet(typeof(ModernUser), 
       Include = new[] { "Id", "FirstName", "LastName" }, 
       Kind = FacetKind.Record,
       PreserveInitOnlyProperties = true)]
public partial record ModernUserImmutableRecord;
```

## Performance Considerations

### Include vs Exclude Performance

- **Include mode**: Generates smaller facets, which can improve serialization performance and reduce memory usage
- **Exclude mode**: Better when you need most properties from the source type

### Generated Code Comparison

```csharp
// Include mode - generates minimal code
[Facet(typeof(User), Include = new[] { "FirstName", "Email" })]
public partial class UserMinimalDto;
// Generated: only FirstName and Email properties

// Exclude mode - generates more code
[Facet(typeof(User), nameof(User.Password))]  
public partial class UserFullDto;
// Generated: all properties except Password
```

## BackTo Method Behavior with Include

When using Include mode, the `BackTo()` method generates source objects with default values for non-included properties:

```csharp
[Facet(typeof(User), Include = new[] { "FirstName", "LastName", "Email" })]
public partial class UserContactDto;

var dto = new UserContactDto();
var backToUser = dto.BackTo(); 

// backToUser.FirstName = dto.FirstName (copied)
// backToUser.LastName = dto.LastName (copied)  
// backToUser.Email = dto.Email (copied)
// backToUser.Id = 0 (default for int)
// backToUser.Password = string.Empty (default for string)
// backToUser.IsActive = false (default for bool)
```

## Best Practices

### When to Use Include

1. **API Responses**: Create focused DTOs for API endpoints
2. **Search Results**: Include only essential data for search listings
3. **Mobile Apps**: Minimize data transfer with targeted DTOs
4. **Microservices**: Create service-specific views of shared models

### When to Use Exclude

1. **Security**: Hide sensitive fields while keeping everything else
2. **Legacy Code**: Maintain existing patterns and behavior
3. **Large Models**: When you need most properties from complex entities

### Naming Conventions

```csharp
// Descriptive names for include-based DTOs
[Facet(typeof(User), Include = new[] { "FirstName", "LastName" })]
public partial class UserNameOnlyDto; // Clear about what's included

[Facet(typeof(Product), Include = new[] { "Id", "Name", "Price" })]
public partial class ProductListItemDto; // Indicates usage context

// Traditional names for exclude-based DTOs  
[Facet(typeof(User), nameof(User.Password))]
public partial class UserDto; // General DTO name when excluding few fields
```

---

See [Expression Mapping](10_ExpressionMapping.md) for advanced query scenarios and [Custom Mapping](04_CustomMapping.md) for complex transformation logic.
