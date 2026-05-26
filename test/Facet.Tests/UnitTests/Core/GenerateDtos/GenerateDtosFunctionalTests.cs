using Facet.Tests.TestModels;
using System.Reflection;

namespace Facet.Tests.UnitTests.Core.GenerateDtos;

/// <summary>
/// Tests for functional usage of generated DTOs with real data scenarios.
/// Verifies that generated DTOs work correctly in typical application patterns.
/// </summary>
public class GenerateDtosFunctionalTests
{
    [Fact]
    public void CreateDto_ShouldWork_ForUserCreationScenario()
    {
        var assembly = Assembly.GetAssembly(typeof(TestUser));
        var createType = assembly?.GetType("Facet.Tests.TestModels.CreateTestUserRequest");
        
        createType.Should().NotBeNull();
        var createDto = Activator.CreateInstance(createType!);

        var firstNameProp = createType.GetProperty("FirstName")!;
        var lastNameProp = createType.GetProperty("LastName")!;
        var emailProp = createType.GetProperty("Email")!;
        var passwordProp = createType.GetProperty("Password")!;
        var isActiveProp = createType.GetProperty("IsActive")!;

        firstNameProp.SetValue(createDto, "Jane");
        lastNameProp.SetValue(createDto, "Doe");
        emailProp.SetValue(createDto, "jane.doe@example.com");
        passwordProp.SetValue(createDto, "SecurePassword123");
        isActiveProp.SetValue(createDto, true);

        firstNameProp.GetValue(createDto).Should().Be("Jane");
        lastNameProp.GetValue(createDto).Should().Be("Doe");
        emailProp.GetValue(createDto).Should().Be("jane.doe@example.com");
        passwordProp.GetValue(createDto).Should().Be("SecurePassword123");
        isActiveProp.GetValue(createDto).Should().Be(true);

        var idProp = createType.GetProperty("Id");
        idProp.Should().BeNull("Create DTOs should not have Id property");
    }

    [Fact]
    public void UpdateDto_ShouldWork_ForUserUpdateScenario()
    {
        var assembly = Assembly.GetAssembly(typeof(TestUser));
        var updateType = assembly?.GetType("Facet.Tests.TestModels.UpdateTestUserRequest");
        
        updateType.Should().NotBeNull();
        var updateDto = Activator.CreateInstance(updateType!);

        var idProp = updateType.GetProperty("Id")!;
        var firstNameProp = updateType.GetProperty("FirstName")!;
        var emailProp = updateType.GetProperty("Email")!;

        idProp.SetValue(updateDto, 42);
        firstNameProp.SetValue(updateDto, "Updated Jane");
        emailProp.SetValue(updateDto, "jane.updated@example.com");

        idProp.GetValue(updateDto).Should().Be(42);
        firstNameProp.GetValue(updateDto).Should().Be("Updated Jane");
        emailProp.GetValue(updateDto).Should().Be("jane.updated@example.com");
    }

    [Fact]
    public void ResponseDto_ShouldWork_ForApiResponseScenario()
    {
        var user = CreateSampleTestUser();
        var assembly = Assembly.GetAssembly(typeof(TestUser));
        var responseType = assembly?.GetType("Facet.Tests.TestModels.TestUserResponse");
        
        responseType.Should().NotBeNull();
        
        var constructor = responseType!.GetConstructor(new[] { typeof(TestUser) });
        constructor.Should().NotBeNull();
        
        var responseDto = constructor!.Invoke(new object[] { user });

        var idProp = responseType.GetProperty("Id")!;
        var firstNameProp = responseType.GetProperty("FirstName")!;
        var lastNameProp = responseType.GetProperty("LastName")!;
        var emailProp = responseType.GetProperty("Email")!;
        var isActiveProp = responseType.GetProperty("IsActive")!;

        idProp.GetValue(responseDto).Should().Be(user.Id);
        firstNameProp.GetValue(responseDto).Should().Be(user.FirstName);
        lastNameProp.GetValue(responseDto).Should().Be(user.LastName);
        emailProp.GetValue(responseDto).Should().Be(user.Email);
        isActiveProp.GetValue(responseDto).Should().Be(user.IsActive);
    }

    [Fact]
    public void QueryDto_ShouldWork_ForSearchScenario()
    {
        var assembly = Assembly.GetAssembly(typeof(TestUser));
        var queryType = assembly?.GetType("Facet.Tests.TestModels.TestUserQuery");
        
        queryType.Should().NotBeNull();
        var queryDto = Activator.CreateInstance(queryType!);

        var firstNameProp = queryType.GetProperty("FirstName")!;
        var isActiveProp = queryType.GetProperty("IsActive")!;

        firstNameProp.SetValue(queryDto, "John");
        isActiveProp.SetValue(queryDto, true);

        firstNameProp.GetValue(queryDto).Should().Be("John");
        isActiveProp.GetValue(queryDto).Should().Be(true);

        firstNameProp.SetValue(queryDto, null);
        firstNameProp.GetValue(queryDto).Should().BeNull("Query DTO properties should accept null for optional filtering");
    }

