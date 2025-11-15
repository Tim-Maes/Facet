namespace Facet.Tests.UnitTests.Wrapper;

public class Address
{
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string ZipCode { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
}

public class Person
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Address Address { get; set; } = new();
    public string SocialSecurityNumber { get; set; } = string.Empty;
}

[Wrapper(typeof(Address), "Country")]
public partial class PublicAddressWrapper { }

[Wrapper(typeof(Person), "SocialSecurityNumber", NestedWrappers = new[] { typeof(PublicAddressWrapper) })]
public partial class PublicPersonWrapper { }

public partial class NestedWrapperTests
{

    [Fact]
    public void Nested_Wrapper_Should_Wrap_Nested_Properties()
    {
        // Arrange
        var person = new Person
        {
            Id = 1,
            Name = "John Doe",
            Address = new Address
            {
                Street = "123 Main St",
                City = "Springfield",
                ZipCode = "12345",
                Country = "USA"
            },
            SocialSecurityNumber = "123-45-6789"
        };

        // Act
        var wrapper = new PublicPersonWrapper(person);

        // Assert
        wrapper.Id.Should().Be(1);
        wrapper.Name.Should().Be("John Doe");
        wrapper.Address.Should().NotBeNull();
        wrapper.Address.Street.Should().Be("123 Main St");
        wrapper.Address.City.Should().Be("Springfield");
        wrapper.Address.ZipCode.Should().Be("12345");

        // Country should be excluded by PublicAddressWrapper
        wrapper.Address.GetType().GetProperty("Country").Should().BeNull();
    }

    [Fact]
    public void Nested_Wrapper_Changes_Should_Propagate_To_Source()
    {
        // Arrange
        var person = new Person
        {
            Id = 1,
            Name = "Jane Smith",
            Address = new Address { City = "Boston" }
        };
        var wrapper = new PublicPersonWrapper(person);

        // Act
        wrapper.Address.City = "New York";

        // Assert
        person.Address.City.Should().Be("New York");
    }

    [Fact]
    public void Nested_Wrapper_Should_Unwrap_Correctly()
    {
        // Arrange
        var person = new Person { Address = new Address { City = "Seattle" } };
        var wrapper = new PublicPersonWrapper(person);

        // Act
        var unwrapped = wrapper.Unwrap();

        // Assert
        unwrapped.Should().BeSameAs(person);
        unwrapped.Address.City.Should().Be("Seattle");
    }

    [Fact]
    public void Nested_Wrapper_With_Nullable_Should_Handle_Null()
    {
        // Arrange
        var person = new Person { Address = null! };

        // Act
        var wrapper = new PublicPersonWrapper(person);

        // Assert
        wrapper.Should().NotBeNull();
    }
}
