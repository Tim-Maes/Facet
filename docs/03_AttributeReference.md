# Facet Attribute Reference

The `[Facet]` attribute is used to declare a new projection (facet) type based on an existing source type.

## Usage

### Exclude Mode (Default)
```csharp
[Facet(typeof(SourceType), exclude: "Property1", "Property2")]
public partial class MyFacet { }
```

### Include Mode (New)
```csharp
[Facet(typeof(SourceType), Include = new[] { "Property1", "Property2" })]
public partial class MyFacet { }
```

## Parameters

| Parameter                      | Type      | Description                                                                 |
|--------------------------------|-----------|-----------------------------------------------------------------------------|
| `sourceType`                   | `Type`    | The type to project from (required).                                        |
| `exclude`                      | `string[]`| Names of properties/fields to exclude from the generated type (optional).   |
| `Include`                      | `string[]`| Names of properties/fields to include in the generated type (optional). Mutually exclusive with `exclude`. |
| `NestedFacets`                 | `Type[]?` | Array of nested facet types to automatically map nested objects (default: null). |
| `IncludeFields`                | `bool`    | Include public fields from the source type (default: false for include mode, false for exclude mode). |
| `GenerateConstructor`          | `bool`    | Generate a constructor that copies values from the source (default: true).   |
| `GenerateParameterlessConstructor` | `bool` | Generate a parameterless constructor for testing and initialization (default: true). |
| `Configuration`                | `Type?`   | Custom mapping config type (see [Custom Mapping](04_CustomMapping.md)).      |
| `GenerateProjection`           | `bool`    | Generate a static LINQ projection (default: true).                          |
| `GenerateBackTo`               | `bool`    | Generate a method to map back from facet to source type (default: true).    |
| `PreserveInitOnlyProperties`   | `bool`    | Preserve init-only modifiers from source properties (default: true for records). |
| `PreserveRequiredProperties`   | `bool`    | Preserve required modifiers from source properties (default: true for records). |
| `NullableProperties`           | `bool`    | Make all properties nullable in the generated facet (default: false). |
| `CopyAttributes`               | `bool`    | Copy attributes from source type members to generated facet members (default: false). See [Attribute Copying](#attribute-copying) below. |
| `UseFullName`                  | `bool`    | Use full type name in generated file names to avoid collisions (default: false). |

## Include vs Exclude

The `Include` and `Exclude` parameters are mutually exclusive:

- **Exclude Mode**: Include all properties except those listed in `exclude` (default behavior)
- **Include Mode**: Only include properties listed in `Include` array

### Include Mode Behavior

When using `Include` mode:
- Only the properties specified in the `Include` array are copied to the facet
- `IncludeFields` defaults to `false` (disabled by default for include mode)
- All other properties from the source type are excluded
- Works with inheritance - you can include properties from base classes

## Examples

### Basic Include Usage
```csharp
// Only include FirstName, LastName, and Email
[Facet(typeof(User), Include = new[] { "FirstName", "LastName", "Email" })]
public partial class UserContactDto;
```

### Single Property Include
```csharp
// Only include the Name property
[Facet(typeof(Product), Include = new[] { "Name" })]
public partial class ProductNameDto;
```

### Include with Custom Properties
```csharp
// Include specific properties and add custom ones
[Facet(typeof(User), Include = new[] { "FirstName", "LastName" })]
public partial class UserSummaryDto
{
    public string FullName { get; set; } = string.Empty; // Custom property
}
```

### Include with Fields
```csharp
// Include fields as well as properties
[Facet(typeof(EntityWithFields), Include = new[] { "Name", "Age" }, IncludeFields = true)]
public partial class EntityDto;
```

### Include with Records
```csharp
// Generate a record type with only specific properties
[Facet(typeof(User), Include = new[] { "FirstName", "LastName" })]
public partial record UserNameRecord;
```

### Traditional Exclude Usage
```csharp
// Exclude sensitive properties (original behavior)
[Facet(typeof(User), exclude: nameof(User.Password))]
public partial record UserDto;
```

### Nullable Properties for Query Models
```csharp
// Make all properties nullable for query/filter scenarios
[Facet(typeof(Product), "InternalNotes", NullableProperties = true, GenerateBackTo = false)]
public partial class ProductQueryDto;

// Usage: All fields are optional for filtering
var query = new ProductQueryDto
{
    Name = "Widget",
    Price = 50.00m
    // Other fields remain null
};
```

**Note:** When using `NullableProperties = true`, it's recommended to set `GenerateBackTo = false` since mapping nullable properties back to non-nullable source properties is not logically sound.

### Nested Facets for Composing DTOs
```csharp
// Define facets for nested types
[Facet(typeof(Address))]
public partial record AddressDto;

[Facet(typeof(Company), NestedFacets = [typeof(AddressDto)])]
public partial record CompanyDto;

[Facet(typeof(Employee),
    exclude: ["PasswordHash", "Salary"],
    NestedFacets = [typeof(CompanyDto), typeof(AddressDto)])]
public partial record EmployeeDto;

// Usage - automatically handles nested mapping
var employee = new Employee
{
    FirstName = "John",
    Company = new Company
    {
        Name = "Acme Corp",
        HeadquartersAddress = new Address { City = "San Francisco" }
    },
    HomeAddress = new Address { City = "Oakland" }
};

var employeeDto = new EmployeeDto(employee);
// employeeDto.Company is CompanyDto
// employeeDto.Company.HeadquartersAddress is AddressDto
// employeeDto.HomeAddress is AddressDto

// BackTo also handles nested types automatically
var mappedEmployee = employeeDto.BackTo();
// All nested objects are properly reconstructed
```

**How NestedFacets Works:**
- The generator automatically detects which properties in your source type match the source types of the nested facets
- For each match, it replaces the property type with the nested facet type
- Constructors automatically call `new NestedFacetType(source.Property)` for nested properties
- Projections work seamlessly for EF Core queries through constructor chaining
- BackTo methods call `.BackTo()` on nested facets to reconstruct the original type hierarchy

**Benefits:**
- No manual property declarations for nested types
- Automatic mapping in constructors, projections, and BackTo methods
- Works with multiple levels of nesting
- Supports multiple nested facets on the same parent type

## When to Use Include vs Exclude

### Use **Include** when:
- You want a facet with only a few specific properties from a large source type
- Creating focused DTOs (e.g., summary views, contact info only)
- Building API response models that should only expose certain fields
- Creating search result DTOs with minimal data

### Use **Exclude** when:
- You want most properties but need to hide a few sensitive ones
- The majority of the source type should be included in the facet
- Following the original Facet pattern for backward compatibility

### Use **NullableProperties** when:
- Creating query/filter DTOs where all search criteria are optional
- Building patch/update models where only changed fields are provided
- Implementing flexible API request models that support partial data
- Generating DTOs similar to the Query DTOs in `GenerateDtos`

**Important considerations:**
- Value types (int, bool, DateTime, enums) become nullable (int?, bool?, etc.)
- Reference types (string, objects) remain reference types but are marked nullable
- Disable `GenerateBackTo` to avoid mapping issues from nullable to non-nullable types

## Attribute Copying

The `CopyAttributes` parameter allows you to copy attributes from the source type's members to the generated facet members. This is particularly useful for preserving data validation attributes when creating DTOs for API models.

### Usage

```csharp
public class User
{
    public int Id { get; set; }

    [Required]
    [StringLength(50)]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Range(0, 150)]
    public int Age { get; set; }

    public string Password { get; set; } = string.Empty;
}

[Facet(typeof(User), "Password", CopyAttributes = true)]
public partial class UserDto;
```

The generated `UserDto` will include all the validation attributes:

```csharp
public partial class UserDto
{
    public int Id { get; set; }

    [Required]
    [StringLength(50)]
    public string FirstName { get; set; }

    [Required]
    [EmailAddress]
    public string Email { get; set; }

    [Range(0, 150)]
    public int Age { get; set; }
}
```

### What Gets Copied

The attribute copying feature intelligently filters attributes to copy only those that make sense on the target:

**Commonly copied attributes include:**
- Data validation attributes: `Required`, `StringLength`, `Range`, `EmailAddress`, `Phone`, `Url`, `RegularExpression`, `CreditCard`, etc.
- Display attributes: `Display`, `DisplayName`, `Description`
- JSON serialization attributes: `JsonPropertyName`, `JsonIgnore`, etc.
- Custom validation attributes that inherit from `ValidationAttribute`

**Automatically excluded attributes:**
- Internal compiler-generated attributes (e.g., `System.Runtime.CompilerServices.*`)
- The base `ValidationAttribute` class itself (only derived validation attributes are copied)
- Attributes that are not valid for the target member type based on `AttributeUsage`

### Attribute Parameters

All attribute parameters are preserved with correct C# syntax:

```csharp
public class Product
{
    [Required]
    [StringLength(100, MinimumLength = 3, ErrorMessage = "Name must be 3-100 characters")]
    public string Name { get; set; } = string.Empty;

    [Range(0.01, 10000.00)]
    public decimal Price { get; set; }

    [RegularExpression(@"^[A-Z]{3}-\d{4}$", ErrorMessage = "Invalid SKU format")]
    public string Sku { get; set; } = string.Empty;
}

[Facet(typeof(Product), CopyAttributes = true)]
public partial class ProductDto;
```

All parameters including named parameters, string literals with escape sequences, and numeric values are correctly preserved.

### With Nested Facets

`CopyAttributes` works seamlessly with `NestedFacets`:

```csharp
[Facet(typeof(Address), CopyAttributes = true)]
public partial class AddressDto;

[Facet(typeof(Order), "InternalNotes", CopyAttributes = true, NestedFacets = [typeof(AddressDto)])]
public partial class OrderDto;
```

Both the parent and nested facets will have their attributes copied from their respective source types.

### When to Use CopyAttributes

**Use `CopyAttributes = true` when:**
- Creating API request/response DTOs that need validation
- Building DTOs for ASP.NET Core model validation
- Preserving display metadata for UI frameworks
- Maintaining JSON serialization attributes
- You want consistent validation between your domain models and DTOs

**Don't use it when:**
- You want different validation rules for your DTOs
- Your source types have attributes specific to their domain concerns (e.g., ORM mapping attributes)
- You prefer to define validation attributes directly on the facet

### Default Behavior

By default, `CopyAttributes = false`, meaning no attributes are copied. This maintains backward compatibility and gives you explicit control over when attributes should be copied.

---

See [Custom Mapping](04_CustomMapping.md) for advanced scenarios.
