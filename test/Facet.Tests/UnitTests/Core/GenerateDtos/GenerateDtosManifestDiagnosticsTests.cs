using System.Collections.Immutable;
using System.Text;
using Facet.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Facet.Tests.UnitTests.Core.GenerateDtos;

/// <summary>
/// Manifest failures are loud, never silent fallbacks: FAC103 (unreadable file), FAC104
/// (unsupported version), FAC105 (source type missing while manifests exist), FAC106
/// (settable property unknown to the model — stale manifest). The only silent state is
/// having no manifests at all, which is tier-1 behavior by design.
/// </summary>
public class GenerateDtosManifestDiagnosticsTests
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

    private sealed class GlobalOptions(bool requireManifest) : AnalyzerConfigOptions
    {
        public override bool TryGetValue(string key, out string value)
        {
            if (requireManifest && key == "build_property.Facet_RequireEfModelManifest")
            {
                value = "true";
                return true;
            }

            value = null!;
            return false;
        }
    }

    private sealed class OptionsProvider(bool requireManifest) : AnalyzerConfigOptionsProvider
    {
        public override AnalyzerConfigOptions GlobalOptions { get; } = new GlobalOptions(requireManifest);
        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => new GlobalOptions(requireManifest);
        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => new GlobalOptions(requireManifest);
    }

    private static ImmutableArray<Diagnostic> RunGenerator(string source, params string[] manifests)
        => RunGenerator(source, requireManifest: false, manifests);

    private static ImmutableArray<Diagnostic> RunGenerator(string source, bool requireManifest, params string[] manifests)
    {
        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => (MetadataReference)MetadataReference.CreateFromFile(a.Location))
            .ToList();
        var compilation = CSharpCompilation.Create("ManifestDiag",
            new[] { CSharpSyntaxTree.ParseText(source, path: "Entities.cs") }, references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        var additionalTexts = manifests
            .Select((text, i) => (AdditionalText)new TestAdditionalText($"/model/Context{i}.facetmodel.json", text))
            .ToArray();
        var driver = CSharpGeneratorDriver.Create(
            new[] { new GenerateDtosGeneratorHoist(new GenerateDtosGenerator()).AsSourceGenerator() },
            additionalTexts,
            optionsProvider: new OptionsProvider(requireManifest));
        return driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out _)
            .GetRunResult().Diagnostics;
    }

    private const string Entities = """
        using Facet;
        using System;
        using System.Collections.Generic;
        namespace ManifestDiag;

        public class Child { public int Id { get; set; } }
        """;

    private const string ParentWithNav = Entities + """
        [GenerateDtos(Types = DtoTypes.Update, ExcludeNavigationProperties = true)]
        public class Parent
        {
            public int Id { get; set; }
            public Child? Owner { get; set; }
        }
        """;

    private const string CompleteParentManifest = """
        {
          "version": 1,
          "entities": [ { "clrType": "ManifestDiag.Parent", "scalar": ["Id"], "nav": ["Owner"] } ]
        }
        """;

    [Fact]
    public void MalformedManifest_ReportsFac103()
    {
        var diagnostics = RunGenerator(ParentWithNav, """{ "version": 1, "entities": [ { "clrType": """);

        var fac103 = diagnostics.Should().ContainSingle(d => d.Id == "FAC103").Subject;
        fac103.Severity.Should().Be(DiagnosticSeverity.Error);
        fac103.GetMessage().Should().Contain("Context0.facetmodel.json");
    }

    [Fact]
    public void UnsupportedVersion_ReportsFac104()
    {
        var diagnostics = RunGenerator(ParentWithNav, """
            {
              "version": 2,
              "entities": [ { "clrType": "ManifestDiag.Parent", "scalar": ["Id"] } ]
            }
            """);

        var fac104 = diagnostics.Should().ContainSingle(d => d.Id == "FAC104").Subject;
        fac104.Severity.Should().Be(DiagnosticSeverity.Error);
        fac104.GetMessage().Should().Contain("version 2");
    }

    [Fact]
    public void TypeMissingFromManifest_ReportsFac105_AtTheAttribute()
    {
        var diagnostics = RunGenerator(ParentWithNav, """
            {
              "version": 1,
              "entities": [ { "clrType": "ManifestDiag.SomeOtherType", "scalar": ["Id"] } ]
            }
            """);

        var fac105 = diagnostics.Should().ContainSingle(d => d.Id == "FAC105").Subject;
        fac105.Severity.Should().Be(DiagnosticSeverity.Warning);
        fac105.GetMessage().Should().Contain("Parent");
        fac105.Location.Should().NotBe(Location.None, "the warning must be anchored to the attribute so it is #pragma-suppressible");
        fac105.Location.GetLineSpan().Path.Should().Be("Entities.cs");
    }

    [Fact]
    public void FlagsCombinedOutputTypes_ReportFac105Once()
    {
        var diagnostics = RunGenerator(Entities + """
            [GenerateDtos(Types = DtoTypes.Update,
                OutputType = OutputType.Interface | OutputType.PartialClass,
                ExcludeNavigationProperties = true)]
            public class Parent
            {
                public int Id { get; set; }
                public Child? Owner { get; set; }
            }
            """,
            """
            {
              "version": 1,
              "entities": [ { "clrType": "ManifestDiag.SomeOtherType", "scalar": ["Id"] } ]
            }
            """);

        diagnostics.Where(d => d.Id == "FAC105").Should().HaveCount(1,
            "attribute expansion into multiple output kinds must not multiply coverage warnings");
    }

    [Fact]
    public void SettablePropertyUnknownToManifest_ReportsFac106()
    {
        var diagnostics = RunGenerator(Entities + """
            [GenerateDtos(Types = DtoTypes.Update, ExcludeNavigationProperties = true)]
            public class Parent
            {
                public int Id { get; set; }
                public Child? Owner { get; set; }
                public bool AddedYesterday { get; set; }
            }
            """,
            CompleteParentManifest);

        var fac106 = diagnostics.Should().ContainSingle(d => d.Id == "FAC106").Subject;
        fac106.Severity.Should().Be(DiagnosticSeverity.Warning);
        fac106.GetMessage().Should().Contain("AddedYesterday");
        fac106.Location.Should().NotBe(Location.None);
    }

    [Fact]
    public void ComputedGetOnlyProperty_DoesNotReportFac106()
    {
        var diagnostics = RunGenerator(Entities + """
            [GenerateDtos(Types = DtoTypes.Update, ExcludeNavigationProperties = true)]
            public class Parent
            {
                public int Id { get; set; }
                public Child? Owner { get; set; }
                public string DisplayLabel => $"#{Id}";
            }
            """,
            CompleteParentManifest);

        diagnostics.Should().NotContain(d => d.Id == "FAC106",
            "the model never maps computed get-only properties, so their absence means nothing");
    }

    [Fact]
    public void IgnoredAndIncludedProperties_DoNotReportFac106()
    {
        var diagnostics = RunGenerator(Entities + """
            [GenerateDtos(Types = DtoTypes.Update, ExcludeNavigationProperties = true,
                IncludeProperties = new[] { "ForcedIn" })]
            public class Parent
            {
                public int Id { get; set; }
                public Child? Owner { get; set; }
                public string? NotMappedByModel { get; set; }
                public string? ForcedIn { get; set; }
            }
            """,
            """
            {
              "version": 1,
              "entities": [
                { "clrType": "ManifestDiag.Parent", "scalar": ["Id"], "nav": ["Owner"], "ignored": ["NotMappedByModel"] }
              ]
            }
            """);

        diagnostics.Should().NotContain(d => d.Id == "FAC106",
            "ignored members are known to the model, and IncludeProperties is explicit user intent");
    }

    [Fact]
    public void NoManifestsAtAll_IsSilent()
    {
        var diagnostics = RunGenerator(ParentWithNav);

        diagnostics.Should().NotContain(d => d.Id == "FAC103" || d.Id == "FAC104" || d.Id == "FAC105" || d.Id == "FAC106",
            "the zero-infrastructure heuristic tier is the documented default, not a degradation");
    }

    [Fact]
    public void StrictMode_NoManifestsAtAll_ReportsFac107()
    {
        var diagnostics = RunGenerator(ParentWithNav, requireManifest: true);

        var fac107 = diagnostics.Should().ContainSingle(d => d.Id == "FAC107").Subject;
        fac107.Severity.Should().Be(DiagnosticSeverity.Error);
        fac107.GetMessage().Should().Contain("Parent");
        fac107.Location.Should().NotBe(Location.None, "the error must anchor to the attribute");
        diagnostics.Should().NotContain(d => d.Id == "FAC105", "strict mode replaces the FAC105 advisory with the FAC107 error");
    }

    [Fact]
    public void StrictMode_TypeMissingFromManifest_ReportsFac107()
    {
        var diagnostics = RunGenerator(ParentWithNav, requireManifest: true, """
            {
              "version": 1,
              "entities": [ { "clrType": "ManifestDiag.SomeOtherType", "scalar": ["Id"] } ]
            }
            """);

        diagnostics.Should().ContainSingle(d => d.Id == "FAC107")
            .Which.Severity.Should().Be(DiagnosticSeverity.Error);
    }

    [Fact]
    public void StrictMode_CoveredType_IsQuiet()
    {
        var diagnostics = RunGenerator(ParentWithNav, requireManifest: true, CompleteParentManifest);

        diagnostics.Should().NotContain(d => d.Id == "FAC107" || d.Id == "FAC105",
            "a covered type satisfies the requirement");
    }

    [Fact]
    public void StrictMode_TypeWithoutExcludeNavigation_IsUnaffected()
    {
        var diagnostics = RunGenerator(Entities + """
            [GenerateDtos(Types = DtoTypes.Update)]
            public class Parent
            {
                public int Id { get; set; }
                public Child? Owner { get; set; }
            }
            """, requireManifest: true);

        diagnostics.Should().NotContain(d => d.Id == "FAC107",
            "the requirement only applies to types that opted into ExcludeNavigationProperties");
    }

    [Fact]
    public void CompleteManifest_IsQuiet()
    {
        var diagnostics = RunGenerator(ParentWithNav, CompleteParentManifest);

        diagnostics.Should().NotContain(d => d.Id.StartsWith("FAC10"),
            "a manifest that covers everything produces no coverage noise");
    }
}
