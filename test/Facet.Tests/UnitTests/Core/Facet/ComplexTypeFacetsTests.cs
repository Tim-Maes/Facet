using Facet.Tests.TestModels;

namespace Facet.Tests.UnitTests.Core.Facet;

public class ComplexTypeFacetsTests
{
    [Fact]
    public void ToFacet_ShouldMapSimpleNestedType_WhenUsingChildrenParameter()
    {
        // Arrange
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

        // Act
        var dto = new CompanyFacet(company);

        // Assert
        dto.Should().NotBeNull();
        dto.Id.Should().Be(1);
        dto.Name.Should().Be("Acme Corp");
        dto.Industry.Should().Be("Technology");

        // Child facet should be automatically mapped
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
        // Arrange
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

        // Act
        var dto = new StaffMemberFacet(employee);

        // Assert
        dto.Should().NotBeNull();
        dto.Id.Should().Be(1);
        dto.FirstName.Should().Be("John");
        dto.LastName.Should().Be("Doe");
        dto.Email.Should().Be("john.doe@example.com");
        dto.HireDate.Should().Be(new DateTime(2020, 1, 15));

        // Excluded properties should not exist
        var dtoType = dto.GetType();
        dtoType.GetProperty("PasswordHash").Should().BeNull("PasswordHash should be excluded");
        dtoType.GetProperty("Salary").Should().BeNull("Salary should be excluded");

        // Company child facet should be mapped
        dto.Company.Should().NotBeNull();
        dto.Company.Id.Should().Be(10);
        dto.Company.Name.Should().Be("Tech Solutions");
        dto.Company.Industry.Should().Be("Software");

        // Nested child facet (Company -> Address)
        dto.Company.HeadquartersAddress.Should().NotBeNull();
        dto.Company.HeadquartersAddress.Street.Should().Be("456 Tech Blvd");
        dto.Company.HeadquartersAddress.City.Should().Be("Austin");

        // HomeAddress child facet should be mapped
        dto.HomeAddress.Should().NotBeNull();
        dto.HomeAddress.Street.Should().Be("789 Residential Ave");
        dto.HomeAddress.City.Should().Be("Round Rock");
        dto.HomeAddress.State.Should().Be("TX");
    }

    [Fact]
    public void ToFacet_ShouldMapDeeplyNestedTypes_WhenUsingChildrenParameter()
    {
        // Arrange
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

        // Act
        var dto = new DepartmentFacet(department);

        // Assert
        dto.Should().NotBeNull();
        dto.Id.Should().Be(5);
        dto.Name.Should().Be("Engineering");
        dto.EmployeeCount.Should().Be(50);

        // Company child facet
        dto.Company.Should().NotBeNull();
        dto.Company.Id.Should().Be(20);
        dto.Company.Name.Should().Be("Innovate Inc");
        dto.Company.HeadquartersAddress.Should().NotBeNull();
        dto.Company.HeadquartersAddress.City.Should().Be("Seattle");

        // Manager (Employee) child facet
        dto.Manager.Should().NotBeNull();
        dto.Manager.Id.Should().Be(2);
        dto.Manager.FirstName.Should().Be("Jane");
        dto.Manager.LastName.Should().Be("Smith");
        dto.Manager.Email.Should().Be("jane.smith@innovate.com");

        // Manager's Company child facet
        dto.Manager.Company.Should().NotBeNull();
        dto.Manager.Company.Name.Should().Be("Innovate Inc");
        dto.Manager.Company.HeadquartersAddress.City.Should().Be("Seattle");

        // Manager's HomeAddress child facet
        dto.Manager.HomeAddress.Should().NotBeNull();
        dto.Manager.HomeAddress.Street.Should().Be("555 Manager Lane");
        dto.Manager.HomeAddress.City.Should().Be("Bellevue");
    }

    [Fact]
    public void BackTo_ShouldMapBackToSourceType_WithSimpleNestedType()
    {
        // Arrange
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

        // Act
        var mappedBack = dto.BackTo();

        // Assert
        mappedBack.Should().NotBeNull();
        mappedBack.Id.Should().Be(1);
        mappedBack.Name.Should().Be("Test Corp");
        mappedBack.Industry.Should().Be("Testing");

        mappedBack.HeadquartersAddress.Should().NotBeNull();
        mappedBack.HeadquartersAddress.Street.Should().Be("111 Test St");
        mappedBack.HeadquartersAddress.City.Should().Be("Test City");
        mappedBack.HeadquartersAddress.State.Should().Be("TC");
        mappedBack.HeadquartersAddress.ZipCode.Should().Be("12345");
        mappedBack.HeadquartersAddress.Country.Should().Be("Testland");
    }

    [Fact]
    public void BackTo_ShouldMapBackToSourceType_WithMultipleNestedTypes()
    {
        // Arrange
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

        // Act
        var mappedBack = dto.BackTo();

        // Assert
        mappedBack.Should().NotBeNull();
        mappedBack.Id.Should().Be(3);
        mappedBack.FirstName.Should().Be("Alice");
        mappedBack.LastName.Should().Be("Johnson");
        mappedBack.Email.Should().Be("alice@example.com");
        mappedBack.HireDate.Should().Be(new DateTime(2019, 6, 1));

        // Excluded properties should have default values
        mappedBack.PasswordHash.Should().BeEmpty();
        mappedBack.Salary.Should().Be(0m);

        // Company should be mapped back
        mappedBack.Company.Should().NotBeNull();
        mappedBack.Company.Id.Should().Be(30);
        mappedBack.Company.Name.Should().Be("Example Co");
        mappedBack.Company.Industry.Should().Be("Examples");
        mappedBack.Company.HeadquartersAddress.Should().NotBeNull();
        mappedBack.Company.HeadquartersAddress.Street.Should().Be("200 Example Rd");
        mappedBack.Company.HeadquartersAddress.City.Should().Be("Example City");

        // HomeAddress should be mapped back
        mappedBack.HomeAddress.Should().NotBeNull();
        mappedBack.HomeAddress.Street.Should().Be("300 Home St");
        mappedBack.HomeAddress.City.Should().Be("Home Town");
    }

    [Fact]
    public void Projection_ShouldWork_WithChildFacets()
    {
        // Arrange
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

        // Act
        var dtos = companies.Select(CompanyFacet.Projection).ToList();

        // Assert
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
        // Arrange & Act
        var dto = new CompanyFacet(new CompanyEntity
        {
            HeadquartersAddress = new AddressEntity { City = "Test" }
        });

        // Assert
        dto.HeadquartersAddress.Should().BeOfType<AddressFacet>();
        dto.HeadquartersAddress.Should().NotBeAssignableTo<AddressEntity>();
    }
}
