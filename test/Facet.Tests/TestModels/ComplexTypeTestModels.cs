namespace Facet.Tests.TestModels;

// Source entities for complex type testing
public class AddressEntity
{
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string ZipCode { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
}

public class CompanyEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Industry { get; set; } = string.Empty;
    public AddressEntity HeadquartersAddress { get; set; } = new();
}

public class StaffMember
{
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public CompanyEntity Company { get; set; } = new();
    public AddressEntity HomeAddress { get; set; } = new();
    public DateTime HireDate { get; set; }
    public decimal Salary { get; set; }
}

public class DepartmentEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public CompanyEntity Company { get; set; } = new();
    public StaffMember Manager { get; set; } = new();
    public int EmployeeCount { get; set; }
}

// Facet DTOs
[Facet(typeof(AddressEntity))]
public partial record AddressFacet;

[Facet(typeof(CompanyEntity), NestedFacets = new[] { typeof(AddressFacet) })]
public partial record CompanyFacet;

[Facet(typeof(StaffMember), exclude: ["PasswordHash", "Salary"], NestedFacets = new[] { typeof(CompanyFacet), typeof(AddressFacet) })]
public partial record StaffMemberFacet;

[Facet(typeof(DepartmentEntity), NestedFacets = new[] { typeof(CompanyFacet), typeof(StaffMemberFacet) })]
public partial record DepartmentFacet;
