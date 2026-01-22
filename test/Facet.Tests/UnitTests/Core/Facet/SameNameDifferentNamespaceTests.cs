using Facet.Tests.TestModels.NamespaceCollision.N1;
using Facet.Tests.TestModels.NamespaceCollision.N2;

namespace Facet.Tests.UnitTests.Core.Facet;

/// <summary>
/// Tests for GitHub issue #249: Generator issue when DTOs with the same name are under different namespaces.
/// Verifies that types with the same simple name in different namespaces generate correctly.
/// </summary>
public class SameNameDifferentNamespaceTests
{
    [Fact]
    public void FacetWithSameName_InDifferentNamespaces_ShouldGenerateSeparateTypes()
    {
        // Arrange & Act - Just accessing the types proves they were generated successfully
        var n1DtoType = typeof(global::Facet.Tests.TestModels.NamespaceCollision.N1.EmployeeDto);
        var n2DtoType = typeof(global::Facet.Tests.TestModels.NamespaceCollision.N2.EmployeeDto);

        // Assert - Both types should exist and be different
        n1DtoType.Should().NotBeNull();
        n2DtoType.Should().NotBeNull();
        n1DtoType.Should().NotBeSameAs(n2DtoType);
        n1DtoType.FullName.Should().Be("Facet.Tests.TestModels.NamespaceCollision.N1.EmployeeDto");
        n2DtoType.FullName.Should().Be("Facet.Tests.TestModels.NamespaceCollision.N2.EmployeeDto");
    }

    [Fact]
    public void FacetWithSameName_N1_ShouldHaveCorrectProperties()
    {
        // Arrange & Act
        var dtoType = typeof(global::Facet.Tests.TestModels.NamespaceCollision.N1.EmployeeDto);

        // Assert - N1 Employee has Salary and Department
        dtoType.GetProperty("Salary").Should().NotBeNull();
        dtoType.GetProperty("Salary")!.PropertyType.Should().Be(typeof(decimal));
        
        dtoType.GetProperty("Department").Should().NotBeNull();
        dtoType.GetProperty("Department")!.PropertyType.Should().Be(typeof(string));
        
        // Should not have Role (that's in N2)
        dtoType.GetProperty("Role").Should().BeNull();
    }

    [Fact]
    public void FacetWithSameName_N2_ShouldHaveCorrectProperties()
    {
        // Arrange & Act
        var dtoType = typeof(global::Facet.Tests.TestModels.NamespaceCollision.N2.EmployeeDto);

        // Assert - N2 Employee has Salary and Role
        dtoType.GetProperty("Salary").Should().NotBeNull();
        dtoType.GetProperty("Salary")!.PropertyType.Should().Be(typeof(decimal));
        
        dtoType.GetProperty("Role").Should().NotBeNull();
        dtoType.GetProperty("Role")!.PropertyType.Should().Be(typeof(string));
        
        // Should not have Department (that's in N1)
        dtoType.GetProperty("Department").Should().BeNull();
    }

    [Fact]
    public void FacetWithSameName_N1_ConstructorFromSource_ShouldWork()
    {
        // Arrange
        var source = new global::Facet.Tests.TestModels.NamespaceCollision.N1.Employee
        {
            Salary = 75000m,
            Department = "Engineering"
        };

        // Act
        var dto = new global::Facet.Tests.TestModels.NamespaceCollision.N1.EmployeeDto(source);

        // Assert
        dto.Salary.Should().Be(75000m);
        dto.Department.Should().Be("Engineering");
    }

    [Fact]
    public void FacetWithSameName_N2_ConstructorFromSource_ShouldWork()
    {
        // Arrange
        var source = new global::Facet.Tests.TestModels.NamespaceCollision.N2.Employee
        {
            Salary = 85000m,
            Role = "Senior Developer"
        };

        // Act
        var dto = new global::Facet.Tests.TestModels.NamespaceCollision.N2.EmployeeDto(source);

        // Assert
        dto.Salary.Should().Be(85000m);
        dto.Role.Should().Be("Senior Developer");
    }
}
