# Facet Attribute Reference

The `[Facet]` attribute is used to declare a new projection (facet) type based on an existing source type.

## Usage

```csharp
[Facet(typeof(SourceType), exclude: "Property1", "Property2")]
public partial class MyFacet { }
```

## Parameters

| Parameter            | Type      | Description                                                                 |
|----------------------|-----------|-----------------------------------------------------------------------------|
| `sourceType`         | `Type`    | The type to project from (required).                                        |
| `exclude`            | `string[]`| Names of properties/fields to exclude from the generated type (optional).   |
| `IncludeFields`      | `bool`    | Include public fields from the source type (default: false).                |
| `GenerateConstructor`| `bool`    | Generate a constructor that copies values from the source (default: true).   |
| `Configuration`      | `Type?`   | Custom mapping config type (see [Custom Mapping](04_CustomMapping.md)).      |
| `GenerateProjection` | `bool`    | Generate a static LINQ projection (default: true).                          |
| `Kind`               | `FacetKind`| Output type: Class, Record, Struct, RecordStruct (default: Class).          |
| `UseFullName`        | `bool`    | Use full type name in generated file names to avoid collisions (default: false). |

## Example

```csharp
[Facet(typeof(User), exclude: nameof(User.Password), Kind = FacetKind.Record)]
public partial record UserDto;
```

---

See [Custom Mapping](04_CustomMapping.md) for advanced scenarios.
