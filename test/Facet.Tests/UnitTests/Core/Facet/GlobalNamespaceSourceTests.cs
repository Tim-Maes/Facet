using Facet.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Facet.Tests.UnitTests.Core.Facet;

/// <summary>
/// Driver-based tests for source types declared without a namespace, as in top-level-statement and
/// minimal API apps. The fully qualified name is then <c>global::Name</c>, which member names such as
/// <c>ProjectionFrom{Source}</c> must not include. This test project's own types all live in
/// namespaces, so only a separate compilation can cover it.
/// </summary>
public class GlobalNamespaceSourceTests
{
    [Fact]
    public void Facet_OfGlobalNamespaceSource_InGlobalNamespace_Compiles()
    {
        var (_, errors) = Run("""
            public class GlobalNsUser
            {
                public int Id { get; set; }
                public string Name { get; set; } = "";
                public string PasswordHash { get; set; } = "";
            }

            [global::Facet.Facet(typeof(GlobalNsUser), nameof(GlobalNsUser.PasswordHash), GenerateToSource = true)]
            public partial class GlobalNsUserDto;
            """);

        errors.Should().BeEmpty();
    }

    [Fact]
    public void Facet_OfGlobalNamespaceSource_InNamespace_Compiles()
    {
        var (_, errors) = Run("""
            public class GlobalNsOrder
            {
                public int Id { get; set; }
                public decimal Total { get; set; }
            }

            namespace Api.Contracts
            {
                [global::Facet.Facet(typeof(GlobalNsOrder), GenerateToSource = true)]
                public partial class GlobalNsOrderDto;
            }
            """);

        errors.Should().BeEmpty();
    }

    [Fact]
    public void MultiSourceFacet_OfGlobalNamespaceSources_UsesSimpleNamesForMembers()
    {
        var (generated, errors) = Run("""
            public class GlobalNsCustomer
            {
                public int Id { get; set; }
                public string Name { get; set; } = "";
            }

            public class GlobalNsSupplier
            {
                public int Id { get; set; }
                public string Name { get; set; } = "";
            }

            [global::Facet.Facet(typeof(GlobalNsCustomer), GenerateToSource = true)]
            [global::Facet.Facet(typeof(GlobalNsSupplier), GenerateToSource = true)]
            public partial class GlobalNsPartyDto;
            """);

        errors.Should().BeEmpty();
        generated.Should().Contain("ProjectionFromGlobalNsCustomer");
        generated.Should().Contain("ProjectionFromGlobalNsSupplier");
        generated.Should().NotContain("global::GlobalNsCustomer>Projection");
    }

    [Fact]
    public void GenerateDtos_OnGlobalNamespaceSource_Compiles()
    {
        var (generated, errors) = Run("""
            [global::Facet.GenerateDtos(Types = global::Facet.DtoTypes.All)]
            public class GlobalNsProduct
            {
                public int Id { get; set; }
                public string Name { get; set; } = "";
            }
            """);

        errors.Should().BeEmpty();
        generated.Should().NotContain("global::GlobalNsProduct" + "Response");
    }

    private static (string Generated, IReadOnlyList<string> Errors) Run(string source)
    {
        var parseOptions = new CSharpParseOptions(LanguageVersion.Latest);
        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(assembly => !assembly.IsDynamic && !string.IsNullOrEmpty(assembly.Location))
            .Select(assembly => assembly.Location)
            .Append(typeof(FacetAttribute).Assembly.Location)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(location => (MetadataReference)MetadataReference.CreateFromFile(location))
            .ToList();

        var compilation = CSharpCompilation.Create(
            "GlobalNamespaceSource",
            [CSharpSyntaxTree.ParseText(source, parseOptions)],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [
                new FacetGenerator().AsSourceGenerator(),
                new GenerateDtosGeneratorHoist(new GenerateDtosGenerator()).AsSourceGenerator(),
            ],
            parseOptions: parseOptions);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out _);

        var generated = string.Join("\n", output.SyntaxTrees.Skip(1).Select(tree => tree.ToString()));
        var errors = driver.GetRunResult().Diagnostics
            .Concat(output.GetDiagnostics())
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Select(diagnostic => diagnostic.ToString())
            .ToList();

        return (generated, errors);
    }
}
