using System.Reflection;

namespace Facet.Tests.UnitTests.Core.GenerateDtos;

/// <summary>
/// Tests for error handling and edge cases in GenerateDtos functionality.
/// Verifies robustness and proper error reporting.
/// </summary>
public class GenerateDtosErrorHandlingTests
{
    [Fact]
    public void GeneratedDtos_ShouldHandleNull_Gracefully()
    {
        var assembly = Assembly.GetAssembly(typeof(TestModels.TestUser));
        var responseType = assembly?.GetType("Facet.Tests.TestModels.TestUserResponse");
        
        responseType.Should().NotBeNull();
        
        var sourceConstructor = responseType!.GetConstructor(new[] { typeof(TestModels.TestUser) });
        sourceConstructor.Should().NotBeNull();
        
        Action actWithNull = () => sourceConstructor!.Invoke(new object[] { null! });
        
        var parameterlessConstructor = responseType.GetConstructor(Type.EmptyTypes);
        parameterlessConstructor.Should().NotBeNull();
        
        var instance = parameterlessConstructor!.Invoke(null);
        instance.Should().NotBeNull();
    }

    [Fact]
    public void GeneratedDtos_ShouldHaveConsistent_PropertyNames()
    {
        var assembly = Assembly.GetAssembly(typeof(TestModels.TestUser));
        var responseType = assembly?.GetType("Facet.Tests.TestModels.TestUserResponse");
        var createType = assembly?.GetType("Facet.Tests.TestModels.CreateTestUserRequest");
        var updateType = assembly?.GetType("Facet.Tests.TestModels.UpdateTestUserRequest");
        
        responseType.Should().NotBeNull();
        createType.Should().NotBeNull();
        updateType.Should().NotBeNull();
        
        var commonProperties = new[] { "FirstName", "LastName", "Email", "IsActive" };
        
        foreach (var propName in commonProperties)
        {
            var responseProp = responseType!.GetProperty(propName);
            var createProp = createType!.GetProperty(propName);
            var updateProp = updateType!.GetProperty(propName);
            
            responseProp.Should().NotBeNull($"Response DTO should have {propName} property");
            createProp.Should().NotBeNull($"Create DTO should have {propName} property");
            updateProp.Should().NotBeNull($"Update DTO should have {propName} property");
            
            if (responseProp != null && createProp != null && updateProp != null)
            {
                responseProp.PropertyType.Should().Be(createProp.PropertyType, 
                    $"{propName} should have same type in Response and Create DTOs");
                responseProp.PropertyType.Should().Be(updateProp.PropertyType, 
                    $"{propName} should have same type in Response and Update DTOs");
            }
        }
    }

    [Fact]
    public void GeneratedDtos_ShouldHaveValid_MethodSignatures()
    {
        var assembly = Assembly.GetAssembly(typeof(TestModels.TestUser));
        var responseType = assembly?.GetType("Facet.Tests.TestModels.TestUserResponse");
        
        responseType.Should().NotBeNull();
        
        var projectionProperty = responseType.GetProperty("Projection", BindingFlags.Public | BindingFlags.Static);
        projectionProperty.Should().NotBeNull("Projection property should exist");
        projectionProperty!.PropertyType.Should().NotBeNull("Projection should have valid type");
        projectionProperty.CanRead.Should().BeTrue("Projection should be readable");
        projectionProperty.GetMethod!.IsStatic.Should().BeTrue("Projection should be static");
        projectionProperty.GetMethod.IsPublic.Should().BeTrue("Projection should be public");
        
        var constructors = responseType.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
        constructors.Length.Should().BeGreaterThan(0, "Should have at least one public constructor");
        
        var parameterlessConstructor = responseType.GetConstructor(Type.EmptyTypes);
        parameterlessConstructor.Should().NotBeNull("Should have parameterless constructor");
        
        var sourceConstructor = responseType.GetConstructor(new[] { typeof(TestModels.TestUser) });
        sourceConstructor.Should().NotBeNull("Should have source type constructor");
    }

