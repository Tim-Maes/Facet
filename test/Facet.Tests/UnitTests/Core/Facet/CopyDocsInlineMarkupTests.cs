using Facet.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Facet.Tests.UnitTests.Core.Facet;

/// <summary>
/// Driver-based tests for <c>CopyDocs</c> on documentation with inline markup. The generated source is
/// compiled with documentation diagnostics on, so a cref the facet cannot resolve would surface as
/// CS1574 and malformed XML as CS1570 — the in-project models cannot show that, because this test
/// project's own build does not fail on warnings.
/// </summary>
public class CopyDocsInlineMarkupTests
{
    /// <summary>
    /// The source type lives in another namespace than the facet, and the facet file has no using for
    /// it: a cref copied as written (<c>cref="Id"</c>) would not bind there.
    /// </summary>
    private const string Source = """
        namespace Domain.Orders
        {
            /// <summary>How an order is paid.</summary>
            public enum PaymentKind { Card, Invoice }

            public class Order
            {
                /// <summary>The order id.</summary>
                public int Id { get; set; }

                /// <summary>
                /// How it is paid, see <see cref="PaymentKind"/>; <see langword="null"/> until checkout.
                /// <para>Matches <see cref="Id"/> in the ledger &amp; uses <c>INV-&lt;n&gt;</c> numbers.</para>
                /// </summary>
                public PaymentKind? Payment { get; set; }
            }

            /// <summary>A customer called <paramref name="Name"/>.</summary>
            /// <param name="Name">The customer's name.</param>
            public record Customer(string Name);
        }

        namespace Api.Contracts
        {
            [global::Facet.Facet(typeof(global::Domain.Orders.Order), CopyDocs = true)]
            public partial class OrderDto { }

            [global::Facet.Facet(typeof(global::Domain.Orders.Customer), CopyDocs = true)]
            public partial class CustomerDto { }
        }
        """;

    private static readonly string[] DocumentationDiagnostics =
    [
        "CS1570", // badly formed XML
        "CS1574", // cref that could not be resolved
        "CS1580", // invalid cref parameter type
        "CS1581", // invalid cref return type
        "CS1584", // syntactically incorrect cref
        "CS1658", // warning inside a cref
        "CS1734", // paramref to a parameter that does not exist
        "CS1735", // typeparamref to a type parameter that does not exist
    ];

    [Fact]
    public void CopyDocs_KeepsCrefsLangwordsAndCodeElements()
    {
        var (generated, _) = Run();

        generated.Should().Contain("How it is paid, see <see cref=\"T:Domain.Orders.PaymentKind\" />; <see langword=\"null\" /> until checkout.");
        generated.Should().Contain("Matches <see cref=\"P:Domain.Orders.Order.Id\" /> in the ledger");
    }

    [Fact]
    public void CopyDocs_KeepsEscapedCharactersEscaped()
    {
        var (generated, _) = Run();

        generated.Should().Contain("&amp; uses <c>INV-&lt;n&gt;</c> numbers.");
        generated.Should().NotContain("INV-<n>");
    }

    [Fact]
    public void CopyDocs_KeepsParagraphs()
    {
        var (generated, _) = Run();

        generated.Should().Contain("<para>Matches");
        generated.Should().Contain("numbers.</para>");
    }

    [Fact]
    public void CopyDocs_TurnsAParamrefTheFacetDoesNotHaveIntoCode()
    {
        var (generated, _) = Run();

        generated.Should().Contain("A customer called <c>Name</c>.");
        generated.Should().NotContain("paramref");
    }

    [Fact]
    public void CopyDocs_ProducesNoDocumentationWarnings_WhenTheFacetLivesInAnotherNamespace()
    {
        var (_, diagnostics) = Run();

        diagnostics.Where(diagnostic => DocumentationDiagnostics.Contains(diagnostic.Id))
            .Select(diagnostic => diagnostic.ToString())
            .Should().BeEmpty();
    }

    private static (string Generated, IReadOnlyList<Diagnostic> Diagnostics) Run()
    {
        var parseOptions = new CSharpParseOptions(LanguageVersion.Latest, DocumentationMode.Diagnose);
        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(assembly => !assembly.IsDynamic && !string.IsNullOrEmpty(assembly.Location))
            .Select(assembly => assembly.Location)
            .Append(typeof(FacetAttribute).Assembly.Location)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(location => (MetadataReference)MetadataReference.CreateFromFile(location))
            .ToList();

        var compilation = CSharpCompilation.Create(
            "CopyDocsInlineMarkup",
            [CSharpSyntaxTree.ParseText(Source, parseOptions)],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        var driver = CSharpGeneratorDriver.Create(
            [new FacetGenerator().AsSourceGenerator()],
            parseOptions: parseOptions);
        driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out _);

        var generated = string.Join(
            "\n",
            output.SyntaxTrees
                .Where(tree => tree.FilePath.Contains("Dto", StringComparison.Ordinal))
                .Select(tree => tree.ToString()));

        return (generated, output.GetDiagnostics());
    }
}
