using Facet.Tests.TestModels;
using Facet.Tests.Utilities;

namespace Facet.Tests.UnitTests.Core.Facet;

public class BackToTests
{
    #region Class Tests

    [Fact]
    public void BackTo_ShouldMapBasicProperties_WhenMappingFromUserDto()
    {
        // Arrange
        var originalUser = TestDataFactory.CreateUser("John", "Doe", "john@example.com");
        var userDto = originalUser.ToFacet<User, UserDto>();

        // Act
        var mappedUser = userDto.BackTo<User>();

        // Assert
        mappedUser.Should().NotBeNull();
        mappedUser.Id.Should().Be(originalUser.Id);
        mappedUser.FirstName.Should().Be("John");
        mappedUser.LastName.Should().Be("Doe");
        mappedUser.Email.Should().Be("john@example.com");
        mappedUser.IsActive.Should().Be(originalUser.IsActive);
        mappedUser.DateOfBirth.Should().Be(originalUser.DateOfBirth);
        mappedUser.LastLoginAt.Should().Be(originalUser.LastLoginAt);
    }

    [Fact]
    public void BackTo_ShouldSetDefaultValues_ForExcludedProperties()
    {
        // Arrange
        var originalUser = TestDataFactory.CreateUser();
        var userDto = originalUser.ToFacet<User, UserDto>();

        // Act
        var mappedUser = userDto.BackTo<User>();

        // Assert
        mappedUser.Should().NotBeNull();
        mappedUser.Password.Should().BeEmpty("Password was excluded from DTO");
        mappedUser.CreatedAt.Should().Be(default(DateTime), "CreatedAt was excluded from DTO");
    }

