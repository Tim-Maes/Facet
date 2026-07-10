using System.Collections.Immutable;
using System.Text;
using Facet.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace Facet.Tests.UnitTests.Core.GenerateDtos;

/// <summary>
/// ExcludeNavigationProperties is manifest-driven with no heuristic fallback, so every failure
/// is loud: FAC103 (unreadable file), FAC104 (unsupported version), FAC105 (source type has no
/// manifest entry — a hard error), FAC106 (settable property unknown to the model, i.e. a stale
/// manifest — a warning). A type with no manifest coverage cannot be shaped, so FAC105 fires
/// whether or not any manifest is present.
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

    private static ImmutableArray<Diagnostic> RunGenerator(string source, params string[] manifests)
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
            additionalTexts);
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
    public void NoManifestAtAll_ReportsFac105_Error()
    {
        var diagnostics = RunGenerator(ParentWithNav);

        var fac105 = diagnostics.Should().ContainSingle(d => d.Id == "FAC105").Subject;
        fac105.Severity.Should().Be(DiagnosticSeverity.Error,
            "with no heuristic fallback, an ExcludeNavigationProperties type with no manifest cannot be shaped");
        fac105.Location.Should().NotBe(Location.None, "the error must anchor to the attribute so it is #pragma-suppressible");
        fac105.Location.GetLineSpan().Path.Should().Be("Entities.cs");
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
        fac105.Severity.Should().Be(DiagnosticSeverity.Error);
        fac105.GetMessage().Should().Contain("Parent");
        fac105.Location.GetLineSpan().Path.Should().Be("Entities.cs");
    }

    [Fact]
    public void AssemblyLevelDeclaration_ReportsFac105_AtTheAssemblyAttribute()
    {
        // [assembly: GenerateDtosFor] flows through the same manifest resolution as the
        // class-level attribute: no manifest entry is the same hard error, anchored at the
        // assembly attribute.
        var diagnostics = RunGenerator("""
            using Facet;
            using System;
            using System.Collections.Generic;

            [assembly: GenerateDtosFor(typeof(ManifestDiag.Parent),
                Types = DtoTypes.Update, ExcludeNavigationProperties = true)]

            namespace ManifestDiag;

            public class Child { public int Id { get; set; } }

            public class Parent
            {
                public int Id { get; set; }
                public Child? Owner { get; set; }
            }
            """);

        var fac105 = diagnostics.Should().ContainSingle(d => d.Id == "FAC105").Subject;
        fac105.Severity.Should().Be(DiagnosticSeverity.Error);
        fac105.GetMessage().Should().Contain("Parent");
        fac105.Location.Should().NotBe(Location.None);
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
            """);

        diagnostics.Where(d => d.Id == "FAC105").Should().HaveCount(1,
            "attribute expansion into multiple output kinds must not multiply coverage errors");
    }

    [Fact]
    public void TypeWithoutExcludeNavigation_NeedsNoManifest()
    {
        var diagnostics = RunGenerator(Entities + """
            [GenerateDtos(Types = DtoTypes.Update)]
            public class Parent
            {
                public int Id { get; set; }
                public Child? Owner { get; set; }
            }
            """);

        diagnostics.Should().NotContain(d => d.Id == "FAC105",
            "the manifest is only required for types that opted into ExcludeNavigationProperties");
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
    public void CompleteManifest_IsQuiet()
    {
        var diagnostics = RunGenerator(ParentWithNav, CompleteParentManifest);

        diagnostics.Should().NotContain(d => d.Id.StartsWith("FAC10"),
            "a manifest that covers the type produces no coverage noise");
    }
}
