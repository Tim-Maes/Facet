using Facet.Extensions;
using Facet.Tests.TestModels;
using System.Reflection;

namespace Facet.Tests.UnitTests.Core.GenerateDtos;

/// <summary>
/// Tests to verify that DTOs generated with [GenerateDtos] work as proper Facets
/// and can use ToFacet, SelectFacets, BackTo, and other Facet extension methods.
/// </summary>
public class GenerateDtosFacetIntegrationTests
{
    [Fact]
    public void GeneratedDtos_ShouldExist()
    {
        // Check which DTOs were actually generated
        var assembly = Assembly.GetAssembly(typeof(TestUser));
        
        // Check for Response DTO (should exist)
        var responseType = assembly?.GetType("Facet.Tests.TestModels.TestUserResponse");
        responseType.Should().NotBeNull("TestUserResponse should be generated");
        
        // Check for other possible DTO types
        var createType = assembly?.GetType("Facet.Tests.TestModels.TestUserCreateRequest") ??
                        assembly?.GetType("Facet.Tests.TestModels.TestUserCreate");
        var updateType = assembly?.GetType("Facet.Tests.TestModels.TestUserUpdateRequest") ??
                        assembly?.GetType("Facet.Tests.TestModels.TestUserUpdate");
        var queryType = assembly?.GetType("Facet.Tests.TestModels.TestUserQuery");
        
        // At least response type should exist
        responseType.Should().NotBeNull();
    }

    [Fact]
    public void ToFacet_Should_Work_WithGeneratedResponseDto()
    {
        // Arrange
        var user = CreateTestUser();

        // Act & Assert - This should work since TestUserResponse exists
        var responseDto = user.ToFacet<TestUserResponse>();
        
        responseDto.Should().NotBeNull();
        responseDto.Id.Should().Be(user.Id);
        responseDto.FirstName.Should().Be("John");
        responseDto.LastName.Should().Be("Doe");
        responseDto.Email.Should().Be("john@example.com");
        responseDto.IsActive.Should().Be(user.IsActive);
    }

    [Fact]
    public void ToFacet_Should_Work_WithAuditableDto()
    {
        // Arrange
        var product = CreateTestProduct();

        // Act & Assert
        var responseDto = product.ToFacet<TestProductResponse>();
        
        responseDto.Should().NotBeNull();
        responseDto.Id.Should().Be(product.Id);
        responseDto.Name.Should().Be("Test Product");
        responseDto.Description.Should().Be("Test Description");
        responseDto.Price.Should().Be(99.99m);
        // Audit fields should be excluded
    }

    [Fact]
    public void SelectFacets_Should_Work_WithGeneratedDtos()
    {
        // Arrange
        var users = new List<TestUser>
        {
            CreateTestUser("Alice", "Smith"),
            CreateTestUser("Bob", "Johnson"),
            CreateTestUser("Carol", "Williams")
        };

        // Act & Assert
        var responseDtos = users.SelectFacets<TestUserResponse>().ToList();
        
        responseDtos.Should().HaveCount(3);
        responseDtos[0].FirstName.Should().Be("Alice");
        responseDtos[1].FirstName.Should().Be("Bob");
        responseDtos[2].FirstName.Should().Be("Carol");
    }

    [Fact]
    public void SelectFacets_Should_Work_WithQueryable()
    {
        // Arrange
        var users = new List<TestUser>
        {
            CreateTestUser("Alice", "Smith"),
            CreateTestUser("Bob", "Johnson")
        }.AsQueryable();

        // Act & Assert  
        var responseDtos = users.SelectFacet<TestUser, TestUserResponse>().ToList();
        
        responseDtos.Should().HaveCount(2);
        responseDtos[0].FirstName.Should().Be("Alice");
        responseDtos[1].FirstName.Should().Be("Bob");
    }

    [Fact]
    public void BackTo_Should_Work_WithGeneratedResponseDto()
    {
        // Arrange
        var user = CreateTestUser();
        var responseDto = user.ToFacet<TestUserResponse>();

        // Act & Assert
        var backToUser = responseDto.BackTo<TestUser>();
        
        backToUser.Should().NotBeNull();
        backToUser.Id.Should().Be(user.Id);
        backToUser.FirstName.Should().Be("John");
        backToUser.LastName.Should().Be("Doe");
        backToUser.Email.Should().Be("john@example.com");
        backToUser.IsActive.Should().Be(user.IsActive);
    }

