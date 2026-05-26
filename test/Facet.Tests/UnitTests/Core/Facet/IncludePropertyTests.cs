using Facet.Tests.TestModels;
using Facet.Tests.Utilities;

namespace Facet.Tests.UnitTests.Core.Facet;

public class IncludePropertyTests
{
    [Fact]
    public void ToFacet_WithInclude_ShouldOnlyIncludeSpecifiedProperties()
    {
        var user = TestDataFactory.CreateUser("John", "Doe");

        var dto = user.ToFacet<User, UserIncludeDto>();

        dto.Should().NotBeNull();
        dto.FirstName.Should().Be("John");
        dto.LastName.Should().Be("Doe");
        dto.Email.Should().Be(user.Email);

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
        var user = TestDataFactory.CreateUser("Jane", "Smith");

        var dto = user.ToFacet<User, UserSingleIncludeDto>();

        dto.Should().NotBeNull();
        dto.FirstName.Should().Be("Jane");
        
        var dtoType = dto.GetType();
        dtoType.GetProperty("LastName").Should().BeNull("LastName should not be included");
        dtoType.GetProperty("Email").Should().BeNull("Email should not be included");
        dtoType.GetProperty("Id").Should().BeNull("Id should not be included");
    }

    [Fact]
    public void ToFacet_WithInclude_ShouldWorkWithSingleObjectProperty()
    {
        var user = TestDataFactory.CreateUser("Jane", "Smith", "", DateTime.Today);

        var dto = user.ToFacet<User, UserSingleObjectIncludeDto>();

        dto.Should().NotBeNull();
        dto.DateOfBirth.Should().Be(DateTime.Today);

        var dtoType = dto.GetType();
        dtoType.GetProperty("LastName").Should().BeNull("LastName should not be included");
        dtoType.GetProperty("Email").Should().BeNull("Email should not be included");
        dtoType.GetProperty("Id").Should().BeNull("Id should not be included");
    }

    [Fact]
    public void ToFacet_ShouldWorkWithSingleObjectProperty()
    {
        var id = Guid.NewGuid();
        
        var tenant = TestDataFactory.CreateTenant(id);

        var dto = tenant.ToFacet<Tenant, TenantSingleObjectIncludeDto>();

        dto.Should().NotBeNull();
        dto.Id.Should().Be(id);

        var dtoType = dto.GetType();
        dtoType.GetProperty("Id").Should().NotBeNull("LastName should be included");
    }

    [Fact]
    public void ToFacet_WithInclude_ShouldWorkWithProductEntity()
    {
        var product = TestDataFactory.CreateProduct("Test Product", 99.99m);

        var dto = product.ToFacet<Product, ProductIncludeDto>();

        dto.Should().NotBeNull();
        dto.Name.Should().Be("Test Product");
        dto.Price.Should().Be(99.99m);

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
        var user = TestDataFactory.CreateUser("Type", "Test");

        var dto = user.ToFacet<User, UserIncludeDto>();

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
        var employee = TestDataFactory.CreateEmployee("Include", "Test", "Engineering");

        var dto = employee.ToFacet<Employee, EmployeeIncludeDto>();

        dto.Should().NotBeNull();
        dto.FirstName.Should().Be("Include"); 
        dto.LastName.Should().Be("Test"); 
        dto.Department.Should().Be("Engineering"); 

        var dtoType = dto.GetType();
        dtoType.GetProperty("Id").Should().BeNull("Id should not be included");
        dtoType.GetProperty("Email").Should().BeNull("Email should not be included");
        dtoType.GetProperty("EmployeeId").Should().BeNull("EmployeeId should not be included");
        dtoType.GetProperty("Salary").Should().BeNull("Salary should not be included");
    }

    [Fact]
    public void ToFacet_WithInclude_AndCustomProperties_ShouldWork()
    {
        var user = TestDataFactory.CreateUser("Custom", "Props");

        var dto = user.ToFacet<User, UserIncludeWithCustomDto>();

        dto.Should().NotBeNull();
        dto.FirstName.Should().Be("Custom");
        dto.LastName.Should().Be("Props");

        dto.FullName.Should().Be(string.Empty);

        var dtoType = dto.GetType();
        dtoType.GetProperty("Email").Should().BeNull("Email should not be included");
        dtoType.GetProperty("Id").Should().BeNull("Id should not be included");
    }

    [Fact]
    public void ToFacet_WithInclude_ShouldSupportModernRecordTypes()
    {
        var modernUser = TestDataFactory.CreateModernUser("Modern", "Include");

        var dto = modernUser.ToFacet<ModernUser, ModernUserIncludeDto>();

        dto.Should().NotBeNull();
        dto.FirstName.Should().Be("Modern");
        dto.LastName.Should().Be("Include");

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
        var fieldEntity = new EntityWithFields
        {
            Name = "Field Test",
            Age = 25,
            Email = "test@test.com",
            Id = 1
        };

        var dto = fieldEntity.ToFacet<EntityWithFields, EntityWithFieldsIncludeDto>();

        dto.Should().NotBeNull();
        dto.Name.Should().Be("Field Test");
        dto.Age.Should().Be(25);

        var dtoType = dto.GetType();
        dtoType.GetField("Id").Should().BeNull("Id field should not be included");
        dtoType.GetProperty("Email").Should().BeNull("Email property should not be included");
    }

    [Fact]
    public void ToFacet_WithInclude_ShouldNotIncludeFields_WhenIncludeFieldsIsFalse()
    {
        var fieldEntity = new EntityWithFields
        {
            Name = "Field Test",
            Age = 25,
            Email = "test@test.com",
            Id = 1
        };

        var dto = fieldEntity.ToFacet<EntityWithFields, EntityWithFieldsIncludeNoFieldsDto>();

        dto.Should().NotBeNull();
        dto.Email.Should().Be("test@test.com"); 

        var dtoType = dto.GetType();
        dtoType.GetField("Name").Should().BeNull("Name field should not be included when IncludeFields = false");
        dtoType.GetField("Age").Should().BeNull("Age field should not be included when IncludeFields = false");
    }

    [Fact]
    public void BackTo_WithInclude_ShouldCreateSourceWithDefaultValues()
    {
        var user = TestDataFactory.CreateUser("Back", "To");
        var dto = user.ToFacet<User, UserIncludeDto>();

        var backToSource = dto.BackTo();

        backToSource.Should().NotBeNull();
        backToSource.FirstName.Should().Be("Back");
        backToSource.LastName.Should().Be("To");
        backToSource.Email.Should().Be(user.Email);

        backToSource.Id.Should().Be(0); 
        backToSource.DateOfBirth.Should().Be(default(DateTime));
        backToSource.Password.Should().Be(string.Empty); 
        backToSource.IsActive.Should().BeFalse(); 
        backToSource.CreatedAt.Should().Be(default(DateTime));
        backToSource.LastLoginAt.Should().BeNull(); 
    }
}
