# Contract Assemblies

How to shape an API contract with Facet without the contract assembly referencing the layer it was
shaped from — the configuration a clean/onion/hexagonal architecture wants, and the subject of
[#143](https://github.com/Tim-Maes/Facet/issues/143).

## The problem

```csharp
// Api.Contracts, which is supposed to be dependency-free
[Facet(typeof(UserUseCaseModel))]
public partial class UserResponse;
```

This needs a project reference to the application layer, because the generator has to read the
source type's members to shape the facet. Three separate things then write that dependency into
the compiled `Api.Contracts.dll`:

| What | Why it references the source assembly |
|---|---|
| `[Facet(typeof(UserUseCaseModel))]` | A `typeof` is serialised into the custom attribute blob. |
| The generated `*.Mappings.g.cs` | `UserResponse(UserUseCaseModel)`, `FromSource` and `Projection` all name the source type. |
| Generated property types | A reference-typed property is copied with the *source's* type, e.g. `AddressUseCaseModel? Address`. |

Close all three and the compiler drops the now-unused reference from metadata on its own.

## Closing them

```xml
<PropertyGroup>
  <!-- do not emit the *.Mappings.g.cs half -->
  <Facet_GenerateMappings>false</Facet_GenerateMappings>
  <!-- do not emit Facet's attributes, and so not the typeof() inside them -->
  <Facet_EmitAttributeMetadata>false</Facet_EmitAttributeMetadata>
</PropertyGroup>

<ItemGroup>
  <!-- compile-time only: not copied, not flowed to consumers -->
  <ProjectReference Include="..\Application\Application.csproj"
                    PrivateAssets="all" Private="false" />
</ItemGroup>
```

`NestedFacets` closes the third, by rewriting nested property types to the facet types:

```csharp
[Facet(typeof(AddressUseCaseModel))]
public partial class AddressResponse;

[Facet(typeof(UserUseCaseModel), NestedFacets = new[] { typeof(AddressResponse) })]
public partial class UserResponse;
```

Without it, `UserResponse.Address` is typed `AddressUseCaseModel?` and the reference comes straight
back. **FAC116** reports that, so it is a build warning rather than something to find by reading
metadata.

The result is an assembly whose only references are to the framework:

```
Api.Contracts.dll  AssemblyRefs: System.Runtime, System.Collections
```

## Where the mapping goes

`Facet_GenerateMappings=false` removes the mapping, it does not relocate it. The mapping has to be
written in a project that references both layers — a mappers project that depends on the
application layer and on the contracts.

A C# partial type cannot span assemblies, so the mapping cannot be another half of the facet type.
It has to be something free-standing — which is what `[assembly: FacetMapper]` generates:

```csharp
// in the mappers project, which references both layers
[assembly: FacetMapper(typeof(UserUseCaseModel), typeof(UserResponse))]
```

That emits a static `UserResponseMapper` carrying `Projection`, `ToUserResponse`,
`SelectUserResponse` and optionally the reverse direction. Nested registrations are inlined into one
expression, so a query still projects the whole graph in a single statement. See
[Assembly-Level Mapper Registration](03_AttributeReference.md#assembly-level-mapper-registration)
for the full surface, the hooks, and the diagnostics.

You can equally write the mapper by hand. Facet's generated facets are built for it: their members
are settable and the generated projection is an object initializer, so a hand-written static class
can both populate one and project to one in a query.

## How the attributes are kept out of metadata

Facet's attributes are `[Conditional(FacetConditionalSymbols.KeepAttributes)]`. The generator reads
them from the semantic model, which happens whether or not they are emitted — but emitting one
writes its `typeof` argument into the attribute blob, which is what pins the reference.

`Facet.Attributes.targets` defines that symbol by default, so existing projects are unchanged.
Setting `Facet_EmitAttributeMetadata` to `false` drops the attributes from metadata, and the
compiler then elides the references only those arguments were keeping alive.

## What you give up

**The reflection-based shorthands.** `Facet.Extensions`' `ToSource<T>()` and `BackTo<T>()` resolve
the source type by reading `Facet.FacetAttribute` at runtime. With the attributes out of metadata
there is nothing to read. `ToFacet<T>()` is unaffected, as it goes via `FromSource` or the
constructor — neither of which a contract assembly has either, so in practice a contract assembly
is mapped from the outside regardless.

**Mapping hooks on `[Facet]`.** `Configuration`, `BeforeMapConfiguration`, `AfterMapConfiguration`
and `ToSourceConfiguration` are invoked from the constructor, `FromSource` and `ToSource` — all in
the suppressed half. Setting one in a contract assembly compiles and does nothing, which **FAC117**
reports. Custom mapping belongs in the mappers project alongside the mapping it customises.

**It is per-project, not per-type.** Both properties are `DefineConstants` and generator
configuration for the whole compilation.

## Reference

| Property | Default | Effect |
|---|---|---|
| `Facet_GenerateMappings` | `true` | `false` suppresses `*.Mappings.g.cs`. Needs `Facet_SplitGeneratedFiles` (also default `true`). |
| `Facet_EmitAttributeMetadata` | `true` | `false` keeps Facet's attributes out of the compiled assembly. |

| Diagnostic | Severity | Meaning |
|---|---|---|
| FAC116 | Warning | A generated property is still typed from the layer the facet was shaped from. |
| FAC117 | Warning | A mapping hook is configured on a facet whose mappings are suppressed, so it cannot run. |

See also [Generated Files Output](12_GeneratedFilesOutput.md) and
[Global Configuration Defaults](21_GlobalConfigurationDefaults.md).
