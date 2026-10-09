# Layered Architecture

A worked example of the problem in [#143](https://github.com/Tim-Maes/Facet/issues/143): shaping an
API contract with Facet without the contract assembly referencing the layer it was shaped from.

See [docs/22_ContractAssemblies.md](../../docs/22_ContractAssemblies.md) for the reference material.

## Running it

Open `LayeredArchitecture.slnx`, set **Api.Host** as the startup project, and F5. Or from the
command line:

```bash
cd samples/LayeredArchitecture
dotnet run --project Api.Host
```

Nothing to pack or restore by hand — the sample references the Facet projects directly, so it
builds with the repository.

Two consequences of that worth knowing, both visible in the solution and the project files:

- **Facet's own projects are in this solution**, under a folder named for the purpose. They have to
  be: Visual Studio restores at solution scope, so a `ProjectReference` to a project outside the
  solution fails with `NU1105`. The command line does a full graph restore and would not have
  told you.
- **`Facet.props` and `Facet.targets` are imported by hand** in `Api.Contracts.csproj` and
  `Api.Mappers.csproj`. A `PackageReference` imports them for you; a `ProjectReference` does not.
  Each import carries a comment saying so, which keeps the rest of those files to the properties a
  real consumer actually writes.

## The four projects

```
Application          UserUseCaseModel, AddressUseCaseModel, OrderUseCaseModel
   ▲         ▲         + EF Core attributes, to show what does not get copied
   │         │
Api.Contracts│       UserResponse, AddressResponse, OrderResponse
   ▲         │         compile-time reference only; nothing survives into the output
   │         │
   └── Api.Mappers   the generated mappers + custom mapping; references both
          ▲
          │
       Api.Host      a console app standing in for a web API
```

`Application` is the layer `Api.Contracts` must not depend on. `Api.Contracts` keeps a *build-time*
project reference to it, because the generator has to read the source type's members to shape the
facet — but `PrivateAssets="all" Private="false"` stops it flowing or being copied, and nothing in
the emitted metadata refers to it.

## What each piece demonstrates

**`Api.Contracts/Api.Contracts.csproj`** — the two properties that do the work:
`Facet_GenerateMappings=false` drops the generated half whose constructor, `FromSource` and
`Projection` each name an `Application` type; `Facet_EmitAttributeMetadata=false` keeps `[Facet]`
out of metadata, so the `typeof(...)` inside it stops pinning a reference.

**`Api.Contracts/Contracts.cs`** — `NestedFacets` is the part that is easy to miss. Without it
`Address` is generated as `AddressUseCaseModel?` and the dependency returns through the property
type alone. FAC116 reports that if you forget; delete the `NestedFacets` argument and rebuild to see
it.

**`Api.Mappers/Registrations.cs`** — one `[assembly: FacetMappersForAssembly]` line registers a
mapper for every facet `Api.Contracts` declares, read from the contract manifest that assembly
records. Add a facet there and its mapper appears here with no edit; anything left uncovered is
FAC115. The explicit `[assembly: FacetMapper]` below it overrides one pair, to attach custom mapping
and the reverse direction.

**`Api.Mappers/UserMapping`** — the imperative/expression split. `ConfigureProjection` is an
expression, so it is merged into `Projection`'s object initializer and translated:

```sql
SELECT "u"."Id", "u"."Name", ..., "u"."Name" || ' #' || CAST("u"."Id" AS TEXT)
FROM "Users" AS "u" ...
```

`AfterMap` is imperative, so it runs in `ToUserResponse` and cannot be in the query. The host prints
`ADA #7` for the in-memory path and `Ada #7` for the query path, which is that difference.

This configuration type lives in `Api.Mappers`, which is why it can name both
`UserUseCaseModel` and `UserResponse` in one signature. The same configuration on `[Facet]` would
have to sit in `Api.Contracts` and would reference `Application` from there — and would in any case
never run, because the members that call it are the suppressed ones (FAC117).

**`Api.Host/Program.cs`** — section 4 reads its own `Api.Contracts.dll` metadata and prints the
assembly references, so the result is demonstrated rather than asserted:

```
System.Collections
System.Runtime
```

`System.Collections` is `List<T>`; `System.Runtime` is everything else. No `Application` and no
`Facet.Attributes` — the contract depends on the framework and nothing more, while still being
generated from a type it cannot see.

The `Application` project carries EF Core attributes (`[Column]`, `[Precision]`) on purpose, to show
they stay where they are. This sample does not set `CopyAttributes`, so nothing is copied at all; if
you turn it on, see `CopyAttributesMode` for keeping package attributes out of a contract.

## Mapping back

There are two reverse mappings, and the difference between them is identity.

`ToUserUseCaseModel` **constructs** a new source, so it carries back only the flat members. A
nested child would have to be a brand-new object, which an ORM reads as an insert with the previous
child left an orphan — so it is not attempted, and FAC118 says so at build time rather than leaving
you to find out.

`ApplyTo` **writes onto an instance you already have**, which is what an update actually needs:

```csharp
var tracked = db.Users.Single(u => u.Id == id);
response.ApplyTo(tracked);          // same entity, same child objects, new values
db.SaveChanges();
```

A nested member is updated in place where the target already has one, so the child keeps its
identity. Where the target's child is null it stays null — creating it is a decision about the
aggregate, not about mapping. Collections are never reconciled, because deciding what an added or
removed element means needs a key and a policy. Section 3 of the host prints all three behaviours.

`ApplyTo` is also generated when the source type has no parameterless constructor, where
`ToUserUseCaseModel` cannot be (FAC112) — a type that is only ever updated needs no change to
support it.

For anything beyond that, `ToSourceConfiguration` runs last in both and is where a policy belongs:

```csharp
[assembly: FacetMapper(typeof(UserUseCaseModel), typeof(UserResponse),
    GenerateToSource = true,
    ToSourceConfiguration = typeof(UserReverseMapping))]

public sealed class UserReverseMapping
    : IFacetToSourceConfiguration<UserResponse, UserUseCaseModel>
{
    public static void Map(UserResponse facet, UserUseCaseModel target) =>
        target.Address = facet.Address is null
            ? null
            : new AddressUseCaseModel { Street = facet.Address.Street, City = facet.Address.City };
}
```
