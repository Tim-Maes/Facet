using System;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Facet.Extensions.EFCore.Tests.Fixtures;
using Facet.Extensions.EFCore.Tests.TestData;
using Facet.Extensions.EFCore;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Xunit.Abstractions;

namespace Facet.Extensions.EFCore.Tests.Extensions;

[Facet(typeof(User))]
public record UserDto(
    int Id,
    string FirstName,
    string LastName,
    string Email,
    bool IsActive,
    DateTime CreatedAt
)
{
    public static Expression<Func<User, UserDto>> Projection =>
        user => new UserDto(
            user.Id,
            user.FirstName,
            user.LastName,
            user.Email,
            user.IsActive,
            user.CreatedAt
        );
}

[Facet(typeof(Product))]
public record ProductDto(
    int Id,
    string Name,
    string Description,
    decimal Price,
    int CategoryId,
    bool IsAvailable,
    DateTime CreatedAt
)
{
    public static Expression<Func<Product, ProductDto>> Projection =>
        product => new ProductDto(
            product.Id,
            product.Name,
            product.Description,
            product.Price,
            product.CategoryId,
            product.IsAvailable,
            product.CreatedAt
        );
}

public class AsyncProjectionExtensionsTests : IClassFixture<TestDbContextFixture>
{
    private readonly TestDbContextFixture _fixture;
    private readonly ITestOutputHelper _output;

    public AsyncProjectionExtensionsTests(TestDbContextFixture fixture, ITestOutputHelper output)
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
        Assert.All(userDtos, dto => Assert.True(dto.IsActive));
        _output.WriteLine($"Retrieved {userDtos.Count} active user DTOs");

        foreach (var dto in userDtos)
        {
            _output.WriteLine($"  - {dto.FirstName} {dto.LastName} ({dto.Email})");
            Assert.NotEqual(0, dto.Id);
            Assert.NotEmpty(dto.FirstName);
            Assert.NotEmpty(dto.LastName);
            Assert.NotEmpty(dto.Email);
        }
    }

    [Fact]
    public async Task FirstFacetAsync_WithEmailFilter_ReturnsMatchingDto()
    {
        // Act
        var userDto = await _fixture.Context.Users
            .Where(u => u.Email.Contains("john"))
            .FirstFacetAsync<UserDto>();

        // Assert
        Assert.NotNull(userDto);
        Assert.Contains("john", userDto.Email, StringComparison.OrdinalIgnoreCase);
        _output.WriteLine($"Found user: {userDto.FirstName} {userDto.LastName} ({userDto.Email})");
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
        _output.WriteLine("No user found with non-existent email - returned null as expected");
    }

    [Fact]
    public async Task SingleFacetAsync_WithUniqueEmail_ReturnsDto()
    {
        // Arrange - We know from seed data that john.doe@example.com should be unique
        var uniqueEmail = "john.doe@example.com";

        // Act
        var userDto = await _fixture.Context.Users
            .Where(u => u.Email == uniqueEmail)
            .SingleFacetAsync<UserDto>();

        // Assert
        Assert.NotNull(userDto);
        Assert.Equal(uniqueEmail, userDto.Email);
        _output.WriteLine($"Found unique user: {userDto.FirstName} {userDto.LastName}");
    }

    [Fact]
    public async Task SingleFacetAsync_WithNonExistentUser_ThrowsException()
    {
        // Act & Assert - SingleFacetAsync should throw when no element is found
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _fixture.Context.Users
                .Where(u => u.Email == "nonexistent@example.com")
                .SingleFacetAsync<UserDto>());

        _output.WriteLine("Single query with non-existent email threw exception as expected");
    }

    [Fact]
    public async Task ToFacetsAsync_WithProductFiltering_ReturnsAvailableProducts()
    {
        // Act
        var productDtos = await _fixture.Context.Products
            .Where(p => p.IsAvailable && p.Price > 50)
            .OrderBy(p => p.Name)
            .ToFacetsAsync<ProductDto>();

        // Assert
        Assert.NotEmpty(productDtos);
        Assert.All(productDtos, dto =>
        {
            Assert.True(dto.IsAvailable);
            Assert.True(dto.Price > 50);
        });

        _output.WriteLine($"Retrieved {productDtos.Count} available products over $50:");
        foreach (var dto in productDtos)
        {
            _output.WriteLine($"  - {dto.Name}: ${dto.Price}");
        }
    }

    [Fact]
    public async Task ToFacetsAsync_WithPagination_ReturnsPagedResults()
    {
        // Act
        var pagedUsers = await _fixture.Context.Users
            .OrderBy(u => u.LastName)
            .ThenBy(u => u.FirstName)
            .Skip(0)
            .Take(1)
            .ToFacetsAsync<UserDto>();

        // Assert
        Assert.Single(pagedUsers);
        _output.WriteLine($"Retrieved page 1 with {pagedUsers.Count} users:");
        foreach (var dto in pagedUsers)
        {
            _output.WriteLine($"  - {dto.FirstName} {dto.LastName}");
        }
    }

    [Fact]
    public async Task ToFacetsAsync_WithComplexQuery_ReturnsCorrectResults()
    {
        // Act - Test complex LINQ query with multiple conditions
        var complexQuery = await _fixture.Context.Users
            .Where(u => u.IsActive && u.CreatedAt < DateTime.UtcNow)
            .OrderBy(u => u.Email)
            .Select(u => new { u.Id, u.Email, u.IsActive })
            .ToListAsync();

        // Also test the Facet projection works with similar complexity
        var facetResults = await _fixture.Context.Users
            .Where(u => u.IsActive && u.CreatedAt < DateTime.UtcNow)
            .OrderBy(u => u.Email)
            .ToFacetsAsync<UserDto>();

        // Assert
        Assert.Equal(complexQuery.Count, facetResults.Count);
        Assert.All(facetResults, dto => Assert.True(dto.IsActive));

        _output.WriteLine("Complex query results match between manual projection and Facet projection:");
        for (int i = 0; i < Math.Min(complexQuery.Count, facetResults.Count); i++)
        {
            Assert.Equal(complexQuery[i].Id, facetResults[i].Id);
            Assert.Equal(complexQuery[i].Email, facetResults[i].Email);
            Assert.Equal(complexQuery[i].IsActive, facetResults[i].IsActive);
            _output.WriteLine($"  ✓ {facetResults[i].Email}");
        }
    }

    [Fact]
    public async Task ToFacetsAsync_EmptyResults_ReturnsEmptyList()
    {
        // Act - Query that should return no results
        var emptyResults = await _fixture.Context.Users
            .Where(u => u.Email.StartsWith("impossible_prefix_that_wont_match"))
            .ToFacetsAsync<UserDto>();

        // Assert
        Assert.Empty(emptyResults);
        _output.WriteLine("Empty query returned empty list as expected");
    }
}