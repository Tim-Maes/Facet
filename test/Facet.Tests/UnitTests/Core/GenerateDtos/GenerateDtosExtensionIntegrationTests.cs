using Facet.Extensions;
using Facet.Tests.TestModels;
using System.Reflection;

namespace Facet.Tests.UnitTests.Core.GenerateDtos;

/// <summary>
/// Tests for integration between GenerateDtos and Facet extension methods.
/// Verifies that generated DTOs work seamlessly with ToFacet, SelectFacets, etc.
/// </summary>
public class GenerateDtosExtensionIntegrationTests
{
    [Fact]
    public void GeneratedResponseDto_ShouldWork_WithToFacetExtension()
    {
        // Arrange
        var user = CreateTestUser();

        // Act
        var responseDto = user.ToFacet<TestUserResponse>();

        // Assert
        responseDto.Should().NotBeNull();
        responseDto.Id.Should().Be(user.Id);
        responseDto.FirstName.Should().Be(user.FirstName);
        responseDto.LastName.Should().Be(user.LastName);
        responseDto.Email.Should().Be(user.Email);
        responseDto.IsActive.Should().Be(user.IsActive);
        responseDto.DateOfBirth.Should().Be(user.DateOfBirth);
        responseDto.LastLoginAt.Should().Be(user.LastLoginAt);
        responseDto.CreatedAt.Should().Be(user.CreatedAt);
    }

    [Fact]
    public void GeneratedResponseDto_ShouldWork_WithSelectFacetsExtension()
    {
        // Arrange
        var users = new List<TestUser>
        {
            CreateTestUser(1, "Alice", "Johnson", "alice@test.com"),
            CreateTestUser(2, "Bob", "Smith", "bob@test.com"),
            CreateTestUser(3, "Charlie", "Brown", "charlie@test.com")
        };

        // Act
        var responseDtos = users.SelectFacets<TestUserResponse>().ToList();

        // Assert
        responseDtos.Should().HaveCount(3);
        responseDtos[0].FirstName.Should().Be("Alice");
        responseDtos[0].LastName.Should().Be("Johnson");
        responseDtos[1].FirstName.Should().Be("Bob");
        responseDtos[1].LastName.Should().Be("Smith");
        responseDtos[2].FirstName.Should().Be("Charlie");
        responseDtos[2].LastName.Should().Be("Brown");
    }

    [Fact]
    public void GeneratedResponseDto_ShouldWork_WithTypedSelectFacets()
    {
        // Arrange
        var users = new List<TestUser>
        {
            CreateTestUser(1, "Test1", "User1"),
            CreateTestUser(2, "Test2", "User2")
        };

        // Act - Using typed version for better performance
        var responseDtos = users.SelectFacets<TestUser, TestUserResponse>().ToList();

        // Assert
        responseDtos.Should().HaveCount(2);
        responseDtos[0].FirstName.Should().Be("Test1");
        responseDtos[1].FirstName.Should().Be("Test2");
    }

    [Fact]
    public void GeneratedAuditableDto_ShouldWork_WithFacetExtensions()
    {
        // Arrange
        var product = CreateTestProduct();

        // Act - Test with auditable DTO (should exclude audit fields)
        var responseDto = product.ToFacet<TestProductResponse>();

        // Assert
        responseDto.Should().NotBeNull();
        responseDto.Id.Should().Be(product.Id);
        responseDto.Name.Should().Be(product.Name);
        responseDto.Description.Should().Be(product.Description);
        responseDto.Price.Should().Be(product.Price);
        responseDto.IsAvailable.Should().Be(product.IsAvailable);
        
        // Verify audit fields are not accessible (they should be excluded)
        var responseType = typeof(TestProductResponse);
        responseType.GetProperty("CreatedAt").Should().BeNull("Audit fields should be excluded");
        responseType.GetProperty("CreatedBy").Should().BeNull("Audit fields should be excluded");
        responseType.GetProperty("UpdatedAt").Should().BeNull("Audit fields should be excluded");
        responseType.GetProperty("UpdatedBy").Should().BeNull("Audit fields should be excluded");
    }

