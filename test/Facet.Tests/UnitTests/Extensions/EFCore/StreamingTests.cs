using Facet.Tests.TestModels;
using Facet.Tests.Utilities;
using Microsoft.EntityFrameworkCore;

namespace Facet.Tests.UnitTests.Extensions.EFCore;

public class StreamingTests : IDisposable
{
    private readonly DbContext _context;

    public StreamingTests()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new TestDbContext(options);
        SeedTestData();
    }

    [Fact]
    public async Task AsAsyncEnumerable_WithSelectFacet_ShouldStreamResults()
    {
        // Arrange & Act
        var userDtos = new List<UserDto>();

        await foreach (var dto in _context.Set<User>()
            .Where(u => u.IsActive)
            .SelectFacet<User, UserDto>()
            .AsAsyncEnumerable())
        {
            userDtos.Add(dto);
        }

        // Assert
        userDtos.Should().HaveCount(2);
        userDtos.All(dto => dto.IsActive).Should().BeTrue();
        userDtos.Select(dto => dto.FirstName).Should().BeEquivalentTo(new[] { "Alice", "Bob" });
    }

    [Fact]
    public async Task AsAsyncEnumerable_WithNonGenericSelectFacet_ShouldStreamResults()
    {
        // Arrange & Act
        var userDtos = new List<UserDto>();

        await foreach (var dto in _context.Set<User>()
            .Where(u => u.FirstName == "Alice")
            .SelectFacet<UserDto>()
            .AsAsyncEnumerable())
        {
            userDtos.Add(dto);
        }

        // Assert
        userDtos.Should().HaveCount(1);
        userDtos.First().FirstName.Should().Be("Alice");
    }

    [Fact]
    public async Task AsAsyncEnumerable_WithComplexQuery_ShouldStreamResults()
    {
        // Arrange & Act
        var userDtos = new List<UserDto>();

        await foreach (var dto in _context.Set<User>()
            .Where(u => u.Email.Contains("@"))
            .OrderBy(u => u.FirstName)
            .SelectFacet<User, UserDto>()
            .AsAsyncEnumerable())
        {
            userDtos.Add(dto);
        }

        // Assert
        userDtos.Should().HaveCount(3);
        userDtos[0].FirstName.Should().Be("Alice");
        userDtos[1].FirstName.Should().Be("Bob");
        userDtos[2].FirstName.Should().Be("Charlie");
    }

    [Fact]
    public async Task AsAsyncEnumerable_WithNestedFacets_ShouldStreamResults()
    {
        // Arrange
        var address = new AddressEntity
        {
            Street = "123 Stream St",
            City = "Stream City",
            State = "ST",
            ZipCode = "11111",
            Country = "Streamland"
        };

        var company = new CompanyEntity
        {
            Id = 100,
            Name = "Stream Company",
            Industry = "Streaming",
            HeadquartersAddress = address
        };

        _context.Set<AddressEntity>().Add(address);
        _context.Set<CompanyEntity>().Add(company);
        _context.SaveChanges();
        _context.ChangeTracker.Clear();

        // Act
        var companyDtos = new List<CompanyFacet>();

        await foreach (var dto in _context.Set<CompanyEntity>()
            .Where(c => c.Id == 100)
            .SelectFacet<CompanyEntity, CompanyFacet>()
            .AsAsyncEnumerable())
        {
            companyDtos.Add(dto);
        }

        // Assert
        companyDtos.Should().HaveCount(1);
        companyDtos.First().Name.Should().Be("Stream Company");
        companyDtos.First().HeadquartersAddress.Should().NotBeNull();
        companyDtos.First().HeadquartersAddress.City.Should().Be("Stream City");
    }

    [Fact]
    public async Task AsAsyncEnumerable_WithTake_ShouldStreamLimitedResults()
    {
        // Arrange & Act
        var userDtos = new List<UserDto>();
        var count = 0;

        await foreach (var dto in _context.Set<User>()
            .OrderBy(u => u.Id)
            .Take(2)
            .SelectFacet<User, UserDto>()
            .AsAsyncEnumerable())
        {
            userDtos.Add(dto);
            count++;
        }

        // Assert
        count.Should().Be(2);
        userDtos.Should().HaveCount(2);
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
        _context.SaveChanges();
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
