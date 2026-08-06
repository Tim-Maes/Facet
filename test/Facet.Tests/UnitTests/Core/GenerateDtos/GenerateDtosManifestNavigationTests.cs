using System.Text;
using Facet.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace Facet.Tests.UnitTests.Core.GenerateDtos;

/// <summary>
/// ExcludeNavigationProperties is driven entirely by the EF model manifest
/// (*.facetmodel.json AdditionalFile): mapped scalars are kept, navigations AND EF-ignored
/// properties drop, value-converted entity-typed columns survive, and IncludeProperties forces
/// members back in. Wiring a manifest into the compilation also flips the default: attributes
/// that leave the flag unset are shaped, with an explicit value winning in both directions.
/// There is no heuristic fallback — an uncovered shaped type drops nothing and is a FAC105
/// error (see GenerateDtosManifestDiagnosticsTests).
/// </summary>
public class GenerateDtosManifestNavigationTests
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

    private static string GenerateUpdateDto(string source, params string[] manifests)
    {
        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => (MetadataReference)MetadataReference.CreateFromFile(a.Location))
            .ToList();
        var compilation = CSharpCompilation.Create("ManifestNav",
            new[] { CSharpSyntaxTree.ParseText(source) }, references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        var additionalTexts = manifests
            .Select((text, i) => (AdditionalText)new TestAdditionalText($"/model/Context{i}.facetmodel.json", text))
            .ToArray();
        var driver = CSharpGeneratorDriver.Create(
            new[] { new GenerateDtosGeneratorHoist(new GenerateDtosGenerator()).AsSourceGenerator() },
            additionalTexts);
        var result = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out _).GetRunResult();
        return result.GeneratedTrees.Select(t => t.ToString())
            .First(t => t.Contains("UpdateParentRequest") && !t.Contains("interface UpdateParentRequest"));
    }

    private const string Entities = """
        using Facet;
        using System;
        using System.Collections.Generic;
        namespace ManifestNav;

        public class Child { public int Id { get; set; } }
        public class Money { public decimal Amount { get; set; } }
        """;

    [Fact]
    public void ManifestKeepSet_DropsNavigationsAndIgnoredProperties()
    {
        var dto = GenerateUpdateDto(Entities + """
            [GenerateDtos(Types = DtoTypes.Update, ExcludeNavigationProperties = true)]
            public class Parent
            {
                public int Id { get; set; }
                public string? Name { get; set; }
                public string? LegacyBlob { get; set; }
                public Child? Owner { get; set; }
                public List<Child> Items { get; set; } = new();
            }
            """,
            """
            {
              "version": 1,
              "entities": [
                { "clrType": "ManifestNav.Parent", "scalar": ["Id", "Name"], "nav": ["Owner", "Items"], "ignored": ["LegacyBlob"] }
              ]
            }
            """);

        dto.Should().Contain("Id");
        dto.Should().Contain("Name");
        dto.Should().NotContain("Owner", "the model designates it a navigation");
        dto.Should().NotContain("Items", "collection navigations drop too");
        dto.Should().NotContain("LegacyBlob", "explicitly ignored members are not data");
    }

    [Fact]
    public void Manifest_KeepsValueConvertedEntityTypedProperty()
    {
        // Money is an entity-shaped class, but the model maps it as a scalar column (value
        // converter) — a designation only the manifest can carry.
        var dto = GenerateUpdateDto(Entities + """
            [GenerateDtos(Types = DtoTypes.Update, ExcludeNavigationProperties = true)]
            public class Parent
            {
                public int Id { get; set; }
                public Money? Price { get; set; }
                public Child? Owner { get; set; }
            }
            """,
            """
            {
              "version": 1,
              "entities": [
                { "clrType": "ManifestNav.Parent", "scalar": ["Id", "Price"], "nav": ["Owner"] }
              ]
            }
            """);

        dto.Should().Contain("Price", "the model maps it as data despite its entity-like shape");
        dto.Should().NotContain("Owner");
    }

    [Fact]
    public void TypeNotInManifest_DropsNothing_AndIsAnError()
    {
        // No heuristic fallback: an uncovered type can't be shaped, so every member is kept
        // (so downstream code still compiles) and FAC105 is the signal — see
        // GenerateDtosManifestDiagnosticsTests for the diagnostic assertion.
        var dto = GenerateUpdateDto(Entities + """
            [GenerateDtos(Types = DtoTypes.Update, ExcludeNavigationProperties = true)]
            public class Parent
            {
                public int Id { get; set; }
                public string? LegacyBlob { get; set; }
                public Child? Owner { get; set; }
            }
            """,
            """
            {
              "version": 1,
              "entities": [
                { "clrType": "ManifestNav.SomeOtherType", "scalar": ["Id"] }
              ]
            }
            """);

        dto.Should().Contain("Owner", "with no manifest entry there is no designation to drop it by");
        dto.Should().Contain("LegacyBlob");
    }

    [Fact]
    public void IncludeProperties_OverridesManifestDrop()
    {
        var dto = GenerateUpdateDto(Entities + """
            [GenerateDtos(Types = DtoTypes.Update, ExcludeNavigationProperties = true,
                IncludeProperties = new[] { "Items" })]
            public class Parent
            {
                public int Id { get; set; }
                public List<Child> Items { get; set; } = new();
                public Child? Owner { get; set; }
            }
            """,
            """
            {
              "version": 1,
              "entities": [
                { "clrType": "ManifestNav.Parent", "scalar": ["Id"], "nav": ["Items", "Owner"] }
              ]
            }
            """);

        dto.Should().Contain("Items", "IncludeProperties is the aggregate-children escape hatch");
        dto.Should().NotContain("Owner");
    }

    [Fact]
    public void MalformedManifest_IsIgnoredAtomically()
    {
        // The file names Parent and then breaks: no partial application is allowed — an entity
        // registered with an accidental empty keep-set would drop every property. The whole
        // file is discarded, so Parent is uncovered: nothing is dropped (and FAC103/FAC105
        // fire — see the diagnostics tests).
        var dto = GenerateUpdateDto(Entities + """
            [GenerateDtos(Types = DtoTypes.Update, ExcludeNavigationProperties = true)]
            public class Parent
            {
                public int Id { get; set; }
                public string? LegacyBlob { get; set; }
                public Child? Owner { get; set; }
            }
            """,
            """
            {
              "version": 1,
              "entities": [
                { "clrType": "ManifestNav.Parent", "scalar": ["Id"
            """);

        dto.Should().Contain("Id");
        dto.Should().Contain("LegacyBlob");
        dto.Should().Contain("Owner", "a malformed manifest is discarded in full — no partial application, no guessing");
    }

    [Fact]
    public void MultipleManifests_UnionKeepSetsPerType()
    {
        var dto = GenerateUpdateDto(Entities + """
            [GenerateDtos(Types = DtoTypes.Update, ExcludeNavigationProperties = true)]
            public class Parent
            {
                public int Id { get; set; }
                public string? Name { get; set; }
                public Child? Owner { get; set; }
            }
            """,
            """
            {
              "version": 1,
              "entities": [ { "clrType": "ManifestNav.Parent", "scalar": ["Id"], "nav": ["Owner"] } ]
            }
            """,
            """
            {
              "version": 1,
              "entities": [ { "clrType": "ManifestNav.Parent", "scalar": ["Name"] } ]
            }
            """);

        dto.Should().Contain("Id");
        dto.Should().Contain("Name", "a property mapped as data in any context's manifest stays");
        dto.Should().NotContain("Owner");
    }

    [Fact]
    public void UnsetFlag_ManifestWired_DefaultsToShaping()
    {
        // Adding the manifest to AdditionalFiles is the project-level opt-in: no per-attribute
        // ExcludeNavigationProperties needed.
        var dto = GenerateUpdateDto(Entities + """
            [GenerateDtos(Types = DtoTypes.Update)]
            public class Parent
            {
                public int Id { get; set; }
                public string? Name { get; set; }
                public Child? Owner { get; set; }
            }
            """,
            """
            {
              "version": 1,
              "entities": [ { "clrType": "ManifestNav.Parent", "scalar": ["Id", "Name"], "nav": ["Owner"] } ]
            }
            """);

        dto.Should().Contain("Name");
        dto.Should().NotContain("Owner", "a wired manifest flips the ExcludeNavigationProperties default to true");
    }

    [Fact]
    public void UnsetFlag_NoManifest_CopiesEverything()
    {
        var dto = GenerateUpdateDto(Entities + """
            [GenerateDtos(Types = DtoTypes.Update)]
            public class Parent
            {
                public int Id { get; set; }
                public Child? Owner { get; set; }
            }
            """);

        dto.Should().Contain("Owner", "without a manifest wired, the default stays plain property copying");
    }

    [Fact]
    public void ExplicitFalse_ManifestWired_OptsOut()
    {
        // The per-type escape hatch for non-entity source types in a manifest-wired project —
        // an explicit value wins over the flipped default.
        var dto = GenerateUpdateDto(Entities + """
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
              "entities": [ { "clrType": "ManifestNav.Parent", "scalar": ["Id"], "nav": ["Owner"] } ]
            }
            """);

        dto.Should().Contain("Owner", "ExcludeNavigationProperties = false opts the type out of manifest shaping");
    }

    [Fact]
    public void DuplicateManifestWiring_IsIdempotent()
    {
        // FacetEfDesignTime auto-wires a project's own manifests; a hand-written
        // <AdditionalFiles> glob may match the same files again. The reader merges per file
        // by union, so seeing the same content twice must change nothing.
        const string manifest = """
            {
              "version": 1,
              "entities": [ { "clrType": "ManifestNav.Parent", "scalar": ["Id", "Name"], "nav": ["Owner"] } ]
            }
            """;
        var dto = GenerateUpdateDto(Entities + """
            [GenerateDtos(Types = DtoTypes.Update)]
            public class Parent
            {
                public int Id { get; set; }
                public string? Name { get; set; }
                public Child? Owner { get; set; }
            }
            """,
            manifest, manifest);

        dto.Should().Contain("Name");
        dto.Should().NotContain("Owner", "duplicate wiring of the same manifest content is a no-op");
    }

    [Fact]
    public void GenerateAuditableDtos_ManifestWired_KeepsLegacyShape()
    {
        // The obsolete attribute has no way to express or opt out of manifest shaping, so a
        // wired manifest must not change what it generates.
        var dto = GenerateUpdateDto(Entities + """
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
              "entities": [ { "clrType": "ManifestNav.Parent", "scalar": ["Id"], "nav": ["Owner"] } ]
            }
            """);

        dto.Should().Contain("Owner", "the obsolete attribute is exempt from the wired-manifest default");
    }

    [Fact]
    public void AssemblyLevelDeclaration_UsesTheManifest()
    {
        // The assembly-level entry point shares the class-level pipeline, so a
        // [assembly: GenerateDtosFor] registration gets the same manifest-driven shaping.
        var dto = GenerateUpdateDto("""
            using Facet;
            using System;
            using System.Collections.Generic;

            [assembly: GenerateDtosFor(typeof(ManifestNav.Parent),
                Types = DtoTypes.Update, ExcludeNavigationProperties = true)]

            namespace ManifestNav;

            public class Child { public int Id { get; set; } }

            public class Parent
            {
                public int Id { get; set; }
                public string? Name { get; set; }
                public Child? Owner { get; set; }
            }
            """,
            """
            {
              "version": 1,
              "entities": [
                { "clrType": "ManifestNav.Parent", "scalar": ["Id", "Name"], "nav": ["Owner"] }
              ]
            }
            """);

        dto.Should().Contain("Name");
        dto.Should().NotContain("Owner", "assembly-level declarations follow the model's designation too");
    }
}
