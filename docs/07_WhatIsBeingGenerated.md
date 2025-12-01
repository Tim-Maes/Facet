# What is Being Generated?

This page shows concrete examples of what Facet generates for different scenarios. These examples are based on the actual test suite and reflect the current version of Facet.

> **Note:** The generated code includes comprehensive XML documentation comments. These are omitted in some examples below for brevity, but they are present in the actual generated code.

---

## 1. Basic Class Facet (Exclude Mode)

**Input:**
```csharp
public class User
{
    public int Id { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string Email { get; set; }
    public string Password { get; set; }
    public DateTime CreatedAt { get; set; }
}

[Facet(typeof(User), "Password", "CreatedAt", GenerateToSource = true)]
public partial class UserDto
{
    public string FullName { get; set; } = string.Empty;
    public int Age { get; set; }
}
```

**Generated:**
```csharp
public partial class UserDto
{
    public int Id { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string Email { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="UserDto"/> class from the specified <see cref="User"/>.
    /// </summary>
    public UserDto(User source) : this(source, 0, new HashSet<object>(ReferenceEqualityComparer.Instance))
    {
    }

    /// <summary>
    /// Constructor with depth tracking to prevent stack overflow from circular references.
    /// </summary>
    public UserDto(User source, int __depth, HashSet<object>? __processed)
    {
        this.Id = source.Id;
        this.FirstName = source.FirstName;
        this.LastName = source.LastName;
        this.Email = source.Email;
    }

    /// <summary>
    /// Creates a new instance of <see cref="UserDto"/> from the specified <see cref="User"/>.
    /// </summary>
    public static UserDto FromSource(User source)
    {
        return new UserDto(source, 0, new HashSet<object>(ReferenceEqualityComparer.Instance));
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="UserDto"/> class with default values.
    /// </summary>
    public UserDto()
    {
    }

    /// <summary>
    /// Gets the projection expression for converting <see cref="User"/> to <see cref="UserDto"/>.
    /// Use this for LINQ and Entity Framework query projections.
    /// </summary>
    public static Expression<Func<User, UserDto>> Projection =>
        source => new UserDto
        {
            Id = source.Id,
            FirstName = source.FirstName,
            LastName = source.LastName,
            Email = source.Email
        };

    /// <summary>
    /// Converts this instance of <see cref="UserDto"/> to an instance of the source type.
    /// </summary>
    public User ToSource()
    {
        return new User
        {
            Id = this.Id,
            FirstName = this.FirstName,
            LastName = this.LastName,
            Email = this.Email
        };
    }

    [Obsolete("Use ToSource() instead. This method will be removed in a future version.")]
    public User BackTo() => ToSource();
}
```

**What's Generated:**
- All properties from source except excluded ones ("Password", "CreatedAt")
- User-defined properties from partial class ("FullName", "Age") are preserved
- Constructor with circular reference protection
- `FromSource()` static factory method for optimal performance
- Parameterless constructor for deserialization
- `Projection` expression for LINQ/EF queries
- `ToSource()` method for reverse mapping (because `GenerateToSource = true`)
- Obsolete `BackTo()` method for backward compatibility

---

## 2. Include Mode

**Input:**
```csharp
public class User
{
    public int Id { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string Email { get; set; }
    public string Password { get; set; }
}

[Facet(typeof(User), Include = new[] { "FirstName", "LastName", "Email" }, GenerateToSource = true)]
public partial class UserIncludeDto;
```

