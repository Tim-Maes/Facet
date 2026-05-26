using Facet.Tests.TestModels;
using Facet.Tests.Utilities;

namespace Facet.Tests.UnitTests.Features;

public class InheritanceTests
{
    [Fact]
    public void ToFacet_ShouldMapEmployeeProperties_IncludingInheritedFromUser()
    {
        var employee = TestDataFactory.CreateEmployee("Jane", "Smith", "Engineering");

        var dto = employee.ToFacet<Employee, EmployeeDto>();

        dto.Should().NotBeNull();
        
        dto.Id.Should().Be(employee.Id);
        dto.FirstName.Should().Be("Jane");
        dto.LastName.Should().Be("Smith");
        dto.Email.Should().Be(employee.Email);
        dto.DateOfBirth.Should().Be(employee.DateOfBirth);
        dto.IsActive.Should().BeTrue();
        dto.LastLoginAt.Should().Be(employee.LastLoginAt);
        
        dto.EmployeeId.Should().Be(employee.EmployeeId);
        dto.Department.Should().Be("Engineering");
        dto.HireDate.Should().Be(employee.HireDate);
    }

    [Fact]
    public void ToFacet_ShouldExcludeSpecifiedProperties_FromEmployeeMapping()
    {
        var employee = TestDataFactory.CreateEmployee();

        var dto = employee.ToFacet<Employee, EmployeeDto>();

        var dtoType = dto.GetType();
        dtoType.GetProperty("Password").Should().BeNull("Password should be excluded");
        dtoType.GetProperty("Salary").Should().BeNull("Salary should be excluded");
        dtoType.GetProperty("CreatedAt").Should().BeNull("CreatedAt should be excluded");
    }

    [Fact]
    public void ToFacet_ShouldMapManagerProperties_IncludingMultipleLevelsOfInheritance()
    {
        var manager = TestDataFactory.CreateManager("Mike", "Johnson", "Development Team");

        var dto = manager.ToFacet<Manager, ManagerDto>();

        dto.Should().NotBeNull();
        
        dto.Id.Should().Be(manager.Id);
        dto.FirstName.Should().Be("Mike");
        dto.LastName.Should().Be("Johnson");
        dto.Email.Should().Be(manager.Email);
        dto.IsActive.Should().BeTrue();
        
        dto.EmployeeId.Should().Be(manager.EmployeeId);
        dto.Department.Should().Be("Engineering");
        dto.HireDate.Should().Be(manager.HireDate);
        
        dto.TeamName.Should().Be("Development Team");
        dto.TeamSize.Should().Be(8);
    }

    [Fact]
    public void ToFacet_ShouldExcludeMultipleProperties_FromManagerMapping()
    {
        var manager = TestDataFactory.CreateManager();

        var dto = manager.ToFacet<Manager, ManagerDto>();

        var dtoType = dto.GetType();
        dtoType.GetProperty("Password").Should().BeNull("Password should be excluded");
        dtoType.GetProperty("Salary").Should().BeNull("Salary should be excluded");
        dtoType.GetProperty("Budget").Should().BeNull("Budget should be excluded");
        dtoType.GetProperty("CreatedAt").Should().BeNull("CreatedAt should be excluded");
    }

    [Fact]
    public void ToFacet_ShouldHandlePolymorphism_WhenMappingDerivedTypes()
    {
        var baseUser = TestDataFactory.CreateUser("Base", "User");
        var employee = TestDataFactory.CreateEmployee("Employee", "User");
        var manager = TestDataFactory.CreateManager("Manager", "User");

        var baseDto = baseUser.ToFacet<User, UserDto>();
        var employeeDto = employee.ToFacet<Employee, EmployeeDto>();
        var managerDto = manager.ToFacet<Manager, ManagerDto>();

        baseDto.FirstName.Should().Be("Base");
        employeeDto.FirstName.Should().Be("Employee");
        managerDto.FirstName.Should().Be("Manager");
        
        baseDto.GetType().GetProperty("EmployeeId").Should().BeNull();
        employeeDto.GetType().GetProperty("EmployeeId").Should().NotBeNull();
        managerDto.GetType().GetProperty("TeamName").Should().NotBeNull();
    }

    [Fact]
    public void ToFacet_ShouldExcludeInheritedProperty_FromGenericBaseClass()
    {
        var category = new Category
        {
            Id = 42,
            Name = "Electronics",
            Description = "Electronic devices and accessories"
        };

        var dto = category.ToFacet<Category, UpdateCategoryViewModel>();

        dto.Should().NotBeNull();
        dto.Name.Should().Be("Electronics");
        dto.Description.Should().Be("Electronic devices and accessories");

        var dtoType = dto.GetType();
        dtoType.GetProperty("Id").Should().BeNull("Id should be excluded from UpdateCategoryViewModel");
    }
}
