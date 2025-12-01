using Facet.Extensions;
using Facet.Tests.TestModels;
using Facet.Tests.Utilities;

namespace Facet.Tests.UnitTests.Extensions;

public class ApplyFacetTests
{
    [Fact]
    public void ApplyFacet_ShouldUpdateChangedProperties_WhenFacetHasDifferentValues()
    {
        // Arrange
        var user = TestDataFactory.CreateUser("John", "Doe", "john@example.com");
        var facet = new UserDto
        {
            Id = user.Id,
            FirstName = "Jane",  // Changed
            LastName = "Doe",    // Unchanged
            Email = "jane@example.com",  // Changed
            IsActive = user.IsActive,     // Unchanged
            DateOfBirth = user.DateOfBirth,  // Unchanged
            LastLoginAt = user.LastLoginAt   // Unchanged
        };

        // Act
        user.ApplyFacet<User, UserDto>(facet);

        // Assert
        user.FirstName.Should().Be("Jane");
        user.Email.Should().Be("jane@example.com");
        user.LastName.Should().Be("Doe");
        user.IsActive.Should().Be(facet.IsActive);
    }

    [Fact]
    public void ApplyFacet_ShouldNotUpdateUnchangedProperties()
    {
        // Arrange
        var user = TestDataFactory.CreateUser("John", "Doe", "john@example.com");
        var originalLastName = user.LastName;
        var originalEmail = user.Email;

        var facet = new UserDto
        {
            Id = user.Id,
            FirstName = "Jane",  // Changed
            LastName = originalLastName,  // Unchanged
            Email = originalEmail,  // Unchanged
            IsActive = user.IsActive,
            DateOfBirth = user.DateOfBirth,
            LastLoginAt = user.LastLoginAt
        };

        // Act
        user.ApplyFacet<User, UserDto>(facet);

        // Assert
        user.FirstName.Should().Be("Jane");
        user.LastName.Should().Be(originalLastName);
        user.Email.Should().Be(originalEmail);
    }

    [Fact]
    public void ApplyFacet_ShouldHandleNullableProperties()
    {
        // Arrange
        var user = TestDataFactory.CreateUser();
        user.LastLoginAt = DateTime.Now;

        var facet = new UserDto
        {
            Id = user.Id,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email,
            IsActive = user.IsActive,
            DateOfBirth = user.DateOfBirth,
            LastLoginAt = null  // Changed to null
        };

        // Act
        user.ApplyFacet<User, UserDto>(facet);

        // Assert
        user.LastLoginAt.Should().BeNull();
    }

    [Fact]
    public void ApplyFacet_ShouldReturnSourceInstance_ForFluentChaining()
    {
        // Arrange
        var user = TestDataFactory.CreateUser();
        var facet = new UserDto
        {
            Id = user.Id,
            FirstName = "Updated",
            LastName = user.LastName,
            Email = user.Email,
            IsActive = user.IsActive,
            DateOfBirth = user.DateOfBirth,
            LastLoginAt = user.LastLoginAt
        };

        // Act
        var result = user.ApplyFacet<User, UserDto>(facet);

        // Assert
        result.Should().BeSameAs(user);
        result.FirstName.Should().Be("Updated");
    }

    [Fact]
    public void ApplyFacet_WithInferredType_ShouldUpdateProperties()
    {
        // Arrange
        var user = TestDataFactory.CreateUser("John", "Doe", "john@example.com");
        var facet = new UserDto
        {
            Id = user.Id,
            FirstName = "Jane",
            LastName = "Smith",
            Email = user.Email,
            IsActive = user.IsActive,
            DateOfBirth = user.DateOfBirth,
            LastLoginAt = user.LastLoginAt
        };

        // Act
        user.ApplyFacet(facet);

        // Assert
        user.FirstName.Should().Be("Jane");
        user.LastName.Should().Be("Smith");
        user.Email.Should().Be(facet.Email);
    }

    [Fact]
    public void ApplyFacetWithChanges_ShouldReturnChangedPropertyNames()
    {
        // Arrange
        var user = TestDataFactory.CreateUser("John", "Doe", "john@example.com");
        var facet = user.ToFacet<User, UserDto>();

        // Modify specific properties
        facet.FirstName = "Jane";  // Changed
        facet.Email = "jane@example.com";  // Changed
        // LastName unchanged

        // Act
        var result = user.ApplyFacetWithChanges<User, UserDto>(facet);

        // Assert
        result.Source.Should().BeSameAs(user);
        result.HasChanges.Should().BeTrue();
        result.ChangedProperties.Should().Contain("FirstName");
        result.ChangedProperties.Should().Contain("Email");
        result.ChangedProperties.Should().NotContain("LastName");
        result.ChangedProperties.Should().NotContain("Id");
        result.ChangedProperties.Should().HaveCountGreaterThanOrEqualTo(2);
    }

    [Fact]
    public void ApplyFacetWithChanges_ShouldReturnNoChanges_WhenAllPropertiesMatch()
    {
        // Arrange
        var user = TestDataFactory.CreateUser("John", "Doe", "john@example.com");
        var facet = user.ToFacet<User, UserDto>();

        // Act
        var result = user.ApplyFacetWithChanges<User, UserDto>(facet);

        // Assert
        result.HasChanges.Should().BeFalse();
        result.ChangedProperties.Should().BeEmpty();
    }

    [Fact]
    public void ApplyFacet_ShouldThrowArgumentNullException_WhenSourceIsNull()
    {
        // Arrange
        User? user = null;
        var facet = new UserDto();

        // Act & Assert
        var act = () => user!.ApplyFacet<User, UserDto>(facet);
        act.Should().Throw<ArgumentNullException>().WithParameterName("source");
    }

    [Fact]
    public void ApplyFacet_ShouldThrowArgumentNullException_WhenFacetIsNull()
    {
        // Arrange
        var user = TestDataFactory.CreateUser();
        UserDto? facet = null;

        // Act & Assert
        var act = () => user.ApplyFacet<User, UserDto>(facet!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("facet");
    }

    [Fact]
    public void ApplyFacet_ShouldHandleBooleanChanges()
    {
        // Arrange
        var user = TestDataFactory.CreateUser(isActive: true);
        var facet = new UserDto
        {
            Id = user.Id,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email,
            IsActive = false,  // Changed
            DateOfBirth = user.DateOfBirth,
            LastLoginAt = user.LastLoginAt
        };

        // Act
        user.ApplyFacet<User, UserDto>(facet);

        // Assert
        user.IsActive.Should().BeFalse();
    }

    [Fact]
    public void ApplyFacet_ShouldOnlyUpdatePropertiesExistingInBoth()
    {
        // Arrange
        var user = TestDataFactory.CreateUser("John", "Doe", "john@example.com");
        var originalPassword = user.Password;
        var originalCreatedAt = user.CreatedAt;

        var facet = new UserDto
        {
            Id = user.Id,
            FirstName = "Jane",
            LastName = "Smith",
            Email = "jane@example.com",
            IsActive = false,
            DateOfBirth = user.DateOfBirth,
            LastLoginAt = user.LastLoginAt
        };

        // Act
        user.ApplyFacet<User, UserDto>(facet);

        // Assert
        user.FirstName.Should().Be("Jane");
        user.LastName.Should().Be("Smith");
        user.Email.Should().Be("jane@example.com");
        user.IsActive.Should().BeFalse();

        // Properties not in facet should remain unchanged
        user.Password.Should().Be(originalPassword);
        user.CreatedAt.Should().Be(originalCreatedAt);
    }
}
