using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using Facet.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Facet.Tests.UnitTests.Core.GenerateDtos;

/// <summary>
/// Tests for the FAC108 per-entity coverage analyzer. The analyzer reads *.facetmodel.json
/// AdditionalFiles and reports one FAC108 per uncovered or partially covered entity.
/// </summary>
public class GenerateDtosCoverageAnalyzerTests
{
    private sealed class TestAdditionalText : AdditionalText
    {
        private readonly SourceText _text;

        public TestAdditionalText(string path, string text)
        {
            Path = path;
            _text = SourceText.From(text, Encoding.UTF8);
        }

        public override string Path { get; }
        public override SourceText GetText(CancellationToken cancellationToken = default) => _text;
    }

    private const string Entities = """
        using Facet;

        namespace CoverageTest;

        public class Computer
        {
            public int Id { get; set; }
            public string Name { get; set; } = "";
        }

        public class Printer
        {
            public int Id { get; set; }
            public string Model { get; set; } = "";
        }

        public class Monitor
        {
            public int Id { get; set; }
            public string Brand { get; set; } = "";
        }
        """;

    private static readonly string ManifestWithThreeEntities = """
        {
          "version": 1,
          "entities": [
            { "clrType": "CoverageTest.Computer", "scalar": ["Id", "Name"] },
            { "clrType": "CoverageTest.Printer", "scalar": ["Id", "Model"] },
            { "clrType": "CoverageTest.Monitor", "scalar": ["Id", "Brand"] }
          ]
        }
        """;

    private static ImmutableArray<Diagnostic> RunAnalyzer(
        string source,
        string? manifest = null,
        string? assemblyAttributes = null)
    {
        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => (MetadataReference)MetadataReference.CreateFromFile(a.Location))
            .ToList();

        // Ensure Facet.Attributes is explicitly referenced — it may not be loaded yet
        // when AppDomain.GetAssemblies() is called.
        var facetAttrsPath = typeof(global::Facet.GenerateDtosForAttribute).Assembly.Location;
        if (!string.IsNullOrEmpty(facetAttrsPath) &&
            !references.Any(r => r.Display == facetAttrsPath))
        {
            references.Add(MetadataReference.CreateFromFile(facetAttrsPath));
        }

