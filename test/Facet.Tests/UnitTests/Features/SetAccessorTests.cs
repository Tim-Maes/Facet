using System.Reflection;
using Facet.Tests.TestModels;

namespace Facet.Tests.UnitTests.Features;

public class SetAccessorTests
{
    [Fact]
    public void SetAccessor_Init_AllPropertiesAreInitOnly()
    {
        var type = typeof(UserImmutableDto);
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead)
            .ToArray();

        properties.Should().NotBeEmpty();
        foreach (var prop in properties)
        {
            var setter = prop.SetMethod;
            setter.Should().NotBeNull($"Property {prop.Name} should have a setter");
            setter!.ReturnParameter.GetRequiredCustomModifiers()
                .Should().Contain(typeof(System.Runtime.CompilerServices.IsExternalInit),
                    $"Property {prop.Name} should be init-only");
        }
    }

    [Fact]
    public void SetAccessor_Init_CanConstructViaObjectInitializer()
    {
        var dto = new UserImmutableDto
        {
            Id = 1,
            FirstName = "Alice",
            LastName = "Smith",
            Email = "alice@example.com",
        };

        dto.Id.Should().Be(1);
        dto.FirstName.Should().Be("Alice");
        dto.LastName.Should().Be("Smith");
        dto.Email.Should().Be("alice@example.com");
    }

    [Fact]
    public void SetAccessor_Init_CanConstructFromSource()
    {
        var user = new User
        {
            Id = 42,
            FirstName = "Bob",
            LastName = "Jones",
            Email = "bob@example.com",
            IsActive = true,
        };

        var dto = new UserImmutableDto(user);

        dto.Id.Should().Be(42);
        dto.FirstName.Should().Be("Bob");
        dto.Email.Should().Be("bob@example.com");
    }

    [Fact]
    public void SetAccessor_Set_AllPropertiesHaveMutableSetter()
    {
        var type = typeof(UserMutableDto);
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite)
            .ToArray();

        properties.Should().NotBeEmpty();
        foreach (var prop in properties)
        {
            var setter = prop.SetMethod;
            setter.Should().NotBeNull($"Property {prop.Name} should have a setter");
            setter!.ReturnParameter.GetRequiredCustomModifiers()
                .Should().NotContain(typeof(System.Runtime.CompilerServices.IsExternalInit),
                    $"Property {prop.Name} should NOT be init-only");
        }
    }

    [Fact]
    public void SetAccessor_Set_CanMutateAfterConstruction()
    {
        var user = new User { Id = 1, FirstName = "Alice", LastName = "Smith", Email = "a@b.com", IsActive = true };
        var dto = new UserMutableDto(user);

        dto.FirstName = "Changed";

        dto.FirstName.Should().Be("Changed");
    }

    [Fact]
    public void SetAccessor_Set_OverridesInitOnlyFromSource()
    {
        var type = typeof(ImmutableEntityMutableDto);
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite)
            .ToArray();

        properties.Should().NotBeEmpty();
        foreach (var prop in properties)
        {
            prop.SetMethod!.ReturnParameter.GetRequiredCustomModifiers()
                .Should().NotContain(typeof(System.Runtime.CompilerServices.IsExternalInit),
                    $"SetAccessor.Set should force set on init-only source property {prop.Name}");
        }
    }

    [Fact]
    public void SetAccessor_Preserve_KeepsInitOnlyFromSource()
    {
        var type = typeof(ImmutableEntityPreserveDto);
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead)
            .ToArray();

        properties.Should().NotBeEmpty();
        foreach (var prop in properties)
        {
            prop.SetMethod!.ReturnParameter.GetRequiredCustomModifiers()
                .Should().Contain(typeof(System.Runtime.CompilerServices.IsExternalInit),
                    $"SetAccessor.Preserve should keep init-only accessor for {prop.Name}");
        }
    }
}