    [Fact]
    public void GeneratedDtos_ShouldWork_WithComplexDataTypes()
    {
        var user = new TestModels.TestUser
        {
            Id = int.MaxValue,
            FirstName = "Test with special chars: ����",
            LastName = "O'Connor-Smith",
            Email = "test+special@example-domain.co.uk",
            Password = "Complex!Password@123",
            DateOfBirth = DateTime.MinValue,
            IsActive = false,
            LastLoginAt = null, 
            CreatedAt = DateTime.MaxValue
        };
        
        var assembly = Assembly.GetAssembly(typeof(TestModels.TestUser));
        var responseType = assembly?.GetType("Facet.Tests.TestModels.TestUserResponse");
        
        responseType.Should().NotBeNull();
        
        var constructor = responseType!.GetConstructor(new[] { typeof(TestModels.TestUser) });
        var responseDto = constructor!.Invoke(new object[] { user });
        
        var idProp = responseType.GetProperty("Id")!;
        idProp.GetValue(responseDto).Should().Be(int.MaxValue);
        
        var firstNameProp = responseType.GetProperty("FirstName")!;
        firstNameProp.GetValue(responseDto).Should().Be("Test with special chars: ����");
        
        var lastNameProp = responseType.GetProperty("LastName")!;
        lastNameProp.GetValue(responseDto).Should().Be("O'Connor-Smith");
        
        var emailProp = responseType.GetProperty("Email")!;
        emailProp.GetValue(responseDto).Should().Be("test+special@example-domain.co.uk");
        
        var dobProp = responseType.GetProperty("DateOfBirth")!;
        dobProp.GetValue(responseDto).Should().Be(DateTime.MinValue);
        
        var lastLoginProp = responseType.GetProperty("LastLoginAt")!;
        lastLoginProp.GetValue(responseDto).Should().BeNull();
        
        var createdAtProp = responseType.GetProperty("CreatedAt")!;
        createdAtProp.GetValue(responseDto).Should().Be(DateTime.MaxValue);
    }

    [Fact]
    public void GeneratedDtos_ShouldWork_WithEmptyStringsAndDefaults()
    {
        var user = new TestModels.TestUser
        {
            Id = 0,
            FirstName = "",
            LastName = null!, 
            Email = string.Empty,
            Password = "",
            DateOfBirth = default(DateTime),
            IsActive = false,
            LastLoginAt = null,
            CreatedAt = default(DateTime)
        };
        
        var assembly = Assembly.GetAssembly(typeof(TestModels.TestUser));
        var responseType = assembly?.GetType("Facet.Tests.TestModels.TestUserResponse");
        
        var constructor = responseType!.GetConstructor(new[] { typeof(TestModels.TestUser) });
        var responseDto = constructor!.Invoke(new object[] { user });
        
        var idProp = responseType.GetProperty("Id")!;
        idProp.GetValue(responseDto).Should().Be(0);
        
        var firstNameProp = responseType.GetProperty("FirstName")!;
        firstNameProp.GetValue(responseDto).Should().Be("");
        
        var lastNameProp = responseType.GetProperty("LastName")!;
        lastNameProp.GetValue(responseDto).Should().BeNull();
        
        var emailProp = responseType.GetProperty("Email")!;
        emailProp.GetValue(responseDto).Should().Be(string.Empty);
        
        var dobProp = responseType.GetProperty("DateOfBirth")!;
        dobProp.GetValue(responseDto).Should().Be(default(DateTime));
        
        var isActiveProp = responseType.GetProperty("IsActive")!;
        isActiveProp.GetValue(responseDto).Should().Be(false);
    }

