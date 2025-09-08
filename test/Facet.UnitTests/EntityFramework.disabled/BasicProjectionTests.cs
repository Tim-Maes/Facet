using System;
using System.Linq;
using System.Threading.Tasks;
using Facet.Extensions.EFCore;
using Facet.UnitTests.TestFixtures;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Xunit.Abstractions;

namespace Facet.UnitTests.EntityFramework;

/// <summary>
/// Tests for basic Entity Framework projection functionality using ToFacetsAsync and related methods.
/// Uses TestDbContextFixture for database test isolation and proper cleanup.
/// </summary>
public class BasicProjectionTests : IClassFixture<TestDbContextFixture>
{
    private readonly TestDbContextFixture _fixture;
    private readonly ITestOutputHelper _output;

    public BasicProjectionTests(TestDbContextFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    [Fact]
    public async Task ToFacetsAsync_WithActiveUsers_ReturnsFilteredDtos()
    {
        // Act
        var userDtos = await _fixture.Context.Users
            .Where(u => u.IsActive)
            .ToFacetsAsync<UserDto>();

        // Assert
        Assert.NotEmpty(userDtos);
        Assert.All(userDtos, dto => {
            Assert.NotEqual(0, dto.Id);
            Assert.NotEmpty(dto.FirstName);
            Assert.NotEmpty(dto.LastName);
            Assert.NotEmpty(dto.Email);
            Assert.True(dto.IsActive);
        });

        _output.WriteLine($"Retrieved {userDtos.Count} active user DTOs");
    }

    [Fact]
    public async Task ToFacetsAsync_WithProductFilter_ReturnsCorrectData()
    {
        // Act
        var productDtos = await _fixture.Context.Products
            .Where(p => p.IsAvailable && p.Price > 50)
            .ToFacetsAsync<ProductDto>();

        // Assert
        Assert.NotEmpty(productDtos);
        Assert.All(productDtos, dto => {
            Assert.NotEqual(0, dto.Id);
            Assert.NotEmpty(dto.Name);
            Assert.True(dto.IsAvailable);
            Assert.True(dto.Price > 50);
        });

        _output.WriteLine($"Retrieved {productDtos.Count} available products over $50");
    }

    [Fact]
    public async Task FirstFacetAsync_WithExistingUser_ReturnsCorrectDto()
    {
        // Act
        var userDto = await _fixture.Context.Users
            .Where(u => u.Email.Contains("john"))
            .FirstFacetAsync<UserDto>();

        // Assert
        Assert.NotNull(userDto);
        Assert.Contains("john", userDto.Email, StringComparison.OrdinalIgnoreCase);
        Assert.NotEqual(0, userDto.Id);
        Assert.NotEmpty(userDto.FirstName);

        _output.WriteLine($"Found user: {userDto.FirstName} {userDto.LastName}");
    }

    [Fact]
    public async Task FirstFacetAsync_WithNonExistentUser_ReturnsNull()
    {
        // Act
        var userDto = await _fixture.Context.Users
            .Where(u => u.Email == "nonexistent@example.com")
            .FirstFacetAsync<UserDto>();

        // Assert
        Assert.Null(userDto);
        _output.WriteLine("Non-existent query returned null as expected");
    }

    [Fact]
    public async Task SingleFacetAsync_WithUniqueUser_ReturnsCorrectDto()
    {
        // Act
        var userDto = await _fixture.Context.Users
            .Where(u => u.Email == "john.doe@example.com")
            .SingleFacetAsync<UserDto>();

        // Assert
        Assert.NotNull(userDto);
        Assert.Equal("john.doe@example.com", userDto.Email);
        Assert.Equal("John", userDto.FirstName);
        Assert.Equal("Doe", userDto.LastName);

        _output.WriteLine($"Found unique user: {userDto.FirstName} {userDto.LastName}");
    }

    [Theory]
    [InlineData("john", true)]
    [InlineData("jane", true)]
    [InlineData("nonexistent", false)]
    public async Task FirstFacetAsync_WithEmailFilter_ReturnsExpectedResult(string emailFilter, bool shouldExist)
    {
        // Act
        var userDto = await _fixture.Context.Users
            .Where(u => u.Email.Contains(emailFilter))
            .FirstFacetAsync<UserDto>();

        // Assert
        if (shouldExist)
        {
            Assert.NotNull(userDto);
            Assert.Contains(emailFilter, userDto.Email, StringComparison.OrdinalIgnoreCase);
        }
        else
        {
            Assert.Null(userDto);
        }

        _output.WriteLine($"Email filter '{emailFilter}': {(userDto != null ? "Found" : "Not found")}");
    }

    [Fact]
    public async Task ToFacetsAsync_WithComplexQuery_ExecutesCorrectly()
    {
        // Act
        var complexQuery = await _fixture.Context.Users
            .Where(u => u.IsActive && u.DateOfBirth > new DateTime(1980, 1, 1))
            .OrderBy(u => u.LastName)
            .ThenBy(u => u.FirstName)
            .ToFacetsAsync<UserDto>();

        // Assert
        Assert.NotEmpty(complexQuery);
        Assert.All(complexQuery, dto => {
            Assert.True(dto.IsActive);
            Assert.True(dto.DateOfBirth > new DateTime(1980, 1, 1));
        });

        // Verify ordering
        if (complexQuery.Count > 1)
        {
            for (int i = 1; i < complexQuery.Count; i++)
            {
                var comparison = string.Compare(complexQuery[i-1].LastName, complexQuery[i].LastName, StringComparison.Ordinal);
                Assert.True(comparison <= 0, "Results should be ordered by LastName");
            }
        }

        _output.WriteLine($"Retrieved {complexQuery.Count} users with complex filtering and ordering");
    }

    [Fact]
    public async Task ToFacetsAsync_WithPagination_ReturnsCorrectSubset()
    {
        // Act
        var pagedResults = await _fixture.Context.Users
            .OrderBy(u => u.Id)
            .Skip(1)
            .Take(2)
            .ToFacetsAsync<UserDto>();

        // Assert
        Assert.NotEmpty(pagedResults);
        Assert.True(pagedResults.Count <= 2);
        Assert.All(pagedResults, dto => Assert.NotEqual(0, dto.Id));

        _output.WriteLine($"Retrieved page with {pagedResults.Count} users");
    }

    [Fact]
    public async Task ToFacetsAsync_WithDateRangeFilter_ReturnsMatchingUsers()
    {
        // Act
        var recentUsers = await _fixture.Context.Users
            .Where(u => u.CreatedAt > DateTime.UtcNow.AddDays(-120))
            .ToFacetsAsync<UserDto>();

        // Assert
        Assert.NotEmpty(recentUsers);
        Assert.All(recentUsers, dto => {
            Assert.NotEqual(0, dto.Id);
            Assert.NotEmpty(dto.FirstName);
        });

        _output.WriteLine($"Found {recentUsers.Count} users created in the last 120 days");
    }

    [Fact]
    public async Task ToFacetsAsync_WithNullableProperty_HandlesNullsCorrectly()
    {
        // Act - Test users with and without LastLoginAt
        var allUsers = await _fixture.Context.Users
            .ToFacetsAsync<UserDto>();

        var usersWithLogin = allUsers.Where(u => u.LastLoginAt.HasValue).ToList();
        var usersWithoutLogin = allUsers.Where(u => !u.LastLoginAt.HasValue).ToList();

        // Assert
        Assert.NotEmpty(allUsers);
        Assert.NotEmpty(usersWithLogin);
        Assert.NotEmpty(usersWithoutLogin);

        _output.WriteLine($"Total users: {allUsers.Count}, With login: {usersWithLogin.Count}, Without login: {usersWithoutLogin.Count}");
    }

    [Fact]
    public async Task ToFacetsAsync_WithDifferentFacetKinds_ExecutesCorrectly()
    {
        // Test different facet kinds work with EF projections
        
        // Act - Test record DTOs
        var productRecords = await _fixture.Context.Products
            .Where(p => p.IsAvailable)
            .ToFacetsAsync<ProductDto>();

        // Act - Test struct DTOs
        var productStructs = _fixture.Context.Products
            .Where(p => p.IsAvailable)
            .AsEnumerable()
            .Select(p => new ProductSummary(p))
            .ToList();

        // Assert
        Assert.NotEmpty(productRecords);
        Assert.NotEmpty(productStructs);
        Assert.Equal(productRecords.Count, productStructs.Count);

        _output.WriteLine($"Record DTOs: {productRecords.Count}, Struct DTOs: {productStructs.Count}");
    }
}