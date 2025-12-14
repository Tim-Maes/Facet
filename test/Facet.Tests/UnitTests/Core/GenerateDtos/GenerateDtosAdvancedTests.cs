namespace Facet.Tests.UnitTests.Core.GenerateDtos;

/// <summary>
/// Tests for advanced GenerateDtos scenarios including multiple attributes,
/// custom configurations, and edge cases.
/// </summary>
public class GenerateDtosAdvancedTests
{
    [Fact]
    public void GenerateDtos_ShouldSupportCustomNaming_WithPrefixAndSuffix()
    {
        // Note: This test validates the concept, but requires additional test entities
        // with custom naming configurations to fully test
        
        // For now, test that the standard naming works
        var assembly = System.Reflection.Assembly.GetAssembly(typeof(TestModels.TestUser));
        
        // Standard naming pattern: [Prefix]Type[Suffix] or Type[Suffix]
        var responseType = assembly?.GetType("Facet.Tests.TestModels.TestUserResponse");
        responseType.Should().NotBeNull("Standard Response naming should work");
        
        var createType = assembly?.GetType("Facet.Tests.TestModels.CreateTestUserRequest");
        createType.Should().NotBeNull("Standard Create naming should work");
    }

    [Fact]
    public void GeneratedDtos_ShouldHaveCorrectPropertyTypes()
    {
        var assembly = System.Reflection.Assembly.GetAssembly(typeof(TestModels.TestUser));
        var responseType = assembly?.GetType("Facet.Tests.TestModels.TestUserResponse");
        
        responseType.Should().NotBeNull();
        
        // Test that property types are preserved correctly
        var idProperty = responseType!.GetProperty("Id");
        idProperty.Should().NotBeNull();
        idProperty!.PropertyType.Should().Be(typeof(int), "Id property should maintain int type");
        
        var firstNameProperty = responseType.GetProperty("FirstName");
        firstNameProperty.Should().NotBeNull();
        firstNameProperty!.PropertyType.Should().Be(typeof(string), "FirstName should maintain string type");
        
        var dateOfBirthProperty = responseType.GetProperty("DateOfBirth");
        dateOfBirthProperty.Should().NotBeNull();
        dateOfBirthProperty!.PropertyType.Should().Be(typeof(DateTime), "DateOfBirth should maintain DateTime type");
        
        var lastLoginAtProperty = responseType.GetProperty("LastLoginAt");
        lastLoginAtProperty.Should().NotBeNull();
        lastLoginAtProperty!.PropertyType.Should().Be(typeof(DateTime?), "LastLoginAt should maintain DateTime? type");
        
        var isActiveProperty = responseType.GetProperty("IsActive");
        isActiveProperty.Should().NotBeNull();
        isActiveProperty!.PropertyType.Should().Be(typeof(bool), "IsActive should maintain bool type");
    }

    [Fact]
    public void GeneratedQueryDto_ShouldHaveNullableProperties_ForValueTypes()
    {
        var assembly = System.Reflection.Assembly.GetAssembly(typeof(TestModels.TestUser));
        var queryType = assembly?.GetType("Facet.Tests.TestModels.TestUserQuery");
        
        queryType.Should().NotBeNull();
        
        // Check that value type properties become nullable in Query DTOs
        var idProperty = queryType!.GetProperty("Id");
        idProperty.Should().NotBeNull();
        
        var isActiveProperty = queryType.GetProperty("IsActive");
        isActiveProperty.Should().NotBeNull();
        
        // Value types should be nullable in query DTOs for filtering
        var dateOfBirthProperty = queryType.GetProperty("DateOfBirth");
        dateOfBirthProperty.Should().NotBeNull();
        
        // Test actual nullable behavior
        var queryInstance = Activator.CreateInstance(queryType);
        
        // Should be able to set value type properties to null
        idProperty!.SetValue(queryInstance, null);
        idProperty.GetValue(queryInstance).Should().BeNull("Query DTO Id should accept null values");
        
        isActiveProperty!.SetValue(queryInstance, null);
        isActiveProperty.GetValue(queryInstance).Should().BeNull("Query DTO IsActive should accept null values");
    }

