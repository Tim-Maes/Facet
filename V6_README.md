# Facet V6 Release Notes

Everything new since V5.0.0.

---

## New Features

### Collection Type Mapping — `CollectionTargetType` & `AsCollection` (#319, #315)
Remap source collection types (e.g. EF Core's `Collection<T>`) to `List<T>` or any other collection type.
- **`CollectionTargetType`** on `[Facet]`, remaps **all** collection properties on the facet to the given type.
- **`AsCollection`** on `[MapFrom]`, remaps a **single** collection property.
- `Collection<T>` is now recognised by the collection-detection pipeline (previously silently skipped).
- `ToSource()` round-trips correctly, reconstructing the original collection type on the entity.

### ToSource Configuration (#316)
Custom reverse-mapping configuration via `IFacetToSourceConfiguration<TFacet, TSource>`, allowing full control over how a facet maps back to its source entity. Supports static and DI-resolved instances.

### Cross-Assembly Facets with `InternalsVisibleTo` (#310, #311)
Facets can now reference source types from other assemblies that expose internals via `[InternalsVisibleTo]`.

### ToSource with Inherited Classes (#309)
`ToSource()` now correctly handles facets that target inherited entity types, mapping base and derived properties.

### Global Configuration Defaults (#283)
Override default `[Facet]` attribute settings project-wide via MSBuild properties in your `.csproj` or a `Facet.props` file, no need to repeat the same options on every attribute.

### `GenerateCopyConstructor` & `GenerateEquality` (#279)
- **`GenerateCopyConstructor`** — generates a copy constructor for cloning and MVVM scenarios.
- **`GenerateEquality`** — generates value-based `Equals`, `GetHashCode`, `==`, and `!=` for class DTOs.

### `nameof(@Object.Member.SubMember)` Support for Flatten Exclude (#284)
Full-path `nameof` expressions using the `@` symbol are now supported in `[Flatten(Exclude = ...)]`.

### Enum Conversions — `ConvertEnumsTo` (#263)
Convert all enum properties on a facet to `string` or `int` with full round-trip support in both projection and `ToSource()`.

### FAC023 Diagnostic (#271)
New analyzer warning `FAC023` when `GenerateToSource = true` but `ToSource` cannot be generated for specific properties, including the property names in the message.

### Mapping Hooks — `MapHooks` (#234)
Before/After hooks to inject validation, defaults, or computed values around the auto-mapping pipeline.

### Facet Dashboard (#231)
New companion package `Facet.Dashboard`, a visual overview of all facets, their source types, and generated members in your solution.

### `ApplyFacet` (#211)
Apply a facet's mapping to an existing instance rather than creating a new one.

### Patch DTOs (#206)
Generate patch/partial-update DTOs with `ApplyFacetWithChanges` support.

### Public Constructor Generation (#202)
Generate a public parameterless constructor on the facet type.

### `ChainToParameterlessConstructor` (#257 area, v5.5.0)
Constructors chain to a parameterless constructor when available.

### ReadOnly Collection Support (#221)
`IReadOnlyList<T>` and `IReadOnlyCollection<T>` are now detected and mapped correctly.

### Opt-in to Disable Auditable Fields (#238)
New parameter to disable the automatic inclusion of auditable fields (e.g. `CreatedAt`, `UpdatedAt`).

### Allow Collection Copying in Flatten (#245)
Flattened properties that are collections are now copied correctly.

### Support Partial Properties (#275)
C# 13 partial properties are now handled correctly by the generator.

---

## Bug Fixes

| Issue / PR | Description |
|---|---|
| #317 | Fix inheritance warnings on generated code |
| #308 | `ApplyFacetWithChanges` fix |
| #306 | Fix `ToSource` in nested facets |
| #301 | Fix static members being incorrectly included |
| #298, #299 | Fix `PreserveRequiredProperties` for record-class facets |
| #296 | Fix `TypeAnalyzer` errors |
| #278, #277 | Fix copy-attribute bugs |
| #276 | Support full-path `nameof` using `@` symbol |
| #273 | Fix static usings for nested facets |
| #267 | Fix non-nullable reference-type properties without initializers |
| #265 | AOT compatibility fix |
| #262 | Nested facet & nullable reference type fix |
| #259 | Null checks on nested facets |
| #257 | Nested facet nullable-enabled error in generated code |
| #254 | Nullable issues with object properties |
| #252 | Ambiguous constructor call |
| #250 | Same facet name in different namespaces |
| #246 | Generic type parameters in attributes |
| #244 | Copy initializers fix |
| #240 | Copy attributes with enum parameters |
| #236 | Hooks integration fix |
| #225 | Namespace fix for copy attributes |
| #222 | Prevent non-compilable generated code |
| #215 | `MapFrom` fix |
| #212 | XML documentation syntax for constructor generator |
| #209 | Suppress warning CS1591 on generated code |
| #204 | Nested facet classes fix |
| #203 | Nullable context warnings |
| #198 | `FlattenTo` naming clash |
| #197 | Remove generator as a runtime dependency |
| #193 | Flatten nested collections |
| #192 | Fix nested custom mappings |
| #189 | Nested facet with duplicate names |

---

## Improvements

- **Performance** — generator performance improvements (v5.0.2)
- **Constructors order** — deterministic ordering of generated constructors (#226)
- **`PreserveRequiredProperties`** — works correctly for record classes (#298)
- **Parameterless constructors as primary constructor**, included when detected (#297)
- **AOT compatibility** — generator output is now AOT-friendly (#265)
- **`Facet.props`** — project-wide configuration via MSBuild props file (v5.8.1)

---

## Package & Infra

- Removed generator as a runtime dependency, `Facet` is now a pure analyzer/generator package (#197)
- New package: **Facet.Dashboard** (#231)
- New package: **Facet.Mapping** (for `IFacetToSourceConfiguration`)
- Targets **.NET 8**, **.NET 9**, and **.NET 10**
