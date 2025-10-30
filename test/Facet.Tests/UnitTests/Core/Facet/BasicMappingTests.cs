using Facet.Tests.TestModels;
using Facet.Tests.Utilities;

namespace Facet.Tests.UnitTests.Core.Facet;

public class BasicMappingTests
{
    [Fact]
    public void ToFacet_ShouldMapBasicProperties_WhenMappingUserToDto()
    {
        // Arrange
        var user = TestDataFactory.CreateUser("John", "Doe", "john@example.com");

        // Act
        var dto = user.ToFacet<User, UserDto>();

        // Assert
        dto.Should().NotBeNull();
        dto.Id.Should().Be(user.Id);
        dto.FirstName.Should().Be("John");
        dto.LastName.Should().Be("John");
        dto.Email.Should().Be("john@example.com");
        dto.IsActive.Should().Be(user.IsActive);
        dto.DateOfBirth.Should().Be(user.DateOfBirth);
        dto.LastLoginAt.Should().Be(user.LastLoginAt);
    }

    [Fact]
    public void ToFacet_ShouldExcludeSpecifiedProperties_WhenMappingUser()
    {
        // Arrange
        var user = TestDataFactory.CreateUser();

        // Act
        var dto = user.ToFacet<User, UserDto>();

        // Assert
        var dtoType = dto.GetType();
        dtoType.GetProperty("Password").Should().BeNull("Password should be excluded");
        dtoType.GetProperty("CreatedAt").Should().BeNull("CreatedAt should be excluded");
    }

    [Fact]
    public void ToFacet_ShouldMapProductProperties_ExcludingInternalNotes()
    {
        // Arrange
        var product = TestDataFactory.CreateProduct("Test Product", 49.99m);

        // Act
        var dto = product.ToFacet<Product, ProductDto>();

        // Assert
        dto.Should().NotBeNull();
        dto.Id.Should().Be(product.Id);
        dto.Name.Should().Be("Test Product");
        dto.Description.Should().Be(product.Description);
        dto.Price.Should().Be(49.99m);
        dto.IsAvailable.Should().Be(product.IsAvailable);
        
        var dtoType = dto.GetType();
        dtoType.GetProperty("InternalNotes").Should().BeNull("InternalNotes should be excluded");
    }

    [Fact]
    public void ToFacet_ShouldHandleNullableProperties_Correctly()
    {
        // Arrange
        var user = TestDataFactory.CreateUser();
        user.LastLoginAt = null;

        // Act
        var dto = user.ToFacet<User, UserDto>();

        // Assert
        dto.LastLoginAt.Should().BeNull();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ToFacet_ShouldPreserveBooleanValues_ForIsActiveProperty(bool isActive)
    {
        // Arrange
        var user = TestDataFactory.CreateUser(isActive: isActive);

        // Act
        var dto = user.ToFacet<User, UserDto>();

        // Assert
        dto.IsActive.Should().Be(isActive);
    }

    [Fact]
    public void ToFacet_ShouldMapMultipleUsers_WithDifferentData()
    {
        // Arrange
        var users = TestDataFactory.CreateUsers();

        // Act
        var dtos = users.Select(u => u.ToFacet<User, UserDto>()).ToList();

        // Assert
        dtos.Should().HaveCount(3);
        dtos[0].FirstName.Should().Be("Alice");
        dtos[1].FirstName.Should().Be("Bob");
        dtos[2].FirstName.Should().Be("Charlie");
        dtos[2].IsActive.Should().BeFalse();
    }
}