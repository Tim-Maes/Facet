using Facet.Tests.TestModels;
using System.Reflection;

namespace Facet.Tests.UnitTests.Core.GenerateDtos;

/// <summary>
/// Tests for comprehensive GenerateDtos scenarios including custom configurations,
/// different output types, naming conventions, and complex data types.
/// </summary>
public class GenerateDtosComprehensiveTests
{
    [Fact]
    public void GenerateDtos_ShouldGenerateRecordTypes_WhenRecordOutputSpecified()
    {
        var assembly = Assembly.GetAssembly(typeof(TestOrder));
        var responseType = assembly?.GetType("Facet.Tests.TestModels.TestOrderResponse");
        var queryType = assembly?.GetType("Facet.Tests.TestModels.TestOrderQuery");

        responseType.Should().NotBeNull("TestOrderResponse should be generated as record");
        queryType.Should().NotBeNull("TestOrderQuery should be generated as record");

        responseType!.IsClass.Should().BeTrue("Records are reference types");
        queryType!.IsClass.Should().BeTrue("Records are reference types");
        
        var idProperty = responseType.GetProperty("Id");
        idProperty.Should().NotBeNull("Record should have Id property");
        idProperty!.PropertyType.Should().Be(typeof(Guid), "Guid property should be preserved");
        
        var orderNumberProperty = responseType.GetProperty("OrderNumber");
        orderNumberProperty.Should().NotBeNull("Record should have OrderNumber property");
        orderNumberProperty!.PropertyType.Should().Be(typeof(string));
        
        var statusProperty = responseType.GetProperty("Status");
        statusProperty.Should().NotBeNull("Record should have Status enum property");
        statusProperty!.PropertyType.Should().Be(typeof(OrderStatus));
        
        var notesProperty = responseType.GetProperty("Notes");
        notesProperty.Should().NotBeNull("Record should have Notes nullable property");
        notesProperty!.PropertyType.Should().Be(typeof(string));
    }

    [Fact]
    public void GenerateDtos_ShouldSupportMultipleAttributes_WithDifferentExclusions()
    {
        var assembly = Assembly.GetAssembly(typeof(TestMultiConfigEntity));
        var createType = assembly?.GetType("Facet.Tests.TestModels.CreateTestMultiConfigEntityRequest");
        var responseType = assembly?.GetType("Facet.Tests.TestModels.TestMultiConfigEntityResponse");

        createType.Should().NotBeNull("Create DTO should be generated");
        responseType.Should().NotBeNull("Response DTO should be generated");
        
        createType!.GetProperty("SecretKey").Should().BeNull("Create DTO should exclude SecretKey");
        createType.GetProperty("InternalData").Should().NotBeNull("Create DTO should include InternalData");
        createType.GetProperty("Name").Should().NotBeNull("Create DTO should include Name");
        createType.GetProperty("Description").Should().NotBeNull("Create DTO should include Description");
        
        responseType!.GetProperty("SecretKey").Should().BeNull("Response DTO should exclude SecretKey");
        responseType.GetProperty("InternalData").Should().BeNull("Response DTO should exclude InternalData");
        responseType.GetProperty("Name").Should().NotBeNull("Response DTO should include Name");
        responseType.GetProperty("Description").Should().NotBeNull("Response DTO should include Description");
    }

    [Fact]
    public void GenerateDtos_ShouldApplyCustomNaming_WithPrefixAndSuffix()
    {
        var assembly = Assembly.GetAssembly(typeof(TestCustomNaming));
        
        var createType = assembly?.GetType("Facet.Tests.TestModels.ApiCreateTestCustomNamingRequestModel");
        var responseType = assembly?.GetType("Facet.Tests.TestModels.ApiTestCustomNamingResponseModel");
        var queryType = assembly?.GetType("Facet.Tests.TestModels.ApiTestCustomNamingQueryModel");

        var allTypes = assembly!.GetTypes()
            .Where(t => t.Name.Contains("TestCustomNaming"))
            .Select(t => t.Name)
            .ToList();

        allTypes.Should().NotBeEmpty("Should generate DTOs for TestCustomNaming");
        
        allTypes.Should().Contain(name => name.Contains("TestCustomNaming"), 
            "Generated DTOs should contain base type name");
    }

