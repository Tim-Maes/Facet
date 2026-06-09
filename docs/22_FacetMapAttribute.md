# FacetMap Attribute

The `[FacetMap]` attribute generates extension methods for mapping between a source entity and an externally-defined target DTO. Unlike `[Facet]`, it does not generate property declarations on the target type, making it ideal for DDD architectures where the DTO lives in a separate shared/contracts assembly.

## When to Use

Use `[FacetMap]` when:

- Your DTO type lives in a separate shared/contracts project
- Both frontend and backend reference the same DTO assembly
- You need the mapping logic only in the domain/backend project
- You want to avoid type duplication across assembly boundaries

Use `[Facet]` when:

- The DTO is generated and lives in the same project as the entity
- You want Facet to generate both the DTO properties and the mapping code

## Setup

### Project Structure

```
SharedContracts/
  └── CustomerDto.cs          (plain POCO, no Facet dependency)

Domain/
  ├── Customer.cs             (entity)
  └── CustomerMappings.cs     (marker class with [FacetMap])
      → Generated: CustomerMappings.FacetMap.g.cs

Frontend/
  └── References SharedContracts only
```

### Installation

The domain project needs the Facet package:

```
dotnet add package Facet
```

The shared/contracts project does not need any Facet dependency.

## Basic Usage

Define your DTO as a plain class in the shared project:

```csharp
// SharedContracts/CustomerDto.cs
namespace MyApp.Contracts;

public class CustomerDto
{
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}
```

In your domain project, create a static partial class with `[FacetMap]`:

```csharp
// Domain/Mappings/CustomerMappings.cs
using Facet;
using MyApp.Contracts;

namespace MyApp.Domain.Mappings;

[FacetMap(typeof(Customer), typeof(CustomerDto), "PasswordHash", "CreatedAt",
    GenerateToSource = true)]
public static partial class CustomerMappings { }
```

This generates extension methods you can use:

```csharp
// Source to target
var dto = customer.ToCustomerDto();

// Target back to source
var entity = dto.ToCustomer();

// LINQ projection for EF Core
var dtos = dbContext.Customers
    .Select(CustomerMappings.CustomerDtoProjection)
    .ToListAsync();
```

## Attribute Parameters

### Constructor Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `sourceType` | `Type` | The source entity type |
| `targetType` | `Type` | The target DTO type (must already exist) |
| `exclude` | `params string[]` | Property names to exclude from mapping |

### Named Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `Include` | `string[]?` | null | Only map these properties (mutually exclusive with exclude) |
| `GenerateToSource` | `bool` | false | Generate reverse mapping (target-to-source) |
| `GenerateProjection` | `bool` | true | Generate LINQ projection expression |
| `Configuration` | `Type?` | null | Custom mapping logic type |
| `ToSourceConfiguration` | `Type?` | null | Custom reverse mapping logic type |
| `MaxDepth` | `int` | 10 | Max depth for nested object mapping |
| `CollectionTargetType` | `Type?` | null | Override collection type for mapped collections |

## Generated Output

For the example above, Facet generates:

```csharp
public static partial class CustomerMappings
{
    public static CustomerDto ToCustomerDto(this Customer source)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));

        var target = new CustomerDto();
        target.Id = source.Id;
        target.FirstName = source.FirstName;
        target.LastName = source.LastName;
        target.Email = source.Email;
        target.IsActive = source.IsActive;

        return target;
    }

    public static Customer ToCustomer(this CustomerDto dto)
    {
        if (dto == null) throw new ArgumentNullException(nameof(dto));

        var target = new Customer();
        target.Id = dto.Id;
        target.FirstName = dto.FirstName;
        target.LastName = dto.LastName;
        target.Email = dto.Email;
        target.IsActive = dto.IsActive;

        return target;
    }

    public static Expression<Func<Customer, CustomerDto>> CustomerDtoProjection =>
        source => new CustomerDto
        {
            Id = source.Id,
            FirstName = source.FirstName,
            LastName = source.LastName,
            Email = source.Email,
            IsActive = source.IsActive
        };
}
```

## Collections

Collections are automatically mapped when both source and target have matching collection properties:

```csharp
public class Order
{
    public int Id { get; set; }
    public List<OrderLine> Lines { get; set; } = new();
}

public class OrderDto
{
    public int Id { get; set; }
    public List<OrderLine> Lines { get; set; } = new();
}

[FacetMap(typeof(Order), typeof(OrderDto), GenerateToSource = true)]
public static partial class OrderMappings { }
```

## Custom Configuration

You can inject custom mapping logic:

```csharp
public class CustomerMapConfig
{
    public static void Map(Customer source, CustomerDto target)
    {
        target.FullName = $"{source.FirstName} {source.LastName}";
    }
}

[FacetMap(typeof(Customer), typeof(CustomerDto),
    Configuration = typeof(CustomerMapConfig),
    GenerateToSource = true)]
public static partial class CustomerMappings { }
```

The `Map` method is called after all automatic property assignments.

## Multiple Mappings

You can place multiple `[FacetMap]` attributes on the same class:

```csharp
[FacetMap(typeof(Customer), typeof(CustomerDto), "PasswordHash", GenerateToSource = true)]
[FacetMap(typeof(Customer), typeof(CustomerSummaryDto), Include = new[] { "Id", "FirstName", "LastName" })]
public static partial class CustomerMappings { }
```

## Naming Conventions

| Generated Member | Name Pattern |
|-----------------|--------------|
| Source-to-target method | `To{TargetTypeName}` (e.g., `ToCustomerDto`) |
| Target-to-source method | `To{SourceTypeName}` (e.g., `ToCustomer`) |
| Projection property | `{TargetTypeName}Projection` (e.g., `CustomerDtoProjection`) |

## Comparison: [Facet] vs [FacetMap]

| Feature | `[Facet]` | `[FacetMap]` |
|---------|-----------|--------------|
| Generates DTO properties | Yes | No |
| Target type requirement | `partial class` in same project | Any class, any assembly |
| Mapping style | Instance members (constructor, methods) | Extension methods |
| Shared assembly support | Requires file-linking workarounds | Native support |
| Facet dependency on DTO | Required | Not required |
| Projection expression | `UserDto.Projection` | `UserMappings.UserDtoProjection` |
| Reverse mapping | `dto.ToSource()` | `dto.ToUser()` |
