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

## 2. Class With Additional Properties

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

## 3. Field Facet Example

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

## 4. Custom Mapping Example

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

## 5. Smart Defaults

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

// 2. CLASS: Defaults to mutable (backward compatibility)
[Facet(typeof(ModernUser))]
public partial class UserClass;
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

---

## 6. Explicit Control Over Init-Only and Required Properties

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

## 7. Init-Only Properties with Custom Mapping

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

## 8. Positional Record Facets

**Input:**
```csharp
public record struct DataRecordStruct(int Code, string Label);

[Facet(typeof(DataRecordStruct), Kind = FacetKind.RecordStruct)]
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

## 9. Legacy Readonly / Init-Only Example

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

## 10. Record Facet with Custom Mapping

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

[Facet(typeof(UserRecord), nameof(UserRecord.FirstName), nameof(UserRecord.LastName), nameof(UserRecord.Registered), Configuration = typeof(UserRecordMapper), Kind = FacetKind.Record)]
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

## 11. Plain Struct Facet Example

**Input:**
```csharp
public struct PersonStruct
{
    public string Name;
    public int Age;
    public string Email;
}

[Facet(typeof(PersonStruct), Kind = FacetKind.Struct, IncludeFields = true)]
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

See the [Quick Start](02_QuickStart.md) and [Advanced Scenarios](06_AdvancedScenarios.md) for more details.
