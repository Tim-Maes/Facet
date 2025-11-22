# Facet Analyzer Rules

Facet includes comprehensive Roslyn analyzers that provide real-time feedback in your IDE. These analyzers catch common mistakes and configuration issues at design-time, before you even compile your code.

## Quick Reference

| Rule ID | Severity | Category | Description |
|---------|----------|----------|-------------|
| [FAC001](#fac001) | Error | Usage | Type must be annotated with [Facet] |
| [FAC002](#fac002) | Info | Performance | Consider using two-generic variant |
| [FAC003](#fac003) | Error | Declaration | Missing partial keyword on [Facet] type |
| [FAC004](#fac004) | Error | Usage | Invalid property name in Exclude/Include |
| [FAC005](#fac005) | Error | Usage | Invalid source type |
| [FAC006](#fac006) | Error | Usage | Invalid Configuration type |
| [FAC007](#fac007) | Warning | Usage | Invalid NestedFacets type |
| [FAC008](#fac008) | Warning | Performance | Circular reference risk |
| [FAC009](#fac009) | Error | Usage | Both Include and Exclude specified |
| [FAC010](#fac010) | Warning | Performance | Unusual MaxDepth value |
| [FAC011](#fac011) | Error | Usage | [GenerateDtos] on non-class type |
| [FAC012](#fac012) | Warning | Usage | Invalid ExcludeProperties |
| [FAC013](#fac013) | Warning | Usage | No DTO types selected |
| [FAC014](#fac014) | Error | Declaration | Missing partial keyword on [Flatten] type |
| [FAC015](#fac015) | Error | Usage | Invalid source type in [Flatten] |
| [FAC016](#fac016) | Warning | Performance | Unusual MaxDepth in [Flatten] |
| [FAC017](#fac017) | Info | Usage | LeafOnly naming collision risk |
| [FAC022](#fac022) | Warning | SourceTracking | Source entity structure changed |

---

## Extension Method Analyzers

### FAC001

**Type must be annotated with [Facet]**

- **Severity**: Error
- **Category**: Usage

#### Description

When using extension methods like `ToFacet<T>()`, `ToSource<T>()`, `SelectFacet<T>()`, etc., the target type must be annotated with the `[Facet]` attribute.

#### Bad Code

```csharp
// UserDto does NOT have [Facet] attribute
public class UserDto
{
    public int Id { get; set; }
    public string Name { get; set; }
}

var dto = user.ToFacet<User, UserDto>(); // ❌ FAC001
```

#### Good Code

```csharp
[Facet(typeof(User))]
public partial class UserDto { }

var dto = user.ToFacet<User, UserDto>(); // ✅ OK
```

---

### FAC002

**Consider using the two-generic variant for better performance**

- **Severity**: Info
- **Category**: Performance

#### Description

When using single-generic extension methods like `ToFacet<TTarget>()`, the library uses reflection to discover the source type. For better performance, use the two-generic variant `ToFacet<TSource, TTarget>()`.

#### Code Triggering Warning

```csharp
var dto = user.ToFacet<UserDto>(); // ℹ️ FAC002: Consider ToFacet<User, UserDto>()
```

#### Recommended Code

```csharp
var dto = user.ToFacet<User, UserDto>(); // ✅ Better performance
```

#### Impact

The performance difference is minimal (a few nanoseconds) but can add up in tight loops or high-throughput scenarios.

---

## [Facet] Attribute Analyzers

### FAC003

**Type with [Facet] attribute must be declared as partial**

- **Severity**: Error
- **Category**: Declaration

#### Description

Source generators require types to be `partial` so they can add generated members. Any type marked with `[Facet]` must be declared as `partial`.

#### Bad Code

```csharp
[Facet(typeof(User))]
public class UserDto { } // ❌ FAC003: Missing 'partial' keyword
```

#### Good Code

```csharp
[Facet(typeof(User))]
public partial class UserDto { } // ✅ OK
```

---

### FAC004

**Property name does not exist in source type**

- **Severity**: Error
- **Category**: Usage

#### Description

Property names specified in `Exclude` or `Include` parameters must exist in the source type.

#### Bad Code

```csharp
public class User
{
    public int Id { get; set; }
    public string Name { get; set; }
}

[Facet(typeof(User), "PasswordHash")] // ❌ FAC004: User doesn't have PasswordHash
public partial class UserDto { }
```

#### Good Code

```csharp
public class User
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string PasswordHash { get; set; }
}

[Facet(typeof(User), "PasswordHash")] // ✅ OK
public partial class UserDto { }
```

---

### FAC005

**Source type is not accessible or does not exist**

- **Severity**: Error
- **Category**: Usage

#### Description

The source type specified in the `[Facet]` attribute must be a valid, accessible type.

#### Bad Code

```csharp
[Facet(typeof(NonExistentType))] // ❌ FAC005
public partial class UserDto { }
```

#### Good Code

```csharp
[Facet(typeof(User))] // ✅ OK
public partial class UserDto { }
```

---

### FAC006

**Configuration type does not implement required interface**

- **Severity**: Error
- **Category**: Usage

#### Description

Configuration types must implement `IFacetMapConfiguration<TSource, TTarget>`, `IFacetMapConfigurationAsync<TSource, TTarget>`, or provide a static `Map` method.

#### Bad Code

```csharp
public class UserMapper // ❌ No interface, no Map method
{
    public void DoSomething(User source, UserDto target) { }
}

[Facet(typeof(User), Configuration = typeof(UserMapper))] // ❌ FAC006
public partial class UserDto { }
```

#### Good Code

```csharp
// Option 1: Implement interface
public class UserMapper : IFacetMapConfiguration<User, UserDto>
{
    public static void Map(User source, UserDto target)
    {
        target.FullName = $"{source.FirstName} {source.LastName}";
    }
}

// Option 2: Provide static Map method
public class UserMapper
{
    public static void Map(User source, UserDto target)
    {
        target.FullName = $"{source.FirstName} {source.LastName}";
    }
}

[Facet(typeof(User), Configuration = typeof(UserMapper))] // ✅ OK
public partial class UserDto { }
```

---

### FAC007

**Nested facet type is not marked with [Facet] attribute**

- **Severity**: Warning
- **Category**: Usage

#### Description

All types specified in the `NestedFacets` array must be marked with the `[Facet]` attribute.

#### Bad Code

```csharp
public class AddressDto { } // ❌ Missing [Facet] attribute

[Facet(typeof(User), NestedFacets = [typeof(AddressDto)])] // ⚠️ FAC007
public partial class UserDto { }
```

#### Good Code

```csharp
[Facet(typeof(Address))]
public partial class AddressDto { }

[Facet(typeof(User), NestedFacets = [typeof(AddressDto)])] // ✅ OK
public partial class UserDto { }
```

---

### FAC008

**Potential stack overflow with circular references**

- **Severity**: Warning
- **Category**: Performance

#### Description

When `MaxDepth` is set to 0 (unlimited) and `PreserveReferences` is `false`, circular references in object graphs can cause stack overflow exceptions.

#### Bad Code

```csharp
[Facet(typeof(User),
    MaxDepth = 0,
    PreserveReferences = false,
    NestedFacets = [typeof(CompanyDto)])] // ⚠️ FAC008
public partial class UserDto { }
```

#### Good Code

```csharp
// Option 1: Enable PreserveReferences (default)
[Facet(typeof(User),
    NestedFacets = [typeof(CompanyDto)])] // ✅ OK (PreserveReferences defaults to true)

// Option 2: Set MaxDepth limit
[Facet(typeof(User),
    MaxDepth = 5,
    NestedFacets = [typeof(CompanyDto)])] // ✅ OK

// Option 3: Both
[Facet(typeof(User),
    MaxDepth = 10,
    PreserveReferences = true,
    NestedFacets = [typeof(CompanyDto)])] // ✅ OK (safest)
```

---

### FAC009

**Cannot specify both Include and Exclude**

- **Severity**: Error
- **Category**: Usage

#### Description

The `Include` and `Exclude` parameters are mutually exclusive. Use either `Include` to whitelist properties or `Exclude` to blacklist properties, but not both.

#### Bad Code

```csharp
[Facet(typeof(User),
    "PasswordHash",  // Exclude parameter
    Include = ["Id", "Name"])] // ❌ FAC009: Can't use both
public partial class UserDto { }
```

#### Good Code

```csharp
// Option 1: Exclude approach
[Facet(typeof(User), "PasswordHash", "SecretKey")] // ✅ OK
public partial class UserDto { }

// Option 2: Include approach
[Facet(typeof(User), Include = ["Id", "Name", "Email"])] // ✅ OK
public partial class UserDto { }
```

---

### FAC010

**MaxDepth value is unusual**

- **Severity**: Warning
- **Category**: Performance

#### Description

MaxDepth values should typically be between 1 and 10 for most scenarios. Negative values are invalid, and values above 100 may indicate a configuration error.

#### Code Triggering Warning

```csharp
[Facet(typeof(User), MaxDepth = -1)] // ⚠️ FAC010: Negative
[Facet(typeof(User), MaxDepth = 500)] // ⚠️ FAC010: Too large
```

#### Good Code

```csharp
[Facet(typeof(User), MaxDepth = 5)] // ✅ OK
[Facet(typeof(User), MaxDepth = 10)] // ✅ OK (default)
```

---

## [GenerateDtos] Attribute Analyzers

### FAC011

**[GenerateDtos] can only be applied to classes**

- **Severity**: Error
- **Category**: Usage

#### Description

The `[GenerateDtos]` and `[GenerateAuditableDtos]` attributes are designed for class types and cannot be applied to structs, interfaces, or other type kinds.

#### Bad Code

```csharp
[GenerateDtos(DtoTypes.All)]
public struct Product { } // ❌ FAC011: Can't use on struct
```

#### Good Code

```csharp
[GenerateDtos(DtoTypes.All)]
public class Product { } // ✅ OK
```

---

### FAC012

**Excluded property does not exist**

- **Severity**: Warning
- **Category**: Usage

#### Description

Properties specified in `ExcludeProperties` should exist in the source type.

#### Bad Code

```csharp
public class Product
{
    public int Id { get; set; }
    public string Name { get; set; }
}

[GenerateDtos(DtoTypes.All,
    ExcludeProperties = ["InternalNotes"])] // ⚠️ FAC012: Doesn't exist
public class Product { }
```

#### Good Code

```csharp
public class Product
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string InternalNotes { get; set; }
}

[GenerateDtos(DtoTypes.All,
    ExcludeProperties = ["InternalNotes"])] // ✅ OK
public class Product { }
```

---

### FAC013

**No DTO types selected for generation**

- **Severity**: Warning
- **Category**: Usage

#### Description

Setting `Types` to `DtoTypes.None` will not generate any DTOs.

#### Bad Code

```csharp
[GenerateDtos(Types = DtoTypes.None)] // ⚠️ FAC013: No DTOs will be generated
public class Product { }
```

#### Good Code

```csharp
[GenerateDtos(Types = DtoTypes.All)] // ✅ OK
public class Product { }

// Or specify specific types
[GenerateDtos(Types = DtoTypes.Create | DtoTypes.Update | DtoTypes.Response)]
public class Product { }
```

---

## [Flatten] Attribute Analyzers

### FAC014

**Type with [Flatten] attribute must be declared as partial**

- **Severity**: Error
- **Category**: Declaration

#### Description

Similar to `[Facet]`, types marked with `[Flatten]` must be `partial`.

#### Bad Code

```csharp
[Flatten(typeof(Person))]
public class PersonFlat { } // ❌ FAC014
```

#### Good Code

```csharp
[Flatten(typeof(Person))]
public partial class PersonFlat { } // ✅ OK
```

---

### FAC015

**Source type is not accessible or does not exist**

- **Severity**: Error
- **Category**: Usage

#### Description

The source type specified in the `[Flatten]` attribute must be valid and accessible.

#### Bad Code

```csharp
[Flatten(typeof(NonExistentType))] // ❌ FAC015
public partial class PersonFlat { }
```

#### Good Code

```csharp
[Flatten(typeof(Person))] // ✅ OK
public partial class PersonFlat { }
```

---

### FAC016

**MaxDepth value is unusual**

- **Severity**: Warning
- **Category**: Performance

#### Description

For flatten scenarios, MaxDepth values should typically be between 1 and 5. Values above 10 may cause excessive property generation.

#### Code Triggering Warning

```csharp
[Flatten(typeof(Person), MaxDepth = -1)] // ⚠️ FAC016: Negative
[Flatten(typeof(Person), MaxDepth = 50)] // ⚠️ FAC016: Too large
```

#### Good Code

```csharp
[Flatten(typeof(Person), MaxDepth = 3)] // ✅ OK (default)
[Flatten(typeof(Person), MaxDepth = 5)] // ✅ OK
```

---

### FAC017

**LeafOnly naming strategy may cause property name collisions**

- **Severity**: Info
- **Category**: Usage

#### Description

Using `FlattenNamingStrategy.LeafOnly` can cause name collisions when multiple nested objects have properties with the same name. Consider using the `Prefix` strategy instead.

#### Code Triggering Warning

```csharp
[Flatten(typeof(Person),
    NamingStrategy = FlattenNamingStrategy.LeafOnly)] // ℹ️ FAC017
public partial class PersonFlat { }
```

#### Potential Issue

```csharp
public class Person
{
    public Address HomeAddress { get; set; }
    public Address WorkAddress { get; set; }
}

public class Address
{
    public string Street { get; set; }
    public string City { get; set; }
}

// With LeafOnly, both addresses map to "Street" and "City" → collision!
```

#### Recommended Code

```csharp
[Flatten(typeof(Person),
    NamingStrategy = FlattenNamingStrategy.Prefix)] // ✅ Better
public partial class PersonFlat { }

// Generates: HomeAddressStreet, HomeAddressCity, WorkAddressStreet, WorkAddressCity
```

---

## Source Signature Analyzers

### FAC022

**Source entity structure changed**

- **Severity**: Warning
- **Category**: SourceTracking

#### Description

When you set `SourceSignature` on a `[Facet]` attribute, the analyzer computes a hash of the source type's properties and compares it to the stored signature. This warning is raised when the source entity's structure changes.

#### Code Triggering Warning

```csharp
public class User
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }  // New property added
}

[Facet(typeof(User), SourceSignature = "oldvalue")]  // ⚠️ FAC022
public partial class UserDto { }
```

#### Resolution

Use the provided code fix to update the signature, or manually update it:

```csharp
[Facet(typeof(User), SourceSignature = "newvalue")]  // ✅ OK
public partial class UserDto { }
```

#### Notes

- The signature is an 8-character hash computed from property names and types
- Respects `Include`/`Exclude` filters when computing the signature
- A code fix provider automatically offers to update the signature
- See [Source Signature Change Tracking](16_SourceSignature.md) for details

---

## Suppressing Analyzer Rules

If you need to suppress a specific analyzer rule, you can use:

### In Code

```csharp
#pragma warning disable FAC002
var dto = user.ToFacet<UserDto>();
#pragma warning restore FAC002
```

### In .editorconfig

```ini
[*.cs]
dotnet_diagnostic.FAC002.severity = none
```

### For Entire Project

```xml
<PropertyGroup>
  <NoWarn>$(NoWarn);FAC002</NoWarn>
</PropertyGroup>
```

---

## Configuration

All analyzers are enabled by default. You can configure their severity in your `.editorconfig` file:

```ini
[*.cs]

# Set a rule to error
dotnet_diagnostic.FAC007.severity = error

# Set a rule to warning
dotnet_diagnostic.FAC002.severity = warning

# Disable a rule
dotnet_diagnostic.FAC017.severity = none
```

---

## See Also

- [Attribute Reference](03_AttributeReference.md)
- [Custom Mapping](04_CustomMapping.md)
- [Advanced Scenarios](06_AdvancedScenarios.md)
