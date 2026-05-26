using Facet.Extensions;
using Facet.Tests.TestModels;
using Facet.Tests.Utilities;

namespace Facet.Tests.UnitTests.Extensions;

public class ApplyFacetTests
{
    [Fact]
    public void ApplyFacet_ShouldUpdateChangedProperties_WhenFacetHasDifferentValues()
    {
        var user = TestDataFactory.CreateUser("John", "Doe", "john@example.com");
        var facet = new UserDto
        {
            Id = user.Id,
            FirstName = "Jane",  
            LastName = "Doe",    
            Email = "jane@example.com",  
            IsActive = user.IsActive,     
            DateOfBirth = user.DateOfBirth,  
            LastLoginAt = user.LastLoginAt   
        };

        user.ApplyFacet<User, UserDto>(facet);

        user.FirstName.Should().Be("Jane");
        user.Email.Should().Be("jane@example.com");
        user.LastName.Should().Be("Doe");
        user.IsActive.Should().Be(facet.IsActive);
    }

    [Fact]
    public void ApplyFacet_ShouldNotUpdateUnchangedProperties()
    {
        var user = TestDataFactory.CreateUser("John", "Doe", "john@example.com");
        var originalLastName = user.LastName;
        var originalEmail = user.Email;

        var facet = new UserDto
        {
            Id = user.Id,
            FirstName = "Jane",  
            LastName = originalLastName,  
            Email = originalEmail,  
            IsActive = user.IsActive,
            DateOfBirth = user.DateOfBirth,
            LastLoginAt = user.LastLoginAt
        };

        user.ApplyFacet<User, UserDto>(facet);

        user.FirstName.Should().Be("Jane");
        user.LastName.Should().Be(originalLastName);
        user.Email.Should().Be(originalEmail);
    }

    [Fact]
    public void ApplyFacet_ShouldHandleNullableProperties()
    {
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
            LastLoginAt = null  
        };

        user.ApplyFacet<User, UserDto>(facet);

        user.LastLoginAt.Should().BeNull();
    }

    [Fact]
    public void ApplyFacet_ShouldReturnSourceInstance_ForFluentChaining()
    {
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

        var result = user.ApplyFacet<User, UserDto>(facet);

        result.Should().BeSameAs(user);
        result.FirstName.Should().Be("Updated");
    }

    [Fact]
    public void ApplyFacet_WithInferredType_ShouldUpdateProperties()
    {
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

        user.ApplyFacet(facet);

        user.FirstName.Should().Be("Jane");
        user.LastName.Should().Be("Smith");
        user.Email.Should().Be(facet.Email);
    }

    [Fact]
    public void ApplyFacetWithChanges_ShouldReturnChangedPropertyNames()
    {
        var user = TestDataFactory.CreateUser("John", "Doe", "john@example.com");
        var facet = user.ToFacet<User, UserDto>();

        facet.FirstName = "Jane";  
        facet.Email = "jane@example.com";  
        
        var result = user.ApplyFacetWithChanges<User, UserDto>(facet);

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
        var user = TestDataFactory.CreateUser("John", "Doe", "john@example.com");
        var facet = user.ToFacet<User, UserDto>();

        var result = user.ApplyFacetWithChanges<User, UserDto>(facet);

        result.HasChanges.Should().BeFalse();
        result.ChangedProperties.Should().BeEmpty();
    }

    [Fact]
    public void ApplyFacet_ShouldThrowArgumentNullException_WhenSourceIsNull()
    {
        User? user = null;
        var facet = new UserDto();

        var act = () => user!.ApplyFacet<User, UserDto>(facet);
        act.Should().Throw<ArgumentNullException>().WithParameterName("source");
    }

    [Fact]
    public void ApplyFacet_ShouldThrowArgumentNullException_WhenFacetIsNull()
    {
        var user = TestDataFactory.CreateUser();
        UserDto? facet = null;

        var act = () => user.ApplyFacet<User, UserDto>(facet!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("facet");
    }

    [Fact]
    public void ApplyFacet_ShouldHandleBooleanChanges()
    {
        var user = TestDataFactory.CreateUser(isActive: true);
        var facet = new UserDto
        {
            Id = user.Id,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email,
            IsActive = false,  
            DateOfBirth = user.DateOfBirth,
            LastLoginAt = user.LastLoginAt
        };

        user.ApplyFacet<User, UserDto>(facet);

        user.IsActive.Should().BeFalse();
    }

    [Fact]
    public void ApplyFacet_ShouldOnlyUpdatePropertiesExistingInBoth()
    {
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

        user.ApplyFacet<User, UserDto>(facet);

        user.FirstName.Should().Be("Jane");
        user.LastName.Should().Be("Smith");
        user.Email.Should().Be("jane@example.com");
        user.IsActive.Should().BeFalse();

        user.Password.Should().Be(originalPassword);
        user.CreatedAt.Should().Be(originalCreatedAt);
    }
}
