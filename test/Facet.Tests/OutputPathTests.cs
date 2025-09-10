using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Linq;
using Xunit;

namespace Facet.Tests
{
    public class OutputPathTests
    {
        [Fact]
        public void FacetWithOutputPath_EmitsDiagnostic()
        {
            // Arrange
            var sourceCode = @"
using System;

public class User
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

[Facet.Facet(typeof(User), ""Password"", OutputPath = ""Generated/Dtos"")]
public partial class UserDto;
";

            // Act
            var compilation = CreateCompilationWithFacet(sourceCode);
            var diagnostics = compilation.GetDiagnostics();

            // Assert
            var facetDiagnostic = diagnostics.FirstOrDefault(d => d.Id == "FACET_OUTPUT_PATH");
            Assert.NotNull(facetDiagnostic);
            Assert.Equal(DiagnosticSeverity.Info, facetDiagnostic.Severity);
            Assert.Contains("UserDto", facetDiagnostic.GetMessage());
            Assert.Contains("Generated/Dtos", facetDiagnostic.GetMessage());
        }

        [Fact]
        public void FacetWithoutOutputPath_DoesNotEmitDiagnostic()
        {
            // Arrange
            var sourceCode = @"
using System;

public class User
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

[Facet.Facet(typeof(User), ""Password"")]
public partial class UserDto;
";

            // Act
            var compilation = CreateCompilationWithFacet(sourceCode);
            var diagnostics = compilation.GetDiagnostics();

            // Assert
            var facetDiagnostic = diagnostics.FirstOrDefault(d => d.Id == "FACET_OUTPUT_PATH");
            Assert.Null(facetDiagnostic);
        }

        [Fact]
        public void FacetWithSpecificFilePath_EmitsDiagnostic()
        {
            // Arrange
            var sourceCode = @"
using System;

public class User
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

[Facet.Facet(typeof(User), ""Password"", OutputPath = ""Models/SafeUser.cs"")]
public partial class SafeUserDto;
";

            // Act
            var compilation = CreateCompilationWithFacet(sourceCode);
            var diagnostics = compilation.GetDiagnostics();

            // Assert
            var facetDiagnostic = diagnostics.FirstOrDefault(d => d.Id == "FACET_OUTPUT_PATH");
            Assert.NotNull(facetDiagnostic);
            Assert.Contains("SafeUserDto", facetDiagnostic.GetMessage());
            Assert.Contains("Models/SafeUser.cs", facetDiagnostic.GetMessage());
        }

        private static Compilation CreateCompilationWithFacet(string sourceCode)
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
            
            var compilation = CSharpCompilation.Create(
                "TestAssembly",
                new[] { syntaxTree },
                new[]
                {
                    MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                    MetadataReference.CreateFromFile(typeof(System.ComponentModel.DataAnnotations.RequiredAttribute).Assembly.Location)
                },
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            // Add the Facet source generator
            var generator = new Facet.Generators.FacetGenerator();
            var driver = CSharpGeneratorDriver.Create(generator);
            
            var result = driver.RunGenerators(compilation);
            return result.Compilation;
        }
    }
}