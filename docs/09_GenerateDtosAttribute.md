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

## Obsolete: GenerateAuditableDtos Attribute

> **?? Deprecated:** The `[GenerateAuditableDtos]` attribute has been replaced by `[GenerateDtos]` with `ExcludeAuditFields = true`. The old attribute will be removed in a future version.
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
