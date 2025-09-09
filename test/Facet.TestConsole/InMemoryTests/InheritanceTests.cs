using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Facet.Extensions;
using Facet.TestConsole.TestInfrastructure;

namespace Facet.TestConsole.InMemoryTests;

public class InheritanceTests : ITestSuite
{
    public Task<List<(string name, bool passed)>> RunTestsAsync()
    {
        var results = new List<(string name, bool passed)>();

        try
        {
            TestInheritanceSupport();
            results.Add(("Inheritance Support", true));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Inheritance Support: {ex.Message}");
            results.Add(("Inheritance Support", false));
        }

        return Task.FromResult(results);
    }

    private static void TestInheritanceSupport()
    {
        Console.WriteLine("1. Testing Inheritance Support:");
        Console.WriteLine("===============================");

        var employees = TestDataFactory.CreateSampleEmployees();
        var managers = TestDataFactory.CreateSampleManagers();

        Console.WriteLine("Employee DTOs (inherits from Person -> BaseEntity):");
        foreach (var employee in employees)
        {
            var employeeDto = employee.ToFacet<Employee, EmployeeDto>();
            Console.WriteLine($"  {employeeDto.DisplayName}");
            Console.WriteLine($"    ID: {employeeDto.Id}, Employee ID: {employeeDto.EmployeeId}");
            Console.WriteLine($"    Department: {employeeDto.Department}, Hire Date: {employeeDto.HireDate:yyyy-MM-dd}");
            Console.WriteLine($"    Created: {employeeDto.CreatedAt:yyyy-MM-dd}, Updated: {employeeDto.UpdatedAt:yyyy-MM-dd}");
            Console.WriteLine();
        }

        Console.WriteLine("Manager DTOs (inherits from Employee -> Person -> BaseEntity):");
        foreach (var manager in managers)
        {
            var managerDto = manager.ToFacet<Manager, ManagerDto>();
            Console.WriteLine($"  {managerDto.DisplayName}");
            Console.WriteLine($"    ID: {managerDto.Id}, Employee ID: {managerDto.EmployeeId}");
            Console.WriteLine($"    Department: {managerDto.Department}, Team: {managerDto.TeamName} ({managerDto.TeamSize} members)");
            Console.WriteLine($"    Hire Date: {managerDto.HireDate:yyyy-MM-dd}");
            Console.WriteLine($"    Created: {managerDto.CreatedAt:yyyy-MM-dd}, Updated: {managerDto.UpdatedAt:yyyy-MM-dd}");
            Console.WriteLine();
        }
    }
}