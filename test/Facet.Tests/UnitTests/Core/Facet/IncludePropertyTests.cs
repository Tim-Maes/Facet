using Facet.Tests.TestModels;
using Facet.Tests.Utilities;

namespace Facet.Tests.UnitTests.Core.Facet;

public class IncludePropertyTests
{
    [Fact]
    public void ToFacet_WithInclude_ShouldOnlyIncludeSpecifiedProperties()
    {
        // Arrange
        var user = TestDataFactory.CreateUser("John", "Doe");

        // Act
        var dto = user.ToFacet<User, UserIncludeDto>();

        // Assert
        dto.Should().NotBeNull();
        dto.FirstName.Should().Be("John");
        dto.LastName.Should().Be("Doe");
        dto.Email.Should().Be(user.Email);
        
        // Verify that excluded properties are not present
        var dtoType = dto.GetType();
        dtoType.GetProperty("Id").Should().BeNull("Id should not be included");
        dtoType.GetProperty("DateOfBirth").Should().BeNull("DateOfBirth should not be included");
        dtoType.GetProperty("Password").Should().BeNull("Password should not be included");
        dtoType.GetProperty("IsActive").Should().BeNull("IsActive should not be included");
        dtoType.GetProperty("CreatedAt").Should().BeNull("CreatedAt should not be included");
        dtoType.GetProperty("LastLoginAt").Should().BeNull("LastLoginAt should not be included");
    }

    [Fact]
    public void ToFacet_WithInclude_ShouldWorkWithSingleProperty()
    {
        // Arrange
        var user = TestDataFactory.CreateUser("Jane", "Smith");

        // Act
        var dto = user.ToFacet<User, UserSingleIncludeDto>();

        // Assert
        dto.Should().NotBeNull();
        dto.FirstName.Should().Be("Jane");
        
        // Verify that all other properties are not present
        var dtoType = dto.GetType();
        dtoType.GetProperty("LastName").Should().BeNull("LastName should not be included");
        dtoType.GetProperty("Email").Should().BeNull("Email should not be included");
        dtoType.GetProperty("Id").Should().BeNull("Id should not be included");
    }

    [Fact]
    public void ToFacet_WithInclude_ShouldWorkWithProductEntity()
    {
        // Arrange
        var product = TestDataFactory.CreateProduct("Test Product", 99.99m);

        // Act
        var dto = product.ToFacet<Product, ProductIncludeDto>();

        // Assert
        dto.Should().NotBeNull();
        dto.Name.Should().Be("Test Product");
        dto.Price.Should().Be(99.99m);
        
        // Verify that excluded properties are not present
        var dtoType = dto.GetType();
        dtoType.GetProperty("Id").Should().BeNull("Id should not be included");
        dtoType.GetProperty("Description").Should().BeNull("Description should not be included");
        dtoType.GetProperty("CategoryId").Should().BeNull("CategoryId should not be included");
        dtoType.GetProperty("IsAvailable").Should().BeNull("IsAvailable should not be included");
        dtoType.GetProperty("CreatedAt").Should().BeNull("CreatedAt should not be included");
        dtoType.GetProperty("InternalNotes").Should().BeNull("InternalNotes should not be included");
    }

    [Fact]
    public void ToFacet_WithInclude_ShouldPreservePropertyTypes()
    {
        // Arrange
        var user = TestDataFactory.CreateUser("Type", "Test");

        // Act
        var dto = user.ToFacet<User, UserIncludeDto>();

        // Assert
        var dtoType = dto.GetType();
        var firstNameProp = dtoType.GetProperty("FirstName");
        var lastNameProp = dtoType.GetProperty("LastName");
        var emailProp = dtoType.GetProperty("Email");
        
        firstNameProp.Should().NotBeNull();
        firstNameProp!.PropertyType.Should().Be(typeof(string));
        
        lastNameProp.Should().NotBeNull();
        lastNameProp!.PropertyType.Should().Be(typeof(string));
        
        emailProp.Should().NotBeNull();
        emailProp!.PropertyType.Should().Be(typeof(string));
    }

    [Fact]
    public void ToFacet_WithInclude_ShouldWorkWithInheritedProperties()
    {
        // Arrange
        var employee = TestDataFactory.CreateEmployee("Include", "Test", "Engineering");

        // Act
        var dto = employee.ToFacet<Employee, EmployeeIncludeDto>();

        // Assert
        dto.Should().NotBeNull();
        dto.FirstName.Should().Be("Include"); // From User base class
        dto.LastName.Should().Be("Test"); // From User base class
        dto.Department.Should().Be("Engineering"); // From Employee class
        
        // Verify excluded properties are not present
        var dtoType = dto.GetType();
        dtoType.GetProperty("Id").Should().BeNull("Id should not be included");
        dtoType.GetProperty("Email").Should().BeNull("Email should not be included");
        dtoType.GetProperty("EmployeeId").Should().BeNull("EmployeeId should not be included");
        dtoType.GetProperty("Salary").Should().BeNull("Salary should not be included");
    }

