# GenerateDtos Attribute Reference

The `[GenerateDtos]` attribute automatically generates standard CRUD DTOs (Create, Update, Response, Query, Upsert, Patch) for domain models, eliminating the need to manually write repetitive DTO classes.

## GenerateDtos Attribute

Generates standard CRUD DTOs for a domain model with full control over which types to generate and their configuration.

### Usage

```csharp
[GenerateDtos(Types = DtoTypes.All, OutputType = OutputType.Record)]
public class User
{
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string Email { get; set; }
    public string Password { get; set; }
}
```

### Parameters

| Parameter             | Type        | Description                                                           |
|----------------------|-------------|-----------------------------------------------------------------------|
| `Types`              | `DtoTypes`  | Which DTO types to generate (default: All).                         |
| `OutputType`         | `OutputType`| The output type for generated DTOs (default: Record).               |
| `Namespace`          | `string?`   | Custom namespace for generated DTOs (default: same as source type). |
| `ExcludeProperties`  | `string[]`  | Properties to exclude from all generated DTOs.                      |
| `ExcludeAuditFields` | `bool`      | Automatically exclude common audit fields (default: false). See [Excluding Audit Fields](#excluding-audit-fields). |
| `ExcludeNavigationProperties` | `bool` | Shape DTOs to exactly what the EF model maps as data. Defaults to **true when an EF model manifest is wired into the project's `<AdditionalFiles>`**, false otherwise; an explicit value wins either way. See [Excluding Navigation Properties](#excluding-navigation-properties). |
| `IncludeProperties`  | `string[]`  | Properties kept in every generated DTO regardless of any exclusion — the escape hatch for aggregate children the EF model designates as navigations. |
| `Prefix`             | `string?`   | Custom prefix for generated DTO names.                              |
| `Suffix`             | `string?`   | Custom suffix for generated DTO names.                              |
| `IncludeFields`      | `bool`      | Include public fields from the source type (default: false).        |
| `GenerateConstructors`| `bool`     | Generate constructors for the DTOs (default: true).                 |
| `GenerateProjections`| `bool`      | Generate projection expressions for the DTOs (default: true).       |
| `ConvertEnumsTo`     | `Type?`     | Convert enum properties to `typeof(string)` or `typeof(int)` (default: null). |
| `UseFullName`        | `bool`      | Use full type name in generated file names to avoid collisions (default: false). |

### DtoTypes Enum

| Value    | Description                           |
|----------|---------------------------------------|
| `None`   | No DTOs generated                     |
| `Create` | DTO for creating new entities         |
| `Update` | DTO for updating existing entities    |
| `Response` | DTO for API responses               |
| `Query`  | DTO for search/filtering operations   |
| `Upsert` | DTO for create-or-update operations   |
| `Patch`  | DTO for partial updates with Optional&lt;T&gt; |
| `All`    | Generate all DTO types                |

### OutputType Enum

| Value         | Description              |
|---------------|--------------------------|
| `Class`       | Generate as classes      |
| `Record`      | Generate as records      |
| `Struct`      | Generate as structs      |
| `RecordStruct`| Generate as record structs |
| `Interface`   | Generate as interfaces declaring entity-mapped properties as get-only members. See [Interface Output](#interface-output). |
| `Partial`     | **Modifier, not a kind**: emits every requested kind as `partial` (constructors kept, projections and `ToSource`/`BackTo` omitted) so a hand-written partial half can extend it. Composes with any kind, including `Interface`. See [Partial Class Output](#partial-class-output). |
| `PartialClass`| Back-compat alias for `Class \| Partial`. Prefer composing the `Partial` modifier explicitly. |

## Interface Output

Setting `OutputType = OutputType.Interface` emits the DTO as an **interface** declaring each entity-mapped property as a get-only member, rather than a concrete class/record/struct. This is useful when you want compile-time enforcement that a hand-written DTO covers all the entity's properties — without giving up control over the DTO's own shape (construction syntax, validation attributes, extra non-entity fields).

### Usage

```csharp
[GenerateDtos(Types = DtoTypes.Update, OutputType = OutputType.Interface)]
public class User
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string? Email { get; set; }
    public bool IsActive { get; set; }
}
```

This generates:

```csharp
public interface IUpdateUserRequest
{
    int Id { get; }
    string Name { get; }
    string? Email { get; }
    bool IsActive { get; }
}
```

### Naming

Interface output prepends an `I` to the generated name, following C# convention. Any `Prefix` you supply sits between the `I` and the entity name:

| Configuration | Generated name |
|---------------|----------------|
| `OutputType = OutputType.Interface` | `IUpdateUserRequest` |
| `OutputType = OutputType.Interface, Prefix = "Admin"` | `IAdminUpdateUserRequest` |
| `OutputType = OutputType.Interface, Suffix = "Contract"` | `IUpdateUserRequestContract` |

### What is (and isn't) emitted

Interfaces declare contract, not behavior, so on interface output the generator emits **only** the property declarations. The following are intentionally **not** emitted:

- Constructors (interfaces can't declare them)
- `Projection` expressions and `FromSource` mappings
- `ToSource` / `BackTo` methods
- The `[Facet]` attribute (it drives runtime mapping on the concrete type and is meaningless on an interface)

Properties are emitted as `{ get; }` only — the implementer chooses whether to back them with `get;`, `get; set;`, `get; init;`, or `required`.

### Patch DTOs

`DtoTypes.Patch` is **skipped** under `OutputType.Interface`. Patch DTOs rely on `Optional<T>` and an `ApplyTo` method whose body must live on a concrete type. If you request `Types = DtoTypes.All` with interface output, every DTO type except `Patch` will be generated.

### When to use it

Use `OutputType.Interface` when you want the generator to act as a **contract producer** rather than a DTO producer. The canonical scenario:

1. The entity has the canonical shape (and grows over time).
2. You write the DTOs by hand — typically as positional records with validation attributes, custom constructors, or extra request-only fields.
3. You want the build to fail the moment an entity property is added but not propagated to the DTO.

```csharp
// Entity declares the contract producer
[GenerateDtos(Types = DtoTypes.Update, OutputType = OutputType.Interface)]
public class User
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string? Email { get; set; }
    public bool IsActive { get; set; }
}

// Hand-written positional record satisfies the generated contract.
// Adding a property to User without updating this record is now a compile error.
public sealed record UpdateUserRequest(
    int Id,
    [Required] string Name,
    string? Email,
    bool IsActive) : IUpdateUserRequest;
```

If you instead want the generator to own the DTO outright — including constructors, projections, and mapping — use `OutputType.Class`, `OutputType.Record`, `OutputType.Struct`, or `OutputType.RecordStruct`.

### Mocking in tests

Interface output — and the automatic interface linking on concrete outputs — pays off in test code. When services and handlers accept the generated interface (`IUpdateUserRequest`) instead of the concrete DTO, tests can supply a mock (Moq, NSubstitute, etc.) and stub only the properties a given test cares about, instead of constructing a full request object and keeping that construction site in sync as the entity grows:

```csharp
var request = new Mock<IUpdateUserRequest>();
request.SetupGet(r => r.Name).Returns("renamed");

await handler.Handle(request.Object);
```

Because the interface is regenerated from the entity, this stays compile-time-checked: adding a property to the entity flows into the interface, and any hand-written implementations fail to build until they cover it — while mock-based tests keep working untouched unless they need the new property. This is often the main reason teams maintain per-DTO interfaces at all; generating them removes that boilerplate without giving up the mockability.

## Partial Class Output

Setting `OutputType = OutputType.PartialClass` emits the DTO as a `public partial class` (not sealed) with get/set properties and the same constructors as `OutputType.Class`, but **without** the `Projection` expression, `ToSource`, or `BackTo` methods. The intent is for callers to extend the DTO with their own hand-written partial in the same project — adding validation attributes, computed members, custom mapping, or extra request-only fields — without giving up the generator-emitted property surface or constructors.

### Usage

```csharp
[GenerateDtos(Types = DtoTypes.Update, OutputType = OutputType.PartialClass)]
public class User
{
    public int Id { get; set; }
    public string Name { get; set; }
    public bool IsActive { get; set; }
}
```

This generates:

```csharp
[Facet.Facet(typeof(User))]
public partial class UpdateUserRequest
{
    public int Id { get; set; }
    public string Name { get; set; } = default!;
    public bool IsActive { get; set; }

    public UpdateUserRequest(User source)
    {
        this.Id = source.Id;
        this.Name = source.Name;
        this.IsActive = source.IsActive;
    }

    public UpdateUserRequest() { }
}
```

You then add a sibling partial file with whatever the generator can't (or shouldn't) own:

```csharp
public partial class UpdateUserRequest
{
    [Required, MinLength(2)]
    public string Name { get; set; } = default!; // overrides the generated declaration via the partial

    // Extra non-entity field
    public string? CorrelationId { get; set; }

    public string DisplayLabel => $"{Id}: {Name}";
}
```

### What is (and isn't) emitted

`OutputType.PartialClass` emits:

- A `public partial class` declaration (the `partial` keyword is the only structural difference from `OutputType.Class`)
- All entity-mapped properties as `public { get; set; }`
- The source-copy constructor (`new XDto(SourceEntity source)`) — with `[SetsRequiredMembers]` when any property is `required`
- The parameterless constructor
- The `[Facet]` attribute

The following are intentionally **not** emitted (in contrast to `OutputType.Class`):

- The `Projection` expression
- `FromSource` factory
- `ToSource` / `BackTo` methods

The rationale: a hand-written partial may add members the generator can't see, so a generator-owned mapping would be incomplete. Callers who want full mapping should use `OutputType.Class` instead; callers who want extensibility own the mapping themselves.

### Not sealed

`OutputType.PartialClass` deliberately does not seal the emitted class so it can serve as a shared base for hand-written derived types — useful when several DTOs share most of an entity's shape but differ in a few fields (e.g. `GlobalSoftware` / `LocalSoftware` extending a generated `SoftwareDto`).

### Composing with `OutputType.Interface`

When the same entity generates **both** an `OutputType.Interface` output and a concrete output (`Class`, `Record`, `Struct`, `RecordStruct`, or `PartialClass`) with overlapping `DtoTypes` — whether from two attributes or from one flags-combined `OutputType` — the concrete type declares the matching generated interface as a base, pairing the two outputs into a contract + implementation set automatically. Records, structs, and record structs can all implement interfaces, so every concrete kind participates.

```csharp
[GenerateDtos(Types = DtoTypes.Update, OutputType = OutputType.Interface)]
[GenerateDtos(Types = DtoTypes.Update, OutputType = OutputType.PartialClass)]
public class User
{
    public int Id { get; set; }
    public string Name { get; set; }
}
```

Generates:

```csharp
public interface IUpdateUserRequest
{
    int Id { get; }
    string Name { get; }
}

public partial class UpdateUserRequest : IUpdateUserRequest
{
    public int Id { get; set; }
    public string Name { get; set; } = default!;
    // ... constructors as above
}
```

The match requires equal `Prefix`, `Suffix`, and `Namespace` between the two attributes. Bits in `DtoTypes` that aren't shared are not coupled — e.g. an Interface attribute covering `Create | Update` paired with a PartialClass attribute covering `Update | Response` produces an `IUpdateUserRequest` interface and partial class only for `Update`; `Create` is interface-only and `Response` is a plain (unimplemented) partial.

### One attribute, several outputs: flags-combined `OutputType`

`OutputType` is a `[Flags]` enum (like `Types`) with two categories of bits: **kinds** (`Class`, `Record`, `Struct`, `RecordStruct`, `Interface`) that select what to emit, and one **modifier** (`Partial`) that applies to every selected kind. When paired attributes would be identical except for the output shape, collapse them by OR-ing — the attribute expands into one output per kind bit (each carrying the modifier), sharing every other option, and the interface pairing above applies exactly as if separate attributes had been written:

```csharp
[GenerateDtos(Types = DtoTypes.Update,
    OutputType = OutputType.Interface | OutputType.PartialClass)]
public class User
{
    public int Id { get; set; }
    public string Name { get; set; }
}
```

This generates the same `IUpdateUserRequest` + `UpdateUserRequest : IUpdateUserRequest` pair as the two-attribute example above (`PartialClass` is the back-compat alias for `Class | Partial`). Any concrete kind pairs the same way — `Interface | Record` yields `public record UpdateUserRequest : IUpdateUserRequest`, and `Interface | RecordStruct` a record struct implementing it.

Because `Partial` is a modifier, it composes with every kind:

```csharp
// One attribute: a partial record implementing a partial interface.
// Both halves are user-extensible — hand-written partials can add validation
// attributes, computed members, or extra contract members.
[GenerateDtos(Types = DtoTypes.Update,
    OutputType = OutputType.Interface | OutputType.Record | OutputType.Partial)]
```

emits `public partial interface IUpdateUserRequest` and `public partial record UpdateUserRequest : IUpdateUserRequest`. A partial concrete kind keeps its generated constructors but omits `Projection`/`ToSource`/`BackTo` (a hand-written half may add members the generator can't see, so generator-owned mapping would be silently incomplete). `Interface | Partial` on its own makes the generated *contract* extensible — a hand-written `partial interface` half can add members that implementations must then satisfy.

Combining multiple **concrete** kinds (e.g. `Class | Record`) is rejected at compile time with **error FAC101**: both bits would generate identically-named types (`UpdateUserRequest`) and collide. The `Interface` output carries an `I` prefix, so `Interface` composes with exactly one concrete kind. Setting `Partial` with **no** kind bits is rejected with **error FAC102** — a modifier with nothing to modify is more likely a mistake than an intentional no-op. A FAC101/FAC102 on one attribute doesn't affect other `[GenerateDtos]` attributes on the same type — their outputs still generate.

### Patch DTOs

`DtoTypes.Patch` is generated normally under `OutputType.PartialClass` — the patch DTO is emitted as `partial class` with its `ApplyTo` method, and a hand-written partial can extend it like any other DTO type.

### When to use it

Pick `OutputType.PartialClass` when:

- You want the generator to own the property surface and constructors, but reserve the right to extend them.
- You want a shared, unsealed base for several derived DTOs.
- You want to layer hand-written validation attributes or computed members onto a generated DTO without forking the generator's output.

If you don't need extensibility, prefer `OutputType.Class` — it also emits `Projection`, `ToSource`, and `BackTo` for full round-tripping.

## Assembly-level generation: `[GenerateDtosFor]`

A source generator can only emit code into the compilation it runs in, so `[GenerateDtos]` on an entity pins the generated DTOs to the **entity's assembly** — even when the `Namespace` option names a downstream layer, the types physically live upstream and the namespace is a cross-assembly fiction. The assembly-level counterpart puts the Request/Response types where most solutions actually want them: **in the Web project, next to the controllers that bind them** — where a Request can hydrate itself from the `DbContext`, and a Response can be enriched with in-memory application state that was never persisted to the database.

A typical layered solution, with references flowing toward the domain:

```text
MyApp.Domain          Schedule, Order — plain entity classes.
                      References nothing below. No Facet attributes needed here.

MyApp.Persistence     AppDbContext + migrations.
                      References: MyApp.Domain

MyApp.Web             Controllers + the request/response DTOs.
                      References: MyApp.Persistence (and therefore MyApp.Domain)
                      → [assembly: GenerateDtosFor(...)] is declared HERE,
                        and the DTOs are generated HERE.
```

(Substitute a dedicated contracts project for `MyApp.Web` if you keep wire types separate — the rule is simply: declare the attribute in the project where the DTOs should live.)

```csharp
// In MyApp.Web:
[assembly: GenerateDtosFor(typeof(Schedule),
    Types = DtoTypes.Create | DtoTypes.Update,
    OutputType = OutputType.Interface | OutputType.Record | OutputType.Partial,
    Namespace = "MyApp.Web.Contracts.V1.Requests")]
```

The source entity is read as metadata from the referenced assembly and needs no attribute of its own. All `[GenerateDtos]` options apply, including flags-combined `OutputType`, the `Partial` modifier, and FAC101/FAC102 validation. Declare one `[assembly: GenerateDtosFor(...)]` per entity; interface/concrete sibling pairing links outputs **per source entity** — two entities registered in the same assembly never cross-pair.

### What moving the declaration downstream changes

- **The dependency arrow points the right way.** The Web project references the domain, and entity classes stay plain C#: no `[GenerateDtos]` attributes, no downstream namespace strings. If no other Facet attributes remain in the domain project, it can drop the generator reference entirely — generation then runs in the (typically much smaller) Web compilation instead of your largest project.
- **Types land in the assembly that owns their namespace.** Anything that discovers types by *assembly* — OpenAPI generators, TypeScript exporters, reflection-based registration, `InternalsVisibleTo` — sees the generated DTOs exactly where hand-written ones would have been. Replacing a hand-written contract with a generated one becomes a true drop-in: same assembly, same namespace, same name.
- **Each layer declares its own shapes.** Several downstream assemblies can independently register DTOs for the same entity (the Web project's request bodies, an application layer's command payloads) without the entity accumulating one attribute per consumer.

### Partial halves gain the downstream dependency graph

This is the quiet superpower of combining `GenerateDtosFor` with the `Partial` modifier. A `partial` type must be completed within a single assembly, so a hand-written half lives in whichever assembly the generated half lives in. With the class-level attribute that means the **entity's** assembly — which, in the hierarchy above, cannot reference `AppDbContext`, repositories, or Web services (references flow toward the domain, never away from it). Members that need those types simply cannot be written there.

Declared in the Web project, the generated half compiles there — and the hand-written half beside it can use everything the Web project references, in both directions of hydration:

```csharp
// MyApp.Web — same assembly as the generated partial halves

public partial record UpdateScheduleRequest : IValidatableObject
{
    // Request → entity: hydrate from the real database.
    public async Task<Schedule> ApplyAsync(AppDbContext db, CancellationToken ct)
    {
        var schedule = await db.Schedules.FirstAsync(s => s.Id == Id, ct);
        // ... map members onto the tracked entity ...
        return schedule;
    }

    // Framework-specific validation, using types the domain shouldn't know:
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (StartAt >= EndAt)
            yield return new ValidationResult("Start must precede end.", [nameof(StartAt)]);
    }
}

public partial record GetScheduleResponse
{
    // Response ← runtime: enrich with in-memory state that is not persisted in
    // the database — a running-job tracker, a cache, a hub connection count.
    public bool IsRunningNow { get; private set; }

    public void Hydrate(IScheduleRunner runner)
        => IsRunningNow = runner.IsRunning(Id);
}
```

Extension methods in a downstream layer were always possible; what same-assembly partials add is members **on the type itself** — instance methods and async factories that take a `DbContext` or services as parameters, extra properties fed from runtime state, interface implementations (`IValidatableObject` and friends, which extension methods can never provide), and attributes applied to the type through the partial half.

## Excluding Audit Fields

Use the `ExcludeAuditFields` property to automatically exclude common audit/tracking fields from the generated DTOs.

When `ExcludeAuditFields = true`, the following fields are automatically excluded:
- `CreatedDate`, `UpdatedDate`
- `CreatedAt`, `UpdatedAt`
- `CreatedBy`, `UpdatedBy`
- `CreatedById`, `UpdatedById`

### Usage

```csharp
[GenerateDtos(Types = DtoTypes.Create | DtoTypes.Update, ExcludeAuditFields = true)]
public class AuditableEntity
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public DateTime CreatedAt { get; set; }    // Will be excluded
    public DateTime UpdatedAt { get; set; }    // Will be excluded
    public string CreatedBy { get; set; }      // Will be excluded
    public string UpdatedBy { get; set; }      // Will be excluded
}
```

You can combine `ExcludeAuditFields` with `ExcludeProperties` to exclude additional properties:

```csharp
[GenerateDtos(ExcludeAuditFields = true, ExcludeProperties = new[] { "InternalNotes", "SecretKey" })]
public class Product
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string InternalNotes { get; set; }  // Will be excluded
    public string SecretKey { get; set; }      // Will be excluded
    public DateTime CreatedAt { get; set; }    // Will be excluded (audit field)
}
```

## Excluding Navigation Properties

EF Core entities carry navigation properties (`Tenant? Owner`, `List<Order> Orders`, …) that
don't belong in wire DTOs — copied as-is they bring raw entity types, serializer cycles, and
accidental graph exposure. Manifest shaping generates DTOs from **exactly what your EF model
maps as data** — and it is the default for every `[GenerateDtos]` attribute in a project that
wires an EF model manifest into its `<AdditionalFiles>` (`ExcludeNavigationProperties` set
explicitly wins in either direction):

```csharp
[GenerateDtos(Types = DtoTypes.Create | DtoTypes.Update)]
public class Schedule
{
    public int Id { get; set; }
    public int? TenantId { get; set; }         // kept   — mapped scalar column
    public Tenant? OwnerTenant { get; set; }   // dropped — navigation
    public List<Job> Jobs { get; } = new();    // dropped — collection navigation
}
```

**Kept**: scalar columns, complex/value-object members, primitive collections — including
entity-shaped classes the model maps through a value converter.
**Dropped**: reference and collection navigations, many-to-many skip navigations, owned
references, `[NotMapped]`/`Ignore(...)` members, and anything else the model has no mapping
for. When an entity-typed member genuinely belongs in the DTO — an owned collection edited
together with its parent — `IncludeProperties = new[] { nameof(Order.Lines) }` forces it back
in, winning over every exclusion (except the fixed convention that Create DTOs never carry
`Id`).

The shaping is driven by a **model manifest** (`{ContextName}.facetmodel.json`): a committed
JSON file generated from your `DbContext` model at design time and read by the generator as an
AdditionalFile. Setup is three steps, once — install the separate `Facet.Extensions.EFCore`
NuGet package into the `DbContext` project and set `<FacetEfDesignTime>true</FacetEfDesignTime>`
there, bootstrap the manifest with a `migrations add`/`remove` pair (no leftover migration),
and point `<AdditionalFiles>` at it — that last step is the switch that turns shaping on for
the whole project, and it is pre-wired when the attributes live in the `DbContext` project
itself (`FacetEfDesignTime` wires the project's own manifests automatically). The
**[Facet.Extensions.EFCore README](../src/Facet.Extensions.EFCore/README.md#shaping-dtos-with-your-ef-model-excludenavigationproperties)**
has the full walkthrough, the layered-solution diagram, and the programmatic writer for
`dotnet ef`-free workflows. After setup, the manifest refreshes automatically with every
migration.

A shaped source type with **no manifest entry is a compile error** (FAC105) that surfaces at
the attribute — never a silently mis-shaped DTO. One caveat under the default: a mistyped
`<AdditionalFiles>` glob matches nothing, so no manifest is wired and no shaping happens at
all. Pin `ExcludeNavigationProperties = true` on one representative entity to make that
failure loud too — the README walkthrough covers this. A source type that is not an EF entity
(a view model, a projection type) opts out with `ExcludeNavigationProperties = false` and
copies properties as-is.

### Diagnostics

Failures are compile-time diagnostics: **FAC105** (error) — a shaped type has no manifest
entry (stale manifest, a non-entity type that should opt out with
`ExcludeNavigationProperties = false`, or — for explicitly shaped types — a wrong
`<AdditionalFiles>` path); **FAC106** (warning) — a settable property (or get-only collection) on a
mapped entity is unknown to the manifest, i.e. added since it was last written — scaffold its
migration or mark it `[NotMapped]`; **FAC103/FAC104** (errors) — a manifest file is
malformed / from an incompatible package version and is ignored in full. FAC105/FAC106 anchor
to the attribute; FAC103/FAC104 are file-level (no source location). Escalate FAC106 in CI
with `<WarningsAsErrors>$(WarningsAsErrors);FAC106</WarningsAsErrors>` so a PR that changes
the model without regenerating the manifest can't merge green. Full table and remedies: the
[Facet.Extensions.EFCore README](../src/Facet.Extensions.EFCore/README.md#when-something-is-off-the-build-says-so)
and [Analyzer Rules](13_AnalyzerRules.md).

## Obsolete: GenerateAuditableDtos Attribute

> **⚠️ Deprecated:** The `[GenerateAuditableDtos]` attribute has been replaced by `[GenerateDtos]` with `ExcludeAuditFields = true`. The old attribute will be removed in a future version.
>
> **Migration:**
> ```csharp
> // Old way (deprecated):
> [GenerateAuditableDtos(Types = DtoTypes.Create)]
> 
> // New way:
> [GenerateDtos(Types = DtoTypes.Create, ExcludeAuditFields = true)]
> ```

## Multiple Attribute Usage

The attribute supports multiple applications for fine-grained control:

```csharp
[GenerateDtos(Types = DtoTypes.Response, ExcludeProperties = new[] { "Password", "InternalNotes" })]
[GenerateDtos(Types = DtoTypes.Upsert, ExcludeProperties = new[] { "Password" })]
public class User
{
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string Password { get; set; }
    public string InternalNotes { get; set; }
}
```

## Generated Files

The attributes generate separate files for each DTO type:

- `UserCreate.g.cs` - For creating new users
- `UserUpdate.g.cs` - For updating existing users
- `UserResponse.g.cs` - For API responses
- `UserQuery.g.cs` - For search operations
- `UserUpsert.g.cs` - For create-or-update operations
- `UserPatch.g.cs` - For partial updates (HTTP PATCH)

When `UseFullName = true`, file names include the full namespace to prevent collisions.

## Patch DTOs for Partial Updates

Patch DTOs are designed for HTTP PATCH scenarios where you need to update only specific fields. They use the `Optional<T>` type to distinguish between three states:

1. **Unspecified** - Property not included in the update
2. **Explicitly Null** - Property should be set to null
3. **Has Value** - Property should be updated to the specified value

### Wire format: JSON Merge Patch (RFC 7396)

The generator gives Patch DTOs merge-patch wire semantics automatically, for **both** JSON stacks — each gated on the consuming compilation actually referencing it, so projects without either still compile:

- **System.Text.Json**: a `[JsonConverter]` + `[JsonIgnore(WhenWritingDefault)]` pair on every generated property, plus an internal converter factory generated into the consuming assembly.
- **Newtonsoft.Json**: a `[JsonConverter]` + `[JsonProperty(DefaultValueHandling = Ignore)]` pair, plus an internal Json.NET converter — because ASP.NET Core apps using `AddNewtonsoftJson` bind MVC request bodies through Json.NET, where System.Text.Json attributes are invisible.

Both serializers honor per-property converter attributes, so **no serializer or MVC startup registration is needed** — the DTOs are self-describing. Facet.Attributes takes no package dependency on either library (the converters are generated, the same trick strongly-typed-ID libraries use).

| JSON payload | `Optional<T>` state | Effect of `ApplyTo` |
|---|---|---|
| property absent | Unspecified (`HasValue == false`) | not touched |
| `"email": null` (nullable target) | Specified null | set to `null` |
| `"isActive": null` (non-nullable value type) | — | `JsonException` → HTTP 400 in ASP.NET Core |
| `"name": "x"` | Specified value | set to `"x"` |

The mechanics: System.Text.Json never invokes a converter for an **absent** property — the field keeps `default(Optional<T>)`, i.e. unspecified. A **present** property always routes through the converter and becomes specified, including explicit null. Serialization skips unspecified properties, so a round-trip never clobbers fields the sender didn't mention.

**Typed clients**: in TypeScript, `undefined` is the "don't touch" value — `JSON.stringify` omits `undefined`-valued keys entirely, so a client type of `email?: string | null` expresses all three states with no sentinel values.

**Known limitation**: nullable-reference annotations are erased at runtime, so an explicit null into `Optional<string>` (non-nullable reference) deserializes as a specified null rather than failing — only non-nullable *value types* get the automatic 400. Validate reference-type nulls server-side where it matters.

### Usage Example

```csharp
[GenerateDtos(Types = DtoTypes.Patch)]
public class User
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string? Email { get; set; }
    public bool IsActive { get; set; }
    public DateTime? LastLoginAt { get; set; }
}
```

This generates a `UserPatch` DTO with all properties wrapped in `Optional<T>`:

```csharp
public class UserPatch
{
    public Optional<int> Id { get; set; }
    public Optional<string> Name { get; set; }
    public Optional<string?> Email { get; set; }
    public Optional<bool> IsActive { get; set; }
    public Optional<DateTime?> LastLoginAt { get; set; }
    
    public void ApplyTo(User target)
    {
        if (Id.HasValue) target.Id = Id.Value;
        if (Name.HasValue) target.Name = Name.Value;
        if (Email.HasValue) target.Email = Email.Value;
        if (IsActive.HasValue) target.IsActive = IsActive.Value;
        if (LastLoginAt.HasValue) target.LastLoginAt = LastLoginAt.Value;
    }
}
```

### Using Patch DTOs

```csharp
// Load existing entity
var user = await dbContext.Users.FindAsync(userId);

// Create patch with only the fields to update
var patch = new UserPatch
{
    Name = "Jane Doe",           // Update name
    IsActive = false,             // Deactivate user
    Email = new Optional<string?>(null)  // Explicitly set email to null
    // LastLoginAt is not set, so it won't be modified
};

// Apply the patch
patch.ApplyTo(user);
await dbContext.SaveChangesAsync();
```

### Implicit Conversion

`Optional<T>` supports implicit conversion for convenience:

```csharp
var patch = new UserPatch
{
    Name = "Jane Doe",  // Implicitly converted to Optional<string>
    IsActive = false    // Implicitly converted to Optional<bool>
};
```

## Enum Conversion

You can convert enum properties in generated DTOs the same way as with `[Facet]`, using `ConvertEnumsTo`.

```csharp
[GenerateDtos(Types = DtoTypes.Response, ConvertEnumsTo = typeof(string))]
public class Order
{
    public int Id { get; set; }
    public OrderStatus Status { get; set; }
}

// Generated DTO property:
// public string Status { get; set; }
```

### Distinguishing Null from Unspecified

```csharp
// Set email to null explicitly
patch.Email = new Optional<string?>(null);  // HasValue = true, Value = null

// Leave email unspecified
var patch2 = new UserPatch();
// patch2.Email.HasValue = false, email won't be modified
```

## Examples

### Basic Usage
```csharp
[GenerateDtos]
public class Product
{
    public string Name { get; set; }
    public decimal Price { get; set; }
    public string Description { get; set; }
}
```

### Selective Generation
```csharp
[GenerateDtos(Types = DtoTypes.Create | DtoTypes.Update, OutputType = OutputType.Class)]
public class Order
{
    public string OrderNumber { get; set; }
    public DateTime OrderDate { get; set; }
    public decimal Total { get; set; }
}
```

### Patch-Only DTO
```csharp
[GenerateDtos(Types = DtoTypes.Patch, OutputType = OutputType.Class)]
public class UserProfile
{
    public string DisplayName { get; set; }
    public string? Bio { get; set; }
    public string? AvatarUrl { get; set; }
}
```

### Custom Namespace and Naming
```csharp
[GenerateDtos(
    Namespace = "MyApp.Api.Contracts",
    Prefix = "Api",
    Suffix = "Dto",
    ExcludeProperties = new[] { "InternalId" }
)]
public class Customer
{
    public string Name { get; set; }
    public string Email { get; set; }
    public string InternalId { get; set; }
}
```

## Optional&lt;T&gt; Type

The `Optional<T>` type is a struct that wraps values and tracks whether they've been explicitly set. It's part of the `Facet` namespace and available for use in your own code.

### Properties and Methods

- `bool HasValue` - Indicates if a value has been set
- `T Value` - Gets the value (throws if `HasValue` is false)
- `T GetValueOrDefault(T defaultValue = default)` - Safely gets the value or a default
- Implicit conversion from `T` to `Optional<T>`
- Equality and comparison operators

### Example

```csharp
var optional1 = new Optional<string>("Hello");  // HasValue = true, Value = "Hello"
var optional2 = new Optional<string?>(null);    // HasValue = true, Value = null
var optional3 = new Optional<string>();         // HasValue = false

optional1.HasValue  // true
optional2.HasValue  // true - explicitly set to null
optional3.HasValue  // false - unspecified
```

---

See [Facet Attribute Reference](03_AttributeReference.md) for the basic `[Facet]` attribute documentation.
