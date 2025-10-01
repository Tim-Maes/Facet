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
| `IncludeFields`                | `bool`    | Include public fields from the source type (default: false for include mode, false for exclude mode). |
| `GenerateConstructor`          | `bool`    | Generate a constructor that copies values from the source (default: true).   |
| `GenerateParameterlessConstructor` | `bool` | Generate a parameterless constructor for testing and initialization (default: true). |
| `Configuration`                | `Type?`   | Custom mapping config type (see [Custom Mapping](04_CustomMapping.md)).      |
| `GenerateProjection`           | `bool`    | Generate a static LINQ projection (default: true).                          |
| `GenerateBackTo`               | `bool`    | Generate a method to map back from facet to source type (default: true).    |
| `Kind`                         | `FacetKind`| Output type: Class, Record, Struct, RecordStruct, Auto (default: Auto).     |
| `PreserveInitOnlyProperties`   | `bool`    | Preserve init-only modifiers from source properties (default: true for records). |
| `PreserveRequiredProperties`   | `bool`    | Preserve required modifiers from source properties (default: true for records). |
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
[Facet(typeof(User), Include = new[] { "FirstName", "LastName" }, Kind = FacetKind.Record)]
public partial record UserNameRecord;
```

### Traditional Exclude Usage
```csharp
// Exclude sensitive properties (original behavior)
[Facet(typeof(User), exclude: nameof(User.Password), Kind = FacetKind.Record)]
public partial record UserDto;
```

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

---

See [Custom Mapping](04_CustomMapping.md) for advanced scenarios.
