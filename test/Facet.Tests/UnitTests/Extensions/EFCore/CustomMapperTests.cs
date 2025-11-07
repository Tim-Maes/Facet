using Facet.Extensions.EFCore.Mapping;
using Facet.Mapping;
using Facet.Tests.TestModels;
using Facet.Tests.Utilities;
using Microsoft.EntityFrameworkCore;

namespace Facet.Tests.UnitTests.Extensions.EFCore;

public class CustomMapperTests : IDisposable
{
    private readonly DbContext _context;

    public CustomMapperTests()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new TestDbContext(options);
        SeedTestData();
    }

    #region ToFacetsAsync with Custom Mapper Tests

    [Fact]
    public async Task ToFacetsAsync_WithInstanceMapper_ShouldApplyCustomMapping()
    {
        // Arrange
        var mapper = new TestUserDtoAsyncMapper();

        // Act
        var users = await _context.Set<User>()
            .Where(u => u.IsActive)
            .ToFacetsAsync<User, TestUserDto>(mapper);

        // Assert
        users.Should().NotBeNull();
        users.Should().HaveCount(2);
        users.All(u => u.FullName.Contains(" ")).Should().BeTrue();
        users.All(u => u.FullName.EndsWith(" [Custom]")).Should().BeTrue();
    }

    [Fact]
    public async Task ToFacetsAsync_WithStaticMapper_ShouldApplyCustomMapping()
    {
        // Arrange & Act
        var users = await _context.Set<User>()
            .Where(u => u.IsActive)
            .ToFacetsAsync<User, TestUserDto, TestUserDtoStaticMapper>();

        // Assert
        users.Should().NotBeNull();
        users.Should().HaveCount(2);
        users.All(u => u.FullName.Contains(" ")).Should().BeTrue();
        users.All(u => u.FullName.EndsWith(" [Static]")).Should().BeTrue();
    }

    [Fact]
    public async Task ToFacetsAsync_WithInstanceMapper_ShouldAutoMapPropertiesFirst()
    {
        // Arrange
        var mapper = new TestUserDtoAsyncMapper();

        // Act
        var users = await _context.Set<User>()
            .ToFacetsAsync<User, TestUserDto>(mapper);

        // Assert
        users.Should().HaveCount(3);
        users.All(u => !string.IsNullOrEmpty(u.FirstName)).Should().BeTrue();
        users.All(u => !string.IsNullOrEmpty(u.Email)).Should().BeTrue();
        users.All(u => u.Id > 0).Should().BeTrue();
    }

    #endregion

    #region FirstFacetAsync with Custom Mapper Tests

    [Fact]
    public async Task FirstFacetAsync_WithInstanceMapper_ShouldApplyCustomMapping()
    {
        // Arrange
        var mapper = new TestUserDtoAsyncMapper();

        // Act
        var user = await _context.Set<User>()
            .Where(u => u.FirstName == "Alice")
            .FirstFacetAsync<User, TestUserDto>(mapper);

        // Assert
        user.Should().NotBeNull();
        user!.FirstName.Should().Be("Alice");
        user.FullName.Should().Be("Alice Johnson [Custom]");
    }

    [Fact]
    public async Task FirstFacetAsync_WithStaticMapper_ShouldApplyCustomMapping()
    {
        // Arrange & Act
        var user = await _context.Set<User>()
            .Where(u => u.FirstName == "Bob")
            .FirstFacetAsync<User, TestUserDto, TestUserDtoStaticMapper>();

        // Assert
        user.Should().NotBeNull();
        user!.FirstName.Should().Be("Bob");
        user.FullName.Should().Be("Bob Smith [Static]");
    }

    [Fact]
    public async Task FirstFacetAsync_WithInstanceMapper_WhenNoMatch_ShouldReturnNull()
    {
        // Arrange
        var mapper = new TestUserDtoAsyncMapper();

        // Act
        var user = await _context.Set<User>()
            .Where(u => u.FirstName == "NonExistent")
            .FirstFacetAsync<User, TestUserDto>(mapper);

        // Assert
        user.Should().BeNull();
    }

    [Fact]
    public async Task FirstFacetAsync_WithStaticMapper_WhenNoMatch_ShouldReturnNull()
    {
        // Arrange & Act
        var user = await _context.Set<User>()
            .Where(u => u.FirstName == "NonExistent")
            .FirstFacetAsync<User, TestUserDto, TestUserDtoStaticMapper>();

        // Assert
        user.Should().BeNull();
    }

    #endregion

    #region SingleFacetAsync with Custom Mapper Tests

    [Fact]
    public async Task SingleFacetAsync_WithInstanceMapper_ShouldApplyCustomMapping()
    {
        // Arrange
        var mapper = new TestUserDtoAsyncMapper();

        // Act
        var user = await _context.Set<User>()
            .Where(u => u.FirstName == "Charlie")
            .SingleFacetAsync<User, TestUserDto>(mapper);

        // Assert
        user.Should().NotBeNull();
        user.FirstName.Should().Be("Charlie");
        user.FullName.Should().Be("Charlie Brown [Custom]");
    }

    [Fact]
    public async Task SingleFacetAsync_WithStaticMapper_ShouldApplyCustomMapping()
    {
        // Arrange & Act
        var user = await _context.Set<User>()
            .Where(u => u.FirstName == "Alice")
            .SingleFacetAsync<User, TestUserDto, TestUserDtoStaticMapper>();

        // Assert
        user.Should().NotBeNull();
        user.FirstName.Should().Be("Alice");
        user.FullName.Should().Be("Alice Johnson [Static]");
    }

    [Fact]
    public async Task SingleFacetAsync_WithInstanceMapper_WhenMultipleMatches_ShouldThrow()
    {
        // Arrange
        var mapper = new TestUserDtoAsyncMapper();

        // Act
        var act = async () => await _context.Set<User>()
            .Where(u => u.IsActive)
            .SingleFacetAsync<User, TestUserDto>(mapper);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    #endregion

    #region Null Argument Tests

    [Fact]
    public async Task ToFacetsAsync_WithNullMapper_ShouldThrowArgumentNullException()
    {
        // Arrange
        IFacetMapConfigurationAsyncInstance<User, TestUserDto> mapper = null!;

        // Act
        var act = async () => await _context.Set<User>()
            .ToFacetsAsync<User, TestUserDto>(mapper);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithMessage("*mapper*");
    }

    [Fact]
    public async Task FirstFacetAsync_WithNullMapper_ShouldThrowArgumentNullException()
    {
        // Arrange
        IFacetMapConfigurationAsyncInstance<User, TestUserDto> mapper = null!;

        // Act
        var act = async () => await _context.Set<User>()
            .FirstFacetAsync<User, TestUserDto>(mapper);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithMessage("*mapper*");
    }

    [Fact]
    public async Task SingleFacetAsync_WithNullMapper_ShouldThrowArgumentNullException()
    {
        // Arrange
        IFacetMapConfigurationAsyncInstance<User, TestUserDto> mapper = null!;

        // Act
        var act = async () => await _context.Set<User>()
            .SingleFacetAsync<User, TestUserDto>(mapper);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithMessage("*mapper*");
    }

    #endregion

    #region Cancellation Token Tests

    [Fact]
    public async Task ToFacetsAsync_WithInstanceMapper_ShouldRespectCancellationToken()
    {
        // Arrange
        var mapper = new TestUserDtoAsyncMapper();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var act = async () => await _context.Set<User>()
            .ToFacetsAsync<User, TestUserDto>(mapper, cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task FirstFacetAsync_WithInstanceMapper_ShouldRespectCancellationToken()
    {
        // Arrange
        var mapper = new TestUserDtoAsyncMapper();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var act = async () => await _context.Set<User>()
            .FirstFacetAsync<User, TestUserDto>(mapper, cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    #endregion

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

// Test DTO
[Facet(typeof(User), "Password", "CreatedAt")]
public partial class TestUserDto
{
    public string FullName { get; set; } = string.Empty;
}

// Instance mapper (supports DI)
public class TestUserDtoAsyncMapper : IFacetMapConfigurationAsyncInstance<User, TestUserDto>
{
    public Task MapAsync(User source, TestUserDto target, CancellationToken cancellationToken = default)
    {
        target.FullName = $"{source.FirstName} {source.LastName} [Custom]";
        return Task.CompletedTask;
    }
}

// Static mapper
public class TestUserDtoStaticMapper : IFacetMapConfigurationAsync<User, TestUserDto>
{
    public static Task MapAsync(User source, TestUserDto target, CancellationToken cancellationToken = default)
    {
        target.FullName = $"{source.FirstName} {source.LastName} [Static]";
        return Task.CompletedTask;
    }
}