    [Fact]
    public void BackTo_Should_Work_WithMultipleGeneratedDtos()
    {
        // Arrange
        var users = new List<TestUser>
        {
            CreateTestUser("Alice", "Smith"),
            CreateTestUser("Bob", "Johnson")
        };
        var responseDtos = users.SelectFacets<TestUserResponse>().ToList();

        // Act & Assert
        var backToUsers = responseDtos.BackTo<TestUserResponse, TestUser>().ToList();
        
        backToUsers.Should().HaveCount(2);
        backToUsers[0].FirstName.Should().Be("Alice");
        backToUsers[1].FirstName.Should().Be("Bob");
    }

    [Fact]
    public void Projection_Should_Be_Available_OnGeneratedDtos()
    {
        // Arrange
        var users = new List<TestUser>
        {
            CreateTestUser("Alice", "Smith"),
            CreateTestUser("Bob", "Johnson")
        }.AsQueryable();

        // Act & Assert - This tests that the Projection property exists and works
        var projectionExpression = TestUserResponse.Projection;
        projectionExpression.Should().NotBeNull();

        var projectedResults = users.Select(projectionExpression).ToList();
        projectedResults.Should().HaveCount(2);
        projectedResults[0].FirstName.Should().Be("Alice");
        projectedResults[1].FirstName.Should().Be("Bob");
    }

    [Fact]
    public void Projection_Should_Work_WithLinqSelect()
    {
        // Arrange
        var users = new List<TestUser>
        {
            CreateTestUser("Test", "User")
        }.AsQueryable();

        // Act & Assert
        var results = users.Select(TestUserResponse.Projection).ToList();
        
        results.Should().HaveCount(1);
        results[0].FirstName.Should().Be("Test");
        results[0].LastName.Should().Be("User");
    }

    [Fact]
    public void Constructor_Should_Work_WithSourceType()
    {
        // Arrange
        var user = CreateTestUser();

        // Act & Assert
        var responseDto = new TestUserResponse(user);
        
        responseDto.Should().NotBeNull();
        responseDto.Id.Should().Be(user.Id);
        responseDto.FirstName.Should().Be("John");
        responseDto.LastName.Should().Be("Doe");
        responseDto.Email.Should().Be("john@example.com");
    }

    [Fact]
    public void ParameterlessConstructor_Should_Work()
    {
        // Act & Assert
        var responseDto = new TestUserResponse();
        
        responseDto.Should().NotBeNull();
        responseDto.Id.Should().Be(default(int));
        responseDto.FirstName.Should().BeNullOrEmpty();
    }

    [Fact]
    public void GeneratedDtos_Should_HaveFacetAttribute()
    {
        // Act & Assert - Verify the generated DTOs have the [Facet] attribute
        var responseType = typeof(TestUserResponse);
        var facetAttributes = responseType.GetCustomAttributes(typeof(FacetAttribute), false);
        
        facetAttributes.Should().NotBeEmpty("Generated DTOs should have [Facet] attribute");
        
        var facetAttribute = (FacetAttribute)facetAttributes[0];
        facetAttribute.SourceType.Should().Be(typeof(TestUser));
    }

    #region Helper Methods

    private static TestUser CreateTestUser(string firstName = "John", string lastName = "Doe")
    {
        return new TestUser
        {
            Id = 1,
            FirstName = firstName,
            LastName = lastName,
            Email = $"{firstName.ToLower()}@example.com",
            Password = "secret123",
            DateOfBirth = new DateTime(1990, 1, 1),
            IsActive = true,
            LastLoginAt = DateTime.Now.AddHours(-2),
            CreatedAt = DateTime.Now.AddDays(-30)
        };
    }

    private static TestProduct CreateTestProduct()
    {
        return new TestProduct
        {
            Id = 1,
            Name = "Test Product",
            Description = "Test Description", 
            Price = 99.99m,
            IsAvailable = true,
            InternalNotes = "Secret notes",
            CreatedAt = DateTime.Now.AddDays(-10),
            UpdatedAt = DateTime.Now.AddHours(-1),
            CreatedBy = "admin",
            UpdatedBy = "admin"
        };
    }

    #endregion
}