    [Fact]
    public void BackTo_ShouldHandleNullableProperties_Correctly()
    {
        // Arrange
        var originalUser = TestDataFactory.CreateUser();
        originalUser.LastLoginAt = null;
        var userDto = originalUser.ToFacet<User, UserDto>();

        // Act
        var mappedUser = userDto.BackTo<User>();


        // Assert
        mappedUser.LastLoginAt.Should().BeNull();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void BackTo_ShouldPreserveBooleanValues_ForIsActiveProperty(bool isActive)
    {
        // Arrange
        var originalUser = TestDataFactory.CreateUser(isActive: isActive);
        var userDto = originalUser.ToFacet<User, UserDto>();

        // Act
        var mappedUser = userDto.BackTo<User>();

        // Assert
        mappedUser.IsActive.Should().Be(isActive);
    }

    [Fact]
    public void BackTo_ShouldHandleEmployeeDto_WithInheritedProperties()
    {
        // Arrange
        var originalEmployee = TestDataFactory.CreateEmployee("Jane", "Smith");
        var employeeDto = originalEmployee.ToFacet<Employee, EmployeeDto>();

        // Act
        var mappedEmployee = employeeDto.BackTo<Employee>();

        // Assert
        mappedEmployee.Should().NotBeNull();
        mappedEmployee.FirstName.Should().Be("Jane");
        mappedEmployee.LastName.Should().Be("Smith");
        mappedEmployee.EmployeeId.Should().Be(originalEmployee.EmployeeId);
        mappedEmployee.Department.Should().Be(originalEmployee.Department);
        mappedEmployee.HireDate.Should().Be(originalEmployee.HireDate);
        
        // Excluded properties should have default values
        mappedEmployee.Password.Should().BeEmpty();
        mappedEmployee.Salary.Should().Be(0);
        mappedEmployee.CreatedAt.Should().Be(default(DateTime));
    }

    [Fact]
    public void BackTo_ShouldHandleManagerDto_WithMultipleLevelsOfInheritance()
    {
        // Arrange
        var originalManager = TestDataFactory.CreateManager("Bob", "Wilson");
        var managerDto = originalManager.ToFacet<Manager, ManagerDto>();

        // Act
        var mappedManager = managerDto.BackTo<Manager>();

        // Assert
        mappedManager.Should().NotBeNull();
        mappedManager.FirstName.Should().Be("Bob");
        mappedManager.LastName.Should().Be("Wilson");
        mappedManager.TeamName.Should().Be(originalManager.TeamName);
        mappedManager.TeamSize.Should().Be(originalManager.TeamSize);
        
        // Excluded properties should have default values
        mappedManager.Budget.Should().Be(0);
        mappedManager.Salary.Should().Be(0);
    }

    #endregion

    #region Record Tests

    [Fact]
    public void BackTo_ShouldMapProductRecord_WithBasicProperties()
    {
        // Arrange
        var originalProduct = TestDataFactory.CreateProduct("Test Product", 49.99m);
        var productDto = originalProduct.ToFacet<Product, ProductDto>();

        // Act
        var mappedProduct = productDto.BackTo<Product>();

        // Assert
        mappedProduct.Should().NotBeNull();
        mappedProduct.Id.Should().Be(originalProduct.Id);
        mappedProduct.Name.Should().Be("Test Product");
        mappedProduct.Description.Should().Be(originalProduct.Description);
        mappedProduct.Price.Should().Be(49.99m);
        mappedProduct.CategoryId.Should().Be(originalProduct.CategoryId);
        mappedProduct.IsAvailable.Should().Be(originalProduct.IsAvailable);
        
        // Excluded property should have default value
        mappedProduct.InternalNotes.Should().BeEmpty();
    }

    [Fact]
    public void BackTo_ShouldHandleRecord_WithPositionalConstructor()
    {
        // Arrange
        var originalClassicUser = TestDataFactory.CreateClassicUser("Alice", "Wonder");
        var classicUserDto = originalClassicUser.ToFacet<ClassicUser, ClassicUserDto>();

        // Act
        var mappedClassicUser = classicUserDto.BackTo<ClassicUser>();

        // Assert
        mappedClassicUser.Should().NotBeNull();
        mappedClassicUser.Id.Should().Be(originalClassicUser.Id);
        mappedClassicUser.FirstName.Should().Be("Alice");
        mappedClassicUser.LastName.Should().Be("Wonder");
        mappedClassicUser.Email.Should().Be(originalClassicUser.Email);
    }

    [Fact]
    public void BackTo_ShouldHandleModernRecord_WithGettersAndInitializers()
    {
        // Arrange
        var originalModernUser = TestDataFactory.CreateModernUser("Alice", "Wonder");
        var modernUserDto = originalModernUser.ToFacet<ModernUser, ModernUserDto>();

        // Act
        var mappedModernUser = modernUserDto.BackTo<ModernUser>();

        // Assert
        mappedModernUser.Should().NotBeNull();
        mappedModernUser.Id.Should().Be(originalModernUser.Id);
        mappedModernUser.FirstName.Should().Be("Alice");
        mappedModernUser.LastName.Should().Be("Wonder");
        mappedModernUser.Email.Should().Be(originalModernUser.Email);
        mappedModernUser.CreatedAt.Should().Be(originalModernUser.CreatedAt);
        
        // Excluded properties should have default values
        mappedModernUser.Bio.Should().BeNull();
        mappedModernUser.PasswordHash.Should().BeNull();
    }

    [Fact]
    public void BackTo_ShouldHandleRecordEquality_WithValueSemantics()
    {
        // Arrange
        var product = TestDataFactory.CreateProduct("Equality Test", 10.99m);
        var dto1 = product.ToFacet<Product, ProductDto>();
        var dto2 = product.ToFacet<Product, ProductDto>();

        // Act
        var mapped1 = dto1.BackTo<Product>();
        var mapped2 = dto2.BackTo<Product>();

        // Assert
        dto1.Should().Be(dto2, "Records should have value equality");
        mapped1.Id.Should().Be(mapped2.Id);
        mapped1.Name.Should().Be(mapped2.Name);
        mapped1.Price.Should().Be(mapped2.Price);
    }

    #endregion

    #region Enum Handling Tests

    [Fact]
    public void BackTo_ShouldPreserveEnumValues_WhenMappingUserWithEnum()
    {
        // Arrange
        var originalUser = TestDataFactory.CreateUserWithEnum("Enum User");
        var userDto = originalUser.ToFacet<UserWithEnum, UserWithEnumDto>();

        // Act
        var mappedUser = userDto.BackTo<UserWithEnum>();

        // Assert
        mappedUser.Should().NotBeNull();
        mappedUser.Id.Should().Be(originalUser.Id);
        mappedUser.Name.Should().Be("Enum User");
        mappedUser.Email.Should().Be(originalUser.Email);
        mappedUser.Status.Should().Be(UserStatus.Active);
    }

    [Theory]
    [InlineData(UserStatus.Active)]
    [InlineData(UserStatus.Inactive)]
    [InlineData(UserStatus.Pending)]
    [InlineData(UserStatus.Suspended)]
    public void BackTo_ShouldHandleAllEnumValues_Correctly(UserStatus status)
    {
        // Arrange
        var originalUser = TestDataFactory.CreateUserWithEnum("Test User", status);
        var userDto = originalUser.ToFacet<UserWithEnum, UserWithEnumDto>();

        // Act
        var mappedUser = userDto.BackTo<UserWithEnum>();

        // Assert
        mappedUser.Status.Should().Be(status);
    }

    #endregion

    #region Collection Tests

    [Fact]
    public void BackTo_ShouldMapMultipleUsers_WithDifferentData()
    {
        // Arrange
        var originalUsers = TestDataFactory.CreateUsers();
        var userDtos = originalUsers.Select(u => u.ToFacet<User, UserDto>()).ToList();

        // Act
        var mappedUsers = userDtos.Select(dto => dto.BackTo<User>()).ToList();

        // Assert
        mappedUsers.Should().HaveCount(3);
        mappedUsers[0].FirstName.Should().Be(originalUsers[0].FirstName);
        mappedUsers[1].FirstName.Should().Be(originalUsers[1].FirstName);
        mappedUsers[2].FirstName.Should().Be(originalUsers[2].FirstName);
        mappedUsers[2].IsActive.Should().Be(originalUsers[2].IsActive);
    }

    [Fact]
    public void BackTo_ShouldMapMultipleProducts_FromRecordDtos()
    {
        // Arrange
        var originalProducts = new List<Product>
        {
            TestDataFactory.CreateProduct("Product A", 19.99m),
            TestDataFactory.CreateProduct("Product B", 39.99m),
            TestDataFactory.CreateProduct("Product C", 59.99m, false)
        };
        var productDtos = originalProducts.Select(p => p.ToFacet<Product, ProductDto>()).ToList();

        // Act
        var mappedProducts = productDtos.Select(dto => dto.BackTo<Product>()).ToList();

        // Assert
        mappedProducts.Should().HaveCount(originalProducts.Count);
        for (int i = 0; i < originalProducts.Count; i++)
        {
            mappedProducts[i].Id.Should().Be(originalProducts[i].Id);
            mappedProducts[i].Name.Should().Be(originalProducts[i].Name);
            mappedProducts[i].Price.Should().Be(originalProducts[i].Price);
            mappedProducts[i].InternalNotes.Should().BeEmpty(); // Excluded property
        }
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void BackTo_ShouldHandleDefaultValues_WhenDtoHasMinimalData()
    {
        // Arrange
        var userDto = new UserDto
        {
            Id = 999,
            FirstName = "Minimal",
            LastName = "User",
            Email = "minimal@test.com",
            DateOfBirth = new DateTime(1985, 1, 1),
            IsActive = true,
            LastLoginAt = null
        };

        // Act
        var mappedUser = userDto.BackTo<User>();

        // Assert
        mappedUser.Should().NotBeNull();
        mappedUser.Id.Should().Be(999);
        mappedUser.FirstName.Should().Be("Minimal");
        mappedUser.LastName.Should().Be("User");
        mappedUser.Email.Should().Be("minimal@test.com");
        mappedUser.Password.Should().BeEmpty(); // Default value for excluded property
        mappedUser.CreatedAt.Should().Be(default(DateTime)); // Default value for excluded property
    }

    [Fact]
    public void BackTo_ShouldRoundTrip_PreservingIncludedProperties()
    {
        // Arrange
        var originalUser = TestDataFactory.CreateUser("Round", "Trip", "round@trip.com");
        
        // Act - Round trip: User -> UserDto -> User
        var userDto = originalUser.ToFacet<User, UserDto>();
        var roundTripUser = userDto.BackTo<User>();
        
        // Assert - Included properties should match
        roundTripUser.Id.Should().Be(originalUser.Id);
        roundTripUser.FirstName.Should().Be(originalUser.FirstName);
        roundTripUser.LastName.Should().Be(originalUser.LastName);
        roundTripUser.Email.Should().Be(originalUser.Email);
        roundTripUser.DateOfBirth.Should().Be(originalUser.DateOfBirth);
        roundTripUser.IsActive.Should().Be(originalUser.IsActive);
        roundTripUser.LastLoginAt.Should().Be(originalUser.LastLoginAt);
        
        // Excluded properties should be defaults (data loss is expected)
        roundTripUser.Password.Should().BeEmpty();
        roundTripUser.CreatedAt.Should().Be(default(DateTime));
    }

    [Fact]
    public void BackTo_ShouldNotBeNull_ForValidDtoInput()
    {
        // Arrange
        var userDto = new UserDto
        {
            Id = 123,
            FirstName = "Test",
            LastName = "User",
            Email = "test@example.com",
            DateOfBirth = DateTime.Now.AddYears(-25),
            IsActive = true,
            LastLoginAt = DateTime.Now.AddHours(-1)
        };

        // Act
        var result = userDto.BackTo<User>();

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<User>();
    }

    [Fact]
    public void BackTo_ShouldPreserveDecimalPrecision_InProductMapping()
    {
        // Arrange
        var originalProduct = TestDataFactory.CreateProduct("Precision Test", 123.456789m);
        var productDto = originalProduct.ToFacet<Product, ProductDto>();

        // Act
        var mappedProduct = productDto.BackTo<Product>();

        // Assert
        mappedProduct.Price.Should().Be(123.456789m);
    }

    [Fact]
    public void BackTo_ShouldHandleDateTimePrecision_Correctly()
    {
        // Arrange
        var specificDate = new DateTime(2024, 3, 15, 14, 30, 45, 123);
        var user = TestDataFactory.CreateUser(dateOfBirth: specificDate);
        var userDto = user.ToFacet<User, UserDto>();

        // Act
        var mappedUser = userDto.BackTo<User>();

        // Assert
        mappedUser.DateOfBirth.Should().Be(specificDate);
    }

    #endregion
}
