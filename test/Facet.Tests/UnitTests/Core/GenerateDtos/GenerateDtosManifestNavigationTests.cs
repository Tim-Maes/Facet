using System.Text;
using Facet.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace Facet.Tests.UnitTests.Core.GenerateDtos;

/// <summary>
/// ExcludeNavigationProperties with an EF model manifest (*.facetmodel.json AdditionalFile):
/// the model's own designation replaces the same-assembly heuristic — mapped scalars are
/// kept, navigations AND EF-ignored properties drop, value-converted entity-typed columns
/// survive — while unlisted types, unsupported manifest versions, malformed files, and
/// IncludeProperties keep their existing behavior.
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
                { "clrType": "ManifestNav.Parent", "scalar": ["Id", "Name"], "nav": ["Owner", "Items"] }
              ]
            }
            """);

        dto.Should().Contain("Id");
        dto.Should().Contain("Name");
        dto.Should().NotContain("Owner", "the model designates it a navigation");
        dto.Should().NotContain("Items", "collection navigations drop too");
        dto.Should().NotContain("LegacyBlob", "properties the model does not map at all (EF-ignored) are not data");
    }

    [Fact]
    public void Manifest_KeepsValueConvertedEntityTypedProperty()
    {
        // Money is a same-assembly class, so the heuristic calls it a navigation — but the
        // model maps it as a scalar column (value converter). The manifest must win.
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
    public void TypeNotInManifest_FallsBackToHeuristic()
    {
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

        dto.Should().NotContain("Owner", "the heuristic still drops same-assembly class-typed properties");
        dto.Should().Contain("LegacyBlob", "without a manifest entry there is no way to know the model ignores it");
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

        dto.Should().Contain("Items", "IncludeProperties is the aggregate-children escape hatch in both tiers");
        dto.Should().NotContain("Owner");
    }

    [Fact]
    public void UnsupportedManifestVersion_FallsBackToHeuristic()
    {
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
              "version": 2,
              "entities": [
                { "clrType": "ManifestNav.Parent", "scalar": ["Id"] }
              ]
            }
            """);

        dto.Should().Contain("LegacyBlob", "a future-version manifest is ignored rather than misread");
        dto.Should().NotContain("Owner", "the heuristic remains in force");
    }

    [Fact]
    public void MalformedManifest_IsIgnoredAtomically()
    {
        // The file names Parent and then breaks: no partial application is allowed — an
        // entity registered with an accidental empty keep-set would drop every property.
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
        dto.Should().Contain("LegacyBlob", "a malformed manifest must not half-apply");
        dto.Should().NotContain("Owner", "the heuristic remains in force");
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
              "entities": [ { "clrType": "ManifestNav.Parent", "scalar": ["Id"] } ]
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
}
