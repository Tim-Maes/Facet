# Global Configuration Defaults

## Overview

Facet allows you to override the default values for attribute properties globally using MSBuild properties. This is useful when you want to apply consistent settings across all your facets without having to specify the same property on every attribute.

## Use Case

When working with mapping libraries like [Mapperly](https://mapperly.riok.app/), you may need to disable certain Facet features (like constructor generation) to avoid conflicts. Instead of setting `GenerateConstructor = false` on every facet, you can configure this globally.

## Supported Configuration Properties

The following properties can be configured globally for the `[Facet]` attribute:

| Property | Default | MSBuild Property |
|----------|---------|------------------|
| `GenerateConstructor` | `true` | `Facet_GenerateConstructor` |
| `GenerateParameterlessConstructor` | `true` | `Facet_GenerateParameterlessConstructor` |
| `GenerateProjection` | `true` | `Facet_GenerateProjection` |
| `GenerateToSource` | `false` | `Facet_GenerateToSource` |
| `IncludeFields` | `false` | `Facet_IncludeFields` |
| `ChainToParameterlessConstructor` | `false` | `Facet_ChainToParameterlessConstructor` |
| `NullableProperties` | `false` | `Facet_NullableProperties` |
| `CopyAttributes` | `false` | `Facet_CopyAttributes` |
| `UseFullName` | `false` | `Facet_UseFullName` |
| `GenerateCopyConstructor` | `false` | `Facet_GenerateCopyConstructor` |
| `GenerateEquality` | `false` | `Facet_GenerateEquality` |
| `MaxDepth` | `10` | `Facet_MaxDepth` |
| `PreserveReferences` | `true` | `Facet_PreserveReferences` |

## How to Configure

### Option 1: Project File (.csproj)

Add MSBuild properties to your `.csproj` file:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>

    <!-- Override Facet defaults -->
    <Facet_GenerateConstructor>false</Facet_GenerateConstructor>
    <Facet_GenerateProjection>true</Facet_GenerateProjection>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Facet" Version="5.7.0" />
  </ItemGroup>
</Project>
```

### Option 2: Directory.Build.props

For multi-project solutions, create a `Directory.Build.props` file at the solution root:

```xml
<Project>
  <PropertyGroup>
    <!-- Override Facet defaults for all projects -->
    <Facet_GenerateConstructor>false</Facet_GenerateConstructor>
    <Facet_GenerateParameterlessConstructor>true</Facet_GenerateParameterlessConstructor>
  </PropertyGroup>
</Project>
```

This approach applies the settings to all projects in the solution directory tree.

## Example: Using with Mapperly

When using Facet alongside [Mapperly](https://mapperly.riok.app/) for mapping, you may want to disable Facet's constructor generation to let Mapperly handle the mapping logic:

**Directory.Build.props:**
```xml
<Project>
  <PropertyGroup>
    <!-- Disable constructor generation for Mapperly compatibility -->
    <Facet_GenerateConstructor>false</Facet_GenerateConstructor>
  </PropertyGroup>
</Project>
```

**Your code:**
```csharp
// Domain model
public class User
{
    public int Id { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string Email { get; set; }
}

// Facet DTO - constructor generation is disabled globally
[Facet(typeof(User))]
public partial class UserDto;

// Mapperly mapper
[Mapper]
public partial class UserMapper
{
    public partial UserDto MapToDto(User user);
}
```

Now all facets will have `GenerateConstructor = false` by default, unless explicitly overridden on the attribute.

## Overriding Global Defaults

You can still override the global default on individual facets by explicitly setting the property:

```csharp
// This facet will generate a constructor despite the global setting
[Facet(typeof(User), GenerateConstructor = true)]
public partial class UserDtoWithConstructor;

// This facet uses the global default (no constructor)
[Facet(typeof(User))]
public partial class UserDtoWithoutConstructor;
```

## Precedence

The configuration precedence from highest to lowest is:

1. **Explicit attribute property** > Set directly on the `[Facet]` attribute
2. **Global MSBuild property** > Set via `Facet_PropertyName` in .csproj or Directory.Build.props
3. **Hardcoded default** > The default value defined in the Facet library

## Example Scenarios

### Scenario 1: All Facets as Query DTOs

Make all facets nullable by default for query/filter scenarios:

```xml
<PropertyGroup>
  <Facet_NullableProperties>true</Facet_NullableProperties>
</PropertyGroup>
```

### Scenario 2: Disable All Code Generation Except Properties

```xml
<PropertyGroup>
  <Facet_GenerateConstructor>false</Facet_GenerateConstructor>
  <Facet_GenerateParameterlessConstructor>false</Facet_GenerateParameterlessConstructor>
  <Facet_GenerateProjection>false</Facet_GenerateProjection>
</PropertyGroup>
```

### Scenario 3: Enable Full Name Generation

Avoid naming conflicts by using full type names for all generated files:

```xml
<PropertyGroup>
  <Facet_UseFullName>true</Facet_UseFullName>
</PropertyGroup>
```

### Scenario 4: Copy All Attributes by Default

Preserve validation and serialization attributes from source types:

```xml
<PropertyGroup>
  <Facet_CopyAttributes>true</Facet_CopyAttributes>
</PropertyGroup>
```

## Notes

- Global defaults are read at build time from MSBuild properties
- Changes to global configuration require a rebuild to take effect
- These settings only affect the `[Facet]` attribute (support for `[Wrapper]` and `[GenerateDtos]` may be added in future versions)
- Boolean values should be `true` or `false` (case-insensitive)
- Numeric values (like `MaxDepth`) should be valid integers

## Related Documentation

- [Attribute Reference](03_AttributeReference.md) - Complete list of attribute properties
- [GenerateDtosAttribute](09_GenerateDtosAttribute.md) - DTO generation
- [WrapperAttribute](14_WrapperAttribute.md) - Wrapper generation
