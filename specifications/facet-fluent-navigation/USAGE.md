# Facet Fluent Navigation - Usage Guide

## Overview

The Facet fluent navigation feature provides a type-safe, fluent API for building EF Core queries with included navigation properties. This feature automatically generates builders, shape interfaces, and DbContext extension methods based on your entity models.

## Prerequisites

1. Install the `Facet.Extensions.EFCore` package
2. Your entities must be decorated with either `[Facet]` or `[GenerateDtos]` attributes
3. Your DbContext must include the entities as DbSets

## Basic Usage

### 1. Entity Setup

```csharp
[GenerateDtos(Types = DtoTypes.Response)]
public class User
{
    public int Id { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string Email { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    
    // Navigation properties
    public ICollection<Order> Orders { get; set; } = new List<Order>();
}

[GenerateDtos(Types = DtoTypes.Response)]
public class Order
{
    public int Id { get; set; }
    public decimal TotalAmount { get; set; }
    public string Status { get; set; }
    public DateTime OrderDate { get; set; }
    
    // Navigation properties
    public int UserId { get; set; }
    public User User { get; set; }
    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
}

[GenerateDtos(Types = DtoTypes.Response)]
public class Product
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public decimal Price { get; set; }
    
    // Navigation properties
    public int CategoryId { get; set; }
    public Category Category { get; set; }
    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
}
```

### 2. Generated Artifacts

The source generator automatically creates:

- **Shape Interfaces**: `IUserShape`, `IOrderShape`, `IProductShape`
- **Fluent Builders**: `FacetUserBuilder<TShape>`, `FacetOrderBuilder<TShape>`, etc.
- **Selectors**: `UserSelectors.BaseShape`, `OrderSelectors.WithUser`, etc.
- **DbContext Extensions**: `context.FacetUser()`, `context.FacetOrder()`, etc.

### 3. Basic Queries

#### Simple Entity Query

```csharp
// Get all active users as DTOs
var activeUsers = await context.FacetUser()
    .Where(u => u.IsActive)
    .ToListAsync();

// Get user by ID
var user = await context.FacetUser()
    .GetByIdAsync(userId);

// Get first user matching criteria
var firstUser = await context.FacetUser()
    .Where(u => u.Email.Contains("@example.com"))
    .FirstOrDefaultAsync();
```

#### Navigation Property Inclusion

```csharp
// Include user's orders
var userWithOrders = await context.FacetUser()
    .WithOrders()
    .Where(u => u.Id == userId)
    .FirstOrDefaultAsync();

// Include order with user and order items
var orderDetails = await context.FacetOrder()
    .WithUser()
    .WithOrderItems()
    .Where(o => o.Id == orderId)
    .FirstOrDefaultAsync();

// Nested navigation: Order with User and OrderItems with Products
var complexOrder = await context.FacetOrder()
    .WithUser()
    .WithOrderItems(items => items.WithProduct())
    .FirstOrDefaultAsync();
```

### 4. Advanced Navigation Patterns

#### Chained Navigation

```csharp
// Get products with their categories and order items
var productDetails = await context.FacetProduct()
    .WithCategory()
    .WithOrderItems(items => items
        .WithOrder(order => order.WithUser()))
    .Where(p => p.Price > 100m)
    .ToListAsync();
```

#### Conditional Navigation

```csharp
// Conditionally include navigation based on user role
var query = context.FacetUser();

if (includeOrders)
{
    query = query.WithOrders();
}

if (includeOrderDetails)
{
    query = query.WithOrders(orders => orders
        .WithOrderItems(items => items.WithProduct()));
}

var results = await query.ToListAsync();
```

### 5. Generic Entry Point

```csharp
// Use generic method for dynamic scenarios
public async Task<List<TDto>> GetEntitiesAsync<TEntity, TDto>() 
    where TEntity : class 
    where TDto : class
{
    return await context.Facet<TEntity, TDto>()
        .ToListAsync();
}
```

### 6. TypeScript Integration

When using TypeScript attributes, shape interfaces are decorated for frontend integration:

```csharp
[GenerateDtos(
    Types = DtoTypes.Response,
    TypeScriptAttributes = new[] { "[TsInterface]", "[TsExport]" })]
public class User
{
    // ... properties
}
```

Generated shape interface:
```csharp
[TsInterface]
[TsExport]
public interface IUserShape
{
    int Id { get; }
    string FirstName { get; }
    string LastName { get; }
    string Email { get; }
    bool IsActive { get; }
    DateTime CreatedAt { get; }
}
```

## Performance Considerations

### 1. Automatic No-Tracking

All fluent builders automatically use `AsNoTracking()` for optimal read performance:

```csharp
// This automatically uses AsNoTracking()
var users = await context.FacetUser().ToListAsync();
```

