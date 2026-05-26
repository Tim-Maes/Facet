
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
