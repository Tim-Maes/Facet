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
        var n1DtoType = typeof(global::Facet.Tests.TestModels.NamespaceCollision.N1.EmployeeDto);
        var n2DtoType = typeof(global::Facet.Tests.TestModels.NamespaceCollision.N2.EmployeeDto);

        n1DtoType.Should().NotBeNull();
        n2DtoType.Should().NotBeNull();
        n1DtoType.Should().NotBeSameAs(n2DtoType);
        n1DtoType.FullName.Should().Be("Facet.Tests.TestModels.NamespaceCollision.N1.EmployeeDto");
        n2DtoType.FullName.Should().Be("Facet.Tests.TestModels.NamespaceCollision.N2.EmployeeDto");
    }

    [Fact]
    public void FacetWithSameName_N1_ShouldHaveCorrectProperties()
    {
        var dtoType = typeof(global::Facet.Tests.TestModels.NamespaceCollision.N1.EmployeeDto);

        dtoType.GetProperty("Salary").Should().NotBeNull();
        dtoType.GetProperty("Salary")!.PropertyType.Should().Be(typeof(decimal));
        
        dtoType.GetProperty("Department").Should().NotBeNull();
        dtoType.GetProperty("Department")!.PropertyType.Should().Be(typeof(string));
        
        dtoType.GetProperty("Role").Should().BeNull();
    }

    [Fact]
    public void FacetWithSameName_N2_ShouldHaveCorrectProperties()
    {
        var dtoType = typeof(global::Facet.Tests.TestModels.NamespaceCollision.N2.EmployeeDto);

        dtoType.GetProperty("Salary").Should().NotBeNull();
        dtoType.GetProperty("Salary")!.PropertyType.Should().Be(typeof(decimal));
        
        dtoType.GetProperty("Role").Should().NotBeNull();
        dtoType.GetProperty("Role")!.PropertyType.Should().Be(typeof(string));
        
        dtoType.GetProperty("Department").Should().BeNull();
    }

    [Fact]
    public void FacetWithSameName_N1_ConstructorFromSource_ShouldWork()
    {
        var source = new global::Facet.Tests.TestModels.NamespaceCollision.N1.Employee
        {
            Salary = 75000m,
            Department = "Engineering"
        };

        var dto = new global::Facet.Tests.TestModels.NamespaceCollision.N1.EmployeeDto(source);

        dto.Salary.Should().Be(75000m);
        dto.Department.Should().Be("Engineering");
    }

    [Fact]
    public void FacetWithSameName_N2_ConstructorFromSource_ShouldWork()
    {
        var source = new global::Facet.Tests.TestModels.NamespaceCollision.N2.Employee
        {
            Salary = 85000m,
            Role = "Senior Developer"
        };

        var dto = new global::Facet.Tests.TestModels.NamespaceCollision.N2.EmployeeDto(source);

        dto.Salary.Should().Be(85000m);
        dto.Role.Should().Be("Senior Developer");
    }
}