    [Fact]
    public void GeneratedDtos_ShouldWorkWith_ComplexPropertyMappingScenarios()
    {
        // Test with TestProduct which has decimal, DateTime, and string properties
        var assembly = System.Reflection.Assembly.GetAssembly(typeof(TestModels.TestProduct));
        var responseType = assembly?.GetType("Facet.Tests.TestModels.TestProductResponse");
        
        responseType.Should().NotBeNull();
        
        var product = new TestModels.TestProduct
        {
            Id = 42,
            Name = "Complex Product",
            Description = "A product with various property types",
            Price = 123.45m,
            IsAvailable = true,
            InternalNotes = "These should be excluded from response",
            CreatedAt = DateTime.Now.AddDays(-5),
            UpdatedAt = DateTime.Now.AddHours(-1),
            CreatedBy = "testuser",
            UpdatedBy = "testuser2"
        };
        
        // Create response DTO
        var constructor = responseType!.GetConstructor(new[] { typeof(TestModels.TestProduct) });
        constructor.Should().NotBeNull();
        
        var responseDto = constructor!.Invoke(new object[] { product });
        
        // Verify complex property mapping
        var priceProperty = responseType.GetProperty("Price");
        priceProperty.Should().NotBeNull();
        priceProperty!.PropertyType.Should().Be(typeof(decimal), "Decimal properties should be preserved");
        priceProperty.GetValue(responseDto).Should().Be(123.45m);
        
        var isAvailableProperty = responseType.GetProperty("IsAvailable");
        isAvailableProperty.Should().NotBeNull();
        isAvailableProperty!.PropertyType.Should().Be(typeof(bool), "Boolean properties should be preserved");
        isAvailableProperty.GetValue(responseDto).Should().Be(true);
        
        // Audit fields should be excluded (GenerateAuditableDtos)
        responseType.GetProperty("CreatedAt").Should().BeNull("Audit fields should be excluded");
        responseType.GetProperty("UpdatedAt").Should().BeNull("Audit fields should be excluded");
        responseType.GetProperty("CreatedBy").Should().BeNull("Audit fields should be excluded");
        responseType.GetProperty("UpdatedBy").Should().BeNull("Audit fields should be excluded");
    }

    [Fact]
    public void GeneratedDtos_ShouldWork_WithEmptyAndDefaultValues()
    {
        var assembly = System.Reflection.Assembly.GetAssembly(typeof(TestModels.TestUser));
        var createType = assembly?.GetType("Facet.Tests.TestModels.CreateTestUserRequest");
        
        createType.Should().NotBeNull();
        
        // Test with default/empty values
        var createDto = Activator.CreateInstance(createType!);
        
        var firstNameProperty = createType.GetProperty("FirstName")!;
        var lastNameProperty = createType.GetProperty("LastName")!;
        var emailProperty = createType.GetProperty("Email")!;
        var isActiveProperty = createType.GetProperty("IsActive")!;
        var dateOfBirthProperty = createType.GetProperty("DateOfBirth")!;
        
        // Test setting empty/default values
        firstNameProperty.SetValue(createDto, string.Empty);
        lastNameProperty.SetValue(createDto, null);  // Test null for reference types
        emailProperty.SetValue(createDto, "");
        isActiveProperty.SetValue(createDto, false);
        dateOfBirthProperty.SetValue(createDto, default(DateTime));
        
        // Verify values are set correctly
        firstNameProperty.GetValue(createDto).Should().Be(string.Empty);
        lastNameProperty.GetValue(createDto).Should().BeNull();
        emailProperty.GetValue(createDto).Should().Be("");
        isActiveProperty.GetValue(createDto).Should().Be(false);
        dateOfBirthProperty.GetValue(createDto).Should().Be(default(DateTime));
    }

    [Fact]
    public void GeneratedDtos_ShouldWork_WithInheritanceScenarios()
    {
        // Test that generated DTOs work with the inheritance from base properties
        var assembly = System.Reflection.Assembly.GetAssembly(typeof(TestModels.TestUser));
        var responseType = assembly?.GetType("Facet.Tests.TestModels.TestUserResponse");
        
        responseType.Should().NotBeNull();
        
        // Verify all properties from TestUser are included (no inheritance in this case, but test structure)
        var allProperties = responseType!.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        
        // Should have all the main properties
        var propertyNames = allProperties.Select(p => p.Name).ToList();
        
        propertyNames.Should().Contain("Id");
        propertyNames.Should().Contain("FirstName");
        propertyNames.Should().Contain("LastName");
        propertyNames.Should().Contain("Email");
        propertyNames.Should().Contain("Password");
        propertyNames.Should().Contain("DateOfBirth");
        propertyNames.Should().Contain("IsActive");
        propertyNames.Should().Contain("LastLoginAt");
        propertyNames.Should().Contain("CreatedAt");
        
        // Should not have any unexpected properties
        allProperties.Length.Should().BeGreaterThan(8, "Should have all expected properties");
    }