        // Assembly-level attributes must precede all other elements except using clauses.
        var fullSource = (assemblyAttributes ?? "") + source;
        var compilation = CSharpCompilation.Create("CoverageTest",
            new[] { CSharpSyntaxTree.ParseText(fullSource, path: "Entities.cs") },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));

        var analyzer = new GenerateDtosCoverageAnalyzer();
        var additionalTexts = manifest != null
            ? ImmutableArray.Create<AdditionalText>(
                new TestAdditionalText("/model/Context.facetmodel.json", manifest))
            : ImmutableArray<AdditionalText>.Empty;

        var analyzerOptions = new AnalyzerOptions(additionalTexts);
        var compilationWithAnalyzers = compilation.WithAnalyzers(
            ImmutableArray.Create<DiagnosticAnalyzer>(analyzer),
            analyzerOptions);

        return compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync().Result;
    }

    [Fact]
    public void ReportsFac108_ForEachUncoveredEntity()
    {
        var diagnostics = RunAnalyzer(Entities, ManifestWithThreeEntities);

        var fac108s = diagnostics.Where(d => d.Id == "FAC108").ToList();
        fac108s.Should().HaveCount(3, "all three entities are uncovered");

        fac108s.Should().Contain(d => d.GetMessage().Contains("Computer"));
        fac108s.Should().Contain(d => d.GetMessage().Contains("Printer"));
        fac108s.Should().Contain(d => d.GetMessage().Contains("Monitor"));
    }

    [Fact]
    public void Fac108_Message_IncludesExpectedDtoTypes()
    {
        var diagnostics = RunAnalyzer(Entities, ManifestWithThreeEntities);

        var computerDiag = diagnostics.First(d => d.Id == "FAC108" && d.GetMessage().Contains("Computer"));
        computerDiag.GetMessage().Should().Contain("Create");
        computerDiag.GetMessage().Should().Contain("Update");
        computerDiag.GetMessage().Should().Contain("no DTOs configured");
    }

    [Fact]
    public void DoesNotReportFac108_WhenNoManifest()
    {
        var diagnostics = RunAnalyzer(Entities);

        diagnostics.Should().NotContain(d => d.Id == "FAC108",
            "without a manifest there is nothing to measure coverage against");
    }

    [Fact]
    public void DoesNotReportFac108_ForFullyConfiguredEntity()
    {
        var diagnostics = RunAnalyzer(Entities, ManifestWithThreeEntities,
            """
            using Facet;

            [assembly: GenerateDtosFor(typeof(CoverageTest.Computer), Types = DtoTypes.Create | DtoTypes.Update, OutputType = OutputType.PartialClass, ExcludeAuditFields = true)]
            """);

        var fac108s = diagnostics.Where(d => d.Id == "FAC108").ToList();
        fac108s.Should().NotContain(d => d.GetMessage().Contains("Computer"),
            "Computer is fully configured with Create | Update");
        fac108s.Should().Contain(d => d.GetMessage().Contains("Printer"));
        fac108s.Should().Contain(d => d.GetMessage().Contains("Monitor"));
    }

    [Fact]
    public void ReportsFac108_PartialCoverage_WhenCreateConfiguredButUpdateMissing()
    {
        var diagnostics = RunAnalyzer(Entities, ManifestWithThreeEntities,
            """
            using Facet;

            [assembly: GenerateDtosFor(typeof(CoverageTest.Computer), Types = DtoTypes.Create, OutputType = OutputType.PartialClass, ExcludeAuditFields = true)]
            """);

        var computerDiag = diagnostics.First(d => d.Id == "FAC108" && d.GetMessage().Contains("Computer"));
        computerDiag.GetMessage().Should().Contain("Create");
        computerDiag.GetMessage().Should().Contain("Update");
        computerDiag.GetMessage().Should().Contain("missing");
    }

    [Fact]
    public void ExcludesOwnedTypes_FromFac108()
    {
        var manifest = """
            {
              "version": 1,
              "entities": [
                { "clrType": "CoverageTest.Computer", "scalar": ["Id", "Name"] },
                { "clrType": "CoverageTest.OwnedSettings", "isOwned": true, "scalar": ["Key", "Value"] }
              ]
            }
            """;

        var diagnostics = RunAnalyzer(Entities, manifest);

        var fac108s = diagnostics.Where(d => d.Id == "FAC108").ToList();
        fac108s.Should().HaveCount(1, "owned types are excluded from coverage");
        fac108s[0].GetMessage().Should().Contain("Computer");
        fac108s[0].GetMessage().Should().NotContain("OwnedSettings");
    }

    [Fact]
    public void Fac108_SeverityIsInfo()
    {
        var diagnostics = RunAnalyzer(Entities, ManifestWithThreeEntities);

        var fac108 = diagnostics.First(d => d.Id == "FAC108");
        fac108.Severity.Should().Be(DiagnosticSeverity.Info);
    }

    [Fact]
    public void Fac108_AnchorsToGenerateDtosForLocation()
    {
        // FAC108 anchors to the [assembly: GenerateDtosFor] attribute location so the
        // code fixer knows which document to modify. Entities are in referenced assemblies,
        // so their class locations can't be used (Roslyn rejects cross-compilation locations).
        var diagnostics = RunAnalyzer(Entities, ManifestWithThreeEntities,
            """
            using Facet;

            [assembly: GenerateDtosFor(typeof(CoverageTest.Computer), Types = DtoTypes.Create, OutputType = OutputType.PartialClass)]
            """);

        // Computer has only Create, so it gets a partial-coverage FAC108.
        var computerDiag = diagnostics.FirstOrDefault(d => d.Id == "FAC108" && d.GetMessage().Contains("Computer"));
        computerDiag.Should().NotBeNull();
        computerDiag!.Location.Should().NotBe(Location.None,
            "FAC108 should anchor to the [assembly: GenerateDtosFor] attribute location");
    }
}
