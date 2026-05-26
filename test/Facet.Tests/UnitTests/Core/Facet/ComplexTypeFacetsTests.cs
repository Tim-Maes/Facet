using Facet.Tests.TestModels;

namespace Facet.Tests.UnitTests.Core.Facet;

public class ComplexTypeFacetsTests
{
    [Fact]
    public void ToFacet_ShouldMapSimpleNestedType_WhenUsingChildrenParameter()
    {
        var company = new CompanyEntity
        {
            Id = 1,
            Name = "Acme Corp",
            Industry = "Technology",
            HeadquartersAddress = new AddressEntity
            {
                Street = "123 Main St",
                City = "San Francisco",
                State = "CA",
                ZipCode = "94105",
                Country = "USA"
            }
        };

        var dto = new CompanyFacet(company);

        dto.Should().NotBeNull();
        dto.Id.Should().Be(1);
        dto.Name.Should().Be("Acme Corp");
        dto.Industry.Should().Be("Technology");

        dto.HeadquartersAddress.Should().NotBeNull();
        dto.HeadquartersAddress.Street.Should().Be("123 Main St");
        dto.HeadquartersAddress.City.Should().Be("San Francisco");
        dto.HeadquartersAddress.State.Should().Be("CA");
        dto.HeadquartersAddress.ZipCode.Should().Be("94105");
        dto.HeadquartersAddress.Country.Should().Be("USA");
    }

    [Fact]
    public void ToFacet_ShouldMapMultipleDifferentNestedTypes_WhenUsingChildrenParameter()
    {
        var employee = new StaffMember
        {
            Id = 1,
            FirstName = "John",
            LastName = "Doe",
            Email = "john.doe@example.com",
            PasswordHash = "secrethash123",
            HireDate = new DateTime(2020, 1, 15),
            Salary = 75000m,
            Company = new CompanyEntity
            {
                Id = 10,
                Name = "Tech Solutions",
                Industry = "Software",
                HeadquartersAddress = new AddressEntity
                {
                    Street = "456 Tech Blvd",
                    City = "Austin",
                    State = "TX",
                    ZipCode = "78701",
                    Country = "USA"
                }
            },
            HomeAddress = new AddressEntity
            {
                Street = "789 Residential Ave",
                City = "Round Rock",
                State = "TX",
                ZipCode = "78664",
                Country = "USA"
            }
        };

        var dto = new StaffMemberFacet(employee);

        dto.Should().NotBeNull();
        dto.Id.Should().Be(1);
        dto.FirstName.Should().Be("John");
        dto.LastName.Should().Be("Doe");
        dto.Email.Should().Be("john.doe@example.com");
        dto.HireDate.Should().Be(new DateTime(2020, 1, 15));

        var dtoType = dto.GetType();
        dtoType.GetProperty("PasswordHash").Should().BeNull("PasswordHash should be excluded");
        dtoType.GetProperty("Salary").Should().BeNull("Salary should be excluded");

        dto.Company.Should().NotBeNull();
        dto.Company.Id.Should().Be(10);
        dto.Company.Name.Should().Be("Tech Solutions");
        dto.Company.Industry.Should().Be("Software");

        dto.Company.HeadquartersAddress.Should().NotBeNull();
        dto.Company.HeadquartersAddress.Street.Should().Be("456 Tech Blvd");
        dto.Company.HeadquartersAddress.City.Should().Be("Austin");

        dto.HomeAddress.Should().NotBeNull();
        dto.HomeAddress.Street.Should().Be("789 Residential Ave");
        dto.HomeAddress.City.Should().Be("Round Rock");
        dto.HomeAddress.State.Should().Be("TX");
    }

