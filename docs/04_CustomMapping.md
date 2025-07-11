# Custom Mapping with IFacetMapConfiguration

Facet supports custom mapping logic for advanced scenarios via the `IFacetMapConfiguration<TSource, TTarget>` interface.

## When to Use Custom Mapping
- You need to compute derived properties.
- You want to format or transform values.
- You need to inject additional logic during mapping.

## How to Use

1. **Implement the Interface:**

```csharp
using Facet.Mapping;

public class UserMapConfig : IFacetMapConfiguration<User, UserDto>
{
    public static void Map(User source, UserDto target)
    {
        target.FullName = $"{source.FirstName} {source.LastName}";
    }
}
```

2. **Reference in the Facet Attribute:**

```csharp
[Facet(typeof(User), Configuration = typeof(UserMapConfig))]
public partial class UserDto { public string FullName { get; set; } }
```

The generated constructor will call your `Map` method after copying properties.

## Notes

- The `Map` method must be `public static` and match the signature.
- You can use this to set any additional or computed properties.

---
