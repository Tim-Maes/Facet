# MapWhen Attribute

The `[MapWhen]` attribute enables conditional property mapping based on source property values. Properties are only mapped when the specified condition evaluates to true.

## Basic Usage

```csharp
[Facet(typeof(Order))]
public partial class OrderDto
{
    [MapWhen("Status == OrderStatus.Completed")]
    public DateTime? CompletedAt { get; set; }
}
```

When `Status` is `Completed`, `CompletedAt` is mapped from source. Otherwise, it defaults to `null`.

## Supported Conditions

### Boolean Properties

```csharp
[MapWhen("IsActive")]
public string? Email { get; set; }
```

### Equality Comparisons

```csharp
[MapWhen("Status == OrderStatus.Completed")]
public DateTime? CompletedAt { get; set; }

[MapWhen("Status != OrderStatus.Cancelled")]
public string? TrackingNumber { get; set; }
```

### Null Checks

```csharp
[MapWhen("Email != null")]
public string? Email { get; set; }
```

### Numeric Comparisons

```csharp
[MapWhen("Age >= 18")]
public string? AdultContent { get; set; }
```

### Negation

```csharp
[MapWhen("!IsDeleted")]
public string? Content { get; set; }
```

## Multiple Conditions

Multiple `[MapWhen]` attributes on the same property are combined with AND logic:

```csharp
[MapWhen("IsActive")]
[MapWhen("Status == OrderStatus.Completed")]
public DateTime? CompletedAt { get; set; }
```

This maps `CompletedAt` only when `IsActive` is true AND `Status` is `Completed`.

## Generated Code

### Constructor

```csharp
// Input
[Facet(typeof(Order))]
public partial class OrderDto
{
    [MapWhen("Status == OrderStatus.Completed")]
    public DateTime? CompletedAt { get; set; }
}

// Generated constructor
public OrderDto(Order source)
{
    Id = source.Id;
    Status = source.Status;
    CompletedAt = (source.Status == OrderStatus.Completed)
        ? source.CompletedAt
        : default;
}
```

### Projection

The same conditional logic is applied in the Projection expression for use with Entity Framework Core:

```csharp
public static Expression<Func<Order, OrderDto>> Projection => source => new OrderDto
{
    Id = source.Id,
    Status = source.Status,
    CompletedAt = (source.Status == OrderStatus.Completed)
        ? source.CompletedAt
        : default
};
```

## Attribute Properties

| Property | Type | Description |
|----------|------|-------------|
| `Condition` | `string` | The condition expression (required) |
| `Default` | `object?` | Custom default value when condition is false |
| `IncludeInProjection` | `bool` | Whether to include in Projection expression (default: true) |

### Custom Default Values

```csharp
[MapWhen("HasPrice", Default = 0)]
public decimal Price { get; set; }
```

### Excluding from Projection

For conditions that can't be translated to SQL:

```csharp
[MapWhen("IsActive", IncludeInProjection = false)]
public string? Email { get; set; }
```

## Expression Syntax

The condition string uses C# expression syntax:

### Supported Operators
- Comparison: `==`, `!=`, `<`, `>`, `<=`, `>=`
- Logical: `&&`, `||`, `!`

### Property Access
- Simple: `IsActive`, `Status`, `Email`
- Enum values: `OrderStatus.Completed`, `Status == OrderStatus.Pending`

### Literals
- Booleans: `true`, `false`
- Null: `null`
- Numbers: `18`, `0`

## Example Use Cases

### Status-Dependent Fields

```csharp
[Facet(typeof(Subscription))]
public partial class SubscriptionDto
{
    public SubscriptionStatus Status { get; set; }

    [MapWhen("Status == SubscriptionStatus.Active")]
    public DateTime? NextBillingDate { get; set; }

    [MapWhen("Status == SubscriptionStatus.Cancelled")]
    public string? CancellationReason { get; set; }
}
```

### Conditional Sensitive Data

```csharp
[Facet(typeof(Employee))]
public partial class EmployeeDto
{
    public string Name { get; set; }

    [MapWhen("!IsSalaryConfidential")]
    public decimal? Salary { get; set; }
}
```

### Age-Gated Content

```csharp
[Facet(typeof(User))]
public partial class UserDto
{
    public int Age { get; set; }

    [MapWhen("Age >= 18")]
    public string? AdultPreferences { get; set; }
}
```

## Limitations

- Method calls in conditions (like `string.IsNullOrEmpty()`) are not supported
- Nested property access (like `Address.City`) is not supported
- Complex expressions may need to be simplified

## Best Practices

1. **Keep conditions simple** - Use basic comparisons and boolean checks
2. **Use nullable properties** - Since conditions may be false, properties should typically be nullable
3. **Consider EF Core translation** - If using projections with a database, ensure conditions can translate to SQL
4. **Test both paths** - Write tests for both when conditions are true and false