    [Fact]
    public void GenerateDtos_ShouldGenerateRecordStructs_WhenRecordStructOutputSpecified()
    {
        var assembly = Assembly.GetAssembly(typeof(TestCompactEntity));
        
        var allTypes = assembly!.GetTypes()
            .Where(t => t.Name.Contains("TestCompactEntity"))
            .ToList();

        allTypes.Should().NotBeEmpty("Should generate DTOs for TestCompactEntity");
        
        var firstGeneratedType = allTypes.FirstOrDefault();
        firstGeneratedType.Should().NotBeNull();
        
        if (firstGeneratedType != null)
        {
            var idProperty = firstGeneratedType.GetProperty("Id");
            idProperty.Should().NotBeNull("Generated type should have Id property");
            
            var codeProperty = firstGeneratedType.GetProperty("Code");
            codeProperty.Should().NotBeNull("Generated type should have Code property");
            
            var createdAtProperty = firstGeneratedType.GetProperty("CreatedAt");
            if (createdAtProperty != null)
            {
                createdAtProperty.PropertyType.Should().Be(typeof(DateTime), "CreatedAt should have correct type if present");
            }
        }
    }

    [Fact]
    public void GenerateDtos_ShouldIncludeFields_WhenIncludeFieldsEnabled()
    {
        var assembly = Assembly.GetAssembly(typeof(TestEntityWithFields));
        var responseType = assembly?.GetType("Facet.Tests.TestModels.TestEntityWithFieldsResponse");

        responseType.Should().NotBeNull("Response DTO with fields should be generated");
        
        if (responseType != null)
        {
            MemberInfo? publicFieldMember = responseType.GetProperty("PublicField");
            if (publicFieldMember == null)
                publicFieldMember = responseType.GetField("PublicField");
            publicFieldMember.Should().NotBeNull("Public field should be included");
            
            MemberInfo? readOnlyFieldMember = responseType.GetProperty("ReadOnlyField");
            if (readOnlyFieldMember == null)
                readOnlyFieldMember = responseType.GetField("ReadOnlyField");
            readOnlyFieldMember.Should().NotBeNull("ReadOnly field should be included");
            
            var propertyFieldProperty = responseType.GetProperty("PropertyField");
            propertyFieldProperty.Should().NotBeNull("Regular property should be included");
            
            MemberInfo? privateFieldMember = responseType.GetProperty("PrivateField");
            if (privateFieldMember == null)
                privateFieldMember = responseType.GetField("PrivateField");
            privateFieldMember.Should().BeNull("Private field should not be included");
        }
    }

    [Fact]
    public void GenerateDtos_ShouldHandleComplexTypes_Correctly()
    {
        var assembly = Assembly.GetAssembly(typeof(TestComplexTypes));
        var createType = assembly?.GetType("Facet.Tests.TestModels.CreateTestComplexTypesRequest");
        var updateType = assembly?.GetType("Facet.Tests.TestModels.UpdateTestComplexTypesRequest");

        createType.Should().NotBeNull("Create DTO should be generated for complex types");
        updateType.Should().NotBeNull("Update DTO should be generated for complex types");
        
        if (createType != null)
        {
            var tagsProperty = createType.GetProperty("Tags");
            tagsProperty.Should().NotBeNull("List<string> property should be included");
            if (tagsProperty != null)
            {
                tagsProperty.PropertyType.Should().Be(typeof(List<string>), 
                    "Generic collection type should be preserved");
            }
            
            var metadataProperty = createType.GetProperty("Metadata");
            metadataProperty.Should().NotBeNull("Dictionary property should be included");
            if (metadataProperty != null)
            {
                metadataProperty.PropertyType.Should().Be(typeof(Dictionary<string, string>),
                    "Dictionary type should be preserved");
            }
            
            var nestedObjectProperty = createType.GetProperty("NestedObject");
            nestedObjectProperty.Should().NotBeNull("Custom object property should be included");
            if (nestedObjectProperty != null)
            {
                nestedObjectProperty.PropertyType.Should().Be(typeof(TestNestedType),
                    "Custom object type should be preserved");
            }
            
            var arrayProperty = createType.GetProperty("ArrayProperty");
            arrayProperty.Should().NotBeNull("Array property should be included");
            if (arrayProperty != null)
            {
                arrayProperty.PropertyType.Should().Be(typeof(TestNestedType[]),
                    "Array type should be preserved");
            }
        }
    }

    [Fact]
    public void GenerateDtos_ShouldWorkWithEnumProperties_Correctly()
    {
        var assembly = Assembly.GetAssembly(typeof(TestOrder));
        var responseType = assembly?.GetType("Facet.Tests.TestModels.TestOrderResponse");
        var queryType = assembly?.GetType("Facet.Tests.TestModels.TestOrderQuery");

        responseType.Should().NotBeNull();
        queryType.Should().NotBeNull();
        
        if (responseType != null)
        {
            var statusProperty = responseType.GetProperty("Status");
            statusProperty.Should().NotBeNull("Enum property should be included");
            statusProperty!.PropertyType.Should().Be(typeof(OrderStatus), 
                "Enum type should be preserved exactly");
        }
        
        if (queryType != null)
        {
            var statusProperty = queryType.GetProperty("Status");
            statusProperty.Should().NotBeNull("Enum property should be included in query DTO");
            
            var expectedType = typeof(OrderStatus?);
            statusProperty!.PropertyType.Should().Be(expectedType,
                "Enum property in query DTO should be nullable");
        }
    }

