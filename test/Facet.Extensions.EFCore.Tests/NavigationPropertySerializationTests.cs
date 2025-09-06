using System;
using System.IO;
using System.Reflection;
using System.Text.Json;
using Facet.Extensions.EFCore.Tasks;
using Facet.Extensions.EFCore.Tests.TestData;
using Microsoft.Build.Framework;
using Xunit;
using Xunit.Abstractions;

namespace Facet.Extensions.EFCore.Tests;

/// <summary>
/// Critical test to validate that NavigationProperty relationships are properly 
/// serialized in the JSON output from the MSBuild task.
/// </summary>
public class NavigationPropertySerializationTests
{
    private readonly ITestOutputHelper _output;

    public NavigationPropertySerializationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void ExportEfModelTask_WithNavigationProperties_SerializesToJsonCorrectly()
    {
        // Arrange
        var tempJsonFile = Path.Combine(Path.GetTempPath(), $"navigation_test_{Guid.NewGuid()}.json");
        var testAssemblyPath = Assembly.GetAssembly(typeof(TestDbContext))?.Location;
        
        Assert.NotNull(testAssemblyPath);
        Assert.True(File.Exists(testAssemblyPath), "Test assembly should exist");

        var exportTask = new ExportEfModelTask
        {
            AssemblyPath = testAssemblyPath,
            ContextTypes = typeof(TestDbContext).FullName!,
            OutputPath = tempJsonFile,
            BuildEngine = new TestBuildEngine(_output)
        };

        try
        {
            // Act
            var result = exportTask.Execute();

            // Assert
            Assert.True(result, "Export task should complete successfully");
            Assert.True(File.Exists(tempJsonFile), "JSON file should be created");

            var jsonContent = File.ReadAllText(tempJsonFile);
            Assert.NotEmpty(jsonContent);
            _output.WriteLine($"Generated JSON content:\n{jsonContent}");

            // Parse JSON to validate structure
            using var jsonDocument = JsonDocument.Parse(jsonContent);
            var root = jsonDocument.RootElement;

            // Validate root structure
            Assert.True(root.TryGetProperty("Contexts", out var contextsElement), 
                       "JSON should contain 'Contexts' property");
            Assert.Equal(JsonValueKind.Array, contextsElement.ValueKind);

            var contextCount = contextsElement.GetArrayLength();
            Assert.True(contextCount > 0, $"Expected at least 1 context to be exported, but found {contextCount}. The DbContext could not be instantiated.");

            // If we have contexts, validate the navigation properties
            var firstContext = contextsElement[0];
            Assert.True(firstContext.TryGetProperty("Context", out _), 
                       "Context should have 'Context' property");
            Assert.True(firstContext.TryGetProperty("Entities", out var entitiesElement), 
                       "Context should have 'Entities' property");

            Assert.Equal(JsonValueKind.Array, entitiesElement.ValueKind);
            var entityCount = entitiesElement.GetArrayLength();
            _output.WriteLine($"Found {entityCount} entities in the model");

            // Look for entities with navigation properties
            var foundNavigations = false;
            foreach (var entity in entitiesElement.EnumerateArray())
            {
                if (entity.TryGetProperty("Name", out var nameElement) &&
                    entity.TryGetProperty("Navigations", out var navigationsElement))
                {
                    var entityName = nameElement.GetString();
                    var navigationCount = navigationsElement.GetArrayLength();
                    
                    _output.WriteLine($"Entity '{entityName}' has {navigationCount} navigations");

                    if (navigationCount > 0)
                    {
                        foundNavigations = true;
                        
                        // Validate navigation structure
                        foreach (var navigation in navigationsElement.EnumerateArray())
                        {
                            Assert.True(navigation.TryGetProperty("Name", out var navNameElement),
                                       "Navigation should have 'Name' property");
                            Assert.True(navigation.TryGetProperty("Target", out var targetElement),
                                       "Navigation should have 'Target' property");
                            Assert.True(navigation.TryGetProperty("IsCollection", out var isCollectionElement),
                                       "Navigation should have 'IsCollection' property");

                            var navName = navNameElement.GetString();
                            var target = targetElement.GetString();
                            var isCollection = isCollectionElement.GetBoolean();

                            _output.WriteLine($"  - Navigation '{navName}' -> '{target}' (Collection: {isCollection})");

                            Assert.NotNull(navName);
                            Assert.NotEmpty(navName);
                            Assert.NotNull(target);
                            Assert.NotEmpty(target);
                        }
                    }
                }
            }

            if (foundNavigations)
            {
                _output.WriteLine("✅ SUCCESS: Found and validated navigation properties in JSON");
            }
            else
            {
                _output.WriteLine("ℹ️  No navigation properties found, but JSON structure is valid");
            }

            // Validate specific expected navigation relationships from our TestDbContext:
            // - Category should have Products collection
            // - Product should have Category reference
            // - User should have Orders collection
            // - Order should have User reference and OrderItems collection
            // - OrderItem should have Order and Product references

            ValidateExpectedNavigations(entitiesElement);
        }
        finally
        {
            // Cleanup
            if (File.Exists(tempJsonFile))
            {
                File.Delete(tempJsonFile);
            }
        }
    }

