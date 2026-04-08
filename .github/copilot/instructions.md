# Facet — Deep Context for GitHub Copilot

## What Is Facet?

Facet is a **C# Roslyn incremental source generator** that eliminates DTO boilerplate at **compile time with zero runtime overhead**. The core metaphor is a gem with many facets: one domain model (e.g. `User`) can have many views (public API, admin, query, patch), each called a "facet". Instead of hand-writing every DTO, constructor, LINQ projection, and reverse mapper, you annotate a `partial` class and Facet generates everything.

### Problem It Solves

Without Facet, teams write enormous amounts of repetitive DTO code:
- Multiple DTO classes per entity (Create, Update, Response, Query, Patch…)
- Hand-written constructors that copy properties one-by-one
- LINQ `.Select(x => new Dto { … })` projections that drift from constructors
- AutoMapper / Mapster configurations that are invisible to the compiler and slow

Facet replaces all of that with a single attribute. Everything is generated at compile time as readable C# source, visible in the IDE and fully debuggable.

### Supported Target Types
Generated types can be: `class`, `record`, `struct`, `record struct`.

### Supported .NET Versions
.NET 8, .NET 9, .NET 10.

---

## Package Ecosystem

| Package | Purpose |
|---|---|
| `Facet` | Core: source generator + Roslyn analyzers + attributes |
| `Facet.Attributes` | Just the attribute types (when you only want to reference them without the generator) |
| `Facet.Extensions` | Provider-agnostic LINQ extension methods (`ToFacet`, `SelectFacet`, `ToSource`, etc.) |
| `Facet.Mapping` | Interfaces for custom static/async mapping configurations |
| `Facet.Mapping.Expressions` | Expression tree transformation — remap predicates/selectors from entity to DTO types |
| `Facet.Extensions.EFCore` | EF Core async variants (`ToFacetsAsync`, `FirstFacetAsync`, `UpdateFromFacet`, etc.) |
| `Facet.Extensions.EFCore.Mapping` | DI-resolved async mapper support for EF Core |
| `Facet.Dashboard` | Visual dashboard to explore generated facets |

---

## Core Attributes

### `[Facet(typeof(TSource), ...)]`

The primary attribute. Placed on a `partial` class/record/struct. The generator reads the source type's properties/fields and emits a populated partial type.

**Key attribute properties:**

| Property | Default | Effect |
|---|---|---|
| `Exclude` (constructor params) | `[]` | Property names to skip |
| `Include` | `null` | Whitelist — only these properties |
| `IncludeFields` | `false` | Also map public fields |
| `GenerateConstructor` | `true` | `new MyDto(sourceInstance)` constructor |
| `GenerateParameterlessConstructor` | `true` | `new MyDto()` parameterless ctor |
| `ChainToParameterlessConstructor` | `false` | Generated ctor calls `: this()` first |
| `GenerateProjection` | `true` | `static Expression<Func<TSource,TDto>> Projection` |
| `GenerateToSource` | `false` | `ToSource()` reverse mapper |
| `Configuration` | `null` | Type with `static void Map(TSource, TTarget)` for custom logic |
| `BeforeMapConfiguration` | `null` | `static void BeforeMap(TSource, TTarget)` hook called before auto-mapping |
| `AfterMapConfiguration` | `null` | `static void AfterMap(TSource, TTarget)` hook called after auto-mapping |
| `NestedFacets` | `null` | Array of DTO types to use for nested object properties |
| `FlattenTo` | `null` | Types to generate `FlattenTo()` methods that unpack collections |
| `NullableProperties` | `false` | Make all properties nullable (useful for patch/query DTOs) |
| `CopyAttributes` | `false` | Copy validation/data annotation attributes from source |
| `PreserveInitOnlyProperties` | `false` (true for records) | Keep `init` modifier |
| `PreserveRequiredProperties` | `false` (true for records) | Keep `required` modifier |
| `MaxDepth` | `10` | Max nesting depth for recursive nested-facet construction |
| `PreserveReferences` | `true` | Track visited objects to prevent circular-ref stack overflows |
| `ConvertEnumsTo` | `null` | `typeof(string)` or `typeof(int)` — convert enum properties |
| `GenerateCopyConstructor` | `false` | `MyDto(MyDto other)` copy constructor for cloning/MVVM |
| `GenerateEquality` | `false` | Value-equality (`Equals`, `GetHashCode`, `==`, `!=`, `IEquatable<T>`) |
| `SourceSignature` | `null` | 8-char hash; analyzer warns (FAC022) when source structure drifts |
| `UseFullName` | `false` | Use fully-qualified name for generated file to avoid collisions |

