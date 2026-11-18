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
using FluentAssertions;

namespace Facet.Tests.UnitTests.Core.GenerateDtos;

/// <summary>
/// Tests for the FAC109 migration complexity analyzer. The analyzer reads *.facetmodel.json
/// AdditionalFiles and scores handwritten DTOs by how easy they would be to replace with
/// Facet-generated partials.
/// </summary>
public class GenerateDtosMigrationComplexityAnalyzerTests
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

    // Entity sources use block-scoped namespaces so DTO sources can be concatenated
    // in the same file without CS8954 (duplicate file-scoped namespace).
    private const string WidgetEntity = """
        namespace MigrationTest
        {
            public class Widget
            {
                public int Id { get; set; }
                public string Name { get; set; } = "";
                public string Description { get; set; } = "";
            }
        }
        """;

    private const string GadgetEntity = """
        namespace MigrationTest
        {
            public class Gadget
            {
                public int Id { get; set; }
                public string Name { get; set; } = "";
                public string Category { get; set; } = "";
                public int Priority { get; set; }
            }
        }
        """;

    private static readonly string ManifestWithWidget = """
        {
          "version": 1,
          "entities": [
            { "clrType": "MigrationTest.Widget", "scalar": ["Id", "Name", "Description"] }
          ]
        }
        """;

    private static readonly string ManifestWithGadget = """
        {
          "version": 1,
          "entities": [
            { "clrType": "MigrationTest.Gadget", "scalar": ["Id", "Name", "Category", "Priority"] }
          ]
        }
        """;

    private static readonly string ManifestWithBoth = """
        {
          "version": 1,
          "entities": [
            { "clrType": "MigrationTest.Widget", "scalar": ["Id", "Name", "Description"] },
            { "clrType": "MigrationTest.Gadget", "scalar": ["Id", "Name", "Category", "Priority"] }
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

        var facetAttrsPath = typeof(global::Facet.GenerateDtosForAttribute).Assembly.Location;
        if (!string.IsNullOrEmpty(facetAttrsPath) &&
            !references.Any(r => r.Display == facetAttrsPath))
        {
            references.Add(MetadataReference.CreateFromFile(facetAttrsPath));
        }

        var fullSource = (assemblyAttributes ?? "") + source;
        var compilation = CSharpCompilation.Create("MigrationTest",
            new[] { CSharpSyntaxTree.ParseText(fullSource, path: "Entities.cs") },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));

        var analyzer = new MigrationComplexityAnalyzer();
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

    // ─── Tests: Easy Migration (Info severity) ─────────────────────────────────

    [Fact]
    public void ReportsFac109_Info_ForSimpleResponseDto()
    {
        var source = WidgetEntity + """
            
            namespace MigrationTest
            {
                public class WidgetResponse
                {
                    public int Id { get; set; }
                    public string Name { get; set; } = "";
                    public string Description { get; set; } = "";
                }
            }
            """;

        var diagnostics = RunAnalyzer(source, ManifestWithWidget);

        var fac109 = diagnostics.FirstOrDefault(d => d.Id == "FAC109");
        fac109.Should().NotBeNull("WidgetResponse is a handwritten DTO matching entity Widget");
        fac109!.Severity.Should().Be(DiagnosticSeverity.Info,
            "all properties are get/set and match entity scalars");
        fac109.GetMessage().Should().Contain("WidgetResponse");
        fac109.GetMessage().Should().Contain("entity 'Widget'");
        fac109.GetMessage().Should().Contain("3 entity-shaped props");
    }

    [Fact]
    public void ReportsFac109_Info_ForGetPrefixedDto()
    {
        var source = WidgetEntity + """
            
            namespace MigrationTest
            {
                public class GetWidgetResponse
                {
                    public int Id { get; set; }
                    public string Name { get; set; } = "";
                    public string Description { get; set; } = "";
                }
            }
            """;

        var diagnostics = RunAnalyzer(source, ManifestWithWidget);

        var fac109 = diagnostics.FirstOrDefault(d => d.Id == "FAC109");
        fac109.Should().NotBeNull("GetWidgetResponse matches entity Widget after stripping Get prefix and Response suffix");
        fac109!.Severity.Should().Be(DiagnosticSeverity.Info);
    }

    // ─── Tests: Medium Migration (Warning severity) ────────────────────────────

    [Fact]
    public void ReportsFac109_Info_ForDtoWithReadOnlyProperties()
    {
        var source = GadgetEntity + """
            
            namespace MigrationTest
            {
                public class GadgetResponse
                {
                    public int Id { get; set; }
                    public string Name { get; set; } = "";
                    public string Category { get; set; } = "";
                    public string DisplayName { get; }
                }
            }
            """;

        var diagnostics = RunAnalyzer(source, ManifestWithGadget);

        var fac109 = diagnostics.FirstOrDefault(d => d.Id == "FAC109");
        fac109.Should().NotBeNull("GadgetResponse matches entity Gadget");
        fac109!.Severity.Should().Be(DiagnosticSeverity.Info,
            "read-only properties are handled by GenerateReadOnlyProperties ({ get; init; })");
        fac109.GetMessage().Should().Contain("GadgetResponse");
        fac109.GetMessage().Should().Contain("read-only");
        fac109.GetMessage().Should().Contain("init-only");
    }

    [Fact]
    public void ReportsFac109_Info_ForDtoWithComputedProperties()
    {
        var source = GadgetEntity + """
            
            namespace MigrationTest
            {
                public class GadgetResponse
                {
                    public int Id { get; set; }
                    public string Name { get; set; } = "";
                    public string Category { get; set; } = "";
                    private string _displayName = "";
                    public string DisplayName
                    {
                        get => _displayName;
                        set => _displayName = value;
                    }
                }
            }
            """;

        var diagnostics = RunAnalyzer(source, ManifestWithGadget);

        var fac109 = diagnostics.FirstOrDefault(d => d.Id == "FAC109");
        fac109.Should().NotBeNull();
        fac109!.Severity.Should().Be(DiagnosticSeverity.Info,
            "computed properties are handled by OnInitialized hook in the partial");
        fac109.GetMessage().Should().Contain("computed");
        fac109.GetMessage().Should().Contain("OnInitialized");
    }

    // ─── Tests: No Diagnostic Expected ─────────────────────────────────────────

    [Fact]
    public void DoesNotReportFac109_WhenNoManifest()
    {
        var source = WidgetEntity + """
            
            namespace MigrationTest
            {
                public class WidgetResponse
                {
                    public int Id { get; set; }
                    public string Name { get; set; } = "";
                }
            }
            """;

        var diagnostics = RunAnalyzer(source);

        diagnostics.Should().NotContain(d => d.Id == "FAC109",
            "without a manifest the analyzer cannot match DTOs to entities");
    }

    [Fact]
    public void DoesNotReportFac109_ForAlreadyFacetConfiguredDto()
    {
        var source = WidgetEntity + """
            
            namespace MigrationTest
            {
                public class WidgetResponse
                {
                    public int Id { get; set; }
                    public string Name { get; set; } = "";
                }
            }
            """;

        var assemblyAttributes = """
            using Facet;

            [assembly: GenerateDtosFor(typeof(MigrationTest.Widget), Types = DtoTypes.Response, OutputType = OutputType.PartialClass)]
            """;

        var diagnostics = RunAnalyzer(source, ManifestWithWidget, assemblyAttributes);

        var fac109s = diagnostics.Where(d => d.Id == "FAC109").ToList();
        fac109s.Should().NotContain(d => d.GetMessage().Contains("Widget"),
            "Widget is already configured via [assembly: GenerateDtosFor]");
    }

    [Fact]
    public void DoesNotReportFac109_ForNonDtoClass()
    {
        var source = WidgetEntity + """
            
            namespace MigrationTest
            {
                public class WidgetHelper
                {
                    public int Id { get; set; }
                    public string Name { get; set; } = "";
                }
            }
            """;

        var diagnostics = RunAnalyzer(source, ManifestWithWidget);

        var fac109 = diagnostics.FirstOrDefault(d => d.Id == "FAC109");
        fac109.Should().BeNull("WidgetHelper does not follow DTO naming conventions (no Response/Request suffix)");
    }

    // ─── Tests: Multiple DTOs ──────────────────────────────────────────────────

    [Fact]
    public void ReportsFac109_ForMultipleMatchingDtos()
    {
        var source = WidgetEntity + GadgetEntity + """
            
            namespace MigrationTest
            {
                public class WidgetResponse
                {
                    public int Id { get; set; }
                    public string Name { get; set; } = "";
                    public string Description { get; set; } = "";
                }

                public class GadgetResponse
                {
                    public int Id { get; set; }
                    public string Name { get; set; } = "";
                    public string Category { get; set; } = "";
                    public int Priority { get; set; }
                }
            }
            """;

        var diagnostics = RunAnalyzer(source, ManifestWithBoth);

        var fac109s = diagnostics.Where(d => d.Id == "FAC109").ToList();
        fac109s.Should().HaveCount(2, "both WidgetResponse and GadgetResponse should be scored");
        fac109s.Should().Contain(d => d.GetMessage().Contains("WidgetResponse"));
        fac109s.Should().Contain(d => d.GetMessage().Contains("GadgetResponse"));
        fac109s.Should().OnlyContain(d => d.Severity == DiagnosticSeverity.Info,
            "both DTOs have only get/set auto-properties");
    }

    // ─── Tests: Message Content ────────────────────────────────────────────────

    [Fact]
    public void Fac109_Message_IncludesEntityName()
    {
        var source = WidgetEntity + """
            
            namespace MigrationTest
            {
                public class WidgetResponse
                {
                    public int Id { get; set; }
                    public string Name { get; set; } = "";
                    public string Description { get; set; } = "";
                }
            }
            """;

        var diagnostics = RunAnalyzer(source, ManifestWithWidget);

        var fac109 = diagnostics.First(d => d.Id == "FAC109");
        fac109.GetMessage().Should().Contain("entity 'Widget'");
    }

    [Fact]
    public void Fac109_Message_IncludesEstimatedLinesSaved()
    {
        var source = WidgetEntity + """
            
            namespace MigrationTest
            {
                public class WidgetResponse
                {
                    public int Id { get; set; }
                    public string Name { get; set; } = "";
                    public string Description { get; set; } = "";
                }
            }
            """;

        var diagnostics = RunAnalyzer(source, ManifestWithWidget);

        var fac109 = diagnostics.First(d => d.Id == "FAC109");
        fac109.GetMessage().Should().Contain("lines saveable");
        // 3 entity-shaped props × 3 + 15 = 24
        fac109.GetMessage().Should().Contain("24");
    }

    // ─── Tests: Create/Update Request DTOs ─────────────────────────────────────

    [Fact]
    public void ReportsFac109_ForCreateRequestBodyDto()
    {
        var source = WidgetEntity + """
            
            namespace MigrationTest
            {
                public class CreateWidgetRequestBody
                {
                    public string Name { get; set; } = "";
                    public string Description { get; set; } = "";
                }
            }
            """;

        var diagnostics = RunAnalyzer(source, ManifestWithWidget);

        var fac109 = diagnostics.FirstOrDefault(d => d.Id == "FAC109");
        fac109.Should().NotBeNull("CreateWidgetRequestBody matches entity Widget");
        fac109!.Severity.Should().Be(DiagnosticSeverity.Info,
            "all properties are get/set auto-properties");
        fac109.GetMessage().Should().Contain("Create request");
        fac109.GetMessage().Should().Contain("CreateWidgetRequestBody");
        fac109.GetMessage().Should().Contain("entity 'Widget'");
        // 2 entity-shaped props × 2 + 8 = 12
        fac109.GetMessage().Should().Contain("12");
    }

    [Fact]
    public void ReportsFac109_ForUpdateRequestBodyDto()
    {
        var source = GadgetEntity + """
            
            namespace MigrationTest
            {
                public class UpdateGadgetRequestBody
                {
                    public int Id { get; set; }
                    public string Name { get; set; } = "";
                    public string Category { get; set; } = "";
                    public int Priority { get; set; }
                }
            }
            """;

        var diagnostics = RunAnalyzer(source, ManifestWithGadget);

        var fac109 = diagnostics.FirstOrDefault(d => d.Id == "FAC109");
        fac109.Should().NotBeNull("UpdateGadgetRequestBody matches entity Gadget");
        fac109!.Severity.Should().Be(DiagnosticSeverity.Info);
        fac109.GetMessage().Should().Contain("Update request");
        fac109.GetMessage().Should().Contain("UpdateGadgetRequestBody");
        // 4 entity-shaped props × 2 + 8 = 16
        fac109.GetMessage().Should().Contain("16");
    }

    [Fact]
    public void DoesNotReportFac109_ForCreateDtoWhenAlreadyConfigured()
    {
        var source = WidgetEntity + """
            
            namespace MigrationTest
            {
                public class CreateWidgetRequestBody
                {
                    public string Name { get; set; } = "";
                    public string Description { get; set; } = "";
                }
            }
            """;

        var assemblyAttributes = """
            using Facet;

            [assembly: GenerateDtosFor(typeof(MigrationTest.Widget), Types = DtoTypes.Create, OutputType = OutputType.PartialClass)]
            """;

        var diagnostics = RunAnalyzer(source, ManifestWithWidget, assemblyAttributes);

        var fac109s = diagnostics.Where(d => d.Id == "FAC109" && d.GetMessage().Contains("Widget")).ToList();
        fac109s.Should().BeEmpty("Widget Create is already configured via [assembly: GenerateDtosFor]");
    }
}
