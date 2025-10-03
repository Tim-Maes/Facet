using Facet.Extensions;
using Facet.Tests.TestModels;
using System.Reflection;

namespace Facet.Tests.UnitTests.Core.GenerateDtos;

/// <summary>
/// Simple test to verify that DTOs are generated and can be instantiated
/// </summary>
public class GenerateDtosSimpleTest
{
    [Fact]
    public void GeneratedDtos_ShouldBeCreated()
    {
        // This simple test just verifies the generated types exist
        var responseType = typeof(TestUserResponse);
        responseType.Should().NotBeNull();
        
        // Check what DTOs are actually available in this assembly
        var assembly = Assembly.GetAssembly(typeof(TestUser));
        var allTypes = assembly?.GetTypes()
            .Where(t => t.Name.StartsWith("TestUser"))
            .ToList() ?? new List<Type>();
        
        // We know TestUserResponse exists based on compilation
        allTypes.Should().Contain(t => t.Name == "TestUserResponse");
        
        // Log available types for debugging
        var typeNames = string.Join(", ", allTypes.Select(t => t.Name));
        Console.WriteLine($"Available TestUser types: {typeNames}");
    }
    
    [Fact]
    public void GeneratedDto_ShouldHaveFacetAttribute()
    {
        // Check if the generated DTO has the Facet attribute
        var responseType = typeof(TestUserResponse);
        var attributes = responseType.GetCustomAttributes(typeof(FacetAttribute), false);
        
        attributes.Should().NotBeEmpty("Generated DTOs should have [Facet] attribute");
    }
    
    [Fact]
    public void ToFacet_ShouldWork_WithBasicMapping()
    {
        // Arrange
        var user = new TestUser
        {
            Id = 1,
            FirstName = "John",
            LastName = "Doe",
            Email = "john@example.com"
        };

        // Act - Simple conversion test
        var responseDto = user.ToFacet<TestUserResponse>();
        
        // Assert
        responseDto.Should().NotBeNull();
    }
    
    [Fact]
    public void AvailableGeneratedTypes_ShouldIncludeExpectedDtos()
    {
        // Get all types in the test assembly that start with TestUser
        var assembly = Assembly.GetAssembly(typeof(TestUser));
        var testUserTypes = assembly?.GetTypes()
            .Where(t => t.Name.StartsWith("TestUser"))
            .Select(t => t.Name)
            .OrderBy(name => name)
            .ToList() ?? new List<string>();
        
        // We should have at least TestUserResponse
        testUserTypes.Should().Contain("TestUserResponse");
        
        // Check what other DTOs were generated
        Console.WriteLine($"Generated DTOs: {string.Join(", ", testUserTypes)}");
        
        // The test entity specifies DtoTypes.All, so we should have multiple DTOs
        testUserTypes.Count.Should().BeGreaterThan(1, "DtoTypes.All should generate multiple DTOs");
    }
}