# Enum Conversion with ConvertEnumsTo

The `ConvertEnumsTo` property on the `[Facet]` attribute allows you to automatically convert all enum properties from a source type into a different representation (`string` or `int`) in the generated facet. This is useful for API DTOs, serialization scenarios, and database storage where you need enum values as strings or integers rather than their enum types.

## Basic Usage

### Convert Enums to Strings

```csharp
public enum UserStatus
{
    Active,
    Inactive,
    Pending,
    Suspended
}

public class User
{
    public int Id { get; set; }
    public string Name { get; set; }
    public UserStatus Status { get; set; }
    public string Email { get; set; }
}

// All enum properties become string in the generated facet
[Facet(typeof(User), ConvertEnumsTo = typeof(string))]
public partial class UserDto;

// Generated property: public string Status { get; set; }
// Instead of:         public UserStatus Status { get; set; }
```

### Convert Enums to Integers

```csharp
// All enum properties become int in the generated facet
[Facet(typeof(User), ConvertEnumsTo = typeof(int))]
public partial class UserDto;

// Generated property: public int Status { get; set; }
```

## How It Works

### Constructor (Source ? Facet)

When converting **to string**, the generated constructor uses `.ToString()`:
```csharp
// Generated code
public UserDto(User source)
{
    this.Id = source.Id;
    this.Name = source.Name;
    this.Status = source.Status.ToString();  // Enum ? string
    this.Email = source.Email;
}
```

When converting **to int**, the generated constructor uses a cast:
```csharp
// Generated code
public UserDto(User source)
{
    this.Id = source.Id;
    this.Name = source.Name;
    this.Status = (int)source.Status;  // Enum ? int
    this.Email = source.Email;
}
```

### Projection (LINQ / EF Core)

The projection expression also handles the conversion, so it works in LINQ queries and with Entity Framework Core:

```csharp
// String conversion projection
public static Expression<Func<User, UserDto>> Projection =>
    source => new UserDto
    {
        Id = source.Id,
        Name = source.Name,
        Status = source.Status.ToString(),  // Translates to SQL
        Email = source.Email
    };

// Int conversion projection
public static Expression<Func<User, UserDto>> Projection =>
    source => new UserDto
    {
        Id = source.Id,
        Name = source.Name,
        Status = (int)source.Status,  // Translates to SQL
        Email = source.Email
    };
```

### ToSource (Facet ? Source) — Reverse Mapping

When `GenerateToSource = true`, the generated `ToSource()` method converts the value back to the original enum type:

**String ? Enum** uses `Enum.Parse`:
```csharp
// Generated code
public User ToSource()
{
    return new User
    {
        Id = this.Id,
        Name = this.Name,
        Status = (UserStatus)System.Enum.Parse(typeof(UserStatus), this.Status),
        Email = this.Email
    };
}
```

**Int ? Enum** uses a cast:
```csharp
// Generated code
public User ToSource()
{
    return new User
    {
        Id = this.Id,
        Name = this.Name,
        Status = (UserStatus)this.Status,
        Email = this.Email
    };
}
```

## Nullable Enum Properties

`ConvertEnumsTo` correctly handles nullable enum properties. The nullability is preserved in the converted type:

```csharp
public class Entity
{
    public int Id { get; set; }
    public string Name { get; set; }
    public UserStatus? Status { get; set; }           // Nullable enum
    public UserStatus NonNullableStatus { get; set; }  // Non-nullable enum
}

// Convert to string
[Facet(typeof(Entity), ConvertEnumsTo = typeof(string))]
public partial class EntityToStringDto;
// Status becomes: string (nullable ref type - null when source is null)
// NonNullableStatus becomes: string

// Convert to int
[Facet(typeof(Entity), ConvertEnumsTo = typeof(int))]
public partial class EntityToIntDto;
// Status becomes: int? (nullable value type)
// NonNullableStatus becomes: int
```