    [Fact]
    public void ToFacet_ShouldMapDeeplyNestedTypes_WhenUsingChildrenParameter()
    {
        var department = new DepartmentEntity
        {
            Id = 5,
            Name = "Engineering",
            EmployeeCount = 50,
            Company = new CompanyEntity
            {
                Id = 20,
                Name = "Innovate Inc",
                Industry = "Innovation",
                HeadquartersAddress = new AddressEntity
                {
                    Street = "100 Innovation Way",
                    City = "Seattle",
                    State = "WA",
                    ZipCode = "98101",
                    Country = "USA"
                }
            },
            Manager = new StaffMember
            {
                Id = 2,
                FirstName = "Jane",
                LastName = "Smith",
                Email = "jane.smith@innovate.com",
                PasswordHash = "anothersecret",
                HireDate = new DateTime(2018, 3, 1),
                Salary = 120000m,
                Company = new CompanyEntity
                {
                    Id = 20,
                    Name = "Innovate Inc",
                    Industry = "Innovation",
                    HeadquartersAddress = new AddressEntity
                    {
                        Street = "100 Innovation Way",
                        City = "Seattle",
                        State = "WA",
                        ZipCode = "98101",
                        Country = "USA"
                    }
                },
                HomeAddress = new AddressEntity
                {
                    Street = "555 Manager Lane",
                    City = "Bellevue",
                    State = "WA",
                    ZipCode = "98004",
                    Country = "USA"
                }
            }
        };

        var dto = new DepartmentFacet(department);

        dto.Should().NotBeNull();
        dto.Id.Should().Be(5);
        dto.Name.Should().Be("Engineering");
        dto.EmployeeCount.Should().Be(50);

        dto.Company.Should().NotBeNull();
        dto.Company.Id.Should().Be(20);
        dto.Company.Name.Should().Be("Innovate Inc");
        dto.Company.HeadquartersAddress.Should().NotBeNull();
        dto.Company.HeadquartersAddress.City.Should().Be("Seattle");

        dto.Manager.Should().NotBeNull();
        dto.Manager.Id.Should().Be(2);
        dto.Manager.FirstName.Should().Be("Jane");
        dto.Manager.LastName.Should().Be("Smith");
        dto.Manager.Email.Should().Be("jane.smith@innovate.com");

        dto.Manager.Company.Should().NotBeNull();
        dto.Manager.Company.Name.Should().Be("Innovate Inc");
        dto.Manager.Company.HeadquartersAddress.City.Should().Be("Seattle");

        dto.Manager.HomeAddress.Should().NotBeNull();
        dto.Manager.HomeAddress.Street.Should().Be("555 Manager Lane");
        dto.Manager.HomeAddress.City.Should().Be("Bellevue");
    }

    [Fact]
    public void ToSource_ShouldMapBackToSourceType_WithSimpleNestedType()
    {
        var originalCompany = new CompanyEntity
        {
            Id = 1,
            Name = "Test Corp",
            Industry = "Testing",
            HeadquartersAddress = new AddressEntity
            {
                Street = "111 Test St",
                City = "Test City",
                State = "TC",
                ZipCode = "12345",
                Country = "Testland"
            }
        };

        var dto = new CompanyFacet(originalCompany);

        var mapped = dto.ToSource();

        mapped.Should().NotBeNull();
        mapped.Id.Should().Be(1);
        mapped.Name.Should().Be("Test Corp");
        mapped.Industry.Should().Be("Testing");

        mapped.HeadquartersAddress.Should().NotBeNull();
        mapped.HeadquartersAddress.Street.Should().Be("111 Test St");
        mapped.HeadquartersAddress.City.Should().Be("Test City");
        mapped.HeadquartersAddress.State.Should().Be("TC");
        mapped.HeadquartersAddress.ZipCode.Should().Be("12345");
        mapped.HeadquartersAddress.Country.Should().Be("Testland");
    }

    [Fact]
    public void ToSource_ShouldMapBackToSourceType_WithMultipleNestedTypes()
    {
        var originalEmployee = new StaffMember
        {
            Id = 3,
            FirstName = "Alice",
            LastName = "Johnson",
            Email = "alice@example.com",
            PasswordHash = "willbemissing",
            HireDate = new DateTime(2019, 6, 1),
            Salary = 85000m,
            Company = new CompanyEntity
            {
                Id = 30,
                Name = "Example Co",
                Industry = "Examples",
                HeadquartersAddress = new AddressEntity
                {
                    Street = "200 Example Rd",
                    City = "Example City",
                    State = "EX",
                    ZipCode = "55555",
                    Country = "Exampleland"
                }
            },
            HomeAddress = new AddressEntity
            {
                Street = "300 Home St",
                City = "Home Town",
                State = "HT",
                ZipCode = "66666",
                Country = "Homeland"
            }
        };

        var dto = new StaffMemberFacet(originalEmployee);

        var mapped = dto.ToSource();

        mapped.Should().NotBeNull();
        mapped.Id.Should().Be(3);
        mapped.FirstName.Should().Be("Alice");
        mapped.LastName.Should().Be("Johnson");
        mapped.Email.Should().Be("alice@example.com");
        mapped.HireDate.Should().Be(new DateTime(2019, 6, 1));

        mapped.PasswordHash.Should().BeEmpty();
        mapped.Salary.Should().Be(0m);

        mapped.Company.Should().NotBeNull();
        mapped.Company.Id.Should().Be(30);
        mapped.Company.Name.Should().Be("Example Co");
        mapped.Company.Industry.Should().Be("Examples");
        mapped.Company.HeadquartersAddress.Should().NotBeNull();
        mapped.Company.HeadquartersAddress.Street.Should().Be("200 Example Rd");
        mapped.Company.HeadquartersAddress.City.Should().Be("Example City");

        mapped.HomeAddress.Should().NotBeNull();
        mapped.HomeAddress.Street.Should().Be("300 Home St");
        mapped.HomeAddress.City.Should().Be("Home Town");
    }

