# What is Being Generated?

This page shows _some_ concrete examples of what Facet generates for different scenarios. These examples help you understand the output you can expect in your own projects.

---

## 1. Class Exclude Example

**Input:**
```csharp
public class Person
{
    public string Name { get; set; }
    public string Email { get; set; }
    public int Age { get; set; }
}

[Facet(typeof(Person), nameof(Person.Email))]
public partial class PersonWithoutEmail { }
```

**Generated:**
```csharp
public partial class PersonWithoutEmail
{
    public string Name { get; set; }
    public int Age { get; set; }
    public PersonWithoutEmail(Project.Namespace.Person source)
    {
        this.Name = source.Name;
        this.Age = source.Age;
    }
    public static Expression<Func<Project.Namespace.Person, PersonWithoutEmail>> Projection =>
        source => new PersonWithoutEmail(source);
}
```

---

## 2. Class Include Example

**Input:**
```csharp
public class Person
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
    public int Age { get; set; }
    public string Password { get; set; }
}

[Facet(typeof(Person), Include = new[] { "Name", "Age" })]
public partial class PersonContactInfo { }
```

**Generated:**
```csharp
public partial class PersonContactInfo
{
    public string Name { get; set; }
    public int Age { get; set; }
    public PersonContactInfo(Project.Namespace.Person source)
    {
        this.Name = source.Name;
        this.Age = source.Age;
    }
    public static Expression<Func<Project.Namespace.Person, PersonContactInfo>> Projection =>
        source => new PersonContactInfo(source);
    public Project.Namespace.Person ToSource()
    {
        return new Project.Namespace.Person
        {
            Name = this.Name,
            Age = this.Age,
            Id = 0,
            Email = string.Empty,
            Password = string.Empty
        };
    }
}
```

---

## 3. Class With Additional Properties

**Input:**
```csharp
[Facet(typeof(Person), nameof(Person.Email), nameof(Person.Age))]
public partial class PersonWithNote
{
    public string Note { get; set; }
}
```

**Generated:**
```csharp
public partial class PersonWithNote
{
    public string Name { get; set; }
    public string Note { get; set; }
    public PersonWithNote(Project.Namespace.Person source)
    {
        this.Name = source.Name;
    }
    public static Expression<Func<Project.Namespace.Person, PersonWithNote>> Projection =>
        source => new PersonWithNote(source);
}
```

---

## 4. Include with Custom Properties

**Input:**
```csharp
[Facet(typeof(Person), Include = new[] { "Name" })]
public partial class PersonSummary
{
    public string DisplayName { get; set; }
    public string Category { get; set; } = "Person";
}
```

**Generated:**
```csharp
public partial class PersonSummary
{
    public string Name { get; set; }
    public string DisplayName { get; set; }
    public string Category { get; set; } = "Person";
    public PersonSummary(Project.Namespace.Person source)
    {
        this.Name = source.Name;
    }
    public static Expression<Func<Project.Namespace.Person, PersonSummary>> Projection =>
        source => new PersonSummary(source);
}
```

---

## 5. Field Facet Example

**Input:**
```csharp
public class PersonWithField
{
    public string Name;
    public int Age;
    public Guid Identifier;
    public string Email { get; set; }
}

[Facet(typeof(PersonWithField), IncludeFields = true)]
public partial class PersonWithFieldFacet { }
```

**Generated:**
```csharp
public partial class PersonWithFieldFacet
{
    public string Name;
    public int Age;
    public Guid Identifier;
    public string Email { get; set; }
    public PersonWithFieldFacet(Project.Namespace.PersonWithField source)
    {
        this.Name = source.Name;
        this.Age = source.Age;
        this.Identifier = source.Identifier;
        this.Email = source.Email;
    }
    public static Expression<Func<Project.Namespace.PersonWithField, PersonWithFieldFacet>> Projection =>
        source => new PersonWithFieldFacet(source);
}
```

---

## 6. Include with Fields Example

**Input:**
```csharp
public class PersonWithField
{
    public string Name;
    public int Age;
    public Guid Identifier;
    public string Email { get; set; }
}

[Facet(typeof(PersonWithField), Include = new[] { "Name", "Email" }, IncludeFields = true)]
public partial class PersonNameAndEmail { }
```

**Generated:**
```csharp
public partial class PersonNameAndEmail
{
    public string Name;
    public string Email { get; set; }
    public PersonNameAndEmail(Project.Namespace.PersonWithField source)
    {
        this.Name = source.Name;
        this.Email = source.Email;
    }
    public static Expression<Func<Project.Namespace.PersonWithField, PersonNameAndEmail>> Projection =>
        source => new PersonNameAndEmail(source);
}
```