For nullable enums, the generated constructor includes null checks:
```csharp
// String conversion with nullable enum
this.Status = source.Status?.ToString();

// Int conversion with nullable enum
this.Status = source.Status.HasValue ? (int?)source.Status.Value : null;
```

## Combining with Other Features

### With NullableProperties

You can combine `ConvertEnumsTo` with `NullableProperties = true` for query/filter DTOs:

```csharp
[Facet(typeof(User), ConvertEnumsTo = typeof(string), NullableProperties = true)]
public partial class UserQueryDto;
// All properties become nullable
// Enum properties are converted to nullable strings
```

### With GenerateToSource

Enable round-trip mapping with enum conversion:

```csharp
[Facet(typeof(User), ConvertEnumsTo = typeof(string), GenerateToSource = true)]
public partial class UserDto;

// Forward: User ? UserDto (enum ? string)
var dto = new UserDto(user);
dto.Status // "Active" (string)

// Reverse: UserDto ? User (string ? enum)
var entity = dto.ToSource();
entity.Status // UserStatus.Active (enum)
```

### With Include/Exclude

```csharp
[Facet(typeof(User),
    Include = [nameof(User.Id), nameof(User.Name), nameof(User.Status)],
    ConvertEnumsTo = typeof(string))]
public partial class UserStatusDto;
```

## Supported Conversion Types

| ConvertEnumsTo | Property Type | Forward Conversion | Reverse Conversion |
|----------------|--------------|-------------------|-------------------|
| `typeof(string)` | `string` | `.ToString()` | `Enum.Parse()` |
| `typeof(int)` | `int` | `(int)` cast | `(EnumType)` cast |

## Use Cases

| Scenario | Recommended Type | Why |
|----------|-----------------|-----|
| JSON API responses | `typeof(string)` | Human-readable enum values |
| Database storage (int column) | `typeof(int)` | Compact storage, fast comparison |
| Frontend consumption | `typeof(string)` | No enum mapping needed in JS/TS |
| gRPC / Protobuf | `typeof(int)` | Protobuf uses integer enums |
| CSV/Excel export | `typeof(string)` | Readable column values |

## Important Considerations

1. **All enum properties are converted**: The setting applies to every enum property in the source type. If you need mixed behavior (some enums converted, some not), consider using separate facets or a custom mapping configuration.

2. **Non-enum properties are unaffected**: Only properties whose type is an enum (or nullable enum) are converted. All other properties remain unchanged.

3. **Round-trip safety**: When using `ConvertEnumsTo = typeof(string)` with `GenerateToSource = true`, ensure the string values match valid enum member names. Invalid strings will throw an `ArgumentException` from `Enum.Parse`.

4. **EF Core compatibility**: Both `ToString()` and `(int)` casts translate correctly to SQL in Entity Framework Core projections.

## Complete Example

```csharp
// Domain model
public enum OrderStatus
{
    Draft,
    Submitted,
    Processing,
    Completed,
    Cancelled
}

public class Order
{
    public int Id { get; set; }
    public string CustomerName { get; set; }
    public OrderStatus Status { get; set; }
    public decimal Total { get; set; }
}

// API response DTO with string enums
[Facet(typeof(Order), ConvertEnumsTo = typeof(string), GenerateToSource = true)]
public partial class OrderResponseDto;

// Usage
var order = new Order
{
    Id = 1,
    CustomerName = "Alice",
    Status = OrderStatus.Processing,
    Total = 99.99m
};

// Forward mapping
var dto = new OrderResponseDto(order);
Console.WriteLine(dto.Status); // "Processing"

// LINQ projection
var dtos = orders.AsQueryable()
    .Select(OrderResponseDto.Projection)
    .ToList();

// Reverse mapping
var entity = dto.ToSource();
Console.WriteLine(entity.Status); // OrderStatus.Processing

// EF Core query
var results = await dbContext.Orders
    .Where(o => o.Status == OrderStatus.Completed)
    .Select(OrderResponseDto.Projection)
    .ToListAsync();
```