**Generated:**
```csharp
public partial class UserIncludeDto
{
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string Email { get; set; }

    public UserIncludeDto(User source) : this(source, 0, new HashSet<object>(ReferenceEqualityComparer.Instance))
    {
    }

    public UserIncludeDto(User source, int __depth, HashSet<object>? __processed)
    {
        this.FirstName = source.FirstName;
        this.LastName = source.LastName;
        this.Email = source.Email;
    }

    public static UserIncludeDto FromSource(User source)
    {
        return new UserIncludeDto(source, 0, new HashSet<object>(ReferenceEqualityComparer.Instance));
    }

    public UserIncludeDto()
    {
    }

    public static Expression<Func<User, UserIncludeDto>> Projection =>
        source => new UserIncludeDto
        {
            FirstName = source.FirstName,
            LastName = source.LastName,
            Email = source.Email
        };

    public User ToSource()
    {
        return new User
        {
            FirstName = this.FirstName,
            LastName = this.LastName,
            Email = this.Email
        };
    }

    [Obsolete("Use ToSource() instead.")]
    public User BackTo() => ToSource();
}
```

**Key Point:** Include mode generates `ToSource()` automatically, initializing excluded properties with their default values.

---

## 3. Custom Mapping Configuration

**Input:**
```csharp
public class User
{
    public int Id { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public DateTime DateOfBirth { get; set; }
}

public class UserDtoMapper : IFacetMapConfiguration<User, UserDto>
{
    public static void Map(User source, UserDto target)
    {
        target.FullName = $"{source.FirstName} {source.LastName}";
        target.Age = CalculateAge(source.DateOfBirth);
    }

    private static int CalculateAge(DateTime birthDate)
    {
        var today = DateTime.Today;
        var age = today.Year - birthDate.Year;
        if (birthDate.Date > today.AddYears(-age)) age--;
        return age;
    }
}

[Facet(typeof(User), "Password", "CreatedAt", Configuration = typeof(UserDtoMapper))]
public partial class UserDto
{
    public string FullName { get; set; } = string.Empty;
    public int Age { get; set; }
}
```

**Generated:**
```csharp
public partial class UserDto
{
    public int Id { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public DateTime DateOfBirth { get; set; }

    public UserDto(User source) : this(source, 0, new HashSet<object>(ReferenceEqualityComparer.Instance))
    {
    }

    public UserDto(User source, int __depth, HashSet<object>? __processed)
    {
        this.Id = source.Id;
        this.FirstName = source.FirstName;
        this.LastName = source.LastName;
        this.DateOfBirth = source.DateOfBirth;
        UserDtoMapper.Map(source, this);  // Custom mapping applied after property copying
    }

    public static UserDto FromSource(User source)
    {
        return new UserDto(source, 0, new HashSet<object>(ReferenceEqualityComparer.Instance));
    }

    public UserDto()
    {
    }

    public static Expression<Func<User, UserDto>> Projection =>
        source => new UserDto
        {
            Id = source.Id,
            FirstName = source.FirstName,
            LastName = source.LastName,
            DateOfBirth = source.DateOfBirth
        };
}
```