---

## 7. Custom Mapping Example

**Input:**
```csharp
public class User
{
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public DateTime Registered { get; set; }
}

public class UserMapper : IFacetMapConfiguration<User, UserDto>
{
    public static void Map(User source, UserDto target)
    {
        target.FullName = $"{source.FirstName} {source.LastName}";
        target.RegisteredText = source.Registered.ToString("yyyy-MM-dd");
    }
}

[Facet(typeof(User), nameof(User.FirstName), nameof(User.LastName), nameof(User.Registered), Configuration = typeof(UserMapper))]
public partial class UserDto
{
    public string FullName { get; set; }
    public string RegisteredText { get; set; }
}
```

**Generated:**
```csharp
public partial class UserDto
{
    public string FullName { get; set; }
    public string RegisteredText { get; set; }
    public UserDto(Project.Namespace.User source)
    {
        Project.Namespace.UserMapper.Map(source, this);
    }
    public static Expression<Func<Project.Namespace.User, UserDto>> Projection =>
        source => new UserDto(source);
}
```

---

## 8. Include Mode with Records

**Input:**
```csharp
public record User(int Id, string FirstName, string LastName, string Email, string Password);

[Facet(typeof(User), Include = new[] { "FirstName", "LastName", "Email" })]
public partial record UserContactRecord;
```

**Generated:**
```csharp
public partial record UserContactRecord(string FirstName, string LastName, string Email);
public partial record UserContactRecord
{
    public UserContactRecord(Project.Namespace.User source) : this(source.FirstName, source.LastName, source.Email) { }
    public static Expression<Func<Project.Namespace.User, UserContactRecord>> Projection =>
        source => new UserContactRecord(source);
}
```

---

## 9. Smart Defaults

Facet automatically chooses sensible defaults based on the target type:

**Input:**
```csharp
public record ModernUser
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string? Email { get; set; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}

// 1. RECORD: Automatically preserves init-only and required modifiers
[Facet(typeof(ModernUser))]
public partial record UserRecord;

// 2. CLASS: Defaults to mutable
[Facet(typeof(ModernUser))]
public partial class UserClass;

// 3. INCLUDE MODE: Only specific properties
[Facet(typeof(ModernUser), Include = new[] { "Id", "Name" })]
public partial class UserMinimal;
```

**Generated for Record:**
```csharp
public partial record UserRecord
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string? Email { get; set; }
    public DateTime CreatedAt { get; init; }
    public UserRecord(Project.Namespace.ModernUser source)
    {
        this.Id = source.Id;
        this.Name = source.Name;
        this.Email = source.Email;
        this.CreatedAt = source.CreatedAt;
    }
    public static Expression<Func<Project.Namespace.ModernUser, UserRecord>> Projection =>
        source => new UserRecord(source);
}
```

**Generated for Class:**
```csharp
public partial class UserClass
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string? Email { get; set; }
    public DateTime CreatedAt { get; set; }
    public UserClass(Project.Namespace.ModernUser source)
    {
        this.Id = source.Id;
        this.Name = source.Name;
        this.Email = source.Email;
        this.CreatedAt = source.CreatedAt;
    }
    public static Expression<Func<Project.Namespace.ModernUser, UserClass>> Projection =>
        source => new UserClass(source);
}
```

**Generated for Include Mode:**
```csharp
public partial class UserMinimal
{
    public string Id { get; set; }
    public string Name { get; set; }
    public UserMinimal(Project.Namespace.ModernUser source)
    {
        this.Id = source.Id;
        this.Name = source.Name;
    }
    public static Expression<Func<Project.Namespace.ModernUser, UserMinimal>> Projection =>
        source => new UserMinimal(source);
    public Project.Namespace.ModernUser ToSource()
    {
        return new Project.Namespace.ModernUser
        {
            Id = this.Id,
            Name = this.Name,
            Email = null,
            CreatedAt = default(DateTime)
        };
    }
}
```

---

## 10. Explicit Control Over Init-Only and Required Properties

You can override smart defaults with explicit control:

**Input:**
```csharp
// Force a class to preserve init-only and required (modern class pattern)
[Facet(typeof(ModernUser),
       PreserveInitOnlyProperties = true,
       PreserveRequiredProperties = true)]
public partial class ImmutableUserClass;

// Force a record to use mutable properties (unusual but possible)
[Facet(typeof(ModernUser), 
       PreserveInitOnlyProperties = false,
       PreserveRequiredProperties = false)]
public partial record MutableUserRecord;
```