**Mutually exclusive**: `Exclude` and `Include` cannot both be set (FAC009 error).

**Multiple attributes**: `[Facet]` allows `AllowMultiple = true`, so one type can have several `[Facet]` attributes for different source types.

---

### `[Flatten(typeof(TSource))]`

Auto-generates a flat (no-nested-objects) projection by traversing the source type's object graph and bringing all primitive/value-type/string properties to the top level.

**Key properties:**
- `MaxDepth` (default 3) — how deep to traverse
- `NamingStrategy`: `Prefix` (default, e.g. `AddressStreet`), `LeafOnly` (e.g. `Street`, collision risk), `SmartLeaf` (leaf name, prefix parent only on collision)
- `Exclude` — dot-notation paths to skip, e.g. `"Address.Country"`
- `IgnoreNestedIds` — skip nested `Id` / `*Id` FK properties
- `IgnoreForeignKeyClashes` — skip `Address.Id` when `AddressId` already exists as FK
- `IncludeCollections` — include collection properties as-is (default false)
- `GenerateProjection` / `GenerateParameterlessConstructor`

---

### `[Wrapper(typeof(TSource))]`

Generates a **reference-based delegate** wrapper (not a value copy). Property getters/setters forward to the wrapped source instance. Useful for: decorator pattern, facade, ViewModel layers, memory efficiency with large graphs.

**Key differences from `[Facet]`:**
- Does not copy values — delegates to the source reference
- `ReadOnly = true` generates getter-only forwarding properties
- `NestedWrappers` for nested delegation

---

### `[GenerateDtos(Types = DtoTypes.All)]`

Applied directly to the **domain class** (not a separate DTO). Generates a full CRUD DTO set as separate types.

**`DtoTypes` flags**: `Create`, `Update`, `Response`, `Query`, `Upsert`, `Patch`, `All`

**Generated names** (example for `Product`): `ProductCreateDto`, `ProductUpdateDto`, `ProductResponseDto`, `ProductQueryDto`, `ProductUpsertDto`, `ProductPatchDto`

**Key properties**: `OutputType` (Class/Record/Struct/RecordStruct), `Namespace`, `ExcludeProperties`, `ExcludeAuditFields` (auto-excludes CreatedAt, UpdatedAt, etc.), `Prefix`, `Suffix`, `GenerateConstructors`, `GenerateProjections`

---

### `[MapFrom("SourceProperty")]` — on a property

Declarative property rename or computed mapping. Applied to a property inside the partial DTO class.

- `Source` — property name, nested path (`"Company.Name"`), or C# expression (`"FirstName + \" \" + LastName"`)
- `Reversible` (default false) — include in `ToSource()` reverse mapper
- `IncludeInProjection` (default true) — include in the generated LINQ expression (set false for non-SQL-translatable expressions)

---

### `[MapWhen("Condition")]` — on a property

Conditional property mapping. The property is only mapped when the condition evaluates to true; otherwise, the property gets its default value (or `Default` if specified).

- Supports `==`, `!=`, `<`, `>`, `<=`, `>=`, `&&`, `||`, `!`, `??`, `?.`
- Multiple `[MapWhen]` on the same property are AND-combined
- `IncludeInProjection` — set false if condition can't be translated to SQL

---

## What Gets Generated (for `[Facet]`)

For a `partial class UserDto` with `[Facet(typeof(User))]`:

1. **Properties** — one per included source member, with type, nullability, `init`, `required` as configured
2. **Source constructor** — `public UserDto(User source)` that copies all members. Handles: nested facets, collections (`List<T>`, arrays, `IEnumerable<T>`), enum conversions, `MapFrom`, `MapWhen`, `BeforeMap`/`AfterMap` hooks, depth tracking (`__depth`, `__processed` HashSet), custom `Configuration.Map()` call
3. **Parameterless constructor** — `public UserDto()` for object initializers and testing
4. **Copy constructor** (opt-in) — `public UserDto(UserDto other)`
5. **Static `Projection` property** — `public static Expression<Func<User, UserDto>> Projection { get; }` — a compile-time LINQ expression identical in semantics to the constructor, usable directly in EF Core `.Select(UserDto.Projection)`
6. **`ToSource()` method** (opt-in) — maps back to `User`
7. **`FlattenTo<T>()` method** (opt-in via `FlattenTo`) — unpacks a collection property into rows combining parent + child properties
8. **Value equality** (opt-in) — `Equals`, `GetHashCode`, `==`, `!=`, `IEquatable<T>`
9. **XML documentation** — propagated from source type and properties