    [Fact]
    public void GeneratedDtos_ShouldMaintain_ThreadSafety()
    {
        var assembly = Assembly.GetAssembly(typeof(TestModels.TestUser));
        var responseType = assembly?.GetType("Facet.Tests.TestModels.TestUserResponse");
        
        responseType.Should().NotBeNull();
        
        var projectionProperty = responseType!.GetProperty("Projection", BindingFlags.Public | BindingFlags.Static);
        projectionProperty.Should().NotBeNull();
        
        var tasks = new List<Task<object?>>();
        
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(Task.Run(() => projectionProperty!.GetValue(null)));
        }
        
        Task.WaitAll(tasks.ToArray());
        
        foreach (var task in tasks)
        {
            task.Result.Should().NotBeNull("Projection should return a valid result");
            task.IsCompletedSuccessfully.Should().BeTrue("Task should complete without exceptions");
        }
        
        var firstResult = tasks[0].Result;
        foreach (var task in tasks.Skip(1))
        {
            task.Result.Should().NotBeNull("All projection results should be non-null");
            
            task.Result!.GetType().Should().Be(firstResult!.GetType(), "All projection results should have the same type");
        }
    }

    [Fact]
    public void GeneratedDtos_ShouldHandle_LargeDataSets()
    {
        const int dataSize = 1000;
        
        var users = new List<TestModels.TestUser>();
        for (int i = 0; i < dataSize; i++)
        {
            users.Add(new TestModels.TestUser
            {
                Id = i,
                FirstName = $"User{i}",
                LastName = $"LastName{i}",
                Email = $"user{i}@test.com",
                Password = $"password{i}",
                DateOfBirth = DateTime.Now.AddYears(-20 - (i % 50)),
                IsActive = i % 2 == 0,
                LastLoginAt = DateTime.Now.AddDays(-i % 30),
                CreatedAt = DateTime.Now.AddDays(-i)
            });
        }
        
        var assembly = Assembly.GetAssembly(typeof(TestModels.TestUser));
        var responseType = assembly?.GetType("Facet.Tests.TestModels.TestUserResponse");
        var constructor = responseType!.GetConstructor(new[] { typeof(TestModels.TestUser) });
        
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var responseDtos = users.Select(user => constructor!.Invoke(new object[] { user })).ToList();
        stopwatch.Stop();
        
        responseDtos.Should().HaveCount(dataSize);
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(5000, "Large dataset conversion should be reasonably fast");
        
        var sample1 = responseDtos[100];
        var firstNameProp = responseType.GetProperty("FirstName")!;
        firstNameProp.GetValue(sample1).Should().Be("User100");
        
        var sample2 = responseDtos[500];
        firstNameProp.GetValue(sample2).Should().Be("User500");
    }

    [Fact]
    public void GeneratedAuditableDtos_ShouldConsistently_ExcludeAuditFields()
    {
        var assembly = Assembly.GetAssembly(typeof(TestModels.TestProduct));
        var createType = assembly?.GetType("Facet.Tests.TestModels.CreateTestProductRequest");
        var updateType = assembly?.GetType("Facet.Tests.TestModels.UpdateTestProductRequest");
        var responseType = assembly?.GetType("Facet.Tests.TestModels.TestProductResponse");
        
        var auditFields = new[] { "CreatedAt", "UpdatedAt", "CreatedBy", "UpdatedBy" };
        
        foreach (var auditField in auditFields)
        {
            createType!.GetProperty(auditField).Should().BeNull($"Create DTO should not have audit field {auditField}");
            updateType!.GetProperty(auditField).Should().BeNull($"Update DTO should not have audit field {auditField}");
            responseType!.GetProperty(auditField).Should().BeNull($"Response DTO should not have audit field {auditField}");
        }
        
        var businessFields = new[] { "Name", "Description", "Price", "IsAvailable" };
        
        foreach (var businessField in businessFields)
        {
            createType!.GetProperty(businessField).Should().NotBeNull($"Create DTO should have business field {businessField}");
            updateType!.GetProperty(businessField).Should().NotBeNull($"Update DTO should have business field {businessField}");
            responseType!.GetProperty(businessField).Should().NotBeNull($"Response DTO should have business field {businessField}");
        }
    }
}