**Generated for Immutable Class:**
```csharp
public partial class ImmutableUserClass
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string? Email { get; set; }
    public DateTime CreatedAt { get; init; }
    public ImmutableUserClass(Project.Namespace.ModernUser source)
    {
        this.Id = source.Id;
        this.Name = source.Name;
        this.Email = source.Email;
        this.CreatedAt = source.CreatedAt;
    }
    public static Expression<Func<Project.Namespace.ModernUser, ImmutableUserClass>> Projection =>
        source => new ImmutableUserClass(source);
}
```

**Generated for Mutable Record:**
```csharp
public partial record MutableUserRecord
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string? Email { get; set; }
    public DateTime CreatedAt { get; set; }
    public MutableUserRecord(Project.Namespace.ModernUser source)
    {
        this.Id = source.Id;
        this.Name = source.Name;
        this.Email = source.Email;
        this.CreatedAt = source.CreatedAt;
    }
    public static Expression<Func<Project.Namespace.ModernUser, MutableUserRecord>> Projection =>
        source => new MutableUserRecord(source);
}
```

---

## 11. Include vs Exclude Comparison

**Same Source Type, Different Approaches:**

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

// Exclude approach - hide password but keep everything else
[Facet(typeof(User), nameof(User.Password))]
public partial class UserExcludeDto;

// Include approach - only get contact info
[Facet(typeof(User), Include = new[] { "FirstName", "LastName", "Email" })]
public partial class UserIncludeDto;
```

**Generated Exclude DTO (5 properties):**
```csharp
public partial class UserExcludeDto
{
    public int Id { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string Email { get; set; }
    public DateTime CreatedAt { get; set; }
    // ... constructor and projection
}
```

**Generated Include DTO (3 properties):**
```csharp
public partial class UserIncludeDto
{
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string Email { get; set; }
    // ... constructor and projection
}
```

---

## 12. Init-Only Properties with Custom Mapping

When using custom mapping with init-only properties, Facet generates a `FromSource` factory method:

**Input:**
```csharp
public record ImmutableUser
{
    public required string Id { get; init; }
    public required string FullName { get; init; }
    public DateTime CreatedAt { get; init; }
}

public class ImmutableUserMapper : IFacetMapConfiguration<ModernUser, ImmutableUserDto>
{
    public static void Map(ModernUser source, ImmutableUserDto target)
    {
        // Custom logic here
        target.FullName = $"{source.FirstName} {source.LastName}";
        target.DisplayName = target.FullName.ToUpper();
    }
}

[Facet(typeof(ModernUser), "Email", 
       Configuration = typeof(ImmutableUserMapper),
       PreserveInitOnlyProperties = true,
       PreserveRequiredProperties = true)]
public partial record ImmutableUserDto
{
    public required string FullName { get; init; }
    public string DisplayName { get; set; }
}
```

**Generated:**
```csharp
public partial record ImmutableUserDto
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public DateTime CreatedAt { get; init; }
    public required string FullName { get; init; }
    public string DisplayName { get; set; }
    
    public ImmutableUserDto(Project.Namespace.ModernUser source)
    {
        // This constructor should not be used for types with init-only properties and custom mapping
        // Use FromSource factory method instead
        throw new InvalidOperationException("Use ImmutableUserDto.FromSource(source) for types with init-only properties");
    }
    
    public static ImmutableUserDto FromSource(Project.Namespace.ModernUser source)
    {
        // Custom mapper creates and returns the instance with init-only properties set
        return Project.Namespace.ImmutableUserMapper.Map(source, null);
    }
    
    public static Expression<Func<Project.Namespace.ModernUser, ImmutableUserDto>> Projection =>
        source => new ImmutableUserDto(source);
}
```

---

## 13. Positional Record Facets

**Input:**
```csharp
public record struct DataRecordStruct(int Code, string Label);

[Facet(typeof(DataRecordStruct))]
public partial record struct DataRecordStructDto { }
```

**Generated:**
```csharp
public partial record struct DataRecordStructDto(int Code, string Label);
public partial record struct DataRecordStructDto
{
    public DataRecordStructDto(Project.Namespace.DataRecordStruct source) : this(source.Code, source.Label) { }
    public static Expression<Func<Project.Namespace.DataRecordStruct, DataRecordStructDto>> Projection =>
        source => new DataRecordStructDto(source);
}
```

---

## 14. Legacy Readonly / Init-Only Example

**Input:**
```csharp
public class ReadonlySourceModel
{
    public string Id { get; }
    public DateTime CreatedAt { get; init; }
    public string Status { get; set; }
    public ReadonlySourceModel(string id, DateTime createdAt)
    {
        Id = id;
        CreatedAt = createdAt;
    }
}

