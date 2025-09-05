using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Facet.Extensions.EFCore.Tasks;
using Facet.Extensions.EFCore.Tests.TestData;
using Microsoft.Build.Framework;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Facet.Extensions.EFCore.Tests.VerifyTests;

/// <summary>
/// Tests that validate NavigationProperty serialization using Verify for robust snapshot testing.
/// This ensures the JSON output matches expected format and content.
/// </summary>
public class NavigationPropertyVerifyTests : VerifyBase, IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ITestOutputHelper _output;

    public NavigationPropertyVerifyTests(ITestOutputHelper output) : base()
    {
        _output = output;
        
        var services = new ServiceCollection();
        
        // Configure EF Core with InMemory database
        services.AddDbContext<TestDbContext>(options =>
            options.UseInMemoryDatabase($"VerifyTest_{Guid.NewGuid()}")
                   .EnableSensitiveDataLogging());
        
        // Add logging
        services.AddLogging(builder =>
            builder.AddConsole()
                   .SetMinimumLevel(LogLevel.Information));

        _serviceProvider = services.BuildServiceProvider();
    }

    [Fact]
    public async Task ExportEfModelTask_NavigationProperties_MatchesExpectedJson()
    {
        // Arrange - setup and seed the database
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TestDbContext>();
        
        await context.Database.EnsureCreatedAsync();
        await SeedTestDataAsync(context);
        
        // Arrange - prepare MSBuild task
        var tempJsonFile = Path.Combine(Path.GetTempPath(), $"verify_test_{Guid.NewGuid()}.json");
        var testAssemblyPath = Assembly.GetAssembly(typeof(TestDbContext))?.Location;
        
        Assert.NotNull(testAssemblyPath);

        var exportTask = new ExportEfModelTask
        {
            AssemblyPath = testAssemblyPath,
            ContextTypes = typeof(TestDbContext).FullName!,
            OutputPath = tempJsonFile,
            BuildEngine = new VerifyTestBuildEngine(_output)
        };

        try
        {
            // Act
            var result = exportTask.Execute();
            Assert.True(result, "Export task should complete successfully");
            
            var jsonContent = await File.ReadAllTextAsync(tempJsonFile);
            _output.WriteLine($"Generated JSON ({jsonContent.Length} chars)");
            
            // Parse to ensure it's valid JSON
            var jsonDocument = JsonDocument.Parse(jsonContent);
            var root = jsonDocument.RootElement;
            
            // Convert to a more readable object for verification
            var modelData = new
            {
                HasContexts = root.TryGetProperty("Contexts", out var contexts),
                ContextCount = contexts.ValueKind == JsonValueKind.Array ? contexts.GetArrayLength() : 0,
                JsonContent = JsonSerializer.Serialize(JsonSerializer.Deserialize<object>(jsonContent), new JsonSerializerOptions 
                { 
                    WriteIndented = true 
                })
            };
            
            // Assert basic structure first
            Assert.True(modelData.HasContexts, "JSON should have Contexts property");
            Assert.True(modelData.ContextCount > 0, $"Should have at least 1 context, found {modelData.ContextCount}");
            
            // Use Verify to validate the JSON content matches expected structure
            await Verify(modelData)
                .UseParameters("NavigationPropertyExport")
                .UseDirectory("__snapshots__");
        }
        finally
        {
            if (File.Exists(tempJsonFile))
            {
                File.Delete(tempJsonFile);
            }
        }
    }

    [Fact]
    public async Task ExportEfModelTask_NavigationProperties_AllEntitiesPresent()
    {
        // This test focuses on validating that all expected entities and their navigations are present
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TestDbContext>();
        
        await context.Database.EnsureCreatedAsync();
        await SeedTestDataAsync(context);
        
        var tempJsonFile = Path.Combine(Path.GetTempPath(), $"verify_entities_{Guid.NewGuid()}.json");
        var testAssemblyPath = Assembly.GetAssembly(typeof(TestDbContext))?.Location;
        
        Assert.NotNull(testAssemblyPath);

        var exportTask = new ExportEfModelTask
        {
            AssemblyPath = testAssemblyPath,
            ContextTypes = typeof(TestDbContext).FullName!,
            OutputPath = tempJsonFile,
            BuildEngine = new VerifyTestBuildEngine(_output)
        };

        try
        {
            var result = exportTask.Execute();
            Assert.True(result);
            
            var jsonContent = await File.ReadAllTextAsync(tempJsonFile);
            var jsonDocument = JsonDocument.Parse(jsonContent);
            
            // Extract entity and navigation information for verification
            var entityInfo = ExtractEntityNavigationInfo(jsonDocument);
            
            // Use Verify to validate the entity structure
            await Verify(entityInfo)
                .UseParameters("EntityNavigations")
                .UseDirectory("__snapshots__");
                
            // Basic assertions to ensure we have expected data
            var entityInfoDynamic = (dynamic)entityInfo;
            Assert.True(entityInfoDynamic.EntityCount >= 5, $"Should have at least 5 entities, found {entityInfoDynamic.EntityCount}");
            Assert.True(entityInfoDynamic.TotalNavigations >= 8, $"Should have at least 8 navigation properties, found {entityInfoDynamic.TotalNavigations}");
        }
        finally
        {
            if (File.Exists(tempJsonFile))
            {
                File.Delete(tempJsonFile);
            }
        }
    }

    private static object ExtractEntityNavigationInfo(JsonDocument jsonDocument)
    {
        var root = jsonDocument.RootElement;
        var entities = new List<object>();
        var totalNavigations = 0;

        if (root.TryGetProperty("Contexts", out var contexts) && 
            contexts.ValueKind == JsonValueKind.Array && 
            contexts.GetArrayLength() > 0)
        {
            var firstContext = contexts[0];
            if (firstContext.TryGetProperty("Entities", out var entitiesElement))
            {
                foreach (var entity in entitiesElement.EnumerateArray())
                {
                    var entityName = entity.TryGetProperty("Name", out var nameElement) ? 
                        nameElement.GetString()?.Split('.').LastOrDefault() ?? "Unknown" : "Unknown";
                    
                    var navigations = new List<object>();
                    if (entity.TryGetProperty("Navigations", out var navElements))
                    {
                        foreach (var nav in navElements.EnumerateArray())
                        {
                            var navName = nav.TryGetProperty("Name", out var navNameEl) ? navNameEl.GetString() : "Unknown";
                            var navTarget = nav.TryGetProperty("Target", out var navTargetEl) ? 
                                navTargetEl.GetString()?.Split('.').LastOrDefault() ?? "Unknown" : "Unknown";
                            var isCollection = nav.TryGetProperty("IsCollection", out var isCollEl) && isCollEl.GetBoolean();
                            
                            navigations.Add(new { Name = navName, Target = navTarget, IsCollection = isCollection });
                            totalNavigations++;
                        }
                    }
                    
                    entities.Add(new 
                    { 
                        Entity = entityName, 
                        NavigationCount = navigations.Count, 
                        Navigations = navigations 
                    });
                }
            }
        }

        return new
        {
            EntityCount = entities.Count,
            TotalNavigations = totalNavigations,
            Entities = entities.OrderBy(e => ((dynamic)e).Entity).ToList()
        };
    }

    private async Task SeedTestDataAsync(TestDbContext context)
    {
        if (await context.Categories.AnyAsync())
            return;

        var category = new Category
        {
            Name = "Electronics",
            Description = "Electronic devices and gadgets"
        };
        context.Categories.Add(category);

        var product = new Product
        {
            Name = "MacBook Pro",
            Description = "High-performance laptop",
            Price = 1299.99m,
            Category = category
        };
        context.Products.Add(product);

        var user = new User
        {
            FirstName = "John",
            LastName = "Doe",
            Email = "john.doe@example.com"
        };
        context.Users.Add(user);

        var order = new Order
        {
            User = user,
            TotalAmount = 1299.99m,
            Status = "Completed"
        };
        context.Orders.Add(order);

        var orderItem = new OrderItem
        {
            Order = order,
            Product = product,
            Quantity = 1,
            UnitPrice = 1299.99m
        };
        context.OrderItems.Add(orderItem);

        await context.SaveChangesAsync();
    }

    public void Dispose()
    {
        if (_serviceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}

/// <summary>
/// Test build engine for Verify tests.
/// </summary>
public class VerifyTestBuildEngine : IBuildEngine
{
    private readonly ITestOutputHelper _output;

    public VerifyTestBuildEngine(ITestOutputHelper output)
    {
        _output = output;
    }

    public bool ContinueOnError => false;
    public int LineNumberOfTaskNode => 0;
    public int ColumnNumberOfTaskNode => 0;
    public string ProjectFileOfTaskNode => "VerifyTest";

    public bool BuildProjectFile(string projectFileName, string[] targetNames,
                               System.Collections.IDictionary globalProperties,
                               System.Collections.IDictionary targetOutputs)
    {
        return true;
    }

    public void LogCustomEvent(CustomBuildEventArgs e)
    {
        _output.WriteLine($"[Custom] {e.Message}");
    }

    public void LogErrorEvent(BuildErrorEventArgs e)
    {
        _output.WriteLine($"[ERROR] {e.Message}");
    }

    public void LogMessageEvent(BuildMessageEventArgs e)
    {
        _output.WriteLine($"[Message] {e.Message}");
    }

    public void LogWarningEvent(BuildWarningEventArgs e)
    {
        _output.WriteLine($"[Warning] {e.Message}");
    }
}