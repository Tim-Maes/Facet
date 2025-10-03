using Facet.Tests.TestModels;
using Facet.Tests.Utilities;

namespace Facet.Tests.UnitTests.Features;

public class InheritanceTests
{
    [Fact]
    public void ToFacet_ShouldMapEmployeeProperties_IncludingInheritedFromUser()
    {
        // Arrange
        var employee = TestDataFactory.CreateEmployee("Jane", "Smith", "Engineering");

        // Act
        var dto = employee.ToFacet<Employee, EmployeeDto>();

        // Assert
        dto.Should().NotBeNull();
        
        // Inherited properties from User
        dto.Id.Should().Be(employee.Id);
        dto.FirstName.Should().Be("Jane");
        dto.LastName.Should().Be("Smith");
        dto.Email.Should().Be(employee.Email);
        dto.DateOfBirth.Should().Be(employee.DateOfBirth);
        dto.IsActive.Should().BeTrue();
        dto.LastLoginAt.Should().Be(employee.LastLoginAt);
        
        // Employee-specific properties
        dto.EmployeeId.Should().Be(employee.EmployeeId);
        dto.Department.Should().Be("Engineering");
        dto.HireDate.Should().Be(employee.HireDate);
    }

    [Fact]
    public void ToFacet_ShouldExcludeSpecifiedProperties_FromEmployeeMapping()
    {
        // Arrange
        var employee = TestDataFactory.CreateEmployee();

        // Act
        var dto = employee.ToFacet<Employee, EmployeeDto>();

        // Assert
        var dtoType = dto.GetType();
        dtoType.GetProperty("Password").Should().BeNull("Password should be excluded");
        dtoType.GetProperty("Salary").Should().BeNull("Salary should be excluded");
        dtoType.GetProperty("CreatedAt").Should().BeNull("CreatedAt should be excluded");
    }

    [Fact]
    public void ToFacet_ShouldMapManagerProperties_IncludingMultipleLevelsOfInheritance()
    {
        // Arrange
        var manager = TestDataFactory.CreateManager("Mike", "Johnson", "Development Team");

        // Act
        var dto = manager.ToFacet<Manager, ManagerDto>();

        // Assert
        dto.Should().NotBeNull();
        
        // Inherited from User
        dto.Id.Should().Be(manager.Id);
        dto.FirstName.Should().Be("Mike");
        dto.LastName.Should().Be("Johnson");
        dto.Email.Should().Be(manager.Email);
        dto.IsActive.Should().BeTrue();
        
        // Inherited from Employee
        dto.EmployeeId.Should().Be(manager.EmployeeId);
        dto.Department.Should().Be("Engineering");
        dto.HireDate.Should().Be(manager.HireDate);
        
        // Manager-specific properties
        dto.TeamName.Should().Be("Development Team");
        dto.TeamSize.Should().Be(8);
    }

    [Fact]
    public void ToFacet_ShouldExcludeMultipleProperties_FromManagerMapping()
    {
        // Arrange
        var manager = TestDataFactory.CreateManager();

        // Act
        var dto = manager.ToFacet<Manager, ManagerDto>();

        // Assert
        var dtoType = dto.GetType();
        dtoType.GetProperty("Password").Should().BeNull("Password should be excluded");
        dtoType.GetProperty("Salary").Should().BeNull("Salary should be excluded");
        dtoType.GetProperty("Budget").Should().BeNull("Budget should be excluded");
        dtoType.GetProperty("CreatedAt").Should().BeNull("CreatedAt should be excluded");
    }

    [Fact]
    public void ToFacet_ShouldHandlePolymorphism_WhenMappingDerivedTypes()
    {
        // Arrange
        var baseUser = TestDataFactory.CreateUser("Base", "User");
        var employee = TestDataFactory.CreateEmployee("Employee", "User");
        var manager = TestDataFactory.CreateManager("Manager", "User");

        // Act
        var baseDto = baseUser.ToFacet<User, UserDto>();
        var employeeDto = employee.ToFacet<Employee, EmployeeDto>();
        var managerDto = manager.ToFacet<Manager, ManagerDto>();

        // Assert
        baseDto.FirstName.Should().Be("Base");
        employeeDto.FirstName.Should().Be("Employee");
        managerDto.FirstName.Should().Be("Manager");
        
        // Each should have their specific properties
        baseDto.GetType().GetProperty("EmployeeId").Should().BeNull();
        employeeDto.GetType().GetProperty("EmployeeId").Should().NotBeNull();
        managerDto.GetType().GetProperty("TeamName").Should().NotBeNull();
    }
}