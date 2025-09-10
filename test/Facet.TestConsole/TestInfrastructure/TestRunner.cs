using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Facet.TestConsole.Tests;
using Facet.TestConsole.GenerateDtosTests;
using Facet.TestConsole.InMemoryTests;

namespace Facet.TestConsole.TestInfrastructure;

public class TestRunner
{
    public static async Task RunAllTestsAsync(IHost host)
    {
        var overallStartTime = DateTime.Now;

        try
        {
            await RunDatabaseInitializationAsync(host);
            await RunGenerateDtosTestsAsync(host);
            await RunInMemoryTestsAsync();
            await RunEfCoreTestsAsync(host);
            RunStandaloneTests();
        }
        catch (Exception ex)
        {
            TestLogger.LogTestResult("Test Execution", false, ex.Message);
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n[FATAL ERROR]: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            Console.ResetColor();
        }

        TestLogger.PrintSummary();
    }

    private static async Task RunDatabaseInitializationAsync(IHost host)
    {
        TestLogger.LogTestStart("Database Initialization");
        var startTime = DateTime.Now;
        
        try
        {
            await Program.InitializeDatabaseAsync(host);
            TestLogger.LogTestResult("Database Initialization", true, duration: DateTime.Now - startTime);
        }
        catch (Exception ex)
        {
            TestLogger.LogTestResult("Database Initialization", false, ex.Message, DateTime.Now - startTime);
            throw;
        }
    }

    private static async Task RunGenerateDtosTestsAsync(IHost host)
    {
        TestLogger.LogTestStart("GenerateDtos Feature Tests");
        var startTime = DateTime.Now;
        
        try
        {
            using var scope = host.Services.CreateScope();
            var generateDtosTests = scope.ServiceProvider.GetRequiredService<GenerateDtosFeatureTests>();
            await generateDtosTests.RunAllTestsAsync();
            TestLogger.LogTestResult("GenerateDtos Feature Tests", true, duration: DateTime.Now - startTime);
        }
        catch (Exception ex)
        {
            TestLogger.LogTestResult("GenerateDtos Feature Tests", false, ex.Message, DateTime.Now - startTime);
            throw;
        }
    }

    private static async Task RunInMemoryTestsAsync()
    {
        TestLogger.LogTestStart("In-Memory Facet Tests");
        var startTime = DateTime.Now;
        
        try
        {
            var testSuites = new List<ITestSuite>
            {
                new InheritanceTests(),
                new ModernRecordTests(),
                new BasicMappingTests(),
                new CustomMappingTests(),
                new FacetKindsTests(),
                new LinqProjectionTests(),
                new ShorthandOverloadTests(),
                new InMemoryTests.ParameterlessConstructorTests(),
                new NestedPartialsTests(),
                new UseFullNameFeatureTests()
            };

            var allResults = new List<(string name, bool passed)>();
            
            foreach (var testSuite in testSuites)
            {
                var results = await testSuite.RunTestsAsync();
                allResults.AddRange(results);
            }
            
            var passedCount = allResults.Count(r => r.passed);
            var totalCount = allResults.Count;
            
            TestLogger.LogTestResult("In-Memory Facet Tests", passedCount == totalCount, 
                passedCount < totalCount ? $"{totalCount - passedCount} tests failed" : null, 
                DateTime.Now - startTime, totalCount, passedCount, totalCount - passedCount);
        }
        catch (Exception ex)
        {
            TestLogger.LogTestResult("In-Memory Facet Tests", false, ex.Message, DateTime.Now - startTime);
            throw;
        }
    }

    private static async Task RunEfCoreTestsAsync(IHost host)
    {
        TestLogger.LogTestStart("EF Core Integration Tests");
        var startTime = DateTime.Now;
        
        try
        {
            using var scope = host.Services.CreateScope();
            
            var updateFromFacetTests = scope.ServiceProvider.GetRequiredService<UpdateFromFacetTests>();
            await updateFromFacetTests.RunAllTestsAsync();

            var efCoreIntegrationTests = scope.ServiceProvider.GetRequiredService<EfCoreIntegrationTests>();
            await efCoreIntegrationTests.RunAllTestsAsync();

            var validationAndErrorTests = scope.ServiceProvider.GetRequiredService<ValidationAndErrorTests>();
            await validationAndErrorTests.RunAllTestsAsync();
            
            TestLogger.LogTestResult("EF Core Integration Tests", true, duration: DateTime.Now - startTime);
        }
        catch (Exception ex)
        {
            TestLogger.LogTestResult("EF Core Integration Tests", false, ex.Message, DateTime.Now - startTime);
            throw;
        }
    }

    private static void RunStandaloneTests()
    {
        var tests = new (string testName, Action testAction)[]
        {
            ("Record Primary Constructor Tests", () => Tests.RecordPrimaryConstructorTests.RunAllTests()),
            ("Parameterless Constructor Tests", () => Tests.ParameterlessConstructorTests.RunAllTests()),
            ("Enum Handling Tests", () => Tests.EnumHandlingTests.RunAllTests())
        };

        foreach (var (testName, testAction) in tests)
        {
            TestLogger.LogTestStart(testName);
            var startTime = DateTime.Now;
            
            try
            {
                testAction();
                TestLogger.LogTestResult(testName, true, duration: DateTime.Now - startTime);
            }
            catch (Exception ex)
            {
                TestLogger.LogTestResult(testName, false, ex.Message, DateTime.Now - startTime);
            }
        }
    }
}