using Facet.Tests.TestModels;
using Facet.Tests.Utilities;
using Microsoft.EntityFrameworkCore;

namespace Facet.Tests.IntegrationTests;

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
        var users = TestDataFactory.CreateUsers();
        _context.Set<User>().AddRange(users);

        var products = new List<Product>
        {
            TestDataFactory.CreateProduct("Product 1"),
            TestDataFactory.CreateProduct("Product 2"),
            TestDataFactory.CreateProduct("Product 3")
        };
        
        for (int i = 0; i < products.Count && i < users.Count; i++)
        {
            products[i].CategoryId = users[i].Id;
        }
        
        _context.Set<Product>().AddRange(products);
        _context.SaveChanges();
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
    }
}