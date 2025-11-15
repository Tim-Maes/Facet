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

    [Fact]
    public void BackTo_Should_Work_WithGeneratedDtos()
    {
        // Arrange - Create a DTO with some values
        var responseDto = new TestUserResponse
        {
            Id = 42,
            FirstName = "Jane",
            LastName = "Smith",
            Email = "jane@example.com",
            IsActive = true
        };

        // Act
        var user = responseDto.BackTo();

        // Assert - Verify all properties are mapped correctly
        user.Should().NotBeNull();
        user.Id.Should().Be(42);
        user.FirstName.Should().Be("Jane");
        user.LastName.Should().Be("Smith");
        user.Email.Should().Be("jane@example.com");
        user.IsActive.Should().BeTrue();
    }

    [Fact]
    public void BackTo_Extension_Should_Work_WithGeneratedDtos()
    {
        // Arrange
        var responseDto = new TestUserResponse
        {
            Id = 123,
            FirstName = "Bob",
            LastName = "Johnson",
            Email = "bob@example.com",
            IsActive = false
        };

        // Act
        var user = responseDto.BackTo<TestUserResponse, TestUser>();

        // Assert
        user.Should().NotBeNull();
        user.Id.Should().Be(123);
        user.FirstName.Should().Be("Bob");
        user.LastName.Should().Be("Johnson");
        user.Email.Should().Be("bob@example.com");
        user.IsActive.Should().BeFalse();
    }

    [Fact]
    public void BackTo_Should_Work_WithCreateRequest()
    {
        // Arrange - Create a Create DTO (which excludes Id)
        var assembly = Assembly.GetAssembly(typeof(TestUser));
        var createRequestType = assembly?.GetType("Facet.Tests.TestModels.CreateTestUserRequest");

        if (createRequestType != null)
        {
            var createRequest = Activator.CreateInstance(createRequestType);
            var firstNameProp = createRequestType.GetProperty("FirstName");
            var lastNameProp = createRequestType.GetProperty("LastName");
            var emailProp = createRequestType.GetProperty("Email");

            firstNameProp?.SetValue(createRequest, "Alice");
            lastNameProp?.SetValue(createRequest, "Williams");
            emailProp?.SetValue(createRequest, "alice@example.com");

            // Act 
            var backToMethod = createRequestType.GetMethod("BackTo");
            backToMethod.Should().NotBeNull("CreateTestUserRequest should have a BackTo method");

            var user = backToMethod!.Invoke(createRequest, null) as TestUser;

            // Assert
            user.Should().NotBeNull();
            user.FirstName.Should().Be("Alice");
            user.LastName.Should().Be("Williams");
            user.Email.Should().Be("alice@example.com");
            // Id should be default since it's excluded from Create DTOs
            user.Id.Should().Be(default(int));
        }
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