# Advanced Usage Scenarios

Facet supports a variety of advanced scenarios to help you tailor projections to your needs.

## 1. Generating Records, Structs, and Record Structs

```csharp
[Facet(typeof(Person), Kind = FacetKind.Record)]
public partial record PersonRecord;

[Facet(typeof(MyStruct), Kind = FacetKind.Struct, IncludeFields = true)]
public partial struct MyStructDto;

[Facet(typeof(MyRecordStruct), Kind = FacetKind.RecordStruct)]
public partial record struct MyRecordStructDto;
```

## 2. Including Public Fields

```csharp
[Facet(typeof(Entity), IncludeFields = true)]
public partial class EntityDto { }
```

## 3. Excluding Properties/Fields

```csharp
[Facet(typeof(User), exclude: nameof(User.Password))]
public partial class UserDto { }
```

## 4. Custom Mapping with Additional Properties

```csharp
public class UserMapConfig : IFacetMapConfiguration<User, UserDto>
{
    public static void Map(User source, UserDto target)
    {
        target.FullName = $"{source.FirstName} {source.LastName}";
    }
}

[Facet(typeof(User), Configuration = typeof(UserMapConfig))]
public partial class UserDto { public string FullName { get; set; } }
```

## 5. LINQ/EF Core Projections

```csharp
using Facet.Extensions; // provider-agnostic
var query = dbContext.People.SelectFacet<PersonDto>();

using Facet.Extensions.EFCore; // for async EF Core support
var dtosAsync = await dbContext.People.ToFacetsAsync<PersonDto>();
```
---

See [Attribute Reference](03_AttributeReference.md), [Custom Mapping](04_CustomMapping.md), and [Extension Methods](05_Extensions.md) for more details.