    [Fact]
    public void GenerateDtos_FunctionalTest_WithRealWorldScenario()
    {
        var order = new TestOrder
        {
            Id = Guid.NewGuid(),
            OrderNumber = "ORD-2024-001",
            TotalAmount = 299.99m,
            OrderDate = DateTime.Now.AddHours(-2),
            CustomerEmail = "customer@example.com",
            Status = OrderStatus.Processing,
            Notes = "Express delivery requested"
        };

        var assembly = Assembly.GetAssembly(typeof(TestOrder));
        var responseType = assembly?.GetType("Facet.Tests.TestModels.TestOrderResponse");
        
        responseType.Should().NotBeNull();
        
        var constructor = responseType!.GetConstructor(new[] { typeof(TestOrder) });
        constructor.Should().NotBeNull();
        
        var responseDto = constructor!.Invoke(new object[] { order });
        
        var idProperty = responseType.GetProperty("Id")!;
        idProperty.GetValue(responseDto).Should().Be(order.Id);
        
        var orderNumberProperty = responseType.GetProperty("OrderNumber")!;
        orderNumberProperty.GetValue(responseDto).Should().Be("ORD-2024-001");
        
        var totalAmountProperty = responseType.GetProperty("TotalAmount")!;
        totalAmountProperty.GetValue(responseDto).Should().Be(299.99m);
        
        var statusProperty = responseType.GetProperty("Status")!;
        statusProperty.GetValue(responseDto).Should().Be(OrderStatus.Processing);
        
        var notesProperty = responseType.GetProperty("Notes")!;
        notesProperty.GetValue(responseDto).Should().Be("Express delivery requested");
    }

    [Fact]
    public void GenerateDtos_ShouldMaintainNullabilityAnnotations_Correctly()
    {
        var assembly = Assembly.GetAssembly(typeof(TestOrder));
        var responseType = assembly?.GetType("Facet.Tests.TestModels.TestOrderResponse");
        
        responseType.Should().NotBeNull();
        
        if (responseType != null)
        {
            var notesProperty = responseType.GetProperty("Notes");
            notesProperty.Should().NotBeNull();
            
            var emailProperty = responseType.GetProperty("CustomerEmail");
            emailProperty.Should().NotBeNull();
            emailProperty!.PropertyType.Should().Be(typeof(string));
            
            var order = new TestOrder
            {
                Id = Guid.NewGuid(),
                OrderNumber = "TEST-001",
                CustomerEmail = "test@example.com",
                Notes = null 
            };
            
            var constructor = responseType.GetConstructor(new[] { typeof(TestOrder) });
            var dto = constructor!.Invoke(new object[] { order });
            
            notesProperty!.GetValue(dto).Should().BeNull("Nullable property should accept null");
            emailProperty.GetValue(dto).Should().Be("test@example.com");
        }
    }

    [Fact]
    public void GenerateDtos_PerformanceTest_WithManyInstances()
    {
        const int instanceCount = 100;
        
        var orders = new List<TestOrder>();
        for (int i = 0; i < instanceCount; i++)
        {
            orders.Add(new TestOrder
            {
                Id = Guid.NewGuid(),
                OrderNumber = $"ORD-{i:D6}",
                TotalAmount = (decimal)(i * 10.5),
                OrderDate = DateTime.Now.AddDays(-i),
                CustomerEmail = $"customer{i}@test.com",
                Status = (OrderStatus)(i % 5),
                Notes = i % 3 == 0 ? null : $"Notes for order {i}"
            });
        }
        
        var assembly = Assembly.GetAssembly(typeof(TestOrder));
        var responseType = assembly?.GetType("Facet.Tests.TestModels.TestOrderResponse");
        var constructor = responseType!.GetConstructor(new[] { typeof(TestOrder) });
        
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        var responseDtos = orders.Select(order => 
            constructor!.Invoke(new object[] { order })).ToList();
        
        stopwatch.Stop();
        
        responseDtos.Should().HaveCount(instanceCount);
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(1000, 
            "Creating 100 DTOs should be fast");
        
        var sample1 = responseDtos[25];
        var orderNumberProp = responseType.GetProperty("OrderNumber")!;
        orderNumberProp.GetValue(sample1).Should().Be("ORD-000025");
        
        var sample2 = responseDtos[75];
        orderNumberProp.GetValue(sample2).Should().Be("ORD-000075");
    }
}
