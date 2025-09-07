using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Text.Json;
using Facet.Extensions.EFCore.Tasks;
using Facet.Extensions.EFCore.Tests.Fixtures;
using Facet.Extensions.EFCore.Tests.TestData;
using Microsoft.Build.Framework;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Facet.Extensions.EFCore.Tests.Tasks;

public class ExportEfModelTaskTests : IClassFixture<TestDbContextFixture>
{
    private readonly TestDbContextFixture _fixture;
    private readonly ITestOutputHelper _output;

    public ExportEfModelTaskTests(TestDbContextFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    [Fact]
    public void Execute_WithValidAssembly_CreatesJsonFile()
    {
        // Arrange
        var tempJsonFile = Path.Combine(Path.GetTempPath(), $"test_efmodel_{Guid.NewGuid()}.json");
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

    [Fact]
    public void Execute_WithInvalidAssemblyPath_ReturnsFalse()
    {
        // Arrange
        var tempJsonFile = Path.Combine(Path.GetTempPath(), $"test_efmodel_{Guid.NewGuid()}.json");
        var invalidAssemblyPath = "/path/that/does/not/exist.dll";

        var exportTask = new ExportEfModelTask
        {
            AssemblyPath = invalidAssemblyPath,
            ContextTypes = "SomeContext",
            OutputPath = tempJsonFile,
            BuildEngine = new TestBuildEngine(_output)
        };

        try
        {
            // Act
            var result = exportTask.Execute();

            // Assert
            Assert.True(result, "Task should return true even when assembly doesn't exist (by design)");
            Assert.False(File.Exists(tempJsonFile), "JSON file should not be created for non-existent assembly");
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

    [Fact]
    public void Execute_ProducesValidJsonStructure()
    {
        // Arrange
        var tempJsonFile = Path.Combine(Path.GetTempPath(), $"test_efmodel_{Guid.NewGuid()}.json");
        var testAssemblyPath = Assembly.GetAssembly(typeof(TestDbContext))?.Location;
        
        Assert.NotNull(testAssemblyPath);

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

            // Validate JSON structure
            using var jsonDocument = JsonDocument.Parse(jsonContent);
            var root = jsonDocument.RootElement;

            Assert.True(root.TryGetProperty("Contexts", out var contextsElement), "JSON should contain 'Contexts' property");
            Assert.Equal(JsonValueKind.Array, contextsElement.ValueKind);
            
            _output.WriteLine($"JSON Content: {jsonContent}");
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

    [Fact]
    public void Execute_WithNoContextTypes_ExportsAllContexts()
    {
        // Arrange
        var tempJsonFile = Path.Combine(Path.GetTempPath(), $"test_efmodel_{Guid.NewGuid()}.json");
        var testAssemblyPath = Assembly.GetAssembly(typeof(TestDbContext))?.Location;
        
        Assert.NotNull(testAssemblyPath);

        var exportTask = new ExportEfModelTask
        {
            AssemblyPath = testAssemblyPath,
            ContextTypes = null, // Export all contexts
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
            using var jsonDocument = JsonDocument.Parse(jsonContent);
            var root = jsonDocument.RootElement;

            Assert.True(root.TryGetProperty("Contexts", out var contextsElement));
            Assert.Equal(JsonValueKind.Array, contextsElement.ValueKind);
            
            _output.WriteLine($"Contexts found: {contextsElement.GetArrayLength()}");
            _output.WriteLine($"JSON Content: {jsonContent}");
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

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Execute_WithInvalidOutputPath_ThrowsException(string? outputPath)
    {
        // Arrange
        var testAssemblyPath = Assembly.GetAssembly(typeof(TestDbContext))?.Location;
        Assert.NotNull(testAssemblyPath);

        var exportTask = new ExportEfModelTask
        {
            AssemblyPath = testAssemblyPath,
            ContextTypes = typeof(TestDbContext).FullName!,
            OutputPath = outputPath!,
            BuildEngine = new TestBuildEngine(_output)
        };

        // Act
        var result = exportTask.Execute();
        
        // Assert - The task returns false when validation fails
        Assert.False(result);
    }

    [Fact]
    public void Execute_CreatesOutputDirectory_WhenItDoesntExist()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"test_dir_{Guid.NewGuid()}");
        var tempJsonFile = Path.Combine(tempDir, "efmodel.json");
        var testAssemblyPath = Assembly.GetAssembly(typeof(TestDbContext))?.Location;
        
        Assert.NotNull(testAssemblyPath);
        Assert.False(Directory.Exists(tempDir), "Test directory should not exist initially");

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
            Assert.True(Directory.Exists(tempDir), "Output directory should be created");
            Assert.True(File.Exists(tempJsonFile), "JSON file should be created in new directory");
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }
}

/// <summary>
/// Test implementation of IBuildEngine for unit testing MSBuild tasks.
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

    public bool BuildProjectFile(string projectFileName, string[] targetNames, IDictionary globalProperties, IDictionary targetOutputs)
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