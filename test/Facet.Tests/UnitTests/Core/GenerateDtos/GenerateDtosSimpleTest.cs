using Facet.Tests.TestModels;
using System.Reflection;

namespace Facet.Tests.UnitTests.Core.GenerateDtos;

public class GenerateDtosSimpleTest
{
    [Fact]
    public void GeneratedDtos_ShouldBeCreated()
    {
        var responseType = typeof(TestUserResponse);
        responseType.Should().NotBeNull();
        
        var assembly = Assembly.GetAssembly(typeof(TestUser));
        var allTypes = assembly?.GetTypes()
            .Where(t => t.Name.StartsWith("TestUser"))
            .ToList() ?? new List<Type>();

        allTypes.Should().Contain(t => t.Name == "TestUserResponse");
    }
    
    [Fact]
    public void GeneratedDto_ShouldHaveFacetAttribute()
    {
        var responseType = typeof(TestUserResponse);
        var attributes = responseType.GetCustomAttributes(typeof(FacetAttribute), false);
        
        attributes.Should().NotBeEmpty("Generated DTOs should have [Facet] attribute");
    }
    
    [Fact]
    public void ToFacet_ShouldWork_WithBasicMapping()
    {
        var user = new TestUser
        {
            Id = 1,
            FirstName = "John",
            LastName = "Doe",
            Email = "john@example.com"
        };

        var responseDto = user.ToFacet<TestUserResponse>();
        
        responseDto.Should().NotBeNull();
    }
    
    [Fact]
    public void AvailableGeneratedTypes_ShouldIncludeExpectedDtos()
    {
        var assembly = Assembly.GetAssembly(typeof(TestUser));
        var testUserTypes = assembly?.GetTypes()
            .Where(t => t.Name.StartsWith("TestUser"))
            .Select(t => t.Name)
            .OrderBy(name => name)
            .ToList() ?? new List<string>();
        
        testUserTypes.Should().Contain("TestUserResponse");

        testUserTypes.Count.Should().BeGreaterThan(1, "DtoTypes.All should generate multiple DTOs");
    }
}
