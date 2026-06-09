# FacetMap: Extension Method Mapping for Cross-Assembly DTOs

## Problem

In DDD architectures with clear layer separation (e.g., SignalR hubs separating frontend from backend), users need DTOs that live in a shared/contracts project referenced by both sides. Currently, Facet generates DTOs as partial classes with instance members (constructors, `ToSource()`, `Projection`), which requires the DTO and entity to be in the same compilation unit. When the same DTO shape exists in two different assemblies, C# treats them as incompatible types, causing cast errors.

## Solution

A new `[FacetMap]` attribute that generates **extension methods** for mapping between a source entity and an externally-defined DTO type. The DTO can be a plain POCO in a shared/contracts assembly. The extension methods are generated in the project where the entity lives.

## Architecture

```
SharedContracts.csproj              (no Facet dependency)
  └── UserDto.cs                    (plain POCO with properties)

Domain.csproj                       (references SharedContracts + Facet)
  └── User.cs                       (entity)
  └── UserMappings.cs               (marker class with [FacetMap])
      → Generated: UserMappings.Extensions.g.cs
        - static UserDto ToUserDto(this User source)
        - static User ToUser(this UserDto dto)
        - static Expression<Func<User, UserDto>> ProjectionToUserDto (property on static class)
```

Both frontend and backend reference SharedContracts. Only the domain project references Facet.

## Deliverables

### 1. New Attribute: `FacetMapAttribute`

Location: `src/Facet.Attributes/FacetMapAttribute.cs`

```csharp
namespace Facet;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class FacetMapAttribute : Attribute
{
    public Type SourceType { get; }
    public Type TargetType { get; }
    public string[] Exclude { get; }
    public string[]? Include { get; set; }
    public bool GenerateToSource { get; set; } = false;
    public bool GenerateProjection { get; set; } = true;
    public Type? Configuration { get; set; }
    public Type? ToSourceConfiguration { get; set; }

    public FacetMapAttribute(Type sourceType, Type targetType, params string[] exclude)
    {
        SourceType = sourceType;
        TargetType = targetType;
        Exclude = exclude;
    }
}
```

Usage:
```csharp
[FacetMap(typeof(User), typeof(UserDto), GenerateToSource = true)]
public static partial class UserMappings { }
```

### 2. New Generator: `FacetMapGenerator`

Location: `src/Facet/Generators/FacetMapGenerators/FacetMapGenerator.cs`

A new `IIncrementalGenerator` that:
1. Finds types decorated with `[FacetMap]`
2. Validates that the marker class is `static partial`
3. Resolves source type members and target type members
4. Matches properties by name and type (same logic as the existing `[Facet]` generator)
5. Generates extension methods on the static class

### 3. Generated Code Shape

For:
```csharp
[FacetMap(typeof(User), typeof(UserDto), "Password", "CreatedAt", GenerateToSource = true)]
public static partial class UserMappings { }
```

Generates:
```csharp
public static partial class UserMappings
{
    public static UserDto ToUserDto(this User source)
    {
        var target = new UserDto();
        target.Id = source.Id;
        target.FirstName = source.FirstName;
        target.LastName = source.LastName;
        target.Email = source.Email;
        return target;
    }

    public static User ToUser(this UserDto source)
    {
        var target = new User();
        target.Id = source.Id;
        target.FirstName = source.FirstName;
        target.LastName = source.LastName;
        target.Email = source.Email;
        return target;
    }

    public static Expression<Func<User, UserDto>> ProjectionToUserDto =>
        source => new UserDto
        {
            Id = source.Id,
            FirstName = source.FirstName,
            LastName = source.LastName,
            Email = source.Email
        };
}
```

### 4. Feature Parity with Instance Methods

The extension methods must support the same capabilities as the existing `[Facet]` instance methods:

- Property matching by name (include/exclude)
- Nested object mapping (when both source and target have matching complex types)
- Collection mapping (List, IList, ICollection, etc.)
- Nullable property handling
- Enum conversion (string/int) via attribute option
- Custom mapping configuration (static `Map(source, target)` method call)
- Custom ToSource configuration
- Projection expression generation for LINQ/EF Core
- MaxDepth for nested recursion prevention
- CollectionTargetType remapping
- MapFrom attribute support on target DTO properties (the generator reads attributes from the target type in the external assembly)
- MapWhen attribute support on target DTO properties

### 5. Property Resolution Strategy

Since the DTO is in an external assembly:
1. The generator inspects the target type's properties via Roslyn symbols (works across assembly boundaries)
2. The generator inspects the source type's properties (same project or referenced)
3. Matches are made by property name (case-sensitive)
4. `[MapFrom]` and `[MapWhen]` attributes on the target DTO properties are read and honored
5. Exclude/Include parameters filter which properties to map

