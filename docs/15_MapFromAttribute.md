# Property Mapping with MapFrom

The `[MapFrom]` attribute provides declarative property mapping, allowing you to rename properties without implementing a full custom mapping configuration. This is perfect for simple property renames, API response shaping, and maintaining clean separation between domain and DTO property names.

## Basic Usage

Use `[MapFrom]` on properties in your Facet class to specify which source property to map from:

```csharp
public class User
{
    public int Id { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string Email { get; set; }
    public int Age { get; set; }
}

[Facet(typeof(User), GenerateToSource = true)]
public partial class UserDto
{
    [MapFrom("FirstName", Reversible = true)]
    public string Name { get; set; } = string.Empty;

    [MapFrom("LastName", Reversible = true)]
    public string FamilyName { get; set; } = string.Empty;
}
```

This generates:
- **Constructor**: Maps `source.FirstName` to `Name` and `source.LastName` to `FamilyName`
- **Projection**: Uses the same mapping for EF Core queries
- **ToSource()**: Reverses the mapping automatically

## How It Works

When you use `[MapFrom]`:

1. **The source property is not auto-generated** - You declare the target property with its new name
2. **Mapping is automatic** - Constructor, Projection, and ToSource all use the mapping
3. **Other properties remain unchanged** - Properties without `[MapFrom]` work normally

```csharp
var user = new User
{
    Id = 1,
    FirstName = "John",
    LastName = "Doe",
    Email = "john@example.com",
    Age = 30
};

var dto = new UserDto(user);
// dto.Id = 1 (auto-mapped)
// dto.Name = "John" (mapped from FirstName)
// dto.FamilyName = "Doe" (mapped from LastName)
// dto.Email = "john@example.com" (auto-mapped)
// dto.Age = 30 (auto-mapped)

// Reverse mapping
var entity = dto.ToSource();
// entity.FirstName = "John" (mapped from Name)
// entity.LastName = "Doe" (mapped from FamilyName)
```

## Attribute Properties

### Source (Required)

The source property name to map from:

```csharp
[MapFrom("FirstName")]
public string Name { get; set; }
```

### Reversible

Controls whether the mapping is included in `ToSource()`. Default is `false` (opt-in).

```csharp
// This property WILL be mapped back to the source
[MapFrom("FirstName", Reversible = true)]
public string Name { get; set; } = string.Empty;

// This property will NOT be mapped back (default)
[MapFrom("LastName")]
public string DisplayName { get; set; } = string.Empty;
```

Use `Reversible = true` when:
- You need the mapping to work both ways (source ↔ DTO)
- The property should be included in `ToSource()` output

Keep `Reversible = false` (default) for:
- One-way mappings (source → DTO only)
- Read-only DTOs that don't need reverse mapping
- Properties that shouldn't modify the source entity

### IncludeInProjection

Controls whether the mapping is included in the static `Projection` expression. Default is `true`.

```csharp
// This property will NOT be included in EF Core projections
[MapFrom("ComplexField", IncludeInProjection = false)]
public string Computed { get; set; } = string.Empty;
```

Use `IncludeInProjection = false` for:
- Mappings that cannot be translated to SQL
- Properties requiring client-side evaluation
- Complex expressions that EF Core doesn't support

## Examples

### Simple Property Rename

```csharp
[Facet(typeof(Customer), GenerateToSource = true)]
public partial class CustomerDto
{
    [MapFrom("CompanyName", Reversible = true)]
    public string Company { get; set; } = string.Empty;

    [MapFrom("ContactName", Reversible = true)]
    public string Contact { get; set; } = string.Empty;
}
```

### One-Way Mapping (Default)

```csharp
[Facet(typeof(Product))]
public partial class ProductDto
{
    // Display-only property, default is not reversible
    [MapFrom("Name")]
    public string ProductTitle { get; set; } = string.Empty;
}
```

### With Nested Facets

MapFrom works with nested facets too:

```csharp
[Facet(typeof(Company), GenerateToSource = true)]
public partial class CompanyDto
{
    [MapFrom("CompanyName", Reversible = true)]
    public string Name { get; set; } = string.Empty;
}

[Facet(typeof(Employee),
    NestedFacets = [typeof(CompanyDto)],
    GenerateToSource = true)]
public partial class EmployeeDto;
```

### Combining with Custom Configuration

MapFrom mappings are applied first, then your custom mapper runs:

```csharp
public class UserMapper : IFacetMapConfiguration<User, UserDto>
{
    public static void Map(User source, UserDto target)
    {
        // This runs AFTER MapFrom mappings are applied
        target.FullName = $"{target.Name} {target.FamilyName}";
    }
}

[Facet(typeof(User), Configuration = typeof(UserMapper), GenerateToSource = true)]
public partial class UserDto
{
    [MapFrom("FirstName", Reversible = true)]
    public string Name { get; set; } = string.Empty;

    [MapFrom("LastName", Reversible = true)]
    public string FamilyName { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;
}
```

## When to Use MapFrom vs Custom Configuration

| Scenario | MapFrom | Custom Config |
|----------|---------|---------------|
| Simple property rename | :white_check_mark: Best choice | Overkill |
| Multiple renames | :white_check_mark: Best choice | Overkill |
| Computed values (e.g., concatenation) | :x: | :white_check_mark: Required |
| Async operations | :x: | :white_check_mark: Required |
| Complex transformations | :x: | :white_check_mark: Required |
| Type conversions | :x: | :white_check_mark: Required |
| Conditional logic | :x: | :white_check_mark: Required |

## Best Practices

1. **Use for simple renames only** - For computed values or transformations, use custom mapping
2. **Set Reversible = false for display properties** - If the DTO property shouldn't update the source
3. **Consider projection compatibility** - Set IncludeInProjection = false for complex client-side logic
4. **Combine with custom mappers when needed** - MapFrom handles the basics, custom mapper handles the rest

## Limitations

- **Simple property access only** - Cannot use expressions like `"FirstName + LastName"`
- **Same type required** - Source and target property types must match
- **No nested path support** - Cannot use `"Company.Name"` (use nested facets instead)

For complex scenarios, use [Custom Mapping](04_CustomMapping.md) instead.
