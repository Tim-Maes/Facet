using Facet.Tests.TestModels;

namespace Facet.Tests.UnitTests.Core.Facet;

public class CollectionNestedFacetsTests
{
    [Fact]
    public void ToFacet_ShouldMapListCollection_WhenUsingNestedFacets()
    {
        // Arrange
        var order = new OrderEntity
        {
            Id = 1,
            OrderNumber = "ORD-2025-001",
            OrderDate = new DateTime(2025, 1, 15),
            Items = new List<OrderItemEntity>
            {
                new() { Id = 1, ProductName = "Laptop", Price = 1200.00m, Quantity = 1 },
                new() { Id = 2, ProductName = "Mouse", Price = 25.00m, Quantity = 2 },
                new() { Id = 3, ProductName = "Keyboard", Price = 75.00m, Quantity = 1 }
            },
            ShippingAddress = new AddressEntity
            {
                Street = "123 Main St",
                City = "Seattle",
                State = "WA",
                ZipCode = "98101",
                Country = "USA"
            }
        };

        // Act
        var dto = new OrderFacet(order);

        // Assert
        dto.Should().NotBeNull();
        dto.Id.Should().Be(1);
        dto.OrderNumber.Should().Be("ORD-2025-001");
        dto.OrderDate.Should().Be(new DateTime(2025, 1, 15));

        // Verify collection mapping
        dto.Items.Should().NotBeNull();
        dto.Items.Should().HaveCount(3);
        dto.Items.Should().AllBeOfType<OrderItemFacet>();

        // Verify first item
        dto.Items[0].Id.Should().Be(1);
        dto.Items[0].ProductName.Should().Be("Laptop");
        dto.Items[0].Price.Should().Be(1200.00m);
        dto.Items[0].Quantity.Should().Be(1);

        // Verify second item
        dto.Items[1].Id.Should().Be(2);
        dto.Items[1].ProductName.Should().Be("Mouse");
        dto.Items[1].Price.Should().Be(25.00m);
        dto.Items[1].Quantity.Should().Be(2);

        // Verify nested address
        dto.ShippingAddress.Should().NotBeNull();
        dto.ShippingAddress.Street.Should().Be("123 Main St");
        dto.ShippingAddress.City.Should().Be("Seattle");
    }

    [Fact]
    public void ToFacet_ShouldMapArrayCollection_WhenUsingNestedFacets()
    {
        // Arrange
        var team = new TeamEntity
        {
            Id = 10,
            Name = "Development Team",
            Members = new[]
            {
                new StaffMember
                {
                    Id = 1,
                    FirstName = "Alice",
                    LastName = "Johnson",
                    Email = "alice@example.com",
                    PasswordHash = "hash1",
                    Salary = 90000m,
                    HireDate = new DateTime(2020, 1, 1),
                    Company = new CompanyEntity { Id = 100, Name = "Tech Corp", Industry = "Technology", HeadquartersAddress = new AddressEntity() },
                    HomeAddress = new AddressEntity { City = "Seattle" }
                },
                new StaffMember
                {
                    Id = 2,
                    FirstName = "Bob",
                    LastName = "Smith",
                    Email = "bob@example.com",
                    PasswordHash = "hash2",
                    Salary = 95000m,
                    HireDate = new DateTime(2019, 5, 15),
                    Company = new CompanyEntity { Id = 100, Name = "Tech Corp", Industry = "Technology", HeadquartersAddress = new AddressEntity() },
                    HomeAddress = new AddressEntity { City = "Portland" }
                }
            }
        };

        // Act
        var dto = new TeamFacet(team);

        // Assert
        dto.Should().NotBeNull();
        dto.Id.Should().Be(10);
        dto.Name.Should().Be("Development Team");

        // Verify array mapping
        dto.Members.Should().NotBeNull();
        dto.Members.Should().HaveCount(2);
        dto.Members.Should().AllBeOfType<StaffMemberFacet>();

        // Verify first member (PasswordHash and Salary should be excluded)
        dto.Members[0].Id.Should().Be(1);
        dto.Members[0].FirstName.Should().Be("Alice");
        dto.Members[0].LastName.Should().Be("Johnson");
        dto.Members[0].Email.Should().Be("alice@example.com");
        dto.Members[0].HireDate.Should().Be(new DateTime(2020, 1, 1));

        var dtoType = dto.Members[0].GetType();
        dtoType.GetProperty("PasswordHash").Should().BeNull("PasswordHash should be excluded");
        dtoType.GetProperty("Salary").Should().BeNull("Salary should be excluded");

        // Verify second member
        dto.Members[1].FirstName.Should().Be("Bob");
        dto.Members[1].LastName.Should().Be("Smith");
    }