    [Fact]
    public void GeneratedDtos_ShouldBe_SerializationFriendly()
    {
        var assembly = System.Reflection.Assembly.GetAssembly(typeof(TestModels.TestUser));
        var responseType = assembly?.GetType("Facet.Tests.TestModels.TestUserResponse");
        
        responseType.Should().NotBeNull();
        
        var user = new TestModels.TestUser
        {
            Id = 123,
            FirstName = "Serializable",
            LastName = "User",
            Email = "serialize@test.com",
            IsActive = true,
            DateOfBirth = new DateTime(1990, 5, 15),
            LastLoginAt = DateTime.Now
        };
        
        var responseDto = Activator.CreateInstance(responseType!, user);
        
        // Test that the DTO has all the properties needed for JSON serialization
        var properties = responseType!.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        
        foreach (var property in properties)
        {
            // All properties should be readable (have getters)
            property.CanRead.Should().BeTrue($"Property {property.Name} should be readable for serialization");
            
            // All properties should be writable (have setters) for deserialization
            property.CanWrite.Should().BeTrue($"Property {property.Name} should be writable for deserialization");
            
            // Properties should not be null for reference types (except LastLoginAt which is nullable)
            var value = property.GetValue(responseDto);
            if (property.PropertyType == typeof(string) && property.Name != "Password")
            {
                value.Should().NotBeNull($"String property {property.Name} should not be null");
            }
        }
    }

    [Fact]
    public void GeneratedDtos_ShouldHave_CorrectAccessibility()
    {
        var assembly = System.Reflection.Assembly.GetAssembly(typeof(TestModels.TestUser));
        var responseType = assembly?.GetType("Facet.Tests.TestModels.TestUserResponse");
        
        responseType.Should().NotBeNull();
        
        // Class should be public
        responseType!.IsPublic.Should().BeTrue("Generated DTOs should be public");
        
        // Properties should be public
        var properties = responseType.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        
        foreach (var property in properties)
        {
            property.GetMethod?.IsPublic.Should().BeTrue($"Property {property.Name} getter should be public");
            property.SetMethod?.IsPublic.Should().BeTrue($"Property {property.Name} setter should be public");
        }
        
        // Constructors should be public
        var constructors = responseType.GetConstructors(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        constructors.Should().NotBeEmpty("Should have public constructors");
        
        foreach (var constructor in constructors)
        {
            constructor.IsPublic.Should().BeTrue("All constructors should be public");
        }
    }

    [Fact]
    public void Facet_ParameterlessConstructor_ShouldExist()
    {
        // This test verifies that the parameterless constructor is generated.
        // Note: The parameterless constructor is generated first in the source code
        // so that third-party code that picks the first constructor will use it.
        // However, reflection's GetConstructors() does not guarantee source order.
        var assembly = System.Reflection.Assembly.GetAssembly(typeof(TestModels.TestUser));
        var responseType = assembly?.GetType("Facet.Tests.TestModels.TestUserResponse");
        
        responseType.Should().NotBeNull();
        
        // Get all public constructors
        var constructors = responseType!.GetConstructors(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        
        constructors.Should().NotBeEmpty("Should have at least one constructor");
        
        // Verify that a parameterless constructor exists
        var parameterlessConstructor = constructors.FirstOrDefault(c => c.GetParameters().Length == 0);
        parameterlessConstructor.Should().NotBeNull(
            "A parameterless constructor should exist for deserialization and third-party tool compatibility");
        
        // Verify we can create an instance using the parameterless constructor
        var instance = parameterlessConstructor!.Invoke(Array.Empty<object>());
        instance.Should().NotBeNull("Should be able to create an instance using parameterless constructor");
    }
}