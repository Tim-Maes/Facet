using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;
using Xunit.Abstractions;
using Facet.Generators;

namespace Facet.UnitTests.SourceGeneration;

/// <summary>
/// Tests to verify the GetTypeWithNullability method behavior and document how 
/// FullyQualifiedFormat handles nullable types differently for value types vs reference types
/// </summary>
public class GetTypeWithNullabilitySimpleTest
{
    private readonly ITestOutputHelper _output;

    public GetTypeWithNullabilitySimpleTest(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void GetTypeWithNullability_TypeFormatBehavior_Documentation()
    {
        // This test documents how FullyQualifiedFormat behaves with different nullable types
        // The behavior varies: value types include '?', reference types omit '?'
        
        var source = @"
#nullable enable
public class TestClass
{
    public string? NullableString { get; set; }
    public int? NullableInt { get; set; }
    public string NonNullableString { get; set; }
    public int NonNullableInt { get; set; }
}";

        var compilation = CreateCompilation(source);
        var semanticModel = compilation.GetSemanticModel(compilation.SyntaxTrees.First());
        var root = compilation.SyntaxTrees.First().GetRoot();

        var properties = root.DescendantNodes()
            .OfType<PropertyDeclarationSyntax>()
            .ToList();

        _output.WriteLine("=== Testing GetTypeWithNullability method ===");

        foreach (var property in properties)
        {
            var symbol = semanticModel.GetDeclaredSymbol(property) as IPropertySymbol;
            if (symbol == null) continue;

            var typeSymbol = symbol.Type;
            
            // Test the actual method
            var result = GenerateDtosGenerator.GetTypeWithNullability(typeSymbol);
            
            // Show the underlying behavior
            var fullyQualifiedString = typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var endsWithQuestionMark = fullyQualifiedString.EndsWith("?");

            _output.WriteLine($"Property: {symbol.Name}");
            _output.WriteLine($"  Source: '{property.Type}'");
            _output.WriteLine($"  FullyQualifiedFormat: '{fullyQualifiedString}'");
            _output.WriteLine($"  EndsWith('?'): {endsWithQuestionMark}");
            _output.WriteLine($"  IsReferenceType: {typeSymbol.IsReferenceType}");
            _output.WriteLine($"  NullableAnnotation: {typeSymbol.NullableAnnotation}");
            _output.WriteLine($"  GetTypeWithNullability Result: '{result}'");

            // Document the actual behavior: FullyQualifiedFormat varies by type
            _output.WriteLine($"  FullyQualifiedFormat behavior: " +
                (endsWithQuestionMark ? "includes '?' for this type" : "omits '?' for this type"));

            _output.WriteLine("");
        }

        _output.WriteLine("CONCLUSION: FullyQualifiedFormat behavior varies by type:");
        _output.WriteLine("- Nullable value types (int?) include '?' in the format");
        _output.WriteLine("- Nullable reference types (string?) omit '?' and rely on NullableAnnotation metadata");
        _output.WriteLine("The GetTypeWithNullability method correctly handles both scenarios.");
    }

    [Theory]
    [InlineData("string?", "System.String?")]
    [InlineData("string", "System.String")]
    [InlineData("int?", "System.Int32?")]
    [InlineData("int", "System.Int32")]
    public void GetTypeWithNullability_VerifyBehavior(
        string sourceType, 
        string expectedResult)
    {
        var source = $@"
#nullable enable
public class TestClass
{{
    public {sourceType} TestProperty {{ get; set; }}
}}";

        var compilation = CreateCompilation(source);
        var semanticModel = compilation.GetSemanticModel(compilation.SyntaxTrees.First());
        var root = compilation.SyntaxTrees.First().GetRoot();

        var property = root.DescendantNodes().OfType<PropertyDeclarationSyntax>().First();
        var symbol = semanticModel.GetDeclaredSymbol(property) as IPropertySymbol;
        
        Assert.NotNull(symbol);
        
        var typeSymbol = symbol.Type;
        var fullyQualifiedString = typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        // Verify our understanding of the type symbol
        _output.WriteLine($"Testing: {sourceType}");
        _output.WriteLine($"  FullyQualified: {fullyQualifiedString}");
        _output.WriteLine($"  IsReferenceType: {typeSymbol.IsReferenceType}");
        _output.WriteLine($"  NullableAnnotation: {typeSymbol.NullableAnnotation}");

        // Note: FullyQualifiedFormat behavior varies by type:
        // - Nullable value types (int?): includes '?' -> "int?"
        // - Nullable reference types (string?): omits '?' -> "string" 
        _output.WriteLine($"  FullyQualifiedFormat includes '?': {fullyQualifiedString.EndsWith("?")}");

        // Test the actual method
        var result = GenerateDtosGenerator.GetTypeWithNullability(typeSymbol);
        _output.WriteLine($"  GetTypeWithNullability Result: '{result}'");

        Assert.Equal(expectedResult, result);
    }

    private static Compilation CreateCompilation(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        
        var references = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(DateTime).Assembly.Location)
        };

        return CSharpCompilation.Create(
            "TestAssembly",
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));
    }
}