[Facet(typeof(ReadonlySourceModel))]
public partial class ReadonlySourceModelFacet { }
```

**Generated:**
```csharp
public partial class ReadonlySourceModelFacet
{
    public string Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public string Status { get; set; }
    public ReadonlySourceModelFacet(Project.Namespace.ReadonlySourceModel source)
    {
        this.Id = source.Id;
        this.CreatedAt = source.CreatedAt;
        this.Status = source.Status;
    }
    public static Expression<Func<Project.Namespace.ReadonlySourceModel, ReadonlySourceModelFacet>> Projection =>
        source => new ReadonlySourceModelFacet(source);
}
```

---

## 15. Record Facet with Custom Mapping

**Input:**
```csharp
public record UserRecord(string FirstName, string LastName, DateTime Registered);

public class UserRecordMapper : IFacetMapConfiguration<UserRecord, UserRecordDto>
{
    public static void Map(UserRecord source, UserRecordDto target)
    {
        target.FullName = $"{source.FirstName} {source.LastName}";
        target.RegisteredText = source.Registered.ToString("MMMM yyyy");
    }
}

[Facet(typeof(UserRecord), nameof(UserRecord.FirstName), nameof(UserRecord.LastName), nameof(UserRecord.Registered), Configuration = typeof(UserRecordMapper))]
public partial record UserRecordDto
{
    public string FullName { get; set; }
    public string RegisteredText { get; set; }
}
```

**Generated:**
```csharp
public partial record UserRecordDto();
public partial record UserRecordDto
{
    public string FullName { get; set; }
    public string RegisteredText { get; set; }
    public UserRecordDto(Project.Namespace.UserRecord source) : this()
    {
        Project.Namespace.UserRecordMapper.Map(source, this);
    }
    public static Expression<Func<Project.Namespace.UserRecord, UserRecordDto>> Projection =>
        source => new UserRecordDto(source);
}
```

---

## 16. Plain Struct Facet Example

**Input:**
```csharp
public struct PersonStruct
{
    public string Name;
    public int Age;
    public string Email;
}

[Facet(typeof(PersonStruct), IncludeFields = true)]
public partial struct PersonStructDto { }
```

**Generated:**
```csharp
public partial struct PersonStructDto
{
    public string Name;
    public int Age;
    public string Email;
    public PersonStructDto(Project.Namespace.PersonStruct source)
    {
        this.Name = source.Name;
        this.Age = source.Age;
        this.Email = source.Email;
    }
    public static Expression<Func<Project.Namespace.PersonStruct, PersonStructDto>> Projection =>
        source => new PersonStructDto(source);
}
```

---

## 17. NullableProperties for Query/Filter DTOs

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

[Facet(typeof(Product), "CreatedAt", NullableProperties = true, GenerateToSource = false)]
public partial class ProductQueryDto { }
```

**Generated:**
```csharp
public partial class ProductQueryDto
{
    public int? Id { get; set; }
    public string? Name { get; set; }
    public decimal? Price { get; set; }
    public bool? IsAvailable { get; set; }

    public ProductQueryDto() { }

    public ProductQueryDto(Project.Namespace.Product source)
    {
        this.Id = source.Id;
        this.Name = source.Name;
        this.Price = source.Price;
        this.IsAvailable = source.IsAvailable;
    }

    public static Expression<Func<Project.Namespace.Product, ProductQueryDto>> Projection =>
        source => new ProductQueryDto(source);

    // Note: No ToSource() method generated when GenerateToSource = false
}
```

**Why nullable properties?**
All value types become nullable (int -> int?, bool -> bool?, DateTime -> DateTime?) and reference types are marked nullable. Perfect for query/filter scenarios where all criteria are optional.

---

## 18. Disabling Code Generation Features

You can control what gets generated with boolean flags:

**Input:**
```csharp
// No constructor - only properties
[Facet(typeof(Person), GenerateConstructor = false)]
public partial class PersonPropertiesOnly { }

// No parameterless constructor
[Facet(typeof(Person), GenerateParameterlessConstructor = false)]
public partial class PersonNoParamless { }

// No projection expression
[Facet(typeof(Person), GenerateProjection = false)]
public partial class PersonNoProjection { }

// No ToSource method
[Facet(typeof(Person), GenerateToSource = false)]
public partial class PersonNoToSource { }
```

