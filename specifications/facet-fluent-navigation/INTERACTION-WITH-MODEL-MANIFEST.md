# Fluent navigation × the EF model manifest (#399)

This branch is stacked on the model-manifest work (`ExcludeNavigationProperties` driven by
committed `*.facetmodel.json` files, the wired-manifest shaping default, and the
`FacetEfDesignTime` MSBuild opt-in). Neither feature has shipped, so this document works
through how they affect each other **before** either contract hardens. It supersedes the
"modernization path" sketch in [RESURRECTION.md](RESURRECTION.md) where the two disagree.

## The convergence: one ground truth, one producer knob

Both features need the same fact from the same source: *which members of an entity does the
EF model treat as data, and which as navigations* — taken from the real model, never guessed
from type shapes. The manifest work built that pipe end to end:

| Prototype-era piece (this branch) | Superseded by (#399) |
|---|---|
| `ExportEfModelTask` + `efmodel.json` (never ported) | `FacetManifestMigrationsScaffolder` writing `{ContextName}.facetmodel.json` beside the snapshot on every `dotnet ef migrations add`/`remove` |
| `EfJsonReader` (Newtonsoft, throws on malformed input) | `EfModelManifest` (System.Text.Json, atomic per-file parse, FAC103/FAC104 diagnostics) |
| `ModelRoot`/`ContextModel`/`EntityModel` DTOs | `ManifestEntity` keep/known sets |
| Hand-rolled `AppDomain.AssemblyResolve` hook | SourceGenerator.Foundations hosting |
| Hand-wired `<AdditionalFiles Include="efmodel.json">` | `FacetEfDesignTime` auto-wires the project's own manifests |
| Unconditional `FacetEfDiagnostic.g.cs` debug file, "No Facet DTOs discovered" warning in non-consuming projects, exception stack traces as generated source | Scoped FAC1xx diagnostics, SGF exception isolation |

The deeper convergence: a fluent **shape interface** (`IOrderShape` — scalar-only, get-only)
has exactly the member set the manifest already records as an entity's keep-set
(`scalar` + `complex`). And a `[GenerateDtos]` DTO under the shaping default is the same
member set as a class. One manifest entry feeds both features; adopting either feature is
the same three steps, and the second feature costs nothing extra to enable.

## What fluent needs that manifest v1 does not record

The old `efmodel.json` carried three things per entity that `facetmodel.json` v1 does not:

1. **Navigation target types** (`NavigationModel.Target`) — *not actually needed in the
   schema.* Once the manifest names the navigation properties, the generator resolves each
   name against the entity's CLR symbol; the property type (and collection element type) is
   the target. Roslyn is the more robust source anyway.
2. **Cardinality** (`NavigationModel.IsCollection`) — same: derivable from the property
   symbol.
3. **Entity keys** (`EntityModel.Keys`) — **not derivable from CLR shape.** `HasKey(...)`,
   composite keys, and keyless entities are pure model configuration. The prototype papers
   over this today: `GetByIdAsync` emits `EF.Property<object>(e, "Id")` with a literal
   `"Id"` — precisely the kind of name-convention heuristic the manifest exists to kill.

So the one real schema need is **`keys`** (ordered primary-key property names per entity).
The manifest is designed for additive growth — unknown JSON properties are ignored, so
adding `"keys": ["TenantId", "Id"]` requires **no version bump** and breaks no older
generator. Deliberately *not* added in #399: the alpha does not need it, and when the fluent
feature lands its generator can require the field and report a stale manifest with the
existing regenerate-remedy (one `dotnet ef migrations add`/`remove` pair). `ModelRoot.Keys`
was parsed but never consumed by any emitter, so nothing ported here depends on it yet.

## Knob coherence: the unified control surface

With both features on the manifest, the whole configuration story is:

- **`FacetEfDesignTime`** (MSBuild, DbContext project) — the producer knob. Writes manifests
  on migrations, wires the project's own manifests into its compilation. Cross-project
  consumers add one `<AdditionalFiles>` glob.
- **`[GenerateDtos]` / `[GenerateDtosFor]`** — the static-DTO surface. Manifest presence
  flips the shaping default; `ExcludeNavigationProperties = false` is the per-type opt-out;
  `IncludeProperties` forces a navigation into the *compile-time contract* (an owned
  collection edited with its parent — in the DTO always, for every consumer).
- **Fluent chains (`.WithUser()`, …)** — the per-query surface. A navigation joins the
  *query-time shape*, typed as a capability interface, without ever entering the static DTO.
  `ChainUseDiscovery` keeps this usage-driven: only chains actually written generate shapes,
  so there is no 2^N explosion and — importantly — **manifest presence alone generates
  nothing**. Wiring a manifest cannot light up fluent machinery by surprise.

`IncludeProperties` and `.WithX()` are therefore two answers to "I want this navigation"
with different lifetimes, not competing knobs: contract-time vs query-time inclusion. Both
features drop navigations by default and require explicit, visible re-inclusion — the same
philosophy at two binding times.

Open decision for the modernization (not resolved here): what drives **builder** emission.
Chains cannot be written until builders and `DbContext` entry points exist, so builder
emission needs its own driver. The prototype used attribute discovery
(`[Facet]`/`[GenerateDtos]`/`[GenerateAuditableDtos]`) plus a
`FacetEmitBuildersFromGenerateDtos` MSBuild property. Under manifest semantics the cleaner
rule is: **builders for every entity in the wired manifest, gated by one MSBuild property**
(the fluent feature's single enable knob), with `FacetMaxChainDepth` as tuning. The
`[GenerateAuditableDtos]` discovery should be dropped outright — #399 already exempts the
obsolete attribute from manifest semantics because it cannot express the opt-out.

Prototype MSBuild knobs (`FacetMaxChainDepth`, `FacetEnableDebugOutput`,
`FacetEmitBuildersFromGenerateDtos`) read `AnalyzerConfigOptions`, which only see MSBuild
properties declared `<CompilerVisibleProperty>` — plumbing that belongs in the same
`buildTransitive` targets infrastructure `FacetEfDesignTime` established.

## Modernization checklist (when this graduates from archive to feature)

1. Delete `EfJsonReader`/`ModelRoot`; read `EfModelManifest` (needs the reader exposed to
   this project — shared-source or `InternalsVisibleTo`, same pattern as the test hoist).
2. Extend the manifest writer with `keys` (additive, no version bump); fluent generator
   reports a FAC-family stale-manifest diagnostic when it is absent. Replace the
   `GetByIdAsync` `"Id"` literal with real key metadata (composite keys included).
3. Resolve navigation targets/cardinality from Roslyn symbols using the manifest's `nav`
   names; report unresolvable names as stale-manifest rather than guessing.
4. Re-host as an SGF `IncrementalGenerator` beside `GenerateDtosGenerator`; replace the
   debug-file/`FacetEfError.g.cs` machinery with scoped FAC1xx diagnostics that stay silent
   in projects that don't use the feature.
5. Builder-emission driver + single enable knob per the open decision above;
   `CompilerVisibleProperty` plumbing in buildTransitive targets.
6. Finish the inherited gaps recorded in RESURRECTION.md (SelectorsEmitter placeholders,
   terminal `NotImplementedException`s, generated-file ordering).