    [Fact]
    public void Projection_ShouldWork_WithChildFacets()
    {
        var companies = new[]
        {
            new CompanyEntity
            {
                Id = 1,
                Name = "Company A",
                Industry = "Industry A",
                HeadquartersAddress = new AddressEntity { City = "City A", State = "CA" }
            },
            new CompanyEntity
            {
                Id = 2,
                Name = "Company B",
                Industry = "Industry B",
                HeadquartersAddress = new AddressEntity { City = "City B", State = "NY" }
            }
        }.AsQueryable();

        var dtos = companies.Select(CompanyFacet.Projection).ToList();

        dtos.Should().HaveCount(2);

        dtos[0].Id.Should().Be(1);
        dtos[0].Name.Should().Be("Company A");
        dtos[0].HeadquartersAddress.Should().NotBeNull();
        dtos[0].HeadquartersAddress.City.Should().Be("City A");
        dtos[0].HeadquartersAddress.State.Should().Be("CA");

        dtos[1].Id.Should().Be(2);
        dtos[1].Name.Should().Be("Company B");
        dtos[1].HeadquartersAddress.Should().NotBeNull();
        dtos[1].HeadquartersAddress.City.Should().Be("City B");
        dtos[1].HeadquartersAddress.State.Should().Be("NY");
    }

    [Fact]
    public void ChildFacet_TypeProperty_ShouldBeCorrectType()
    {
        var dto = new CompanyFacet(new CompanyEntity
        {
            HeadquartersAddress = new AddressEntity { City = "Test" }
        });

        dto.HeadquartersAddress.Should().BeOfType<AddressFacet>();
        dto.HeadquartersAddress.Should().NotBeAssignableTo<AddressEntity>();
    }

    [Fact]
    public void ToFacet_ShouldMapNestedFacets_WhenUsingExtensionMethod()
    {
        var staffMember = new StaffMember
        {
            Id = 1,
            FirstName = "John",
            LastName = "Doe",
            Email = "john.doe@example.com",
            PasswordHash = "secrethash123",
            HireDate = new DateTime(2020, 1, 15),
            Salary = 75000m,
            Company = new CompanyEntity
            {
                Id = 10,
                Name = "Tech Solutions",
                Industry = "Software",
                HeadquartersAddress = new AddressEntity
                {
                    Street = "456 Tech Blvd",
                    City = "Austin",
                    State = "TX",
                    ZipCode = "78701",
                    Country = "USA"
                }
            },
            HomeAddress = new AddressEntity
            {
                Street = "789 Residential Ave",
                City = "Round Rock",
                State = "TX",
                ZipCode = "78664",
                Country = "USA"
            }
        };

        var dto = staffMember.ToFacet<StaffMemberFacet>();

        dto.Should().NotBeNull();
        dto.Id.Should().Be(1);
        dto.FirstName.Should().Be("John");
        dto.LastName.Should().Be("Doe");
        dto.Email.Should().Be("john.doe@example.com");

        dto.Company.Should().NotBeNull();
        dto.Company.Should().BeOfType<CompanyFacet>();
        dto.Company.Id.Should().Be(10);
        dto.Company.Name.Should().Be("Tech Solutions");
        dto.Company.Industry.Should().Be("Software");

        dto.Company.HeadquartersAddress.Should().NotBeNull();
        dto.Company.HeadquartersAddress.Should().BeOfType<AddressFacet>();
        dto.Company.HeadquartersAddress.Street.Should().Be("456 Tech Blvd");
        dto.Company.HeadquartersAddress.City.Should().Be("Austin");

        dto.HomeAddress.Should().NotBeNull();
        dto.HomeAddress.Should().BeOfType<AddressFacet>();
        dto.HomeAddress.Street.Should().Be("789 Residential Ave");
        dto.HomeAddress.City.Should().Be("Round Rock");
    }