**Generated for GenerateConstructor = false:**
```csharp
public partial class PersonPropertiesOnly
{
    public string Name { get; set; }
    public string Email { get; set; }
    public int Age { get; set; }

    // No constructor generated!
    // User must manually populate properties

    public static Expression<Func<Project.Namespace.Person, PersonPropertiesOnly>> Projection =>
        source => new PersonPropertiesOnly { Name = source.Name, Email = source.Email, Age = source.Age };
}
```

**Generated for GenerateProjection = false:**
```csharp
public partial class PersonNoProjection
{
    public string Name { get; set; }
    public string Email { get; set; }
    public int Age { get; set; }

    public PersonNoProjection(Project.Namespace.Person source)
    {
        this.Name = source.Name;
        this.Email = source.Email;
        this.Age = source.Age;
    }

    // No Projection property generated!
}
```

---

## 19. Parameterless Constructor Generation

By default, Facet generates both a source constructor and a parameterless constructor:

**Input:**
```csharp
[Facet(typeof(Person))]
public partial class PersonDto { }
```

**Generated:**
```csharp
public partial class PersonDto
{
    public string Name { get; set; }
    public string Email { get; set; }
    public int Age { get; set; }

    // Parameterless constructor (for deserialization, testing, etc.)
    public PersonDto() { }

    // Source constructor (for mapping)
    public PersonDto(Project.Namespace.Person source)
    {
        this.Name = source.Name;
        this.Email = source.Email;
        this.Age = source.Age;
    }

    public static Expression<Func<Project.Namespace.Person, PersonDto>> Projection =>
        source => new PersonDto(source);

    public Project.Namespace.Person ToSource()
    {
        return new Project.Namespace.Person
        {
            Name = this.Name,
            Email = this.Email,
            Age = this.Age
        };
    }
}
```

**Why two constructors?**
- **Parameterless**: Needed for JSON deserialization, object initializers, testing
- **Source constructor**: Used for efficient mapping from entities

---

## 20. XML Documentation Preservation

Facet preserves XML documentation from source types:

**Input:**
```csharp
/// <summary>
/// Represents a person in the system
/// </summary>
public class Person
{
    /// <summary>
    /// Gets or sets the person's full name
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the person's email address
    /// </summary>
    public string Email { get; set; }

    /// <summary>
    /// Gets or sets the person's age in years
    /// </summary>
    public int Age { get; set; }
}

[Facet(typeof(Person))]
public partial class PersonDto { }
```

**Generated:**
```csharp
public partial class PersonDto
{
    /// <summary>
    /// Gets or sets the person's full name
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the person's email address
    /// </summary>
    public string Email { get; set; }

    /// <summary>
    /// Gets or sets the person's age in years
    /// </summary>
    public int Age { get; set; }

    public PersonDto() { }

    public PersonDto(Project.Namespace.Person source)
    {
        this.Name = source.Name;
        this.Email = source.Email;
        this.Age = source.Age;
    }

    public static Expression<Func<Project.Namespace.Person, PersonDto>> Projection =>
        source => new PersonDto(source);
}
```

**Note:** Type-level XML documentation is preserved on the generated partial class as well!

---

## 21. Combining Multiple Options

Real-world example combining several features:

**Input:**
```csharp
public class Product
{
    public int Id { get; set; }
    public string Name { get; set; }
    public decimal Price { get; set; }
    public string InternalNotes { get; set; }
    public DateTime CreatedAt { get; set; }
}

// Query DTO: nullable properties, no ToSource, exclude internal fields
[Facet(typeof(Product),
       "InternalNotes", "CreatedAt",
       NullableProperties = true,
       GenerateToSource = false)]
public partial record ProductQueryDto { }

// API Response: exclude internal fields, preserve docs
[Facet(typeof(Product), "InternalNotes")]
public partial class ProductResponse { }

// Admin DTO: include everything
[Facet(typeof(Product))]
public partial class ProductAdminDto { }
```

**Generated ProductQueryDto:**
```csharp
public partial record ProductQueryDto
{
    public int? Id { get; set; }
    public string? Name { get; set; }
    public decimal? Price { get; set; }

    public ProductQueryDto() { }

    public ProductQueryDto(Project.Namespace.Product source)
    {
        this.Id = source.Id;
        this.Name = source.Name;
        this.Price = source.Price;
    }

    public static Expression<Func<Project.Namespace.Product, ProductQueryDto>> Projection =>
        source => new ProductQueryDto(source);

    // No ToSource() - GenerateToSource = false
}
```

---

See the [Quick Start](02_QuickStart.md) and [Advanced Scenarios](06_AdvancedScenarios.md) for more details.