---

## Generator Pipeline (Technical)

All generators implement **`IIncrementalGenerator`** (Roslyn incremental source generators):

```
SyntaxProvider.ForAttributeWithMetadataName(...)
  → predicate: node is TypeDeclarationSyntax
  → transform: ModelBuilder.BuildModel(ctx, globalConfig, token)
  → .Collect() all models (for nested facet lookup)
  → RegisterSourceOutput: CodeBuilder.Generate(model, facetLookup)
  → spc.AddSource("{FullName}.g.cs", ...)
```

### Key classes

| Class | Role |
|---|---|
| `FacetGenerator` | Roslyn entry point, wires up the incremental pipeline |
| `ModelBuilder` | Reads Roslyn symbols → builds `FacetTargetModel` (immutable, equatable, for incremental caching) |
| `AttributeParser` | Extracts attribute constructor args and named properties |
| `AttributeValidator` | Validates property names, types, config contracts |
| `AttributeProcessor` | Processes `[MapFrom]`, `[MapWhen]`, nested facets into `FacetMember` metadata |
| `CodeBuilder` | Orchestrates code emission for the full generated partial type |
| `MemberGenerator` | Emits property declarations |
| `ConstructorGenerator` | Emits source constructor (the main mapping logic) |
| `CopyConstructorGenerator` | Emits copy constructor |
| `ProjectionGenerator` | Emits the `Projection` expression tree |
| `ToSourceGenerator` | Emits `ToSource()` reverse mapper |
| `FlattenToGenerator` | Emits `FlattenTo<T>()` methods |
| `EqualityGenerator` | Emits `Equals`, `GetHashCode`, operators |
| `ExpressionBuilder` | Handles `MapFrom` expression strings in projections |
| `TypeAnalyzer` | Analyzes source member types (nullability, init-only, collections, enums, etc.) |
| `NullabilityAnalyzer` | Determines nullable context for each property |
| `GlobalConfigurationDefaults` | Reads MSBuild properties (`build_property.Facet_*`) to override attribute defaults project-wide |

### `FacetTargetModel` & `FacetMember`

These are immutable, fully equatable data models. Roslyn incremental generators require equatable models so the pipeline can short-circuit when inputs haven't changed. Both implement `IEquatable<T>` with complete structural equality.

`FacetMember` captures per-property metadata: type name, kind (Property/Field), nullability flags, `init`/`required`/`readonly`, XML docs, attributes to copy, `MapFrom` / `MapWhen` data, collection info, nested facet info, enum conversion info, default value expression.

### Global Configuration via MSBuild

Any `[Facet]` attribute default can be overridden project-wide in `.csproj`:
```xml
<PropertyGroup>
  <Facet_GenerateToSource>true</Facet_GenerateToSource>
  <Facet_NullableProperties>false</Facet_NullableProperties>
  <Facet_MaxDepth>5</Facet_MaxDepth>
  <!-- etc. -->
</PropertyGroup>
```
Keys: `Facet_GenerateConstructor`, `Facet_GenerateParameterlessConstructor`, `Facet_GenerateProjection`, `Facet_GenerateToSource`, `Facet_IncludeFields`, `Facet_ChainToParameterlessConstructor`, `Facet_NullableProperties`, `Facet_CopyAttributes`, `Facet_UseFullName`, `Facet_GenerateCopyConstructor`, `Facet_GenerateEquality`, `Facet_MaxDepth`, `Facet_PreserveReferences`

---

## Roslyn Analyzers (FAC001–FAC023)

17 comprehensive analyzers provide real-time feedback:

| ID | Severity | Rule |
|---|---|---|
| FAC001 | Error | Target type of extension method not annotated with `[Facet]` |
| FAC002 | Info | Use two-generic `ToFacet<TSource,TTarget>()` for better perf (avoids reflection) |
| FAC003 | Error | `[Facet]` type must be `partial` |
| FAC004 | Error | Property name in `Exclude`/`Include` doesn't exist in source type |
| FAC005 | Error | Source type can't be resolved |
| FAC006 | Error | Configuration type doesn't have required `static Map()` method |
| FAC007 | Warning | Type in `NestedFacets` isn't annotated with `[Facet]` |
| FAC008 | Warning | `MaxDepth=0` and `PreserveReferences=false` — circular ref risk |
| FAC009 | Error | Both `Include` and `Exclude` specified (mutually exclusive) |
| FAC010 | Warning | Unusual `MaxDepth` value (negative or >100) |
| FAC011 | Error | `[GenerateDtos]` on non-class type |
| FAC012 | Warning | `ExcludeProperties` name doesn't exist in source |
| FAC013 | Warning | `Types = DtoTypes.None` generates nothing |
| FAC014 | Error | `[Flatten]` type must be `partial` |
| FAC015 | Error | `[Flatten]` source type can't be resolved |
| FAC016 | Warning | Unusual `MaxDepth` in `[Flatten]` |
| FAC017 | Info | `LeafOnly` naming strategy may cause collisions |
| FAC022 | Warning | Source entity structure changed — update `SourceSignature` |
| FAC023 | Warning | `GenerateToSource=true` but `ToSource` can't be generated (no parameterless ctor or setters) |

A **code fix provider** exists for FAC022 (auto-updates `SourceSignature` hash).

---

## Extension Methods (`Facet.Extensions`)

**Object-to-facet mapping:**
- `source.ToFacet<TSource, TTarget>()` — typed, fast (cached delegate)
- `source.ToFacet<TTarget>()` — uses reflection to find source type from `[Facet]` attribute

**Collection mapping:**
- `IEnumerable<TSource>.ToFacets<TSource, TTarget>()`
- `IQueryable<TSource>.SelectFacet<TSource, TTarget>()` — returns `IQueryable<TTarget>` using the generated `Projection`

**Reverse mapping:**
- `dto.ToSource<TFacet, TSource>()`

All methods have two-generic (fast) and single-generic (uses reflection) variants.

---

## EF Core Extensions (`Facet.Extensions.EFCore`)

Async materializing variants:
- `query.ToFacetsAsync<TSource, TTarget>(ct)` → `Task<List<TTarget>>`
- `query.FirstFacetAsync<TSource, TTarget>(ct)` → `Task<TTarget?>`
- `query.SingleFacetAsync<TSource, TTarget>(ct)` → `Task<TTarget>`

Entity update utilities:
- `entity.UpdateFromFacet(dto, context)` — EF Core change-tracked update (only sets actually-changed properties)
- `entity.UpdateFromFacetAsync(dto, context, ct)`
- `entity.UpdateFromFacetWithChanges(dto, context)` → `FacetUpdateResult<TEntity>` (entity + list of changed property names)

---

## Expression Mapping (`Facet.Mapping.Expressions`)

`ExpressionMapper` transforms expression trees from the source entity type to the projected DTO type. Useful when you have a predicate or selector written against `User` and need to run it against `UserDto`.

- `PropertyPathMapper` builds a property-to-property mapping between source and target
- Cached per `(TSource, TTarget)` pair in a `ConcurrentDictionary`
- `FacetExpressionExtensions` provides fluent API

---

## Custom Mapping Patterns

### Static configuration (compile-time)
```csharp
public class UserConfig : IFacetMapConfiguration<User, UserDto>
{
    public static void Map(User source, UserDto target)
        => target.FullName = $"{source.FirstName} {source.LastName}";
}

[Facet(typeof(User), Configuration = typeof(UserConfig))]
public partial class UserDto { public string FullName { get; set; } }
```

### With return value (for init-only / records)
Implement `IFacetMapConfigurationWithReturn<TSource, TTarget>` — `static TTarget Map(TSource, TTarget)`.

### Async (DI-resolved)
`IFacetMapConfigurationAsync<TSource, TTarget>` — instance-based, supports `Task`.

### Before/After hooks
`IFacetBeforeMapConfiguration<TSource, TTarget>` — `static void BeforeMap(TSource, TTarget)`
`IFacetAfterMapConfiguration<TSource, TTarget>` — `static void AfterMap(TSource, TTarget)`

---

## Inheritance & Base Class Support

The generator traverses base classes to include inherited properties. When a facet type inherits from a base facet type, the generator suppresses duplicate members that are already declared in the base.

---

## Source Signature Tracking (FAC022)

