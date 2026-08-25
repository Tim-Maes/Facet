# Changelog

All notable changes to Facet are documented in this file.

## [Unreleased]

### Added

- Added `SetAccessor` parameter to `[Facet]` with `PropertySetAccessor` enum (`Preserve` / `Set` / `Init`) to override the set accessor emitted on all generated properties (#381). Supports the immutable builder pattern: one mutable facet for building, one init-only facet as the frozen read model.
- Each facet type now emits two generated files by default: `{Type}.Properties.g.cs` (property declarations) and `{Type}.Mappings.g.cs` (constructors, projections, and conversion methods). Opt out globally by setting `<Facet_SplitGeneratedFiles>false</Facet_SplitGeneratedFiles>` in your `.csproj` or `Directory.Build.props` to restore the previous single-file output.

### Fixed

- `SelectFacet<TOut>()` and `SelectFacet<TSource, TTarget>()` now work with `[FacetMap]`-generated projections. Previously, these generic projection methods only discovered projections defined directly on the target type (via `[Facet]`). FacetMap projections, which live on marker classes, are now automatically discovered via assembly scanning. This enables generic repository/service patterns like `queryable.SelectFacet<T, TOut>()` to work seamlessly with FacetMap types.

## [6.6.3] - 2026-05-22

### Fixed

- Fixed warnings in generator and analyzer output (#380).

### Changed

- Refreshed README content and benchmark snapshots around the release.

## [6.6.2] - 2026-05-21

### Fixed

- Improved preserve-references behavior.
- Fixed multi-source facet behavior (#365).

### Dependencies

- Bumped `Microsoft.NET.Test.Sdk` to 18.5.1 (#364).
- Bumped `FluentAssertions` to 8.10.0 (#374).
- Bumped `Microsoft.EntityFrameworkCore.InMemory` to 10.0.8 (#375).
- Bumped `Microsoft.EntityFrameworkCore.Sqlite` to 10.0.8 (#376).
- Bumped `Microsoft.SourceLink.GitHub` to 10.0.300 (#377).

### Notes

- Includes the `v6.6.2-alpha` pre-release in the historical tag archive below.

## [6.6.1] - 2026-05-21

### Added

- Added `OutputType.PartialClass` support to `GenerateDtos` (#379).

## [6.6.0] - 2026-05-20

### Added

- Added `OutputType.Interface` support to `GenerateDtos` (#378).
- Added `ConvertEnumsTo` parity for `GenerateDtos` so generated DTO sets support the same enum conversion model as `Facet` (#373).

## [6.5.x] - 2026-05-02 to 2026-05-07

### Added

- Added `ApplyToSource` support (#363).
- Added `CopyDocs` for documentation propagation (#357).
- Added `CopyDocs` support from external assemblies (#370).
- Added an `InheritDocs` parameter (#360).
- Added `MaxDepth` support for `ToSource()` (#361).

### Fixed

- Fixed expression mapping when navigation properties are involved (#362).
- Fixed `@inheritdocs` resolution so inherited documentation continues climbing until content is found (#366).
- Fixed incorrect default values for custom struct types when `GenerateToSource = true` (#368).
- Tightened `MaxDepth` consistency and related regression coverage.

### Dependencies

- Bumped `Microsoft.EntityFrameworkCore.InMemory` to 10.0.7 (#351).
- Bumped `Microsoft.EntityFrameworkCore.Sqlite` to 10.0.7 (#352).
- Bumped `Microsoft.SourceLink.GitHub` to 10.0.203 (#353).

## [6.4.x] - 2026-04-21 to 2026-05-01

### Added

- Improved inherited base projection configuration selection and added multi-source regression coverage (#345).

### Fixed

- Fixed multi-level mapping when different configurations are involved (#350).
- Fixed `ConvertEnumsTo` behavior (#355).
- Improved `MaxDepth` consistency.

### Changed

- Updated workflow dependencies, including `actions/github-script` (#346).
- Bumped `Microsoft.EntityFrameworkCore.InMemory` to 10.0.6 (#347).
- Bumped `Microsoft.EntityFrameworkCore.Sqlite` to 10.0.6 (#348).
- Bumped `Microsoft.SourceLink.GitHub` to 10.0.202 (#349).

## [6.0.0] - 2026-04-08

### Added

- `CollectionTargetType` and per-property `AsCollection` support for remapping source collection types.
- `IFacetToSourceConfiguration<TFacet, TSource>` for custom reverse mapping.
- Cross-assembly facets through `InternalsVisibleTo`.
- Correct `ToSource()` support for inherited classes.
- Global configuration defaults through MSBuild and `Facet.props`.
- `GenerateCopyConstructor` and `GenerateEquality`.
- `ConvertEnumsTo` support with round-trip mapping.
- FAC023 diagnostics for incomplete `ToSource` generation.
- Before/after mapping hooks with `MapHooks`.
- `Facet.Dashboard`.
- `ApplyFacet`.
- Patch DTO generation.
- Public constructor generation.
- Read-only collection support.
- Support for disabling automatic auditable-field inclusion.
- Support for partial properties.

### Fixed

- Fixed inheritance warnings on generated code (#317).
- Fixed `ApplyFacetWithChanges` (#308).
- Fixed nested `ToSource` mapping (#306).
- Fixed static members being incorrectly included (#301).
- Fixed `PreserveRequiredProperties` for record-class facets (#298, #299).
- Fixed `TypeAnalyzer` errors (#296).
- Fixed nested facet, nullability, constructor, and naming-clash issues across the V6 line.
- Removed the generator as a runtime dependency (#197).

### Changed

- Improved generator performance and constructor ordering.
- Made generator output more AOT-friendly.
- Expanded project-wide configuration support via `Facet.props`.

## [5.0.0] - 2025-11-24

### Added

- `[MapWhen]` for conditional property mapping.
- `[MapFrom]` for declarative property renaming.
- `[FlattenTo]` for collection unpacking.
- SmartLeaf flattening.
- Source signature tracking with FAC022.
- Automatic EF Core navigation loading.
- Advanced async mapping with DI support.
- Expression transformation utilities.
- A broad Roslyn analyzer set with code fixes.

### Changed

- Replaced obsolete `GenerateBackTo` with `GenerateToSource`.
- Made `GenerateToSource` opt-in by default.

## Historical tag archive

### 6.x releases

| Version | Date | Summary |
| --- | --- | --- |
| 6.6.3 | 2026-05-22 | fix warnings (#380) |
| 6.6.2-alpha | 2026-05-21 | Pre-release for 6.6.2 stabilization. |
| 6.6.2 | 2026-05-21 | preserve references and maintenance updates |
| 6.6.1 | 2026-05-21 | `GenerateDtos`: add `OutputType.PartialClass` (#379) |
| 6.6.0 | 2026-05-20 | `GenerateDtos`: add `OutputType.Interface` (#378) |
| 6.5.5 | 2026-05-07 | `CopyDocs` from external assembly (#370) |
| 6.5.4 | 2026-05-05 | Fix `GenerateToSource = true` default value for custom struct types (#368) |
| 6.5.3 | 2026-05-05 | Fix `@inheritdocs` resolution (#366) |
| 6.5.2 | 2026-05-03 | Add `ApplyToSource` (#363) |
| 6.5.1 | 2026-05-03 | Fix expression mapping with navigation properties (#362) |
| 6.5.0 | 2026-05-02 | `MaxDepth` with `ToSource()` (#361) |
| 6.4.5 | 2026-05-01 | Add `InheritDocs` parameter (#360) |
| 6.4.4 | 2026-04-28 | Fix `ConvertEnumsTo` (#355) |
| 6.4.3 | 2026-04-27 | `MaxDepth` consistency |
| 6.4.2 | 2026-04-25 | Release housekeeping for 6.4.1 follow-up |
| 6.4.1 | 2026-04-25 | Fix multi-level mapping with different configs (#350) |
| 6.4.0 | 2026-04-21 | Fix inherited base projection config selection and add regression coverage (#345) |
| 6.3.4 | 2026-04-17 | Add unit test coverage (#344) |
| 6.3.3 | 2026-04-16 | Fix inherited facet projections not populating properties |
| 6.3.2 | 2026-04-16 | Fix inherited facet projection mappings not being applied |
| 6.3.1 | 2026-04-15 | Fix multi-source facet failures |
| 6.2.0 | 2026-04-15 | Support multiple `[Facet]` attributes on the same target type (#334) |
| 6.1.4 | 2026-04-14 | Fix CS8601 and CS8602 warnings (#333) |
| 6.1.3 | 2026-04-14 | Fix unnecessary `new` keyword on `FromSource` (#331) |
| 6.1.2 | 2026-04-13 | Fix generated static using directives |
| 6.1.1 | 2026-04-13 | Support immutable collection types in nested facets |
| 6.1.0-alpha | 2026-04-12 | Pre-release for projection map config (#327) |
| 6.0.2 | 2026-04-11 | Fix wrong property naming on derived generated classes (#324) |
| 6.0.1 | 2026-04-10 | Add compiler error on invalid `MapFrom` properties (#323) |
| 6.0.0 | 2026-04-08 | V6 major release |

### 5.x releases

| Version | Date | Summary |
| --- | --- | --- |
| 5.9.1 | 2026-04-03 | Support `InternalsVisibleTo` for cross-assembly facets (#310, #311) |
| 5.9.0 | 2026-04-01 | `ToSource()` with inherited classes (#309) |
| 5.8.8 | 2026-04-01 | `ApplyFacetWithChanges` fix (#308) |
| 5.8.7 | 2026-04-01 | Fix `ToSource()` in nested facets (#306) |
| 5.8.6 | 2026-03-31 | Fix static members being included (#301) |
| 5.8.5 | 2026-03-31 | Fix `PreserveRequiredProperties` for record-class facets (#298, #299) |
| 5.8.4 | 2026-03-29 | Include parameterless constructors as a primary constructor (#297) |
| 5.8.3 | 2026-03-26 | Fix `TypeAnalyzer` (#296) |
| 5.8.2 | 2026-03-05 | Add `nameof` resolver support to flatten excludes (#284) |
| 5.8.1 | 2026-03-04 | Add `Facet.props` support |
| 5.8.0 | 2026-03-04 | Workflow dependency maintenance (`actions/upload-artifact` v7) (#280) |
| 5.7.0 | 2026-02-24 | Add `GenerateCopyConstructor` and `GenerateEquality` (#279) |
| 5.6.5 | 2026-02-22 | Add full-path `nameof` support with `@` (#276) |
| 5.6.4 | 2026-02-20 | Support partial properties (#275) |
| 5.6.3 | 2026-02-19 | Fix static usings for nested facets (#273) |
| 5.6.2 | 2026-02-15 | Fix non-nullable reference type properties without initializers (#267) |
| 5.6.1 | 2026-02-13 | AOT compatibility fixes (#265) |
| 5.6.0 | 2026-02-12 | Add enum conversions (`ConvertEnumsTo`) (#263) |
| 5.5.3 | 2026-02-12 | Fix nested facets with nullable references (#262) |
| 5.5.2 | 2026-01-29 | Add null checks on nested facets (#259) |
| 5.5.1 | 2026-01-28 | Fix nested facet nullable-enabled generated code (#257) |
| 5.5.0 | 2026-01-27 | Add `ChainToParameterlessConstructor` |
| 5.4.4 | 2026-01-27 | Fix nullable issues with object properties (#254) |
| 5.4.3 | 2026-01-23 | Fix ambiguous constructor calls (#252) |
| 5.4.2 | 2026-01-22 | Fix same facet name in different namespaces (#250) |
| 5.4.1 | 2026-01-13 | Fix generic type parameters in attributes (#246) |
| 5.4.0 | 2026-01-12 | Allow collection copying in flatten (#245) |
| 5.3.3 | 2026-01-12 | Fix copied initializers (#244) |
| 5.3.2 | 2026-01-08 | Fix copy attributes with enum parameters (#240) |
| 5.3.1 | 2026-01-06 | Fix hooks integration (#236) |
| 5.3.0 | 2026-01-02 | Add `MapHooks` (#234) |
| 5.2.0-alpha | 2025-12-18 | Pre-release dashboard demo |
| 5.2.0 | 2025-12-23 | Workflow dependency maintenance (`actions/checkout` v6) (#233) |
| 5.1.12 | 2025-12-14 | Deterministic constructor ordering (#226) |
| 5.1.11 | 2025-12-09 | Prevent non-compilable generated code (#222) |
| 5.1.10 | 2025-12-05 | Fix `MapFrom` (#215) |
| 5.1.9 | 2025-12-03 | Fix XML documentation syntax for constructor generator (#212) |
| 5.1.8 | 2025-12-01 | Add `ApplyFacet` (#211) |
| 5.1.7 | 2025-12-01 | Suppress CS1591 on generated code (#209) |
| 5.1.6 | 2025-11-28 | Add patch DTOs (#206) |
| 5.1.5 | 2025-11-28 | Fix nullable context warnings (#203) |
| 5.1.4 | 2025-11-28 | Add public constructor generation (#202) |
| 5.1.3 | 2025-11-28 | Workflow updates |
| 5.1.2 | 2025-11-27 | Fix `FlattenTo` naming clash (#198) |
| 5.1.1 | 2025-11-27 | Versioning fix |
| 5.1.0 | 2025-11-27 | Remove generator as a runtime dependency (#197) |
| 5.0.4 | 2025-11-26 | Fix flattening nested collections (#193) |
| 5.0.3 | 2025-11-26 | Fix nested custom mappings (#192) |
| 5.0.2 | 2025-11-26 | Documentation refresh |
| 5.0.1 | 2025-11-25 | Fix nested facets with duplicate names (#189) |
| 5.0.0 | 2025-11-24 | V5 GA release |
| 5.0.0-alpha5 | 2025-11-24 | Add `FlattenTo` for unpacking facet collections into flattened rows (#187) |
| 5.0.0-alpha4 | 2025-11-23 | Fix inherited members (#181) |
| 5.0.0-alpha3 | 2025-11-22 | Add `MapWhen` (#179) |
| 5.0.0-alpha2 | 2025-11-22 | Add source change detection (#177) |
| 5.0.0-alpha | 2025-11-21 | Add .NET 10 support (#176) |

### 4.x releases

| Version | Date | Summary |
| --- | --- | --- |
| 4.4.3 | 2025-11-21 | Support access modifiers (#171) |
| 4.4.2 | 2025-11-21 | Add SmartLeaf flattening (#169) |
| 4.4.1 | 2025-11-20 | Fix `LeafOnly` (#165) |
| 4.4.1-alpha4 | 2025-11-16 | NuGet config update |
| 4.4.1-alpha3 | 2025-11-16 | Integration merge during 4.4.1 stabilization |
| 4.4.1-alpha2 | 2025-11-16 | NuGet config adjustments |
| 4.4.1-alpha | 2025-11-16 | Add SourceLink (#162) |
| 4.4.0 | 2025-11-15 | Add nested wrappers (#160) |
| 4.4.0-alpha | 2025-11-14 | Documentation refresh during 4.4.0 incubation |
| 4.3.3.1 | 2025-11-14 | Versioning correction |
| 4.3.3 | 2025-11-14 | Fix package structure |
| 4.3.2.1 | 2025-11-14 | Package structure work |
| 4.3.2 | 2025-11-13 | Documentation refresh |
| 4.3.1 | 2025-11-12 | Fix facets from static class sources (#146) |
| 4.3.0 | 2025-11-08 | Establish 4.3.0 release line |
| 4.3.0-alpha | 2025-11-07 | Add `Facet.Extensions.EFCore.Mapping` (#139) |

### 3.x releases

| Version | Date | Summary |
| --- | --- | --- |
| 3.4.0 | 2025-11-08 | Fix global namespace support (#141) |
| 3.3.0 | 2025-11-07 | Flatten foreign-key clash handling (#138) |
| 3.3.0-alpha.1 | 2025-11-04 | Generator refactor |
| 3.3.0-alpha | 2025-11-03 | Add `Flatten` attribute (#135) |
| 3.2.2 | 2025-10-29 | Fix complex data structures and nesting (#133) |
| 3.2.1 | 2025-10-27 | 3.2.1 stabilization release |
| 3.2.1-alpha.1 | 2025-10-27 | Fix defaults and add tests (#129) |
| 3.2.1-alpha | 2025-10-26 | Version rollback during stabilization (#125) |
| 3.2.0-alpha | 2025-10-26 | Add `PreserveReference` and `MaxDepth` (#124) |
| 3.1.14 | 2025-10-24 | Implement fix (#120) |
| 3.1.13 | 2025-10-24 | Versioning correction |
| 3.1.12 | 2025-10-21 | Fix nullable assignments (#109) |
| 3.1.11 | 2025-10-21 | Fix FAC001 issue (#108) |
| 3.1.4 | 2025-10-23 | Fix nullable facet collections (#118) |
| 3.1.3 | 2025-10-23 | Add nullable nested facets (#117) |
| 3.1.3-alpha | 2025-10-22 | Pre-release for 3.1.3 stabilization |
| 3.1.2-alpha | 2025-10-22 | Fix EF projections without `Include` (#113) |
| 3.1.2 | 2025-10-21 | Bump `Microsoft.EntityFrameworkCore` to 9.0.10 (#98) |
| 3.1.1 | 2025-10-21 | Fix LINQ using statements for collections (#102) |
| 3.1.0 | 2025-10-19 | Add nested collection support (#97) |
| 3.0.0 | 2025-10-17 | Refactor `FacetGenerator` |

### 2.x releases

| Version | Date | Summary |
| --- | --- | --- |
| 2.9.31 | 2025-10-13 | Add `[SetsRequiredMembers]` to `GenerateDtos` (#90) |
| 2.9.3-alpha | 2025-10-07 | Add analyzers for usage errors and performance hints (#57) |
| 2.9.3 | 2025-10-08 | Enable unit tests on fork PRs |
| 2.9.2 | 2025-10-06 | Generate facets with nullable properties (#80) |
| 2.9.1 | 2025-10-03 | `GenerateDtos` also generates facets (#70) |
| 2.9.0 | 2025-10-01 | Add `Facet` include parameter (#68) |
| 2.8.2 | 2025-10-01 | Fix nullable strings (#67) |
| 2.8.1 | 2025-09-21 | Fix `BackTo()` with excluded required fields (#59) |
| 2.8.0 | 2025-09-17 | Documentation refresh |
| 2.7.0 | 2025-09-12 | Add expression mapping support (#55) |
| 2.6.2 | 2025-09-12 | Implementation and test follow-up (#54) |
| 2.6.1 | 2025-09-10 | Fix duplicate type names (#51) |
| 2.6.0 | 2025-09-09 | Support nested partial types (#49) |
| 2.5.0 | 2025-09-04 | Add `GenerateDtos` attribute (#39) |
| 2.4.8 | 2025-09-03 | Namespace fix (#46) |
| 2.4.7 | 2025-09-01 | Add XML docs support (#42) |
| 2.4.6 | 2025-09-01 | Generate parameterless constructors by default (#40) |
| 2.4.5 | 2025-08-30 | File-scoped namespaces and generated source header (#36) |
| 2.4.4 | 2025-08-27 | Pipeline permissions update |
| 2.4.3 | 2025-08-27 | CI/CD pipeline update |
| 2.4.2 | 2025-08-27 | CI/CD maintenance |
| 2.4.1 | 2025-08-26 | Record primary constructor support (#33) |
| 2.3.0 | 2025-08-20 | Add bi-directional EF mapping support (#31) |
| 2.2.0 | 2025-08-20 | Documentation update |
| 2.1.0 | 2025-08-18 | Default `FacetKind` and record improvements (#23) |
| 2.0.1 | 2025-08-05 | Documentation refresh |
| 2.0.0 | 2025-08-04 | Add async mapping (#15) |

### 1.x releases

| Version | Date | Summary |
| --- | --- | --- |
| 1.9.3 | 2025-07-04 | Version bump |
| 1.9.2 | 2025-07-02 | Documentation refresh |
| 1.9.1 | 2025-07-03 | Version update |
| 1.9.0 | 2025-07-03 | Workflow update |
| 1.8.0 | 2025-06-04 | Documentation refresh |
| 1.7.0 | 2025-05-06 | Documentation refresh |
| 1.6.0 | 2025-04-27 | Documentation refresh |
| 1.5.0 | 2025-04-26 | Documentation refresh |
| 1.4.0 | 2025-04-25 | Add incremental caching (#8) |
| 1.3.0 | 2025-04-24 | Documentation refresh |
| 1.2.0 | 2025-04-24 | Merge incremental source generator work (#6) |
| 1.1.1 | 2025-04-23 | Post-release merge maintenance |
| 1.1.0 | 2025-04-23 | Version bump |
| 1.0.2 | 2025-04-23 | Cleanup |
| 1.0.1 | 2025-04-23 | Version bump |
| 1.0.0 | 2025-04-23 | Initial release |