**Note:** Custom mapping is applied in the constructor but NOT in the `Projection` expression (since expressions can't call arbitrary methods).

---

## 4. Record Facets with Positional Parameters

**Input:**
```csharp
public record ClassicUser(string Id, string FirstName, string LastName, string? Email);

[Facet(typeof(ClassicUser), GenerateToSource = true)]
public partial record ClassicUserDto;
```

**Generated:**
```csharp
public partial record ClassicUserDto(string Id, string FirstName, string LastName, string? Email);

public partial record ClassicUserDto
{
    [SetsRequiredMembers]
    public ClassicUserDto(ClassicUser source) : this(source, 0, new HashSet<object>(ReferenceEqualityComparer.Instance))
    {
    }

    [SetsRequiredMembers]
    public ClassicUserDto(ClassicUser source, int __depth, HashSet<object>? __processed)
        : this(source.Id, source.FirstName, source.LastName, source.Email)
    {
    }

    public ClassicUserDto() : this(string.Empty, string.Empty, string.Empty, null)
    {
    }

    public static Expression<Func<ClassicUser, ClassicUserDto>> Projection =>
        source => new ClassicUserDto
        {
            Id = source.Id,
            FirstName = source.FirstName,
            LastName = source.LastName,
            Email = source.Email
        };

    public ClassicUser ToSource()
    {
        return new ClassicUser(this.Id, this.FirstName, this.LastName, this.Email);
    }

    [Obsolete("Use ToSource() instead.")]
    public ClassicUser BackTo() => ToSource();
}
```

**Note:** Records generate positional parameters in addition to the standard members.

---

## 5. NullableProperties for Query/Filter DTOs

**Input:**
```csharp
public class Product
{
    public int Id { get; set; }
    public string Name { get; set; }
    public decimal Price { get; set; }
    public bool IsAvailable { get; set; }
    public DateTime CreatedAt { get; set; }
}

[Facet(typeof(Product), "InternalNotes", "CreatedAt", NullableProperties = true, GenerateToSource = false)]
public partial class ProductQueryDto;
```

**Generated:**
```csharp
public partial class ProductQueryDto
{
    public int? Id { get; set; }
    public string? Name { get; set; }
    public decimal? Price { get; set; }
    public bool? IsAvailable { get; set; }

    public ProductQueryDto(Product source) : this(source, 0, new HashSet<object>(ReferenceEqualityComparer.Instance))
    {
    }

    public ProductQueryDto(Product source, int __depth, HashSet<object>? __processed)
    {
        this.Id = source.Id;
        this.Name = source.Name;
        this.Price = source.Price;
        this.IsAvailable = source.IsAvailable;
    }

    public static ProductQueryDto FromSource(Product source)
    {
        return new ProductQueryDto(source, 0, new HashSet<object>(ReferenceEqualityComparer.Instance));
    }

    public ProductQueryDto()
    {
    }

    public static Expression<Func<Product, ProductQueryDto>> Projection =>
        source => new ProductQueryDto
        {
            Id = source.Id,
            Name = source.Name,
            Price = source.Price,
            IsAvailable = source.IsAvailable
        };

    // Note: No ToSource() method generated when GenerateToSource = false
}
```

**Why nullable properties?** All value types become nullable (int -> int?, bool -> bool?, decimal -> decimal?) and reference types are marked nullable. Perfect for query/filter scenarios where all criteria are optional.

## 6. MapFrom Attribute - Property Renaming

**Input:**
```csharp
public class MapFromTestEntity
{
    public int Id { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string Email { get; set; }
}

[Facet(typeof(MapFromTestEntity), GenerateToSource = true)]
public partial class MapFromSimpleFacet
{
    [MapFrom(nameof(MapFromTestEntity.FirstName), Reversible = true)]
    public string Name { get; set; } = string.Empty;
}
```

**Generated:**
```csharp
public partial class MapFromSimpleFacet
{
    public int Id { get; set; }
    public string LastName { get; set; }
    public string Email { get; set; }

    public MapFromSimpleFacet(MapFromTestEntity source) : this(source, 0, new HashSet<object>(ReferenceEqualityComparer.Instance))
    {
    }

    public MapFromSimpleFacet(MapFromTestEntity source, int __depth, HashSet<object>? __processed)
    {
        this.Id = source.Id;
        this.LastName = source.LastName;
        this.Email = source.Email;
        this.Name = source.FirstName;  // Mapped from FirstName
    }

    public static MapFromSimpleFacet FromSource(MapFromTestEntity source)
    {
        return new MapFromSimpleFacet(source, 0, new HashSet<object>(ReferenceEqualityComparer.Instance));
    }

    public MapFromSimpleFacet()
    {
    }

    public static Expression<Func<MapFromTestEntity, MapFromSimpleFacet>> Projection =>
        source => new MapFromSimpleFacet
        {
            Id = source.Id,
            LastName = source.LastName,
            Email = source.Email,
            Name = source.FirstName  // Mapped in projection too
        };

    public MapFromTestEntity ToSource()
    {
        return new MapFromTestEntity
        {
            Id = this.Id,
            LastName = this.LastName,
            Email = this.Email,
            FirstName = this.Name  // Reverse mapping (because Reversible = true)
        };
    }

    [Obsolete("Use ToSource() instead.")]
    public MapFromTestEntity BackTo() => ToSource();
}
```

**Key Points:**
- `MapFrom` renames properties during mapping
- `Reversible = true` enables reverse mapping in `ToSource()`
- `Reversible = false` (default) means property won't be mapped in `ToSource()`
- `IncludeInProjection = false` excludes the property from the `Projection` expression

---

## 7. MapWhen Attribute - Conditional Mapping

**Input:**
```csharp
public class MapWhenTestEntity
{
    public int Id { get; set; }
    public string Name { get; set; }
    public bool IsActive { get; set; }
    public OrderStatus Status { get; set; }
    public string? Email { get; set; }
    public int Age { get; set; }
}

public enum OrderStatus
{
    Pending,
    Processing,
    Completed,
    Cancelled
}

[Facet(typeof(MapWhenTestEntity))]
public partial class MapWhenMixedFacet
{
    [MapWhen("IsActive")]
    public string? Email { get; set; }

    [MapWhen("Status == OrderStatus.Completed")]
    public DateTime? CompletedAt { get; set; }
}
```

**Generated:**
```csharp
public partial class MapWhenMixedFacet
{
    public int Id { get; set; }
    public string Name { get; set; }
    public bool IsActive { get; set; }
    public OrderStatus Status { get; set; }
    public int Age { get; set; }

    public MapWhenMixedFacet(MapWhenTestEntity source) : this(source, 0, new HashSet<object>(ReferenceEqualityComparer.Instance))
    {
    }

    public MapWhenMixedFacet(MapWhenTestEntity source, int __depth, HashSet<object>? __processed)
    {
        this.Id = source.Id;
        this.Name = source.Name;
        this.IsActive = source.IsActive;
        this.Status = source.Status;
        this.Age = source.Age;

        // Conditional mapping
        if (source.IsActive)
        {
            this.Email = source.Email;
        }

        if (source.Status == OrderStatus.Completed)
        {
            this.CompletedAt = source.CompletedAt;
        }
    }

    public static MapWhenMixedFacet FromSource(MapWhenTestEntity source)
    {
        return new MapWhenMixedFacet(source, 0, new HashSet<object>(ReferenceEqualityComparer.Instance));
    }

    public MapWhenMixedFacet()
    {
    }

    public static Expression<Func<MapWhenTestEntity, MapWhenMixedFacet>> Projection =>
        source => new MapWhenMixedFacet
        {
            Id = source.Id,
            Name = source.Name,
            IsActive = source.IsActive,
            Status = source.Status,
            Age = source.Age,
            Email = source.IsActive ? source.Email : default,
            CompletedAt = source.Status == OrderStatus.Completed ? source.CompletedAt : default
        };
}
```

**Key Points:**
- `MapWhen` adds conditional logic to property mapping
- Supports boolean properties, equality checks, null checks, comparisons
- Can use multiple `[MapWhen]` attributes for AND logic
- `IncludeInProjection = false` excludes conditional from projection

---

## 8. Wrapper Attribute - Delegation Pattern

**Input:**
```csharp
public class User
{
    public int Id { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string Email { get; set; }
    public string Password { get; set; }
    public decimal Salary { get; set; }
}

[Wrapper(typeof(User), "Password", "Salary")]
public partial class PublicUserWrapper { }
```

**Generated:**
```csharp
public partial class PublicUserWrapper
{
    private readonly User _source;

    public PublicUserWrapper(User source)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
    }

    public int Id
    {
        get => _source.Id;
        set => _source.Id = value;
    }

    public string FirstName
    {
        get => _source.FirstName;
        set => _source.FirstName = value;
    }

    public string LastName
    {
        get => _source.LastName;
        set => _source.LastName = value;
    }

    public string Email
    {
        get => _source.Email;
        set => _source.Email = value;
    }

    // Password and Salary are excluded - no properties generated

    public User Unwrap() => _source;
}
```

**Key Points:**
- `Wrapper` creates a delegation wrapper, not a copy
- Changes to wrapper properties affect the source object
- `Unwrap()` returns the original source object
- Useful for hiding sensitive properties without copying data

---

## 9. Flatten Attribute - Flattening Nested Objects

**Input:**
```csharp
public class Person
{
    public int Id { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public Address Address { get; set; }
    public ContactInfo ContactInfo { get; set; }
}

public class Address
{
    public string Street { get; set; }
    public string City { get; set; }
    public string ZipCode { get; set; }
    public Country Country { get; set; }
}

public class Country
{
    public string Name { get; set; }
    public string Code { get; set; }
}

public class ContactInfo
{
    public string Email { get; set; }
    public string Phone { get; set; }
}

[Flatten(typeof(Person))]
public partial class PersonFlatDto;
```

**Generated:**
```csharp
public partial class PersonFlatDto
{
    public int Id { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }

    // Flattened from Address
    public string AddressStreet { get; set; }
    public string AddressCity { get; set; }
    public string AddressZipCode { get; set; }

    // Flattened from Address.Country
    public string AddressCountryName { get; set; }
    public string AddressCountryCode { get; set; }

    // Flattened from ContactInfo
    public string ContactInfoEmail { get; set; }
    public string ContactInfoPhone { get; set; }

    public PersonFlatDto(Person source)
    {
        this.Id = source.Id;
        this.FirstName = source.FirstName;
        this.LastName = source.LastName;

        if (source.Address != null)
        {
            this.AddressStreet = source.Address.Street;
            this.AddressCity = source.Address.City;
            this.AddressZipCode = source.Address.ZipCode;

            if (source.Address.Country != null)
            {
                this.AddressCountryName = source.Address.Country.Name;
                this.AddressCountryCode = source.Address.Country.Code;
            }
        }

        if (source.ContactInfo != null)
        {
            this.ContactInfoEmail = source.ContactInfo.Email;
            this.ContactInfoPhone = source.ContactInfo.Phone;
        }
    }

    public PersonFlatDto()
    {
    }
}
```

**Key Points:**
- `Flatten` recursively flattens nested object properties
- Naming strategy: `ParentPropertyChildProperty` (can be customized)
- `MaxDepth` parameter controls nesting depth
- `IgnoreNestedIds` excludes nested Id properties
- `NamingStrategy = FlattenNamingStrategy.LeafOnly` or `SmartLeaf` for different naming
- Collections are ignored by default

---

## 10. Nested Facets

**Input:**
```csharp
public class UserForNestedFacet
{
    public int Id { get; set; }
    public string Name { get; set; }
    public UserAddressForNestedFacet Address { get; set; }
}

public class UserAddressForNestedFacet
{
    public string Street { get; set; }
    public string City { get; set; }
    public string FormattedAddress => $"{Street}, {City}";
}

[Facet(typeof(UserForNestedFacet), Include = [
    nameof(UserForNestedFacet.Id),
    nameof(UserForNestedFacet.Name),
    nameof(UserForNestedFacet.Address)
], NestedFacets = [typeof(UserDetailResponse.UserAddressItem)])]
public partial class UserDetailResponse
{
    [Facet(typeof(UserAddressForNestedFacet), Include = [
        nameof(UserAddressForNestedFacet.FormattedAddress)
    ])]
    public partial class UserAddressItem;
}
```

**Generated:**
```csharp
// UserAddressItem is generated as a nested class
public partial class UserDetailResponse
{
    public partial class UserAddressItem
    {
        public string FormattedAddress { get; set; }

        public UserAddressItem(UserAddressForNestedFacet source) : this(source, 0, new HashSet<object>(ReferenceEqualityComparer.Instance))
        {
        }

        public UserAddressItem(UserAddressForNestedFacet source, int __depth, HashSet<object>? __processed)
        {
            this.FormattedAddress = source.FormattedAddress;
        }

        public static UserAddressItem FromSource(UserAddressForNestedFacet source)
        {
            return new UserAddressItem(source, 0, new HashSet<object>(ReferenceEqualityComparer.Instance));
        }

        public UserAddressItem()
        {
        }

        public static Expression<Func<UserAddressForNestedFacet, UserAddressItem>> Projection =>
            source => new UserAddressItem
            {
                FormattedAddress = source.FormattedAddress
            };
    }
}

// UserDetailResponse uses the nested facet
public partial class UserDetailResponse
{
    public int Id { get; set; }
    public string Name { get; set; }
    public UserAddressItem? Address { get; set; }

    public UserDetailResponse(UserForNestedFacet source) : this(source, 0, new HashSet<object>(ReferenceEqualityComparer.Instance))
    {
    }

    public UserDetailResponse(UserForNestedFacet source, int __depth, HashSet<object>? __processed)
    {
        this.Id = source.Id;
        this.Name = source.Name;
        this.Address = source.Address != null ? new UserAddressItem(source.Address, __depth + 1, __processed) : null;
    }

    public static UserDetailResponse FromSource(UserForNestedFacet source)
    {
        return new UserDetailResponse(source, 0, new HashSet<object>(ReferenceEqualityComparer.Instance));
    }

    public UserDetailResponse()
    {
    }

    public static Expression<Func<UserForNestedFacet, UserDetailResponse>> Projection =>
        source => new UserDetailResponse
        {
            Id = source.Id,
            Name = source.Name,
            Address = source.Address != null ? new UserAddressItem(source.Address) : null
        };
}
```

**Key Points:**
- Nested facets create nested DTOs
- Depth tracking prevents infinite recursion for circular references
- Nested facets are specified via `NestedFacets` parameter

---

## 11. Generation Control Flags

Facet provides fine-grained control over what gets generated:

```csharp
[Facet(typeof(Source),
    GenerateConstructor = false,              // Skip source constructor
    GenerateParameterlessConstructor = false, // Skip parameterless constructor
    GenerateProjection = false,               // Skip Projection expression
    GenerateToSource = false)]                // Skip ToSource() method
public partial class MyDto;
```

**Common Combinations:**

- **Query DTO**: `NullableProperties = true, GenerateToSource = false`
- **Response DTO**: `GenerateToSource = false` (read-only)
- **Manual construction**: `GenerateConstructor = false` (for custom logic)
- **EF projections only**: `GenerateConstructor = false` (only use `Projection`)

---

## Summary of Generated Members

For a basic facet, Facet generates:

### Properties
- All non-excluded properties from source
- User-defined properties from partial class
- XML documentation copied from source

### Constructors
- `Dto(Source source)` - primary constructor
- `Dto(Source source, int __depth, HashSet<object>? __processed)` - depth-tracking constructor
- `Dto()` - parameterless constructor (optional)

### Methods
- `static Dto FromSource(Source source)` - factory method for optimal runtime performance
- `Source ToSource()` - reverse mapping (when `GenerateToSource = true`)
- `Source BackTo()` - obsolete, calls `ToSource()`
- `Source Unwrap()` - for Wrapper only

### Expressions
- `static Expression<Func<Source, Dto>> Projection` - for LINQ/EF queries

---

## Key Differences from Earlier Versions

If you're upgrading from an earlier version of Facet, here are the major changes to generated code:

1. **Circular reference protection**: Constructors now include `__depth` and `__processed` parameters
2. **`FromSource()` method**: New static factory method for optimal performance
3. **`ToSource()` replaces `BackTo()`**: `BackTo()` is now obsolete
4. **Comprehensive XML documentation**: All generated members include XML docs
5. **Parameterless constructor**: Now generated by default
6. **New attributes**: `MapFrom`, `MapWhen`, `Wrapper`, `Flatten` added
7. **`[SetsRequiredMembers]` attribute**: Used on constructors for records with required members

---

See also:
- [Quick Start](02_QuickStart.md) - Basic usage and getting started
- [Attribute Reference](03_AttributeReference.md) - Complete attribute documentation
- [Advanced Scenarios](06_AdvancedScenarios.md) - Complex mapping scenarios