Setting `SourceSignature` on a `[Facet]` attribute stores an 8-char SHA hash of the source type's property names and types. When the source type changes (property added, removed, renamed, type changed), the analyzer recalculates the hash, detects the mismatch, and emits FAC022 with the new hash. A code fix auto-applies the update. This prevents silent API drift when domain models evolve.

---

# Facet Developer Guidelines

## Overview
Facet is a C# source generator library that enables compile-time DTO generation, projections, and mapping without runtime overhead. The library consists of multiple packages that work together to provide a complete projection and mapping solution for .NET applications.

## Project Structure
- **src/Facet/** � Core source generator library (attributes, generators, shared utilities)
- **src/Facet.Extensions/** � Provider-agnostic extension methods for mapping and projections
- **src/Facet.Mapping/** � Advanced static mapping configuration with async support
- **src/Facet.Mapping.Expressions/** � Expression tree transformation utilities
- **src/Facet.Extensions.EFCore/** � Entity Framework Core-specific async extensions
- **src/Facet.Extensions.EFCore.Mapping/** � Advanced EF Core custom mapper support with DI
- **test/Facet.Tests/** � Comprehensive unit and integration tests
- **docs/** � Documentation and guides

## Build & Runtime Commands
- `dotnet build` or `dotnet build Facet.sln` � Compiles the entire solution
- `dotnet test` � Runs all tests in the test project
- `dotnet test --filter "FullyQualifiedName~{TestCategory}"` � Runs specific test category (e.g., GenerateDtos, Facet, Flatten)
- `dotnet pack` � Creates NuGet packages for distribution
- `dotnet build --no-incremental` � Clean build (useful when generator changes aren't being picked up)

**Important**: Source generators cache aggressively. If changes to generators aren't reflected:
1. Clean the solution: `dotnet clean`
2. Delete `obj/` and `bin/` directories
3. Rebuild with `dotnet build --no-incremental`

## Architecture Patterns

### Source Generator Structure (src/Facet/Generators/)
All generators follow Roslyn's incremental generator pattern:

- **FacetGenerator.cs** � Generates projections from `[Facet]` attribute
- **GenerateDtosGenerator.cs** � Generates CRUD DTOs from `[GenerateDtos]` and `[GenerateAuditableDtos]`
- **FlattenGenerator.cs** � Generates flattened DTOs from `[Flatten]` attribute
- **Shared/** � Common utilities (`GeneratorUtilities.cs`, `AttributeValidator.cs`, etc.)

**Key Principles**:
- Use incremental generators (`IIncrementalGenerator`) for performance
- Generators must be deterministic and stateless
- Use `ForAttributeWithMetadataName` for attribute-based discovery
- Always handle `CancellationToken` to support incremental compilation
- Generated code should include `#nullable enable` directive
- Use `<auto-generated>` header in all generated files
- Never throw exceptions in generators � swallow and return null instead

### Generator Hint Names
- Hint names (file names passed to `context.AddSource()`) **cannot contain** invalid filename characters like `:`, `<`, `>`, `|`, etc.
- For types in global namespace, strip the `global::` prefix before building hint names
- Use `.GetSafeName()` extension method for complex type names

### Model Structure
Each generator defines target models that capture attribute data:
- `FacetTargetModel` � For `[Facet]` attribute
- `GenerateDtosTargetModel` � For `[GenerateDtos]` attribute
- `FlattenTargetModel` � For `[Flatten]` attribute
- `FacetMember` � Represents properties/fields with metadata (nullability, init-only, required)

### Extension Methods (src/Facet.Extensions/)
- Static extension methods for `ToFacet<T>()`, `SelectFacets<T>()`, `ToSource<T>()`
- Generic overloads: parameterless (`ToFacet<TTarget>()`) and typed (`ToFacet<TSource, TTarget>()`)
- Typed versions are faster due to avoiding reflection
- Always provide both sync and async variants where applicable

### EF Core Integration (src/Facet.Extensions.EFCore/)
- Use `IQueryable<T>` extensions for database projections
- Leverage `SelectFacet<T>()` for compile-time LINQ expressions
- `ToFacetsAsync()` for materialized async operations
- Navigation properties auto-loaded via expression analysis (no `.Include()` needed)

## Code Conventions

### C# Style
- **Namespace style**: File-scoped namespaces (`namespace Facet.Generators;`)
- **Indentation**: 4 spaces (no tabs)
- **Naming**:
  - PascalCase for public APIs, types, properties, methods
  - `_camelCase` for private fields
  - `camelCase` for local variables and parameters
- **Null handling**: Use nullable reference types (`#nullable enable`)
- **String interpolation**: Prefer `$"..."` over `string.Format()` or concatenation
- **Collections**: Use `ImmutableArray<T>` for generator models

### XML Documentation
- **Mandatory** for all public APIs (types, methods, properties)
- Use `<summary>`, `<param>`, `<returns>`, `<example>` tags
- Include usage examples for complex features
- Preserve XML docs in generated code where applicable

### Generated Code Quality
- Always include file header with `<auto-generated>` comment
- Enable nullable reference types with `#nullable enable`
- Add XML doc comments with `<summary>` explaining the purpose
- Include the source type in generated type documentation
- For constructors, document parameters and purpose
- Include usage examples in projection properties

### Error Handling in Generators
```csharp
try
{
    // Generator logic
}
catch (Exception)
{
    // Swallow exceptions to prevent generator crashes
    // Optionally emit a diagnostic instead
    return null;
}
```

Never let exceptions bubble up from generators � this crashes the compilation.

## Testing Requirements

### Test Organization
Tests are organized by feature in `test/Facet.Tests/`:
- **UnitTests/Core/** � Core generator functionality tests
  - `Facet/` � `[Facet]` attribute tests
  - `GenerateDtos/` � `[GenerateDtos]` tests
  - `Flatten/` � `[Flatten]` tests
- **UnitTests/Extensions/** � Extension method tests
- **IntegrationTests/** � End-to-end scenarios
- **TestModels/** � Test entities and DTOs

### Test Conventions
- Use xUnit framework
- Test class naming: `{Feature}{Aspect}Tests.cs` (e.g., `FacetGeneratorTests.cs`)
- Test method naming: `{Method}_{Scenario}_{ExpectedBehavior}` (e.g., `ToFacet_WithNestedObject_MapsCorrectly`)
- Use `[Fact]` for simple tests, `[Theory]` with `[InlineData]` for parameterized tests
- Always test edge cases: global namespace, nullable types, collections, nested objects

### Coverage Requirements
- **All generators** must have tests covering:
  - Basic generation scenario
  - Global namespace classes (no namespace)
  - Nullable reference types
  - Collections (List, Array, IEnumerable)
  - Nested objects
  - Complex types (generics, nullable value types)
  - Edge cases (empty classes, read-only fields, init-only properties)

### Running Tests
```bash
# Run all tests
dotnet test

# Run specific test category
dotnet test --filter "FullyQualifiedName~GenerateDtos"

# Run tests with detailed output
dotnet test --logger "console;verbosity=detailed"
```

## Problem-Solving Workflow

### For Source Generator Issues
1. **Reproduce**: Create minimal test case in `test/Facet.Tests/TestModels/`
2. **Debug**:
   - Check generated files in `obj/Generated/Facet/`
   - Use `#if DEBUG` to output diagnostics during development
   - Attach debugger to generator process (set `DOTNET_CLI_UI_LANGUAGE=en` and use VS)
3. **Fix**: Implement focused fix in the appropriate generator
4. **Test**: Add regression test to prevent future issues
5. **Verify**: Run full test suite to ensure no regressions

### For Extension Method Issues
1. **Isolate**: Create unit test that reproduces the issue
2. **Verify Generated Code**: Ensure the generator produces correct projections
3. **Fix Extension Logic**: Update extension method implementation
4. **Performance**: Consider both generic (`<T>`) and typed (`<TSource, TTarget>`) variants

## Git Workflow

### Branching Strategy
- **Main Branch**: `master` (stable, release-ready code)
- **Feature Branches**: `feature/issue-{id}-{description}` or `feature/{description}`
- **Bug Fixes**: `fix/issue-{id}-{description}` or `fix/{description}`
- **Enhancements**: `enhancement/issue-{id}-{description}`

### Branch Creation
```bash
git checkout master
git pull origin master
git checkout -b fix/issue-140-global-namespace-hint-name
```

### Commit Message Format
Follow conventional commits:
```
{type}({scope}): {description}

{optional body}

{optional footer}
```

**Types**:
- `fix` � Bug fixes
- `feat` � New features
- `docs` � Documentation changes
- `test` � Test additions or modifications
- `refactor` � Code refactoring without behavior changes
- `perf` � Performance improvements
- `chore` � Build process, dependencies, tooling

**Scopes**: `generator`, `facet`, `dtos`, `flatten`, `extensions`, `efcore`, `tests`, `docs`

**Examples**:
```
fix(generator): handle global namespace in GetSimpleTypeName

The method now strips the global:: prefix before extracting type name,
preventing invalid characters in hint names.

Fixes #140
```

```
feat(dtos): add support for record structs in GenerateDtos

Users can now specify OutputType.RecordStruct for value-type DTOs.

Closes #123
```

### Pull Request Guidelines
1. Reference the issue number in PR title: `Fix #140: Handle global namespace in DTO generation`
2. Provide clear description of changes
3. Include test results (all tests must pass)
4. Update documentation if adding new features
5. Ensure no breaking changes unless major version bump

## Key Design Principles

### 1. Zero Runtime Cost
All code generation happens at compile time. Generated code should be as efficient as hand-written code.

### 2. Type Safety
Leverage C#'s type system. No reflection at runtime (except in non-generic extension methods).

### 3. Developer Experience
- Intuitive attribute-based API
- Rich IntelliSense support
- Clear error messages via diagnostics
- Comprehensive XML documentation

### 4. Compatibility
- Support .NET Standard 2.0 for Facet (generator)
- Support .NET 6+ for extensions
- Support EF Core 6+ for EF Core packages

### 5. Performance
- Incremental generators for fast compilation
- Cached expression trees for projections
- Typed generic methods for best performance

## Common Pitfalls

### Source Generator Development
- **Forgetting to handle global namespace**: Always check for and strip `global::` prefix
- **Invalid hint names**: Sanitize type names before using as file names
- **Non-deterministic generation**: Ensure generators produce same output for same input
- **Ignoring cancellation tokens**: Always pass and check `CancellationToken`
- **Throwing exceptions**: Swallow exceptions and return null or emit diagnostics

### Testing
- **Missing global namespace tests**: Always test classes with and without namespaces
- **Forgetting edge cases**: Test nullable types, collections, nested objects, etc.
- **Not verifying generated code**: Check `obj/Generated/` to ensure correct output

### Extension Methods
- **Providing typed overloads**: Always offer `<TSource, TTarget>` (performance) and `<TTarget>`
- **Mixing sync/async**: Provide both variants consistently

## Documentation Requirements

### When Adding New Features
1. Update relevant README.md in package directory
2. Add examples to main README.md
3. Create or update docs in `docs/` directory
4. Add XML documentation to all public APIs
5. Include usage examples in XML docs

### Documentation Style
- Use clear, concise language
- Provide code examples for all features
- Show both basic and advanced usage
- Explain "why" not just "what"
- Include performance considerations where relevant

## References

- [Roslyn Source Generators Cookbook](https://github.com/dotnet/roslyn/blob/main/docs/features/source-generators.cookbook.md)
- [Incremental Generators](https://github.com/dotnet/roslyn/blob/main/docs/features/incremental-generators.md)
- [C# Coding Conventions](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)
- **Project Documentation**: `docs/README.md`
- **Comprehensive Blog Post**: [Facets in .NET](https://tim-maes.com/blog/2025/09/28/facets-in-dotnet-(2)/)

## Quick Reference

### Adding a New Generator Feature
1. Update or create the target model in `src/Facet/Generators/{GeneratorName}/`
2. Implement the generator logic following `IIncrementalGenerator` pattern
3. Add test entities to `test/Facet.Tests/TestModels/`
4. Create comprehensive tests in `test/Facet.Tests/UnitTests/Core/`
5. Verify generated code in `obj/Generated/Facet/`
6. Update documentation and examples

### Debugging Generator Issues
1. Check `obj/Generated/Facet/{GeneratorName}/` for generated files
2. Use `dotnet build --no-incremental` to force regeneration
3. Add test case in `test/Facet.Tests/` to reproduce
4. Verify hint names don't contain invalid characters
5. Check for proper null handling and cancellation token usage

### Before Committing
- [ ] All tests pass (`dotnet test`)
- [ ] Code follows conventions (file-scoped namespaces, XML docs, etc.)
- [ ] Added tests for new features or bug fixes
- [ ] Updated documentation if needed
- [ ] Verified generated code quality
- [ ] No breaking changes (or documented if necessary)
