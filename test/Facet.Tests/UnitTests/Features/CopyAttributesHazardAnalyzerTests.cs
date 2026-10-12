using System.Collections.Immutable;
using System.Linq;
using Facet.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Facet.Tests.UnitTests.Features;

/// <summary>
/// Tests for FAC109, which reports an attribute <c>CopyAttributes</c> would carry from a package
/// assembly onto a generated member.
/// </summary>
/// <remarks>
/// The diagnostic is what makes the hazard visible while the default mode still preserves the
/// behaviour Facet had before, so these assert both that it fires and that it goes quiet once the
/// project has dealt with it — by either mode or exclusion.
/// </remarks>
public class CopyAttributesHazardAnalyzerTests
{
    /// <summary>
    /// An entity carrying one attribute from a package (<c>[Precision]</c>, in
    /// Microsoft.EntityFrameworkCore.Abstractions) and several the shared framework carries.
    /// Each case appends its own facet declaration.
    /// </summary>
    private const string Entity = """
        using System.ComponentModel.DataAnnotations;
        using System.ComponentModel.DataAnnotations.Schema;
        using Facet;
        using Microsoft.EntityFrameworkCore;

        namespace Hazard;

        public class Invoice
        {
            [Key]
            public int Id { get; set; }

            [Required]
            [Column("reference")]
            public string Reference { get; set; } = "";

            [Precision(18, 2)]
            public decimal Total { get; set; }
        }

        """;

    [Fact]
    public void ReportsAPackageAttributeUnderTheDefaultMode()
    {
        var diagnostics = Analyze("""
            [Facet(typeof(Invoice), CopyAttributes = true)]
            public partial class InvoiceDto;
            """);

        var fac109 = diagnostics.Where(d => d.Id == "FAC109").ToList();

        fac109.Should().HaveCount(1,
            "only [Precision] comes from a package; [Key], [Required] and [Column] do not");

        fac109[0].Severity.Should().Be(DiagnosticSeverity.Info,
            "a warning would be noise for the projects that do deploy the package, and a new "
            + "warning on upgrade breaks any build using TreatWarningsAsErrors");

        var message = fac109[0].GetMessage();
        message.Should().Contain("PrecisionAttribute");
        message.Should().Contain("Invoice.Total");
        message.Should().Contain("Microsoft.EntityFrameworkCore.Abstractions");
        message.Should().Contain("ContractSafe");
    }

    [Fact]
    public void DoesNotReportAttributesTheSharedFrameworkCarries()
    {
        // [Key], [Required] and [Column] resolve wherever the facet does, so copying them is no
        // hazard — reporting them would be telling people off for nothing.
        var diagnostics = Analyze("""
            [Facet(typeof(Invoice), CopyAttributes = true,
                   CopyAttributesExclude = new[] { "Precision" })]
            public partial class InvoiceDto;
            """);

        diagnostics.Should().NotContain(d => d.Id == "FAC109");
    }

    [Fact]
    public void IsSilentWhenCopyAttributesIsOff()
    {
        var diagnostics = Analyze("""
            [Facet(typeof(Invoice))]
            public partial class InvoiceDto;
            """);

        diagnostics.Should().NotContain(d => d.Id == "FAC109");
    }

    [Fact]
    public void IsSilentOnceTheModeHandlesIt()
    {
        var diagnostics = Analyze("""
            [Facet(typeof(Invoice), CopyAttributes = true,
                   CopyAttributesMode = AttributeCopyMode.ContractSafe)]
            public partial class InvoiceDto;
            """);

        diagnostics.Should().NotContain(d => d.Id == "FAC109");
    }

    [Fact]
    public void IsSilentForAWrapperThatDidNotAskToCopy()
    {
        // [Wrapper] has never inherited the project-wide CopyAttributes default, so an unadorned
        // wrapper copies nothing and has nothing to report.
        var diagnostics = Analyze("""
            [Wrapper(typeof(Invoice))]
            public partial class InvoiceFacade;
            """);

        diagnostics.Should().NotContain(d => d.Id == "FAC109");
    }

    private static ImmutableArray<Diagnostic> Analyze(string facetDeclaration)
    {
        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => (MetadataReference)MetadataReference.CreateFromFile(a.Location))
            .ToList();

        foreach (var required in new[]
                 {
                     typeof(global::Facet.FacetAttribute).Assembly.Location,
                     typeof(Microsoft.EntityFrameworkCore.PrecisionAttribute).Assembly.Location,
                 })
        {
            if (!string.IsNullOrEmpty(required) && references.All(r => r.Display != required))
                references.Add(MetadataReference.CreateFromFile(required));
        }

        var compilation = CSharpCompilation.Create(
            "HazardTests",
            new[] { CSharpSyntaxTree.ParseText(Entity + facetDeclaration) },
            references,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));

        // GetAnalyzerDiagnosticsAsync returns only analyzer diagnostics, so a test source that does
        // not compile would look indistinguishable from an analyzer that did not fire.
        compilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .Should().BeEmpty("the test source must compile for its symbols to bind");

        return compilation
            .WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new CopyAttributesHazardAnalyzer()))
            .GetAnalyzerDiagnosticsAsync().Result;
    }
}
