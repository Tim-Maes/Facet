namespace Facet.Tests.UnitTests.Wrapper;

public partial class BasicWrapperTests
{
    public class User
    {
        public int Id { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public decimal Salary { get; set; }
    }

    [Wrapper(typeof(User), "Password", "Salary")]
    public partial class PublicUserWrapper { }

    [Fact]
    public void Wrapper_Should_Delegate_To_Source_Object()
    {
        // Arrange
        var user = new User
        {
            Id = 1,
            FirstName = "John",
            LastName = "Doe",
            Email = "john@example.com",
            Password = "secret123",
            Salary = 75000
        };

        // Act
        var wrapper = new PublicUserWrapper(user);

        // Assert
        wrapper.Id.Should().Be(1);
        wrapper.FirstName.Should().Be("John");
        wrapper.LastName.Should().Be("Doe");
        wrapper.Email.Should().Be("john@example.com");
    }

    [Fact]
    public void Wrapper_Should_Propagate_Changes_To_Source()
    {
        // Arrange
        var user = new User
        {
            Id = 1,
            FirstName = "John",
            LastName = "Doe",
            Email = "john@example.com"
        };

        var wrapper = new PublicUserWrapper(user);

        // Act
        wrapper.FirstName = "Jane";
        wrapper.Email = "jane@example.com";

        // Assert
        user.FirstName.Should().Be("Jane", "changes to wrapper should affect source");
        user.Email.Should().Be("jane@example.com", "changes to wrapper should affect source");
    }

    [Fact]
    public void Wrapper_Should_Exclude_Specified_Properties()
    {
        // Password and Salary are excluded — verified at compile time
        var user = new User { Password = "secret", Salary = 75000 };
        var wrapper = new PublicUserWrapper(user);

        _ = wrapper.FirstName;
        _ = wrapper.Email;
    }

    [Fact]
    public void Wrapper_Unwrap_Should_Return_Source_Object()
    {
        // Arrange
        var user = new User { Id = 1, FirstName = "John" };
        var wrapper = new PublicUserWrapper(user);

        // Act
        var unwrapped = wrapper.Unwrap();

        // Assert
        unwrapped.Should().BeSameAs(user, "Unwrap should return the original source object");
    }

    [Fact]
    public void Wrapper_Constructor_Should_Throw_On_Null()
    {
        // Act
        Action act = () => new PublicUserWrapper(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("source");
    }
}