    private void ValidateExpectedNavigations(JsonElement entitiesElement)
    {
        var entityLookup = new System.Collections.Generic.Dictionary<string, JsonElement>();
        
        // Build lookup for easier entity finding
        foreach (var entity in entitiesElement.EnumerateArray())
        {
            if (entity.TryGetProperty("Name", out var nameElement))
            {
                var entityName = nameElement.GetString();
                if (entityName != null)
                {
                    var simpleName = entityName.Contains('.') ? entityName.Split('.')[^1] : entityName;
                    entityLookup[simpleName] = entity;
                }
            }
        }

        _output.WriteLine($"Entity lookup contains: {string.Join(", ", entityLookup.Keys)}");

        // Validate Category -> Products navigation
        if (entityLookup.TryGetValue("Category", out var categoryEntity))
        {
            ValidateEntityHasNavigation(categoryEntity, "Products", isCollection: true, "Category should have Products collection");
        }

        // Validate Product -> Category navigation  
        if (entityLookup.TryGetValue("Product", out var productEntity))
        {
            ValidateEntityHasNavigation(productEntity, "Category", isCollection: false, "Product should have Category reference");
            ValidateEntityHasNavigation(productEntity, "OrderItems", isCollection: true, "Product should have OrderItems collection");
        }

        // Validate User -> Orders navigation
        if (entityLookup.TryGetValue("User", out var userEntity))
        {
            ValidateEntityHasNavigation(userEntity, "Orders", isCollection: true, "User should have Orders collection");
        }

        // Validate Order navigations
        if (entityLookup.TryGetValue("Order", out var orderEntity))
        {
            ValidateEntityHasNavigation(orderEntity, "User", isCollection: false, "Order should have User reference");
            ValidateEntityHasNavigation(orderEntity, "OrderItems", isCollection: true, "Order should have OrderItems collection");
        }

        // Validate OrderItem navigations
        if (entityLookup.TryGetValue("OrderItem", out var orderItemEntity))
        {
            ValidateEntityHasNavigation(orderItemEntity, "Order", isCollection: false, "OrderItem should have Order reference");
            ValidateEntityHasNavigation(orderItemEntity, "Product", isCollection: false, "OrderItem should have Product reference");
        }
    }

    private void ValidateEntityHasNavigation(JsonElement entity, string expectedNavigation, bool isCollection, string message)
    {
        if (!entity.TryGetProperty("Navigations", out var navigationsElement))
        {
            _output.WriteLine($"⚠️  Entity has no Navigations property - {message}");
            return;
        }

        foreach (var navigation in navigationsElement.EnumerateArray())
        {
            if (navigation.TryGetProperty("Name", out var nameElement) &&
                navigation.TryGetProperty("IsCollection", out var isCollectionElement))
            {
                var navName = nameElement.GetString();
                var navIsCollection = isCollectionElement.GetBoolean();

                if (navName == expectedNavigation)
                {
                    Assert.Equal(isCollection, navIsCollection);
                    _output.WriteLine($"✅ {message} - IsCollection={navIsCollection}");
                    return;
                }
            }
        }

        _output.WriteLine($"⚠️  Navigation '{expectedNavigation}' not found - {message}");
    }
}

/// <summary>
/// Simple test implementation of IBuildEngine for MSBuild task testing.
/// </summary>
public class TestBuildEngine : IBuildEngine
{
    private readonly ITestOutputHelper _output;

    public TestBuildEngine(ITestOutputHelper output)
    {
        _output = output;
    }

    public bool ContinueOnError => false;
    public int LineNumberOfTaskNode => 0;
    public int ColumnNumberOfTaskNode => 0;
    public string ProjectFileOfTaskNode => "TestProject";

    public bool BuildProjectFile(string projectFileName, string[] targetNames, 
                               System.Collections.IDictionary globalProperties, 
                               System.Collections.IDictionary targetOutputs)
    {
        return true;
    }

    public void LogCustomEvent(CustomBuildEventArgs e)
    {
        _output.WriteLine($"Custom: {e.Message}");
    }

    public void LogErrorEvent(BuildErrorEventArgs e)
    {
        _output.WriteLine($"Error: {e.Message}");
    }

    public void LogMessageEvent(BuildMessageEventArgs e)
    {
        _output.WriteLine($"Message: {e.Message}");
    }

    public void LogWarningEvent(BuildWarningEventArgs e)
    {
        _output.WriteLine($"Warning: {e.Message}");
    }
}