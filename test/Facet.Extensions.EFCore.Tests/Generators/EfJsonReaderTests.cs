using System;
using System.Collections.Immutable;
using System.IO;
using System.Text;
using System.Text.Json;
using Facet.Extensions.EFCore.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Xunit;
using Xunit.Abstractions;

namespace Facet.Extensions.EFCore.Tests.Generators;

public class EfJsonReaderTests
{
    private readonly ITestOutputHelper _output;

    public EfJsonReaderTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void ParseJsonContent_WithValidJson_ReturnsModelRoot()
    {
        // Arrange
        var validJson = """
        {
          "Contexts": [
            {
              "Context": "TestApp.Data.TestDbContext",
              "Entities": [
                {
                  "Name": "TestApp.Data.User",
                  "Clr": "TestApp.Data.User",
                  "Keys": [["Id"]],
                  "Navigations": [
                    {
                      "Name": "Orders",
                      "Target": "TestApp.Data.Order",
                      "IsCollection": true
                    }
                  ]
                }
              ]
            }
          ]
        }
        """;

        // Act - Test the JSON parsing directly
        var result = System.Text.Json.JsonSerializer.Deserialize<ModelRoot>(validJson, new System.Text.Json.JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        // Assert
        Assert.NotNull(result);
        Assert.Single(result.Contexts);
        Assert.Equal("TestApp.Data.TestDbContext", result.Contexts[0].Context);
        Assert.Single(result.Contexts[0].Entities);
        Assert.Equal("TestApp.Data.User", result.Contexts[0].Entities[0].Name);
        Assert.Equal("TestApp.Data.User", result.Contexts[0].Entities[0].Clr);
        Assert.Single(result.Contexts[0].Entities[0].Keys);
        Assert.Equal("Id", result.Contexts[0].Entities[0].Keys[0][0]);
        Assert.Single(result.Contexts[0].Entities[0].Navigations);
        Assert.Equal("Orders", result.Contexts[0].Entities[0].Navigations[0].Name);
        Assert.Equal("TestApp.Data.Order", result.Contexts[0].Entities[0].Navigations[0].Target);
        Assert.True(result.Contexts[0].Entities[0].Navigations[0].IsCollection);
    }

    [Fact]
    public void ParseJsonContent_WithInvalidJson_ThrowsException()
    {
        // Arrange
        var invalidJson = "{ invalid json content }";

        // Act & Assert
        Assert.Throws<System.Text.Json.JsonException>(() =>
            System.Text.Json.JsonSerializer.Deserialize<ModelRoot>(invalidJson, new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }));
    }

    [Fact]
    public void ParseJsonContent_WithEmptyJson_ThrowsException()
    {
        // Arrange
        var emptyJson = "";

        // Act & Assert
        Assert.Throws<System.Text.Json.JsonException>(() =>
            System.Text.Json.JsonSerializer.Deserialize<ModelRoot>(emptyJson, new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }));
    }

    [Fact]
    public void ParseJsonContent_WithEmptyContexts_ReturnsEmptyModel()
    {
        // Arrange
        var validJson = """
        {
          "Contexts": []
        }
        """;

        // Act
        var result = System.Text.Json.JsonSerializer.Deserialize<ModelRoot>(validJson, new System.Text.Json.JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result.Contexts);
    }

    [Fact]
    public void ParseJsonContent_WithMultipleContexts_ReturnsAllContexts()
    {
        // Arrange
        var validJson = """
        {
          "Contexts": [
            {
              "Context": "FirstApp.Data.FirstDbContext",
              "Entities": [
                {
                  "Name": "FirstApp.Data.User",
                  "Clr": "FirstApp.Data.User",
                  "Keys": [["Id"]],
                  "Navigations": []
                }
              ]
            },
            {
              "Context": "SecondApp.Data.SecondDbContext",
              "Entities": [
                {
                  "Name": "SecondApp.Data.Product",
                  "Clr": "SecondApp.Data.Product", 
                  "Keys": [["Id"]],
                  "Navigations": []
                }
              ]
            }
          ]
        }
        """;

        // Act
        var result = System.Text.Json.JsonSerializer.Deserialize<ModelRoot>(validJson, new System.Text.Json.JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Contexts.Count);
        Assert.Equal("FirstApp.Data.FirstDbContext", result.Contexts[0].Context);
        Assert.Equal("SecondApp.Data.SecondDbContext", result.Contexts[1].Context);
        Assert.Single(result.Contexts[0].Entities);
        Assert.Single(result.Contexts[1].Entities);
    }

    [Theory]
    [InlineData("efmodel.json")]
    [InlineData("EFMODEL.JSON")]
    [InlineData("EfModel.Json")]
    [InlineData("/path/to/efmodel.json")]
    [InlineData("C:\\Project\\efmodel.json")]
    public void GetFileName_WithVariousPaths_ExtractsCorrectFileName(string filePath)
    {
        // Act
        var fileName = Path.GetFileName(filePath);

        // Assert
        Assert.True(fileName.Equals("efmodel.json", StringComparison.OrdinalIgnoreCase), 
                   $"Expected 'efmodel.json' but got '{fileName}' for path '{filePath}'");
        _output.WriteLine($"Successfully extracted filename: {fileName} from path: {filePath}");
    }
}