    [Fact]
    public void ToFacet_ShouldMapICollectionType_WhenUsingNestedFacets()
    {
        // Arrange
        var project = new ProjectEntity
        {
            Id = 500,
            Name = "Project Phoenix",
            Teams = new List<TeamEntity>
            {
                new()
                {
                    Id = 1,
                    Name = "Backend Team",
                    Members = new[]
                    {
                        new StaffMember
                        {
                            Id = 10,
                            FirstName = "Charlie",
                            LastName = "Brown",
                            Email = "charlie@example.com",
                            PasswordHash = "hash",
                            Salary = 100000m,
                            Company = new CompanyEntity { Id = 1, Name = "Corp", Industry = "Tech", HeadquartersAddress = new AddressEntity() },
                            HomeAddress = new AddressEntity()
                        }
                    }
                },
                new()
                {
                    Id = 2,
                    Name = "Frontend Team",
                    Members = Array.Empty<StaffMember>()
                }
            }
        };

        // Act
        var dto = new ProjectFacet(project);

        // Assert
        dto.Should().NotBeNull();
        dto.Id.Should().Be(500);
        dto.Name.Should().Be("Project Phoenix");

        // Verify ICollection mapping
        dto.Teams.Should().NotBeNull();
        dto.Teams.Should().HaveCount(2);
        dto.Teams.Should().AllBeOfType<TeamFacet>();

        // Verify first team
        var firstTeam = dto.Teams.ElementAt(0);
        firstTeam.Id.Should().Be(1);
        firstTeam.Name.Should().Be("Backend Team");
        firstTeam.Members.Should().HaveCount(1);
        firstTeam.Members[0].FirstName.Should().Be("Charlie");

        // Verify second team
        var secondTeam = dto.Teams.ElementAt(1);
        secondTeam.Id.Should().Be(2);
        secondTeam.Name.Should().Be("Frontend Team");
        secondTeam.Members.Should().BeEmpty();
    }

    [Fact]
    public void ToFacet_ShouldHandleEmptyCollections_WhenUsingNestedFacets()
    {
        // Arrange
        var order = new OrderEntity
        {
            Id = 1,
            OrderNumber = "ORD-EMPTY",
            OrderDate = DateTime.Now,
            Items = new List<OrderItemEntity>(),
            ShippingAddress = new AddressEntity()
        };

        // Act
        var dto = new OrderFacet(order);

        // Assert
        dto.Should().NotBeNull();
        dto.Items.Should().NotBeNull();
        dto.Items.Should().BeEmpty();
    }

