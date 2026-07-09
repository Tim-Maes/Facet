# Fluent Navigation — Resurrection Notes (2026-07)

This folder and `src/Facet.Generation.Shared/` are a **curated port of the 2025-09 fluent
navigation prototype**, brought forward onto current `master` so the work is preserved,
reviewable, and buildable — not lost on a stale branch. It is **not wired into anything**:
the project is `IsPackable=false`, referenced by no shipping project, and its generator runs
in no consumer. Treat this PR as a design artifact plus compiling source, with a concrete
modernization path.

## What this is

A source-generated fluent API for EF Core queries with compile-time-safe navigation
inclusion and automatic DTO projection:

```csharp
var order = await context.FacetOrder()
    .WithUser()
    .WithOrderItems(items => items.WithProduct())
    .GetByIdAsync(orderId);
```

Generated per entity:

- **Shape interface** `IOrderShape` — scalar-only, get-only (the DTO contract), plus an
  internal `OrderShape` implementation used as the projection target.
- **Capability interfaces** per navigation, *generic in the navigation's own shape* so
  composition nests without a combinatorial explosion of concrete types:
  `IOrderWithUser<TShape> : IOrderShape` adding `TShape User { get; }`
  (`IReadOnlyList<TShape>` for collections). `.WithUser().WithOrderItems(i => i.WithProduct())`
  yields `IOrderWithUser<...>` composed with `IOrderWithOrderItems<IOrderItemWithProduct<IProductShape>>`.
- **Fluent builders** (`FacetOrderBuilder<TShape>`) with immutable chaining, automatic
  `AsNoTracking()`, and terminal methods (`ToListAsync`, `GetByIdAsync`, …).
- **DbContext entry points** `context.FacetOrder()` and a generic `context.Facet<TEntity, TDto>()`.
- **Chain-use discovery** (`ChainUseDiscovery.cs`) — scans the consuming compilation for the
  `.WithX()` chains actually written and only generates those shapes, avoiding the 2^N
  explosion of every navigation combination.

The navigation metadata (which properties are navigations, targets, cardinality) comes from
the EF model, not from type-shape guessing — the same principle `ExcludeNavigationProperties`
now applies via the model manifest.

## Provenance

- Original branch: `feature/enhanced-dto-generation-and-ef-updates` (closed PR #47), which
  accumulated several intertwined efforts. The fluent navigation core is the piece worth
  keeping and is what this port extracts.
- The design docs here (`USAGE.md`, `implementation-plan.md`, `research/…`) are copied
  verbatim from that branch; they describe the intended end state, including parts that were
  never finished.

## What was ported, and how

| Piece | Disposition |
|---|---|
| `ChainUseDiscovery`, `Emission/*` (Shape/Capability/FluentBuilder/Selectors emitters, Diagnostics) | Verbatim |
| `ModelRoot`, `FacetDtoInfo`, `EfJsonReader`, `FacetConfiguration` | Verbatim (legacy input format — see below) |
| `FacetEfGenerator` (orchestrator) | Moved into this project from the old `Facet.Extensions.EFCore`; its hand-rolled `AppDomain.AssemblyResolve` hook was **removed** — SourceGenerator.Foundations (adopted in #399) provides dependency embedding/resolution properly |
| csproj | Single `netstandard2.0` (the old multi-targeting existed for MSBuild-task hosting that is not ported) |

## What was deliberately left behind

- **`ExportEfModelTask` / `efmodel.json` build-time export** — superseded. The
  `*.facetmodel.json` model manifest (written by `FacetDesignTimeServices` on
  `dotnet ef migrations add/remove`, see #399) is the maintained way to get EF model truth
  into the generator, with versioning, atomic parsing, and FAC103–FAC106 diagnostics.
- **The old branch's `GenerateDtosAttribute` additions** (`ExcludeMembersFromType`,
  `InterfaceContracts`) — not ported here; `InterfaceContracts` is largely answered by
  `OutputType.Interface` (#396), and `ExcludeMembersFromType` deserves its own focused PR if
  still wanted.
- **Integration tests** — the old tests exercised paths that end in the known-unfinished
  stubs below; porting them would freeze failure. New tests should come with the retarget.

## Known-unfinished (as inherited)

1. `SelectorsEmitter` emits placeholder projection expressions rather than real
   entity→shape mappings.
2. Builder terminal methods throw `NotImplementedException` in some paths.
3. Generated-file cross-references caused ordering/circular issues; the in-progress fix was
   consolidating shape output into a single `AllShapeInterfaces.g.cs`.

## Modernization path

1. **Retarget the model input**: replace `EfJsonReader`/`ModelRoot` (Newtonsoft, legacy
   `efmodel.json`) with the `*.facetmodel.json` manifest reader (`System.Text.Json` under
   SGF-embedded dependencies) — the manifest already records navigations with target and
   cardinality per entity, which is everything the emitters consume `ModelRoot` for. This
   also deletes this project's Newtonsoft dependency.
2. **SGF-host the generator**: `FacetEfGenerator` becomes an SGF `IncrementalGenerator`
   (exception isolation, logging, embedded deps), like `GenerateDtosGenerator`.
3. **Finish `SelectorsEmitter`** projections and builder terminals per
   `implementation-plan.md` (the plan estimated ~70% of the architecture already existed).
4. **Diagnostics parity**: shape/builder generation should fail loudly on missing manifest
   coverage, mirroring FAC105/FAC106 semantics rather than generating placeholders.