    [Fact]
    public void ToFacet_WithInclude_AndCustomProperties_ShouldWork()
    {
        // Arrange
        var user = TestDataFactory.CreateUser("Custom", "Props");

        // Act  
        var dto = user.ToFacet<User, UserIncludeWithCustomDto>();

        // Assert
        dto.Should().NotBeNull();
        dto.FirstName.Should().Be("Custom");
        dto.LastName.Should().Be("Props");
        
        // Custom property should exist and have default value
        dto.FullName.Should().Be(string.Empty);
        
        // Verify excluded properties are not present
        var dtoType = dto.GetType();
        dtoType.GetProperty("Email").Should().BeNull("Email should not be included");
        dtoType.GetProperty("Id").Should().BeNull("Id should not be included");
    }

    [Fact]
    public void ToFacet_WithInclude_ShouldSupportModernRecordTypes()
    {
        // Arrange
        var modernUser = TestDataFactory.CreateModernUser("Modern", "Include");

        // Act
        var dto = modernUser.ToFacet<ModernUser, ModernUserIncludeDto>();

        // Assert
        dto.Should().NotBeNull();
        dto.FirstName.Should().Be("Modern");
        dto.LastName.Should().Be("Include");
        
        // Verify excluded properties are not present
        var dtoType = dto.GetType();
        dtoType.GetProperty("Id").Should().BeNull("Id should not be included");
        dtoType.GetProperty("Email").Should().BeNull("Email should not be included");
        dtoType.GetProperty("CreatedAt").Should().BeNull("CreatedAt should not be included");
        dtoType.GetProperty("Bio").Should().BeNull("Bio should not be included");
        dtoType.GetProperty("PasswordHash").Should().BeNull("PasswordHash should not be included");
    }

    [Fact]
    public void ToFacet_WithInclude_ShouldWorkWithFields_WhenIncludeFieldsIsTrue()
    {
        // Arrange  
        var fieldEntity = new EntityWithFields
        {
            Name = "Field Test",
            Age = 25,
            Email = "test@test.com",
            Id = 1
        };

        // Act
        var dto = fieldEntity.ToFacet<EntityWithFields, EntityWithFieldsIncludeDto>();

        // Assert
        dto.Should().NotBeNull();
        dto.Name.Should().Be("Field Test");
        dto.Age.Should().Be(25);
        
        // Verify excluded field is not present
        var dtoType = dto.GetType();
        dtoType.GetField("Id").Should().BeNull("Id field should not be included");
        dtoType.GetProperty("Email").Should().BeNull("Email property should not be included");
    }

    [Fact] 
    public void ToFacet_WithInclude_ShouldNotIncludeFields_WhenIncludeFieldsIsFalse()
    {
        // This test verifies that IncludeFields defaults to false for include mode
        // Arrange  
        var fieldEntity = new EntityWithFields
        {
            Name = "Field Test",
            Age = 25,
            Email = "test@test.com",
            Id = 1
        };

        // Act
        var dto = fieldEntity.ToFacet<EntityWithFields, EntityWithFieldsIncludeNoFieldsDto>();

        // Assert
        dto.Should().NotBeNull();
        dto.Email.Should().Be("test@test.com"); // Property should be included
        
        // Verify fields are not included even if specified in include array
        var dtoType = dto.GetType();
        dtoType.GetField("Name").Should().BeNull("Name field should not be included when IncludeFields = false");
        dtoType.GetField("Age").Should().BeNull("Age field should not be included when IncludeFields = false");
    }

    [Fact]
    public void BackTo_WithInclude_ShouldCreateSourceWithDefaultValues()
    {
        // Arrange
        var user = TestDataFactory.CreateUser("Back", "To");
        var dto = user.ToFacet<User, UserIncludeDto>();

        // Act
        var backToSource = dto.BackTo();

        // Assert
        backToSource.Should().NotBeNull();
        backToSource.FirstName.Should().Be("Back");
        backToSource.LastName.Should().Be("To");
        backToSource.Email.Should().Be(user.Email);
        
        // Properties not included in facet should have default values
        backToSource.Id.Should().Be(0); // Default for int
        backToSource.DateOfBirth.Should().Be(default(DateTime));
        backToSource.Password.Should().Be(string.Empty); // Default for string
        backToSource.IsActive.Should().BeFalse(); // Default for bool
        backToSource.CreatedAt.Should().Be(default(DateTime));
        backToSource.LastLoginAt.Should().BeNull(); // Nullable property
    }
}