    [Fact]
    public void UpsertDto_ShouldWork_ForCreateOrUpdateScenario()
    {
        var assembly = Assembly.GetAssembly(typeof(TestUser));
        var upsertType = assembly?.GetType("Facet.Tests.TestModels.UpsertTestUserRequest");
        
        upsertType.Should().NotBeNull();

        var createUpsertDto = Activator.CreateInstance(upsertType!);
        var idProp = upsertType.GetProperty("Id")!;
        var firstNameProp = upsertType.GetProperty("FirstName")!;

        firstNameProp.SetValue(createUpsertDto, "New User");
        
        firstNameProp.GetValue(createUpsertDto).Should().Be("New User");
        var idValue = idProp.GetValue(createUpsertDto);
        
        idValue.Should().Be(0);

        var updateUpsertDto = Activator.CreateInstance(upsertType);

        idProp.SetValue(updateUpsertDto, 123);
        firstNameProp.SetValue(updateUpsertDto, "Updated User");

        idProp.GetValue(updateUpsertDto).Should().Be(123);
        firstNameProp.GetValue(updateUpsertDto).Should().Be("Updated User");
    }

    [Fact]
    public void ProjectionProperty_ShouldWork_ForLinqQueries()
    {
        var assembly = Assembly.GetAssembly(typeof(TestUser));
        var responseType = assembly?.GetType("Facet.Tests.TestModels.TestUserResponse");
        
        responseType.Should().NotBeNull();
        
        var users = new List<TestUser>
        {
            CreateSampleTestUser(1, "Alice", "Smith", "alice@example.com"),
            CreateSampleTestUser(2, "Bob", "Johnson", "bob@example.com"),
            CreateSampleTestUser(3, "Charlie", "Brown", "charlie@example.com")
        };

        var projectionProperty = responseType!.GetProperty("Projection", BindingFlags.Public | BindingFlags.Static);
        projectionProperty.Should().NotBeNull();
        
        var projection = projectionProperty!.GetValue(null);
        projection.Should().NotBeNull();

        var queryable = users.AsQueryable();
        
        if (projection is System.Linq.Expressions.LambdaExpression lambdaExpr)
        {
            var compiledDelegate = lambdaExpr.Compile();
            var projectedResults = users.Select(user => compiledDelegate.DynamicInvoke(user)).ToList();

            projectedResults.Should().HaveCount(3);
            
            var firstResult = projectedResults[0];
            var firstResultType = firstResult?.GetType();
            firstResultType.Should().Be(responseType);
            
            var firstNameProp = responseType.GetProperty("FirstName")!;
            firstNameProp.GetValue(firstResult).Should().Be("Alice");
        }
        else
        {
            Assert.Fail("Projection property should be of type LambdaExpression");
        }
    }

    [Fact]
    public void AuditableDtos_ShouldExcludeAuditFields_InPracticalScenarios()
    {
        var product = CreateSampleTestProduct();
        var assembly = Assembly.GetAssembly(typeof(TestProduct));
        var createType = assembly?.GetType("Facet.Tests.TestModels.CreateTestProductRequest");
        var responseType = assembly?.GetType("Facet.Tests.TestModels.TestProductResponse");
        
        createType.Should().NotBeNull();
        responseType.Should().NotBeNull();

        var createDto = Activator.CreateInstance(createType!);
        var nameProp = createType.GetProperty("Name")!;
        var priceProp = createType.GetProperty("Price")!;
        
        nameProp.SetValue(createDto, "New Product");
        priceProp.SetValue(createDto, 49.99m);

        nameProp.GetValue(createDto).Should().Be("New Product");
        priceProp.GetValue(createDto).Should().Be(49.99m);
        
        createType.GetProperty("CreatedAt").Should().BeNull("Create DTO should not have audit field CreatedAt");
        createType.GetProperty("CreatedBy").Should().BeNull("Create DTO should not have audit field CreatedBy");
        createType.GetProperty("UpdatedAt").Should().BeNull("Create DTO should not have audit field UpdatedAt");
        createType.GetProperty("UpdatedBy").Should().BeNull("Create DTO should not have audit field UpdatedBy");

        var responseDto = Activator.CreateInstance(responseType!, product);
        
        var responseNameProp = responseType!.GetProperty("Name")!;
        responseNameProp.GetValue(responseDto).Should().Be(product.Name);
        
        responseType.GetProperty("CreatedAt").Should().BeNull("Response DTO should not have audit field CreatedAt");
        responseType.GetProperty("CreatedBy").Should().BeNull("Response DTO should not have audit field CreatedBy");
    }

    #region Helper Methods

    private static TestUser CreateSampleTestUser(int id = 1, string firstName = "John", string lastName = "Doe", string email = "john.doe@example.com")
    {
        return new TestUser
        {
            Id = id,
            FirstName = firstName,
            LastName = lastName,
            Email = email,
            Password = "hashedpassword",
            DateOfBirth = new DateTime(1990, 1, 1),
            IsActive = true,
            LastLoginAt = DateTime.Now.AddHours(-2),
            CreatedAt = DateTime.Now.AddDays(-30)
        };
    }

    private static TestProduct CreateSampleTestProduct()
    {
        return new TestProduct
        {
            Id = 1,
            Name = "Sample Product",
            Description = "A sample product for testing",
            Price = 29.99m,
            IsAvailable = true,
            InternalNotes = "Internal notes here",
            CreatedAt = DateTime.Now.AddDays(-10),
            UpdatedAt = DateTime.Now.AddHours(-1),
            CreatedBy = "system",
            UpdatedBy = "admin"
        };
    }

    #endregion
}
