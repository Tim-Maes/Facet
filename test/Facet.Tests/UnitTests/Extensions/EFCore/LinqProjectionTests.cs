using Facet.Tests.TestModels;
using Facet.Tests.Utilities;
using Microsoft.EntityFrameworkCore;

namespace Facet.Tests.UnitTests.Extensions.EFCore;

public class LinqProjectionTests : IDisposable
{
    private readonly DbContext _context;

    public LinqProjectionTests()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new TestDbContext(options);
        SeedTestData();
    }

    [Fact]
    public void Select_ShouldProjectToFacet_InLinqQueries()
    {
        // Arrange & Act
        var userDtos = _context.Set<User>()
            .Select(u => u.ToFacet<User, UserDto>())
            .ToList();

        // Assert
        userDtos.Should().NotBeEmpty();
        userDtos.Should().HaveCount(3);
        userDtos.All(dto => !string.IsNullOrEmpty(dto.FirstName)).Should().BeTrue();
        userDtos.All(dto => !string.IsNullOrEmpty(dto.Email)).Should().BeTrue();
        
        var dtoType = userDtos.First().GetType();
        dtoType.GetProperty("Password").Should().BeNull();
        dtoType.GetProperty("CreatedAt").Should().BeNull();
    }

    [Fact]
    public void Where_ThenSelect_ShouldFilterAndProjectToFacet()
    {
        // Arrange & Act
        var activeDtos = _context.Set<User>()
            .Where(u => u.IsActive)
            .Select(u => u.ToFacet<User, UserDto>())
            .ToList();

        // Assert
        activeDtos.Should().HaveCount(2);
        activeDtos.All(dto => dto.IsActive).Should().BeTrue();
        activeDtos.Select(dto => dto.FirstName).Should().BeEquivalentTo(new[] { "Alice", "Bob" });
    }

    [Fact]
    public void OrderBy_ThenSelect_ShouldOrderAndProjectToFacet()
    {
        // Arrange & Act
        var orderedDtos = _context.Set<User>()
            .OrderBy(u => u.FirstName)
            .Select(u => u.ToFacet<User, UserDto>())
            .ToList();

        // Assert
        orderedDtos.Should().HaveCount(3);
        orderedDtos[0].FirstName.Should().Be("Alice");
        orderedDtos[1].FirstName.Should().Be("Bob");
        orderedDtos[2].FirstName.Should().Be("Charlie");
    }

    [Fact]
    public void Take_ThenSelect_ShouldLimitAndProjectToFacet()
    {
        // Arrange & Act
        var limitedDtos = _context.Set<User>()
            .OrderBy(u => u.Id)
            .Take(2)
            .Select(u => u.ToFacet<User, UserDto>())
            .ToList();

        // Assert
        limitedDtos.Should().HaveCount(2);
    }

    [Fact]
    public void GroupBy_ThenSelect_ShouldGroupAndProjectToFacet()
    {
        // Arrange & Act
        var groupedResults = _context.Set<User>()
            .GroupBy(u => u.IsActive)
            .Select(g => new
            {
                IsActive = g.Key,
                Users = g.Select(u => u.ToFacet<User, UserDto>()).ToList(),
                Count = g.Count()
            })
            .ToList();

        // Assert
        groupedResults.Should().HaveCount(2);
        
        var activeGroup = groupedResults.First(g => g.IsActive);
        var inactiveGroup = groupedResults.First(g => !g.IsActive);
        
        activeGroup.Count.Should().Be(2);
        activeGroup.Users.Should().HaveCount(2);
        activeGroup.Users.All(u => u.IsActive).Should().BeTrue();
        
        inactiveGroup.Count.Should().Be(1);
        inactiveGroup.Users.Should().HaveCount(1);
        inactiveGroup.Users.All(u => !u.IsActive).Should().BeTrue();
    }

    [Fact]
    public void Join_ThenSelect_ShouldJoinAndProjectToFacet()
    {
        // Arrange & Act
        var joinResults = _context.Set<User>()
            .Join(_context.Set<Product>(),
                user => user.Id,
                product => product.CategoryId,
                (user, product) => new
                {
                    User = user.ToFacet<User, UserDto>(),
                    Product = product.ToFacet<Product, ProductDto>()
                })
            .ToList();

        // Assert
        joinResults.Should().NotBeEmpty();
        joinResults.All(r => r.User != null).Should().BeTrue();
        joinResults.All(r => r.Product != null).Should().BeTrue();
        joinResults.All(r => r.User.GetType().GetProperty("Password") == null).Should().BeTrue();
        joinResults.All(r => r.Product.GetType().GetProperty("InternalNotes") == null).Should().BeTrue();
    }

    [Fact]
    public void ComplexQuery_WithMultipleOperations_ShouldProjectToFacetCorrectly()
    {
        // Arrange & Act
        var complexResults = _context.Set<User>()
            .Where(u => u.Email.Contains("@"))
            .OrderByDescending(u => u.DateOfBirth)
            .Skip(1)
            .Take(1)
            .Select(u => u.ToFacet<User, UserDto>())
            .ToList();

        // Assert
        complexResults.Should().HaveCount(1);
        complexResults.First().Email.Should().Contain("@");
    }

    [Fact]
    public async Task SelectAsync_ShouldProjectToFacet_InAsyncQueries()
    {
        // Arrange & Act
        var userDtos = await _context.Set<User>()
            .Select(u => u.ToFacet<User, UserDto>())
            .ToListAsync();

        // Assert
        userDtos.Should().NotBeEmpty();
        userDtos.Should().HaveCount(3);
        userDtos.All(dto => !string.IsNullOrEmpty(dto.FirstName)).Should().BeTrue();
    }

    [Fact]
    public async Task FirstOrDefaultAsync_ShouldProjectToFacet()
    {
        // Arrange & Act
        var firstDto = await _context.Set<User>()
            .Where(u => u.FirstName == "Alice")
            .Select(u => u.ToFacet<User, UserDto>())
            .FirstOrDefaultAsync();

        // Assert
        firstDto.Should().NotBeNull();
        firstDto!.FirstName.Should().Be("Alice");
        firstDto.GetType().GetProperty("Password").Should().BeNull();
    }

    private void SeedTestData()
    {
        var baseId = Random.Shared.Next(1000, 9999);
        var users = new List<User>
        {
            TestDataFactory.CreateUser("Alice", "Johnson", "alice.johnson@example.com", new DateTime(1985, 3, 22), true),
            TestDataFactory.CreateUser("Bob", "Smith", "bob.smith@example.com", new DateTime(1992, 8, 10), true),
            TestDataFactory.CreateUser("Charlie", "Brown", "charlie.brown@example.com", new DateTime(1988, 12, 5), false)
        };
        
        for (int i = 0; i < users.Count; i++)
        {
            users[i].Id = baseId + i;
        }
        
        _context.Set<User>().AddRange(users);

        var products = new List<Product>
        {
            TestDataFactory.CreateProduct("Product 1"),
            TestDataFactory.CreateProduct("Product 2"),
            TestDataFactory.CreateProduct("Product 3")
        };
        
        // Ensure unique product IDs and link to users
        for (int i = 0; i < products.Count && i < users.Count; i++)
        {
            products[i].Id = baseId + 100 + i;
            products[i].CategoryId = users[i].Id;
        }
        
        _context.Set<Product>().AddRange(products);
        _context.SaveChanges();
    }

    [Fact]
    public void Projection_WithNestedFacets_ShouldLoadNavigationPropertiesWithoutInclude()
    {
        // Arrange
        var address = new AddressEntity
        {
            Street = "123 Main St",
            City = "Test City",
            State = "TS",
            ZipCode = "12345",
            Country = "Testland"
        };

        var company = new CompanyEntity
        {
            Id = 1,
            Name = "Test Company",
            Industry = "Technology",
            HeadquartersAddress = address
        };

        _context.Set<AddressEntity>().Add(address);
        _context.Set<CompanyEntity>().Add(company);
        _context.SaveChanges();

        // Clear the context to ensure we're not using cached entities
        _context.ChangeTracker.Clear();

        // Act - Use projection WITHOUT .Include()
        var companyDto = _context.Set<CompanyEntity>()
            .Where(c => c.Id == 1)
            .Select(CompanyFacet.Projection)
            .FirstOrDefault();

        // Assert
        companyDto.Should().NotBeNull();
        companyDto!.Id.Should().Be(1);
        companyDto.Name.Should().Be("Test Company");
        companyDto.Industry.Should().Be("Technology");

        // The nested facet should be loaded and mapped
        companyDto.HeadquartersAddress.Should().NotBeNull();
        companyDto.HeadquartersAddress.Street.Should().Be("123 Main St");
        companyDto.HeadquartersAddress.City.Should().Be("Test City");
        companyDto.HeadquartersAddress.State.Should().Be("TS");
        companyDto.HeadquartersAddress.ZipCode.Should().Be("12345");
        companyDto.HeadquartersAddress.Country.Should().Be("Testland");
    }

    [Fact]
    public void SelectFacet_WithNestedFacets_ShouldLoadNavigationPropertiesWithoutInclude()
    {
        // Arrange
        var address = new AddressEntity
        {
            Street = "789 SelectFacet Ave",
            City = "SelectFacet City",
            State = "SF",
            ZipCode = "99999",
            Country = "Selectland"
        };

        var company = new CompanyEntity
        {
            Id = 2,
            Name = "SelectFacet Company",
            Industry = "Software",
            HeadquartersAddress = address
        };

        _context.Set<AddressEntity>().Add(address);
        _context.Set<CompanyEntity>().Add(company);
        _context.SaveChanges();
        _context.ChangeTracker.Clear();

        // Act
        var companyDto = _context.Set<CompanyEntity>()
            .Where(c => c.Id == 2)
            .SelectFacet<CompanyEntity, CompanyFacet>()
            .FirstOrDefault();

        // Assert
        companyDto.Should().NotBeNull();
        companyDto!.Name.Should().Be("SelectFacet Company");

        // The nested facet should be loaded via SelectFacet
        companyDto.HeadquartersAddress.Should().NotBeNull();
        companyDto.HeadquartersAddress.Street.Should().Be("789 SelectFacet Ave");
        companyDto.HeadquartersAddress.City.Should().Be("SelectFacet City");
    }

    [Fact]
    public void SelectFacet_NonGeneric_WithNestedFacets_ShouldLoadNavigationPropertiesWithoutInclude()
    {
        // Arrange
        var address = new AddressEntity
        {
            Street = "456 NonGeneric St",
            City = "NonGeneric City",
            State = "NG",
            ZipCode = "88888",
            Country = "NGland"
        };

        var company = new CompanyEntity
        {
            Id = 3,
            Name = "NonGeneric Company",
            Industry = "Finance",
            HeadquartersAddress = address
        };

        _context.Set<AddressEntity>().Add(address);
        _context.Set<CompanyEntity>().Add(company);
        _context.SaveChanges();
        _context.ChangeTracker.Clear();

        // Act
        IQueryable nonTypedQuery = _context.Set<CompanyEntity>().Where(c => c.Id == 3);
        var companyDto = nonTypedQuery
            .SelectFacet<CompanyFacet>()
            .FirstOrDefault();

        // Assert
        companyDto.Should().NotBeNull();
        companyDto!.Name.Should().Be("NonGeneric Company");

        // The nested facet should be loaded
        companyDto.HeadquartersAddress.Should().NotBeNull();
        companyDto.HeadquartersAddress.Street.Should().Be("456 NonGeneric St");
        companyDto.HeadquartersAddress.City.Should().Be("NonGeneric City");
    }

    [Fact]
    public void Projection_WithCollectionNestedFacets_ShouldLoadCollectionWithoutInclude()
    {
        // Arrange
        var order = new OrderEntity
        {
            Id = 1,
            OrderNumber = "ORD-001",
            OrderDate = DateTime.UtcNow,
            Items = new List<OrderItemEntity>
            {
                new OrderItemEntity { Id = 1, ProductName = "Item 1", Price = 10.00m, Quantity = 2 },
                new OrderItemEntity { Id = 2, ProductName = "Item 2", Price = 20.00m, Quantity = 1 }
            },
            ShippingAddress = new AddressEntity
            {
                Street = "456 Shipping Rd",
                City = "Ship City",
                State = "SC",
                ZipCode = "67890",
                Country = "Shipland"
            }
        };

        _context.Set<OrderEntity>().Add(order);
        _context.SaveChanges();
        _context.ChangeTracker.Clear();

        // Act
        var orderDto = _context.Set<OrderEntity>()
            .Where(o => o.Id == 1)
            .Select(OrderFacet.Projection)
            .FirstOrDefault();

        // Assert
        orderDto.Should().NotBeNull();
        orderDto!.Id.Should().Be(1);
        orderDto.OrderNumber.Should().Be("ORD-001");

        // Collection nested facets should be loaded
        orderDto.Items.Should().NotBeNull();
        orderDto.Items.Should().HaveCount(2);
        orderDto.Items.Should().Contain(i => i.ProductName == "Item 1");
        orderDto.Items.Should().Contain(i => i.ProductName == "Item 2");

        // Single nested facet should also be loaded
        orderDto.ShippingAddress.Should().NotBeNull();
        orderDto.ShippingAddress.City.Should().Be("Ship City");
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}

public class TestDbContext : DbContext
{
    public TestDbContext(DbContextOptions<TestDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>().HasKey(u => u.Id);
        modelBuilder.Entity<Product>().HasKey(p => p.Id);
        modelBuilder.Entity<Employee>().HasBaseType<User>();
        modelBuilder.Entity<Manager>().HasBaseType<Employee>();

        // Add entities for nested facet tests
        modelBuilder.Entity<AddressEntity>().HasKey(a => new { a.Street, a.City, a.State });
        modelBuilder.Entity<CompanyEntity>().HasKey(c => c.Id);
        modelBuilder.Entity<OrderEntity>().HasKey(o => o.Id);
        modelBuilder.Entity<OrderItemEntity>().HasKey(i => i.Id);
    }
}