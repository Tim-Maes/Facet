# Wrapper Attribute - Reference-Based Property Delegation

## Overview

The `[Wrapper]` attribute generates wrapper classes that **delegate** to a source object instance, creating a reference-based facade pattern. Unlike `[Facet]` which creates independent value copies, wrappers maintain a reference to the source object, so changes to wrapper properties affect the underlying source.

## When to Use Wrapper vs Facet

| Use Case | Use Wrapper | Use Facet |
|----------|-------------|-----------|
| DTOs for API/serialization | ❌ | ✅ |
| EF Core query projections | ❌ | ✅ |
| Facade pattern (hide properties) | ✅ | ❌ |
| ViewModel with live binding | ✅ | ❌ |
| Decorator pattern | ✅ | ❌ |
| Read-only views | ✅ | ❌ |
| Memory efficiency (avoid duplication) | ✅ | ❌ |
| Disconnected data transfer | ❌ | ✅ |

## Basic Usage

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

// Wrapper that hides sensitive properties
[Wrapper(typeof(User), "Password", "Salary")]
public partial class PublicUserWrapper { }

// Usage
var user = new User
{
    Id = 1,
    FirstName = "John",
    Password = "secret123",
    Salary = 75000
};

var wrapper = new PublicUserWrapper(user);

// Read from wrapper (delegates to source)
Console.WriteLine(wrapper.FirstName); // "John"

// Modify through wrapper (affects source!)
wrapper.FirstName = "Jane";
Console.WriteLine(user.FirstName); // "Jane"

// Sensitive properties not accessible
// wrapper.Password;  // ❌ Compile error
// wrapper.Salary;    // ❌ Compile error
```

## Attribute Parameters

### Constructor Parameters

```csharp
[Wrapper(Type sourceType, params string[] exclude)]
```

- **sourceType**: The type to wrap and delegate to
- **exclude**: Property/field names to exclude from the wrapper

### Named Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `Include` | `string[]?` | `null` | Include only these properties (mutually exclusive with Exclude) |
| `IncludeFields` | `bool` | `false` | Include public fields from source type |
| `ReadOnly` | `bool` | `false` | Generate get-only properties (immutable facade) |
| `CopyAttributes` | `bool` | `false` | Copy validation attributes from source to wrapper |
| `UseFullName` | `bool` | `false` | Use full type name for generated file |

## Include/Exclude Patterns

### Exclude Pattern (Default)

```csharp
// Exclude specific properties
[Wrapper(typeof(User), "Password", "Salary", "SocialSecurity")]
public partial class PublicUserWrapper { }
```

### Include Pattern

```csharp
// Only include specific properties
[Wrapper(typeof(User), Include = ["Id", "FirstName", "LastName", "Email"])]
public partial class UserContactWrapper { }
```

## Read-Only Wrappers

Generate immutable facades that prevent accidental modifications:

```csharp
[Wrapper(typeof(Product), ReadOnly = true)]
public partial class ReadOnlyProductView { }

var product = new Product { Name = "Laptop", Price = 1299.99m };
var view = new ReadOnlyProductView(product);

// Can read
Console.WriteLine(view.Name);    // "Laptop"
Console.WriteLine(view.Price);   // 1299.99

// Cannot write (compile error CS0200)
// view.Name = "Desktop";  // Property is read-only
// view.Price = 999.99m;   // Property is read-only

// Still reflects source changes
product.Name = "Desktop";
Console.WriteLine(view.Name);    // "Desktop"
```

### Use Cases for ReadOnly Wrappers

- **Security**: Prevent modifications to sensitive domain objects
- **API Design**: Provide read-only views to consumers
- **Defensive Programming**: Ensure certain contexts can't mutate state
- **Event Handlers**: Pass immutable views to prevent side effects

## Copying Attributes

Copy validation and other attributes from source to wrapper:

```csharp
public class Product
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; }

    [Range(0, 10000)]
    public decimal Price { get; set; }
}

[Wrapper(typeof(Product), CopyAttributes = true)]
public partial class ProductWrapper { }
```

Generated code:

```csharp
public partial class ProductWrapper
{
    [Required]
    [StringLength(100)]
    public string Name
    {
        get => _source.Name;
        set => _source.Name = value;
    }

    [Range(0, 10000)]
    public decimal Price
    {
        get => _source.Price;
        set => _source.Price = value;
    }
}
```

## Including Fields

By default, only properties are wrapped. Enable field wrapping:

```csharp
public class Entity
{
    public int Id;  // Field
    public string Name { get; set; }  // Property
}

[Wrapper(typeof(Entity), IncludeFields = true)]
public partial class EntityWrapper { }
```

## Generated Code Structure

### Mutable Wrapper (Default)

```csharp
[Wrapper(typeof(User), "Password")]
public partial class UserWrapper { }
```

Generates:

```csharp
public partial class UserWrapper
{
    private readonly User _source;