    [Fact]
    public void BackTo_ShouldMapListCollectionBackToSource()
    {
        // Arrange
        var order = new OrderEntity
        {
            Id = 1,
            OrderNumber = "ORD-2025-001",
            OrderDate = new DateTime(2025, 1, 15),
            Items = new List<OrderItemEntity>
            {
                new() { Id = 1, ProductName = "Laptop", Price = 1200.00m, Quantity = 1 },
                new() { Id = 2, ProductName = "Mouse", Price = 25.00m, Quantity = 2 }
            },
            ShippingAddress = new AddressEntity
            {
                Street = "123 Main St",
                City = "Seattle",
                State = "WA",
                ZipCode = "98101",
                Country = "USA"
            }
        };

        var dto = new OrderFacet(order);

        // Act
        var backToOrder = dto.BackTo();

        // Assert
        backToOrder.Should().NotBeNull();
        backToOrder.Id.Should().Be(1);
        backToOrder.OrderNumber.Should().Be("ORD-2025-001");
        backToOrder.Items.Should().HaveCount(2);
        backToOrder.Items.Should().AllBeOfType<OrderItemEntity>();

        backToOrder.Items[0].Id.Should().Be(1);
        backToOrder.Items[0].ProductName.Should().Be("Laptop");
        backToOrder.Items[0].Price.Should().Be(1200.00m);

        backToOrder.ShippingAddress.Street.Should().Be("123 Main St");
    }

    [Fact]
    public void BackTo_ShouldMapArrayCollectionBackToSource()
    {
        // Arrange
        var team = new TeamEntity
        {
            Id = 10,
            Name = "Development Team",
            Members = new[]
            {
                new StaffMember
                {
                    Id = 1,
                    FirstName = "Alice",
                    LastName = "Johnson",
                    Email = "alice@example.com",
                    PasswordHash = "hash1",
                    Salary = 90000m,
                    HireDate = new DateTime(2020, 1, 1),
                    Company = new CompanyEntity { Id = 100, Name = "Tech Corp", Industry = "Technology", HeadquartersAddress = new AddressEntity() },
                    HomeAddress = new AddressEntity { City = "Seattle" }
                }
            }
        };

        var dto = new TeamFacet(team);

        // Act
        var backToTeam = dto.BackTo();

        // Assert
        backToTeam.Should().NotBeNull();
        backToTeam.Id.Should().Be(10);
        backToTeam.Name.Should().Be("Development Team");
        backToTeam.Members.Should().HaveCount(1);
        backToTeam.Members.Should().BeOfType<StaffMember[]>();

        backToTeam.Members[0].FirstName.Should().Be("Alice");
        backToTeam.Members[0].LastName.Should().Be("Johnson");
    }

    [Fact]
    public void BackTo_ShouldMapICollectionBackToSource()
    {
        // Arrange
        var project = new ProjectEntity
        {
            Id = 500,
            Name = "Project Phoenix",
            Teams = new List<TeamEntity>
            {
                new() { Id = 1, Name = "Backend Team", Members = Array.Empty<StaffMember>() },
                new() { Id = 2, Name = "Frontend Team", Members = Array.Empty<StaffMember>() }
            }
        };

        var dto = new ProjectFacet(project);

        // Act
        var backToProject = dto.BackTo();

        // Assert
        backToProject.Should().NotBeNull();
        backToProject.Id.Should().Be(500);
        backToProject.Name.Should().Be("Project Phoenix");
        backToProject.Teams.Should().HaveCount(2);
        backToProject.Teams.Should().AllBeOfType<TeamEntity>();

        backToProject.Teams.ElementAt(0).Name.Should().Be("Backend Team");
        backToProject.Teams.ElementAt(1).Name.Should().Be("Frontend Team");
    }

    [Fact]
    public void Collection_ShouldPreserveTypeFromSource()
    {
        // Arrange
        var order = new OrderEntity
        {
            Id = 1,
            OrderNumber = "ORD-001",
            OrderDate = DateTime.Now,
            Items = new List<OrderItemEntity>
            {
                new() { Id = 1, ProductName = "Item1", Price = 10, Quantity = 1 }
            },
            ShippingAddress = new AddressEntity()
        };

        // Act
        var dto = new OrderFacet(order);

        // Assert - Items should be List<OrderItemFacet>
        dto.Items.Should().BeAssignableTo<List<OrderItemFacet>>();
    }
}
