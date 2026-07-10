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
/// manifest — a warning). A shaped type with no manifest coverage cannot be generated safely,
/// so FAC105 fires whether or not any manifest is present. Wiring a manifest into the
/// compilation also flips the default: attributes that leave the flag unset are shaped and held
/// to the same coverage rules, while a rejected-only manifest set does NOT flip the default —
/// FAC103/FAC104 are already fatal, and a FAC105 cascade would bury them.
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
    public void UnsetFlag_NoManifest_NeedsNoManifest()
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
            "with no manifest wired, an unset flag resolves to plain property copying");
    }

    [Fact]
    public void UnsetFlag_ManifestWired_TypeMissing_ReportsFac105_MentioningTheDefault()
    {
        var diagnostics = RunGenerator(Entities + """
            [GenerateDtos(Types = DtoTypes.Update)]
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

        var fac105 = diagnostics.Should().ContainSingle(d => d.Id == "FAC105").Subject;
        fac105.Severity.Should().Be(DiagnosticSeverity.Error);
        fac105.GetMessage().Should().Contain("defaults to ExcludeNavigationProperties",
            "the message must explain where the requirement came from when the user never set the flag");
        fac105.GetMessage().Should().Contain("ExcludeNavigationProperties = false",
            "and name the per-type escape hatch");
    }

    [Fact]
    public void UnsetFlag_EmptyValidManifest_ReportsFac105()
    {
        // A manifest that parses but lists no entities is still a wired manifest: the project
        // opted in, so an uncovered source type is the same hard error, not a silent skip.
        var diagnostics = RunGenerator(Entities + """
            [GenerateDtos(Types = DtoTypes.Update)]
            public class Parent
            {
                public int Id { get; set; }
                public Child? Owner { get; set; }
            }
            """,
            """{ "version": 1, "entities": [] }""");

        diagnostics.Should().ContainSingle(d => d.Id == "FAC105");
    }

    [Fact]
    public void UnsetFlag_MalformedManifestOnly_DoesNotFlipTheDefault()
    {
        // Every supplied manifest was rejected: FAC103 already fails the build, and flipping
        // the default on top would bury it under a FAC105 per source type.
        var diagnostics = RunGenerator(Entities + """
            [GenerateDtos(Types = DtoTypes.Update)]
            public class Parent
            {
                public int Id { get; set; }
                public Child? Owner { get; set; }
            }
            """,
            """{ "version": 1, "entities": [ { "clrType": """);

        diagnostics.Should().ContainSingle(d => d.Id == "FAC103");
        diagnostics.Should().NotContain(d => d.Id == "FAC105");
    }

    [Fact]
    public void UnsetFlag_CoveredByManifest_IsQuiet()
    {
        var diagnostics = RunGenerator(Entities + """
            [GenerateDtos(Types = DtoTypes.Update)]
            public class Parent
            {
                public int Id { get; set; }
                public Child? Owner { get; set; }
            }
            """,
            CompleteParentManifest);

        diagnostics.Should().NotContain(d => d.Id.StartsWith("FAC10"),
            "the flipped default holds covered types to the same rules as explicit opt-in, with no extra noise");
    }

    [Fact]
    public void UnsetFlag_ManifestWired_StaleProperty_ReportsFac106()
    {
        // The stale-manifest completeness check guards implicitly shaped types too.
        var diagnostics = RunGenerator(Entities + """
            [GenerateDtos(Types = DtoTypes.Update)]
            public class Parent
            {
                public int Id { get; set; }
                public Child? Owner { get; set; }
                public bool AddedYesterday { get; set; }
            }
            """,
            CompleteParentManifest);

        var fac106 = diagnostics.Should().ContainSingle(d => d.Id == "FAC106").Subject;
        fac106.GetMessage().Should().Contain("AddedYesterday");
    }

    [Fact]
    public void GenerateAuditableDtos_ManifestWired_IsExemptFromTheDefault()
    {
        // The obsolete attribute declares neither ExcludeNavigationProperties nor
        // IncludeProperties, so the flipped default must not reach it: FAC105's remedy
        // ("set ExcludeNavigationProperties = false") would not even compile there.
        var diagnostics = RunGenerator(Entities + """
            #pragma warning disable CS0618
            [GenerateAuditableDtos(Types = DtoTypes.Update)]
            #pragma warning restore CS0618
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

        diagnostics.Should().NotContain(d => d.Id.StartsWith("FAC10"),
            "the attribute cannot express the opt-out, so it keeps its legacy unshaped behavior");
    }

    [Fact]
    public void ExplicitFalse_ManifestWired_IsQuiet()
    {
        // The non-entity escape hatch: an uncovered type with an explicit false produces no
        // coverage diagnostics at all.
        var diagnostics = RunGenerator(Entities + """
            [GenerateDtos(Types = DtoTypes.Update, ExcludeNavigationProperties = false)]
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

        diagnostics.Should().NotContain(d => d.Id.StartsWith("FAC10"));
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
