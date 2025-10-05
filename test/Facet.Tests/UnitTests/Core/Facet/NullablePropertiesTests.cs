using Facet.Tests.TestModels;
using Facet.Tests.Utilities;

namespace Facet.Tests.UnitTests.Core.Facet;

public class NullablePropertiesTests
{
    [Fact]
    public void ProductQueryDto_ShouldHaveAllPropertiesNullable_WhenNullablePropertiesIsTrue()
    {
        // Arrange & Act
        var dtoType = typeof(ProductQueryDto);

        // Assert
        var idProp = dtoType.GetProperty("Id");
        idProp.Should().NotBeNull();
        idProp!.PropertyType.Should().Be(typeof(int?), "Id should be nullable int");

        var nameProp = dtoType.GetProperty("Name");
        nameProp.Should().NotBeNull();
        nameProp!.PropertyType.Should().Be(typeof(string), "Name is a reference type");

        var descriptionProp = dtoType.GetProperty("Description");
        descriptionProp.Should().NotBeNull();
        descriptionProp!.PropertyType.Should().Be(typeof(string), "Description is a reference type");

        var priceProp = dtoType.GetProperty("Price");
        priceProp.Should().NotBeNull();
        priceProp!.PropertyType.Should().Be(typeof(decimal?), "Price should be nullable decimal");

        var categoryIdProp = dtoType.GetProperty("CategoryId");
        categoryIdProp.Should().NotBeNull();
        categoryIdProp!.PropertyType.Should().Be(typeof(int?), "CategoryId should be nullable int");

        var isAvailableProp = dtoType.GetProperty("IsAvailable");
        isAvailableProp.Should().NotBeNull();
        isAvailableProp!.PropertyType.Should().Be(typeof(bool?), "IsAvailable should be nullable bool");

        // Excluded properties should not exist
        dtoType.GetProperty("InternalNotes").Should().BeNull("InternalNotes should be excluded");
        dtoType.GetProperty("CreatedAt").Should().BeNull("CreatedAt should be excluded");
    }

    [Fact]
    public void UserQueryDto_ShouldHaveAllPropertiesNullable_WhenNullablePropertiesIsTrue()
    {
        // Arrange & Act
        var dtoType = typeof(UserQueryDto);

        // Assert - All properties should be nullable
        var idProp = dtoType.GetProperty("Id");
        idProp.Should().NotBeNull();
        idProp!.PropertyType.Should().Be(typeof(int?), "Id should be nullable int");

        var firstNameProp = dtoType.GetProperty("FirstName");
        firstNameProp.Should().NotBeNull();
        firstNameProp!.PropertyType.Should().Be(typeof(string), "FirstName is a reference type");

        var lastNameProp = dtoType.GetProperty("LastName");
        lastNameProp.Should().NotBeNull();
        lastNameProp!.PropertyType.Should().Be(typeof(string), "LastName is a reference type");

        var emailProp = dtoType.GetProperty("Email");
        emailProp.Should().NotBeNull();
        emailProp!.PropertyType.Should().Be(typeof(string), "Email is a reference type");

        var dateOfBirthProp = dtoType.GetProperty("DateOfBirth");
        dateOfBirthProp.Should().NotBeNull();
        dateOfBirthProp!.PropertyType.Should().Be(typeof(DateTime?), "DateOfBirth should be nullable DateTime");

        var isActiveProp = dtoType.GetProperty("IsActive");
        isActiveProp.Should().NotBeNull();
        isActiveProp!.PropertyType.Should().Be(typeof(bool?), "IsActive should be nullable bool");

        var lastLoginAtProp = dtoType.GetProperty("LastLoginAt");
        lastLoginAtProp.Should().NotBeNull();
        lastLoginAtProp!.PropertyType.Should().Be(typeof(DateTime?), "LastLoginAt should remain nullable DateTime");

        // Excluded properties should not exist
        dtoType.GetProperty("Password").Should().BeNull("Password should be excluded");
        dtoType.GetProperty("CreatedAt").Should().BeNull("CreatedAt should be excluded");
    }

    [Fact]
    public void UserWithEnumQueryDto_ShouldHaveEnumAsNullable_WhenNullablePropertiesIsTrue()
    {
        // Arrange & Act
        var dtoType = typeof(UserWithEnumQueryDto);

        // Assert - Enum property should be nullable
        var statusProp = dtoType.GetProperty("Status");
        statusProp.Should().NotBeNull();
        statusProp!.PropertyType.Should().Be(typeof(UserStatus?), "Status should be nullable UserStatus enum");

        var idProp = dtoType.GetProperty("Id");
        idProp.Should().NotBeNull();
        idProp!.PropertyType.Should().Be(typeof(int?), "Id should be nullable int");

        var nameProp = dtoType.GetProperty("Name");
        nameProp.Should().NotBeNull();
        nameProp!.PropertyType.Should().Be(typeof(string), "Name is a reference type");

        var emailProp = dtoType.GetProperty("Email");
        emailProp.Should().NotBeNull();
        emailProp!.PropertyType.Should().Be(typeof(string), "Email is a reference type");
    }

    [Fact]
    public void ProductQueryDto_ShouldCreateInstance_WithNullValues()
    {
        // Arrange & Act
        var dto = new ProductQueryDto();

        // Assert
        dto.Should().NotBeNull();
        dto.Id.Should().BeNull("Id should default to null");
        dto.Name.Should().BeNull("Name should default to null");
        dto.Description.Should().BeNull("Description should default to null");
        dto.Price.Should().BeNull("Price should default to null");
        dto.CategoryId.Should().BeNull("CategoryId should default to null");
        dto.IsAvailable.Should().BeNull("IsAvailable should default to null");
    }

    [Fact]
    public void ProductQueryDto_ShouldMapFromSource_WithNullableProperties()
    {
        // Arrange
        var product = TestDataFactory.CreateProduct("Test Product", 99.99m);

        // Act
        var dto = new ProductQueryDto(product);

        // Assert
        dto.Id.Should().Be(product.Id);
        dto.Name.Should().Be(product.Name);
        dto.Description.Should().Be(product.Description);
        dto.Price.Should().Be(product.Price);
        dto.CategoryId.Should().Be(product.CategoryId);
        dto.IsAvailable.Should().Be(product.IsAvailable);
    }

    [Fact]
    public void UserQueryDto_ShouldMapFromSource_WithNullableProperties()
    {
        // Arrange
        var user = TestDataFactory.CreateUser("Jane", "Doe", "jane@example.com");

        // Act
        var dto = new UserQueryDto(user);

        // Assert - Values should be mapped correctly
        dto.Id.Should().Be(user.Id);
        dto.FirstName.Should().Be(user.FirstName);
        dto.LastName.Should().Be(user.LastName);
        dto.Email.Should().Be(user.Email);
        dto.DateOfBirth.Should().Be(user.DateOfBirth);
        dto.IsActive.Should().Be(user.IsActive);
        dto.LastLoginAt.Should().Be(user.LastLoginAt);
    }

    [Fact]
    public void ProductQueryDto_ShouldAllowPartialData_ForQueryScenarios()
    {
        // Arrange & Act
        var queryDto = new ProductQueryDto
        {
            Name = "Test",
            Price = 50.00m
            // Other fields remain null
        };

        // Assert
        queryDto.Name.Should().Be("Test");
        queryDto.Price.Should().Be(50.00m);
        queryDto.Id.Should().BeNull();
        queryDto.Description.Should().BeNull();
        queryDto.CategoryId.Should().BeNull();
        queryDto.IsAvailable.Should().BeNull();
    }
}