    /// <summary>
    /// Initializes a new instance of the UserWrapper wrapper.
    /// </summary>
    /// <param name="source">The source object to wrap.</param>
    /// <exception cref="System.ArgumentNullException">Thrown when source is null.</exception>
    public UserWrapper(User source)
    {
        _source = source ?? throw new System.ArgumentNullException(nameof(source));
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

    /// <summary>
    /// Returns the wrapped source object.
    /// </summary>
    public User Unwrap() => _source;
}
```

### Read-Only Wrapper

```csharp
[Wrapper(typeof(User), "Password", ReadOnly = true)]
public partial class ReadOnlyUserWrapper { }
```

Generates:

```csharp
public partial class ReadOnlyUserWrapper
{
    private readonly User _source;

    public ReadOnlyUserWrapper(User source)
    {
        _source = source ?? throw new System.ArgumentNullException(nameof(source));
    }

    public int Id
    {
        get => _source.Id;
        // No setter - read-only!
    }

    public string FirstName
    {
        get => _source.FirstName;
        // No setter - read-only!
    }

    public User Unwrap() => _source;
}
```

## Unwrap Method

Every wrapper includes an `Unwrap()` method to access the underlying source:

```csharp
var user = new User { Id = 1, FirstName = "John" };
var wrapper = new PublicUserWrapper(user);

// Access the original source object
User original = wrapper.Unwrap();
Console.WriteLine(ReferenceEquals(user, original)); // True
```

## Null Safety

All wrappers include null checks in the constructor:

```csharp
var wrapper = new UserWrapper(null);
// Throws ArgumentNullException with parameter name "source"
```

## Common Patterns

### API Facade Pattern

Hide internal/sensitive properties from API consumers:

```csharp
public class Order
{
    public int Id { get; set; }
    public string OrderNumber { get; set; }
    public decimal Total { get; set; }
    public decimal InternalCost { get; set; }  // Internal only
    public decimal ProfitMargin { get; set; }   // Internal only
}

[Wrapper(typeof(Order), "InternalCost", "ProfitMargin")]
public partial class PublicOrderView { }

// API endpoint
public PublicOrderView GetOrder(int id)
{
    Order order = _repository.GetOrder(id);
    return new PublicOrderView(order);
}
```

### ViewModel Pattern

Expose domain model subset to UI layer:

```csharp
public class Customer
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
    public byte[] PasswordHash { get; set; }
    public string SecurityToken { get; set; }
}

[Wrapper(typeof(Customer), "PasswordHash", "SecurityToken")]
public partial class CustomerViewModel { }

// View model usage
var customer = _customerService.GetCustomer(id);
var viewModel = new CustomerViewModel(customer);
DataContext = viewModel;  // UI binds to wrapper, changes propagate
```

### Testing Instrumentation

Wrap domain objects with test tracking:

```csharp
[Wrapper(typeof(User))]
public partial class InstrumentedUserWrapper { }

// Test helper
public partial class InstrumentedUserWrapper
{
    public List<string> AccessedProperties { get; } = new();

    // Override generated properties to add tracking
    public new string FirstName
    {
        get
        {
            AccessedProperties.Add(nameof(FirstName));
            return _source.FirstName;
        }
    }
}
```

## Best Practices

### Do

- Use wrappers for **runtime facades** and **ViewModels**
- Use wrappers when you need **synchronized changes** between wrapper and source
- Use `ReadOnly = true` for **immutable views** and **security**
- Use wrappers to **hide sensitive properties** from external consumers
- Combine with Facet when you need both patterns for different purposes

### Don't

- Don't use wrappers for **DTOs** or **data transfer** (use Facet instead)
- Don't use wrappers for **EF Core query projections** (use Facet instead)
- Don't use wrappers for **serialization** (use Facet instead)
- Don't wrap **Facets** - both should target the same source type

## Performance Considerations

- **Memory**: Wrappers add minimal overhead (one reference field)
- **CPU**: Property access is a simple field dereference (very fast)
- **GC**: Wrapper keeps source alive as long as wrapper exists
- **No reflection**: All property access is direct, compile-time bound

## Comparison: Wrapper vs Facet

```csharp
public class User { public string Name { get; set; } }

// Facet: Creates independent copy
[Facet(typeof(User))]
public partial class UserDto { }

var user = new User { Name = "John" };
var dto = user.ToFacet<User, UserDto>();
dto.Name = "Jane";
Console.WriteLine(user.Name);  // "John" - independent

// Wrapper: Delegates to source
[Wrapper(typeof(User))]
public partial class UserWrapper { }

var wrapper = new UserWrapper(user);
wrapper.Name = "Jane";
Console.WriteLine(user.Name);  // "Jane" - synchronized!
```

| Aspect | Facet | Wrapper |
|--------|-------|---------|
| Data Storage | Independent copy | Reference to source |
| Memory | Duplicates data | No duplication |
| Changes | Independent | Synchronized to source |
| Use Case | DTOs, EF projections | Facades, ViewModels |
| EF Core | Query projections | Not applicable |
| Serialization | Safe | Serializes wrapper, not source |

## Limitations

The following features are planned:

- **NestedWrappers**: Recursive wrapping of nested objects
- **Custom Mapping**: Add computed properties via configuration
- **Init-only Properties**: Support for C# 9+ init accessors
- **Full Records Support**: Enhanced record type support

## See Also

- [Facet Attribute](03_AttributeReference.md) - For value-copying behavior
- [Advanced Scenarios](06_AdvancedScenarios.md) - Complex patterns
- [Quick Start](02_QuickStart.md) - Getting started with Facet