### 2. Projection-Based Queries

The fluent API uses EF Core's projection capabilities, generating efficient SQL:

```csharp
// Generates SELECT with only necessary columns
var userSummaries = await context.FacetUser()
    .Select(u => new { u.Id, u.FirstName, u.LastName })
    .ToListAsync();
```

### 3. Selective Navigation Loading

Only requested navigations are included in queries:

```csharp
// Only loads User and Order data, not OrderItems
var ordersWithUsers = await context.FacetOrder()
    .WithUser()
    .ToListAsync();
```

## Error Handling

### Common Error Scenarios

1. **Missing Projection Property**:
   ```
   InvalidOperationException: DTO type 'UserDto' does not have a static 'Projection' property.
   ```
   **Solution**: Ensure your DTO is generated by Facet or manually implement the Projection property.

2. **Circular Navigation**:
   ```
   NotImplementedException: Nested navigation configuration not yet implemented
   ```
   **Solution**: Use simpler navigation patterns or load data in multiple queries.

3. **Type Resolution**:
   ```
   CS0246: The type or namespace name 'FacetUserBuilder<>' could not be found
   ```
   **Solution**: Ensure all generated files are included in compilation and namespaces are consistent.

## Best Practices

### 1. Shape Interface Usage

Use shape interfaces for consistent typing:

```csharp
public async Task<IUserShape> GetUserShapeAsync(int id)
{
    return await context.FacetUser()
        .GetByIdAsync(id);
}
```

### 2. Repository Pattern Integration

```csharp
public class UserRepository
{
    private readonly MyDbContext _context;

    public UserRepository(MyDbContext context)
    {
        _context = context;
    }

    public async Task<List<IUserShape>> GetActiveUsersAsync()
    {
        return await _context.FacetUser()
            .Where(u => u.IsActive)
            .ToListAsync();
    }

    public async Task<IUserWithOrdersShape> GetUserWithOrdersAsync(int id)
    {
        return await _context.FacetUser()
            .WithOrders()
            .GetByIdAsync(id);
    }
}
```

### 3. Testing

Use the fluent API in integration tests:

```csharp
[Fact]
public async Task GetUserWithOrders_ReturnsCorrectData()
{
    // Arrange
    var user = await SeedUserWithOrdersAsync();

    // Act
    var result = await _context.FacetUser()
        .WithOrders()
        .GetByIdAsync(user.Id);

    // Assert
    Assert.NotNull(result);
    Assert.NotEmpty(result.Orders);
}
```

## Migration from Existing Code

### From SelectFacet

```csharp
// Before
var users = await context.Users
    .SelectFacet<User, UserDto>()
    .ToListAsync();

// After
var users = await context.FacetUser()
    .ToListAsync();
```

### From Manual Includes

```csharp
// Before
var ordersWithUsers = await context.Orders
    .Include(o => o.User)
    .SelectFacet<Order, OrderDto>()
    .ToListAsync();

// After
var ordersWithUsers = await context.FacetOrder()
    .WithUser()
    .ToListAsync();
```

## Troubleshooting

### Build Issues

1. **Clean and Rebuild**: Source generators sometimes need a clean rebuild
   ```bash
   dotnet clean && dotnet build
   ```

2. **Check Generated Files**: Verify files are generated in `obj/Debug/.../generated/`

3. **Namespace Issues**: Ensure generated code uses consistent namespaces

### Runtime Issues

1. **Missing EF Model**: Ensure entities are properly configured in DbContext
2. **Projection Errors**: Verify DTO properties match entity properties
3. **SQL Performance**: Use EF logging to verify generated SQL queries

## Limitations

### Current Limitations

1. **Nested Navigation Configuration**: Complex nested scenarios use placeholder implementations
2. **Dynamic Navigation**: Runtime navigation configuration is limited
3. **Custom Projections**: Advanced projection scenarios may need manual implementation

### Future Enhancements

1. **Full Nested Navigation Support**: Complete implementation of complex navigation chains
2. **Dynamic Builder Configuration**: Runtime navigation property selection
3. **Advanced Projection Patterns**: Support for custom projection expressions
4. **Performance Optimizations**: Enhanced SQL generation and caching

## Contributing

To contribute to the fluent navigation feature:

1. Review the implementation plan in `specifications/facet-fluent-navigation/implementation-plan.md`
2. Check existing integration tests in `test/Facet.Extensions.EFCore.Tests/IntegrationTests/`
3. Follow the established patterns for source generation
4. Add comprehensive tests for new functionality

## Support

For issues and questions:

1. Check existing GitHub issues
2. Review the implementation plan for known limitations
3. Create detailed issue reports with minimal reproduction cases
4. Include generated source files when reporting compilation issues