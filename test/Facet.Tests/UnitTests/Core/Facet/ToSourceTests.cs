using Facet.Tests.TestModels;
using Facet.Tests.Utilities;

namespace Facet.Tests.UnitTests.Core.Facet;

public class ToSourceTests
{
    #region Class Tests

    [Fact]
    public void ToSourceShorthand_ShouldMapBasicProperties_WhenMappingFromUserDto()
    {
        var originalUser = TestDataFactory.CreateUser("John", "Doe", "john@example.com");
        var userDto = originalUser.ToFacet<User, UserDto>();

        var mappedUser = userDto.ToSource<User>();

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
    public void ToSource_ShouldMapBasicProperties_WhenMappingFromUserDto()
    {
        var originalUser = TestDataFactory.CreateUser("John", "Doe", "john@example.com");
        var userDto = originalUser.ToFacet<User, UserDto>();

        var mappedUser = userDto.ToSource<UserDto, User>();

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
    public void ToSource_ShouldSetDefaultValues_ForExcludedProperties()
    {
        var originalUser = TestDataFactory.CreateUser();
        var userDto = originalUser.ToFacet<User, UserDto>();

        var mappedUser = userDto.ToSource<UserDto, User>();

        mappedUser.Should().NotBeNull();
        mappedUser.Password.Should().BeEmpty("Password was excluded from DTO");
        mappedUser.CreatedAt.Should().Be(default(DateTime), "CreatedAt was excluded from DTO");
    }

    [Fact]
    public void ToSource_ShouldHandleNullableProperties_Correctly()
    {
        var originalUser = TestDataFactory.CreateUser();
        originalUser.LastLoginAt = null;
        var userDto = originalUser.ToFacet<User, UserDto>();

        var mappedUser = userDto.ToSource<User>();

        mappedUser.LastLoginAt.Should().BeNull();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ToSource_ShouldPreserveBooleanValues_ForIsActiveProperty(bool isActive)
    {
        var originalUser = TestDataFactory.CreateUser(isActive: isActive);
        var userDto = originalUser.ToFacet<User, UserDto>();

        var mappedUser = userDto.ToSource<User>();

        mappedUser.IsActive.Should().Be(isActive);
    }

    [Fact]
    public void ToSource_ShouldHandleEmployeeDto_WithInheritedProperties()
    {
        var originalEmployee = TestDataFactory.CreateEmployee("Jane", "Smith");
        var employeeDto = originalEmployee.ToFacet<Employee, EmployeeDto>();

        var mappedEmployee = employeeDto.ToSource<Employee>();

        mappedEmployee.Should().NotBeNull();
        mappedEmployee.FirstName.Should().Be("Jane");
        mappedEmployee.LastName.Should().Be("Smith");
        mappedEmployee.EmployeeId.Should().Be(originalEmployee.EmployeeId);
        mappedEmployee.Department.Should().Be(originalEmployee.Department);
        mappedEmployee.HireDate.Should().Be(originalEmployee.HireDate);

        mappedEmployee.Password.Should().BeEmpty();
        mappedEmployee.Salary.Should().Be(0);
        mappedEmployee.CreatedAt.Should().Be(default(DateTime));
    }

    [Fact]
    public void ToSource_ShouldHandleManagerDto_WithMultipleLevelsOfInheritance()
    {
        var originalManager = TestDataFactory.CreateManager("Bob", "Wilson");
        var managerDto = originalManager.ToFacet<Manager, ManagerDto>();

        var mappedManager = managerDto.ToSource<Manager>();

        mappedManager.Should().NotBeNull();
        mappedManager.FirstName.Should().Be("Bob");
        mappedManager.LastName.Should().Be("Wilson");
        mappedManager.TeamName.Should().Be(originalManager.TeamName);
        mappedManager.TeamSize.Should().Be(originalManager.TeamSize);

        mappedManager.Budget.Should().Be(0);
        mappedManager.Salary.Should().Be(0);
    }

    #endregion

    #region Record Tests

    [Fact]
    public void ToSource_ShouldMapProductRecord_WithBasicProperties()
    {
        var originalProduct = TestDataFactory.CreateProduct("Test Product", 49.99m);

        var productDto = originalProduct.ToFacet<Product, ProductDto>();

        var mappedProduct = productDto.ToSource<Product>();

        mappedProduct.Should().NotBeNull();
        mappedProduct.Id.Should().Be(originalProduct.Id);
        mappedProduct.Name.Should().Be("Test Product");
        mappedProduct.Description.Should().Be(originalProduct.Description);
        mappedProduct.Price.Should().Be(49.99m);
        mappedProduct.CategoryId.Should().Be(originalProduct.CategoryId);
        mappedProduct.IsAvailable.Should().Be(originalProduct.IsAvailable);

        mappedProduct.InternalNotes.Should().BeEmpty();
    }

    [Fact]
    public void ToSource_ShouldHandleRecord_WithPositionalConstructor()
    {
        var originalClassicUser = TestDataFactory.CreateClassicUser("Alice", "Wonder");
        var classicUserDto = originalClassicUser.ToFacet<ClassicUser, ClassicUserDto>();

        var mappedClassicUser = classicUserDto.ToSource<ClassicUser>();

        mappedClassicUser.Should().NotBeNull();
        mappedClassicUser.Id.Should().Be(originalClassicUser.Id);
        mappedClassicUser.FirstName.Should().Be("Alice");
        mappedClassicUser.LastName.Should().Be("Wonder");
        mappedClassicUser.Email.Should().Be(originalClassicUser.Email);
    }

    [Fact]
    public void ToSource_ShouldHandleModernRecord_WithGettersAndInitializers()
    {
        var originalModernUser = TestDataFactory.CreateModernUser("Alice", "Wonder");
        var modernUserDto = originalModernUser.ToFacet<ModernUser, ModernUserDto>();

        var mappedModernUser = modernUserDto.ToSource<ModernUser>();

        mappedModernUser.Should().NotBeNull();
        mappedModernUser.Id.Should().Be(originalModernUser.Id);
        mappedModernUser.FirstName.Should().Be("Alice");
        mappedModernUser.LastName.Should().Be("Wonder");
        mappedModernUser.Email.Should().Be(originalModernUser.Email);
        mappedModernUser.CreatedAt.Should().Be(originalModernUser.CreatedAt);

        mappedModernUser.Bio.Should().BeNull();
        mappedModernUser.PasswordHash.Should().BeNull();
    }

    [Fact]
    public void ToSource_ShouldHandleRecordEquality_WithValueSemantics()
    {
        var product = TestDataFactory.CreateProduct("Equality Test", 10.99m);
        var dto1 = product.ToFacet<Product, ProductDto>();
        var dto2 = product.ToFacet<Product, ProductDto>();

        var mapped1 = dto1.ToSource<Product>();
        var mapped2 = dto2.ToSource<Product>();

        dto1.Should().Be(dto2, "Records should have value equality");
        mapped1.Id.Should().Be(mapped2.Id);
        mapped1.Name.Should().Be(mapped2.Name);
        mapped1.Price.Should().Be(mapped2.Price);
    }

    #endregion

    #region Enum Handling Tests

    [Fact]
    public void ToSource_ShouldPreserveEnumValues_WhenMappingUserWithEnum()
    {
        var originalUser = TestDataFactory.CreateUserWithEnum("Enum User");
        var userDto = originalUser.ToFacet<UserWithEnum, UserWithEnumDto>();

        var mappedUser = userDto.ToSource<UserWithEnum>();

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
    public void ToSource_ShouldHandleAllEnumValues_Correctly(UserStatus status)
    {
        var originalUser = TestDataFactory.CreateUserWithEnum("Test User", status);
        var userDto = originalUser.ToFacet<UserWithEnum, UserWithEnumDto>();

        var mappedUser = userDto.ToSource<UserWithEnum>();

        mappedUser.Status.Should().Be(status);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void ToSource_ShouldHandleDefaultValues_WhenDtoHasMinimalData()
    {
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

        var mappedUser = userDto.ToSource<User>();

        mappedUser.Should().NotBeNull();
        mappedUser.Id.Should().Be(999);
        mappedUser.FirstName.Should().Be("Minimal");
        mappedUser.LastName.Should().Be("User");
        mappedUser.Email.Should().Be("minimal@test.com");
        mappedUser.Password.Should().BeEmpty(); 
        mappedUser.CreatedAt.Should().Be(default(DateTime)); 
    }

    [Fact]
    public void ToSource_ShouldRoundTrip_PreservingIncludedProperties()
    {
        var originalUser = TestDataFactory.CreateUser("Round", "Trip", "round@trip.com");

        var userDto = originalUser.ToFacet<User, UserDto>();
        var roundTripUser = userDto.ToSource<User>();

        roundTripUser.Id.Should().Be(originalUser.Id);
        roundTripUser.FirstName.Should().Be(originalUser.FirstName);
        roundTripUser.LastName.Should().Be(originalUser.LastName);
        roundTripUser.Email.Should().Be(originalUser.Email);
        roundTripUser.DateOfBirth.Should().Be(originalUser.DateOfBirth);
        roundTripUser.IsActive.Should().Be(originalUser.IsActive);
        roundTripUser.LastLoginAt.Should().Be(originalUser.LastLoginAt);

        roundTripUser.Password.Should().BeEmpty();
        roundTripUser.CreatedAt.Should().Be(default(DateTime));
    }

    [Fact]
    public void ToSource_ShouldNotBeNull_ForValidDtoInput()
    {
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

        var result = userDto.ToSource<User>();

        result.Should().NotBeNull();
        result.Should().BeOfType<User>();
    }

    [Fact]
    public void ToSource_ShouldPreserveDecimalPrecision_InProductMapping()
    {
        var originalProduct = TestDataFactory.CreateProduct("Precision Test", 123.456789m);
        var productDto = originalProduct.ToFacet<Product, ProductDto>();

        var mappedProduct = productDto.ToSource<Product>();

        mappedProduct.Price.Should().Be(123.456789m);
    }

    [Fact]
    public void ToSource_ShouldHandleDateTimePrecision_Correctly()
    {
        var specificDate = new DateTime(2024, 3, 15, 14, 30, 45, 123);
        var user = TestDataFactory.CreateUser(dateOfBirth: specificDate);
        var userDto = user.ToFacet<User, UserDto>();

        var mappedUser = userDto.ToSource<User>();

        mappedUser.DateOfBirth.Should().Be(specificDate);
    }

    #endregion
}