### 6. Naming Conventions

Method names are derived from the target type name:
- Source-to-Target: `To{TargetTypeName}` (e.g., `ToUserDto`)
- Target-to-Source: `To{SourceTypeName}` (e.g., `ToUser`)
- Projection: `ProjectionTo{TargetTypeName}` (e.g., `ProjectionToUserDto`)

For multi-source (multiple `[FacetMap]` on same class):
- `To{TargetTypeName}` per unique target
- `To{SourceTypeName}` for reverse per unique source
- `ProjectionTo{TargetTypeName}From{SourceTypeName}` if ambiguous

### 7. File Structure

New files:
```
src/Facet.Attributes/
  └── FacetMapAttribute.cs

src/Facet/Generators/FacetMapGenerators/
  ├── FacetMapGenerator.cs          (IIncrementalGenerator entry point)
  ├── FacetMapModelBuilder.cs       (builds model from attribute data)
  ├── FacetMapCodeBuilder.cs        (orchestrates code generation)
  ├── FacetMapExtensionGenerator.cs (generates extension method bodies)
  └── FacetMapProjectionGenerator.cs (generates projection expressions)

test/Facet.Tests/
  └── TestModels/
      ├── FacetMapTestEntities.cs   (source entities for FacetMap tests)
      └── FacetMapTestDtos.cs       (external-style DTOs, plain POCOs)
  └── UnitTests/Core/FacetMap/
      ├── BasicFacetMapTests.cs
      ├── FacetMapToSourceTests.cs
      ├── FacetMapProjectionTests.cs
      ├── FacetMapNestedObjectTests.cs
      ├── FacetMapCollectionTests.cs
      └── FacetMapCustomConfigTests.cs

docs/
  └── 22_FacetMapAttribute.md
```

### 8. Validation and Diagnostics

New analyzer diagnostics (prefix `FACET1xx` for the new generator):
- `FACET100`: Target class must be `static partial`
- `FACET101`: SourceType and TargetType cannot be the same
- `FACET102`: TargetType must have a parameterless constructor (for object initializer mapping)
- `FACET103`: No matching properties found between source and target
- `FACET104`: Include/Exclude property not found on source type

### 9. What This Does NOT Do

- Does NOT modify the existing `[Facet]` attribute or generator in any way
- Does NOT generate property declarations (the target DTO already has its properties)
- Does NOT require the target type to be `partial`
- Does NOT require the target type to reference Facet
- Does NOT change any existing generated output

### 10. Implementation Order

1. Create `FacetMapAttribute` in `Facet.Attributes`
2. Create `FacetMapGenerator` scaffold (IIncrementalGenerator, model builder)
3. Implement property resolution and matching logic (reuse from existing `TypeAnalyzer`/`AttributeProcessor` where possible)
4. Implement extension method code generation (source-to-target direction)
5. Implement ToSource extension method generation (target-to-source direction)
6. Implement projection expression generation
7. Add nested object and collection support
8. Add custom configuration support
9. Add diagnostics/validation
10. Write unit tests for all scenarios
11. Write documentation (docs/22_FacetMapAttribute.md)
12. Verify no regressions in existing tests

### 11. Reusable Components

These existing components can be reused or adapted:
- `TypeAnalyzer` for resolving property members from type symbols
- `ExpressionHelper` for building projection member init expressions
- `CodeGenerationHelpers` for namespace collection, type name formatting
- `NullabilityAnalyzer` for nullable reference type handling
- `NameOfResolver` for resolving MapFrom expressions
- `GeneratorUtilities` for shared emit helpers

### 12. Global Configuration

The new generator should respect applicable global MSBuild properties:
- `Facet_SplitGeneratedFiles` (not applicable here, single file per marker class)
- `Facet_GenerateToSource` (as default for `GenerateToSource`)
- `Facet_GenerateProjection` (as default for `GenerateProjection`)
- `Facet_MaxDepth` (as default for nested depth)

### 13. Documentation Requirements

Create `docs/22_FacetMapAttribute.md` covering:
- Problem statement (DDD cross-assembly scenarios)
- When to use `[FacetMap]` vs `[Facet]`
- Setup (project structure, references)
- Basic usage with code examples
- Advanced scenarios (nested, collections, custom config)
- Generated code examples
- Comparison table: `[Facet]` vs `[FacetMap]`

Update `README.md`:
- Add `[FacetMap]` to the features list
- Add a brief mention in the Quick Start or Advanced Features section
