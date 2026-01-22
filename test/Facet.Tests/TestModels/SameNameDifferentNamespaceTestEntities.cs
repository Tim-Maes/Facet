// Test entities for GitHub issue #249
// Generator issue when DTOs with the same name are under different namespaces

namespace Facet.Tests.TestModels.NamespaceCollision.N1
{
    public class Employee
    {
        public decimal Salary { get; set; }
        public string Department { get; set; } = string.Empty;
    }

    [Facet(typeof(Employee))]
    public partial class EmployeeDto;
}

namespace Facet.Tests.TestModels.NamespaceCollision.N2
{
    public class Employee
    {
        public decimal Salary { get; set; }
        public string Role { get; set; } = string.Empty;
    }

    [Facet(typeof(Employee))]
    public partial class EmployeeDto;
}