    [Fact]
    public void GeneratedDtos_ShouldWork_InLinqQueryScenarios()
    {
        // Arrange
        var users = new List<TestUser>
        {
            CreateTestUser(1, "Active", "User1", email: "active1@test.com", isActive: true),
            CreateTestUser(2, "Inactive", "User2", email: "inactive2@test.com", isActive: false),
            CreateTestUser(3, "Active", "User3", email: "active3@test.com", isActive: true)
        };

        // Act - Complex LINQ scenario with generated DTOs
        var activeUserDtos = users
            .Where(u => u.IsActive)
            .SelectFacets<TestUserResponse>()
            .Where(dto => dto.FirstName.StartsWith("Active"))
            .OrderBy(dto => dto.LastName)
            .ToList();

        // Assert
        activeUserDtos.Should().HaveCount(2);
        activeUserDtos[0].LastName.Should().Be("User1");
        activeUserDtos[1].LastName.Should().Be("User3");
        activeUserDtos.All(dto => dto.IsActive).Should().BeTrue();
    }

    [Fact]
    public void GeneratedDtos_ShouldWork_WithProjectionProperty()
    {
        // Arrange
        var users = new List<TestUser>
        {
            CreateTestUser(1, "Projection", "Test1"),
            CreateTestUser(2, "Projection", "Test2")
        }.AsQueryable();

        // Act - Using static Projection property
        var results = users.Select(TestUserResponse.Projection).ToList();

        // Assert
        results.Should().HaveCount(2);
        results.Should().AllBeOfType<TestUserResponse>();
        results[0].FirstName.Should().Be("Projection");
        results[0].LastName.Should().Be("Test1");
        results[1].FirstName.Should().Be("Projection");
        results[1].LastName.Should().Be("Test2");
    }

    [Fact]
    public void GeneratedDtos_ShouldWork_WithAsyncExtensions()
    {
        // Arrange
        var users = new List<TestUser>
        {
            CreateTestUser(1, "Async", "Test1"),
            CreateTestUser(2, "Async", "Test2")
        };

        // Act - Simulate async operation
        var task = Task.FromResult(users.SelectFacets<TestUserResponse>().ToList());
        var results = task.Result;

        // Assert
        results.Should().HaveCount(2);
        results[0].FirstName.Should().Be("Async");
        results[1].FirstName.Should().Be("Async");
    }

    [Fact]
    public void GeneratedDtos_ShouldMaintain_TypeSafety()
    {
        // Arrange
        var user = CreateTestUser();

        // Act & Assert - Compile-time type safety
        TestUserResponse responseDto = user.ToFacet<TestUserResponse>();
        responseDto.Should().NotBeNull();
        
        // Verify that we get strongly typed objects
        responseDto.Should().BeOfType<TestUserResponse>();
        
        // Properties should be accessible with IntelliSense
        string firstName = responseDto.FirstName;
        int id = responseDto.Id;
        bool isActive = responseDto.IsActive;
        
        firstName.Should().NotBeNull();
        id.Should().BeGreaterThanOrEqualTo(0);
    }

    #region Helper Methods

    private static TestUser CreateTestUser(
        int id = 1, 
        string firstName = "Test", 
        string lastName = "User", 
        string email = null,
        bool isActive = true)
    {
        return new TestUser
        {
            Id = id,
            FirstName = firstName,
            LastName = lastName,
            Email = email ?? $"{firstName.ToLower()}.{lastName.ToLower()}@test.com",
            Password = "testpassword",
            DateOfBirth = new DateTime(1990, 1, 1),
            IsActive = isActive,
            LastLoginAt = DateTime.Now.AddHours(-1),
            CreatedAt = DateTime.Now.AddDays(-10)
        };
    }

    private static TestProduct CreateTestProduct()
    {
        return new TestProduct
        {
            Id = 1,
            Name = "Integration Test Product",
            Description = "Product for extension integration testing",
            Price = 99.99m,
            IsAvailable = true,
            InternalNotes = "Internal notes that should be excluded",
            CreatedAt = DateTime.Now.AddDays(-5),
            UpdatedAt = DateTime.Now.AddHours(-2),
            CreatedBy = "testuser",
            UpdatedBy = "testadmin"
        };
    }

    #endregion
}