    [Fact]
    public void SelectFacets_ShouldMapNestedFacets_WhenProjectingCollection()
    {
        var staffMembers = new[]
        {
            new StaffMember
            {
                Id = 1,
                FirstName = "Alice",
                LastName = "Smith",
                Email = "alice@example.com",
                Company = new CompanyEntity
                {
                    Id = 100,
                    Name = "Company A",
                    Industry = "Tech",
                    HeadquartersAddress = new AddressEntity { City = "New York", State = "NY" }
                },
                HomeAddress = new AddressEntity { City = "Brooklyn", State = "NY" }
            },
            new StaffMember
            {
                Id = 2,
                FirstName = "Bob",
                LastName = "Johnson",
                Email = "bob@example.com",
                Company = new CompanyEntity
                {
                    Id = 200,
                    Name = "Company B",
                    Industry = "Finance",
                    HeadquartersAddress = new AddressEntity { City = "Chicago", State = "IL" }
                },
                HomeAddress = new AddressEntity { City = "Evanston", State = "IL" }
            }
        }.AsQueryable();

        var dtos = staffMembers.SelectFacets<StaffMemberFacet>().ToList();

        dtos.Should().HaveCount(2);

        dtos[0].FirstName.Should().Be("Alice");
        dtos[0].Company.Should().BeOfType<CompanyFacet>();
        dtos[0].Company.Name.Should().Be("Company A");
        dtos[0].Company.HeadquartersAddress.Should().BeOfType<AddressFacet>();
        dtos[0].Company.HeadquartersAddress.City.Should().Be("New York");
        dtos[0].HomeAddress.Should().BeOfType<AddressFacet>();
        dtos[0].HomeAddress.City.Should().Be("Brooklyn");

        dtos[1].FirstName.Should().Be("Bob");
        dtos[1].Company.Should().BeOfType<CompanyFacet>();
        dtos[1].Company.Name.Should().Be("Company B");
        dtos[1].Company.HeadquartersAddress.Should().BeOfType<AddressFacet>();
        dtos[1].Company.HeadquartersAddress.City.Should().Be("Chicago");
        dtos[1].HomeAddress.Should().BeOfType<AddressFacet>();
        dtos[1].HomeAddress.City.Should().Be("Evanston");
    }

    [Fact]
    public void ToFacet_WithTypedParameters_ShouldMapNestedFacets()
    {
        var company = new CompanyEntity
        {
            Id = 1,
            Name = "Acme Corp",
            Industry = "Technology",
            HeadquartersAddress = new AddressEntity
            {
                Street = "123 Main St",
                City = "San Francisco",
                State = "CA",
                ZipCode = "94105",
                Country = "USA"
            }
        };

        var dto = company.ToFacet<CompanyEntity, CompanyFacet>();

        dto.Should().NotBeNull();
        dto.Id.Should().Be(1);
        dto.Name.Should().Be("Acme Corp");
        dto.Industry.Should().Be("Technology");

        dto.HeadquartersAddress.Should().NotBeNull();
        dto.HeadquartersAddress.Should().BeOfType<AddressFacet>();
        dto.HeadquartersAddress.Street.Should().Be("123 Main St");
        dto.HeadquartersAddress.City.Should().Be("San Francisco");
        dto.HeadquartersAddress.State.Should().Be("CA");
        dto.HeadquartersAddress.ZipCode.Should().Be("94105");
        dto.HeadquartersAddress.Country.Should().Be("USA");
    }

    [Fact]
    public void SelectFacets_WithTypedParameters_ShouldMapNestedFacets()
    {
        var companies = new[]
        {
            new CompanyEntity
            {
                Id = 1,
                Name = "Company A",
                Industry = "Industry A",
                HeadquartersAddress = new AddressEntity { City = "City A", State = "CA" }
            },
            new CompanyEntity
            {
                Id = 2,
                Name = "Company B",
                Industry = "Industry B",
                HeadquartersAddress = new AddressEntity { City = "City B", State = "NY" }
            }
        }.AsQueryable();

        var dtos = companies.SelectFacets<CompanyEntity, CompanyFacet>().ToList();

        dtos.Should().HaveCount(2);

        dtos[0].Id.Should().Be(1);
        dtos[0].Name.Should().Be("Company A");
        dtos[0].HeadquartersAddress.Should().NotBeNull();
        dtos[0].HeadquartersAddress.Should().BeOfType<AddressFacet>();
        dtos[0].HeadquartersAddress.City.Should().Be("City A");
        dtos[0].HeadquartersAddress.State.Should().Be("CA");

        dtos[1].Id.Should().Be(2);
        dtos[1].Name.Should().Be("Company B");
        dtos[1].HeadquartersAddress.Should().NotBeNull();
        dtos[1].HeadquartersAddress.Should().BeOfType<AddressFacet>();
        dtos[1].HeadquartersAddress.City.Should().Be("City B");
        dtos[1].HeadquartersAddress.State.Should().Be("NY");
    }
}
