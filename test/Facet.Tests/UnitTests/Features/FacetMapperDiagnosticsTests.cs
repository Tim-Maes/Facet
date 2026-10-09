using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Facet.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Facet.Tests.UnitTests.Features;

/// <summary>
/// Driver-based tests for the mapper generator's own diagnostics: FAC110 (a facet property left
/// unmapped), FAC111 (an unusable registration), FAC112 (no reverse mapping possible), FAC113 (two
/// mappers that would share a name) and FAC114 (a facet in this same compilation).
/// </summary>
/// <remarks>
/// Run against a compilation built here rather than against the test assembly's own build, so each
/// case can state exactly the situation it is about — and so a diagnostic that should fire can be
/// asserted rather than merely appearing in build output.
/// </remarks>
public class FacetMapperDiagnosticsTests
{
    [Fact]
    public void Fac110_ReportsAnUnmappedProperty_WithTheReason()
    {
        var diagnostics = Run("""
            using Facet;
            [assembly: FacetMapper(typeof(MapperDiag.Source), typeof(MapperDiag.Target))]

            namespace MapperDiag;

            public class Source { public int Id { get; set; } public System.DateTime When { get; set; } }

            public class Target
            {
                public int Id { get; set; }
                public string Renamed { get; set; } = "";
                public int When { get; set; }
            }
            """);

        var fac110 = diagnostics.Where(d => d.Id == "FAC110").ToList();

        fac110.Should().HaveCount(2);
        fac110.Should().Contain(d =>
            d.GetMessage().Contains("Renamed") && d.GetMessage().Contains("no public readable property"));
        fac110.Should().Contain(d =>
            d.GetMessage().Contains("When") && d.GetMessage().Contains("not implicitly convertible"));
    }

    [Fact]
    public void Fac110_IsSilentForAnExcludedProperty()
    {
        var diagnostics = Run("""
            using Facet;
            [assembly: FacetMapper(typeof(MapperDiag.Source), typeof(MapperDiag.Target), "Renamed")]

            namespace MapperDiag;

            public class Source { public int Id { get; set; } }
            public class Target { public int Id { get; set; } public string Renamed { get; set; } = ""; }
            """);

        diagnostics.Should().NotContain(d => d.Id == "FAC110");
    }

    [Fact]
    public void Fac111_ReportsAFacetWithNoParameterlessConstructor()
    {
        // A projection can only construct with an object initializer, so this cannot be generated
        // at all — hence an error rather than a warning.
        var diagnostics = Run("""
            using Facet;
            [assembly: FacetMapper(typeof(MapperDiag.Source), typeof(MapperDiag.Target))]

            namespace MapperDiag;

            public class Source { public int Id { get; set; } }

            public class Target
            {
                public Target(int id) { Id = id; }
                public int Id { get; set; }
            }
            """);

        var fac111 = diagnostics.Where(d => d.Id == "FAC111").ToList();

        fac111.Should().HaveCount(1);
        fac111[0].Severity.Should().Be(DiagnosticSeverity.Error);
        fac111[0].GetMessage().Should().Contain("parameterless constructor");
    }

    [Fact]
    public void Fac112_ReportsGenerateToSource_WhenTheSourceCannotBeConstructed()
    {
        var diagnostics = Run("""
            using Facet;
            [assembly: FacetMapper(typeof(MapperDiag.Source), typeof(MapperDiag.Target),
                GenerateToSource = true)]

            namespace MapperDiag;

            public class Source
            {
                public Source(int id) { Id = id; }
                public int Id { get; set; }
            }

            public class Target { public int Id { get; set; } }
            """);

        var fac112 = diagnostics.Where(d => d.Id == "FAC112").ToList();

        fac112.Should().HaveCount(1);
        fac112[0].GetMessage().Should().Contain("parameterless constructor");
    }

    [Fact]
    public void Fac113_ReportsTwoMappersThatWouldShareAName()
    {
        var diagnostics = Run("""
            using Facet;
            [assembly: FacetMapper(typeof(MapperDiag.Source), typeof(MapperDiag.Target))]
            [assembly: FacetMapper(typeof(MapperDiag.OtherSource), typeof(MapperDiag.Target))]

            namespace MapperDiag;

            public class Source { public int Id { get; set; } }
            public class OtherSource { public int Id { get; set; } }
            public class Target { public int Id { get; set; } }
            """);

        var fac113 = diagnostics.Where(d => d.Id == "FAC113").ToList();

        fac113.Should().HaveCount(1);
        fac113[0].GetMessage().Should().Contain("TargetMapper");
        fac113[0].GetMessage().Should().Contain("set Name");
    }

    [Fact]
    public void Fac113_IsSilentWhenTheSecondMapperIsNamed()
    {
        var diagnostics = Run("""
            using Facet;
            [assembly: FacetMapper(typeof(MapperDiag.Source), typeof(MapperDiag.Target))]
            [assembly: FacetMapper(typeof(MapperDiag.OtherSource), typeof(MapperDiag.Target),
                Name = "TargetFromOtherMapper")]

            namespace MapperDiag;

            public class Source { public int Id { get; set; } }
            public class OtherSource { public int Id { get; set; } }
            public class Target { public int Id { get; set; } }
            """);

        diagnostics.Should().NotContain(d => d.Id == "FAC113");
    }

    [Fact]
    public void Fac114_ReportsAFacetDeclaredInTheSameCompilation()
    {
        // FacetGenerator adds this type's members in a pass FacetMapperGenerator cannot observe,
        // so the mapper would silently map nothing.
        var diagnostics = Run("""
            using Facet;
            [assembly: FacetMapper(typeof(MapperDiag.Source), typeof(MapperDiag.LocalFacet))]

            namespace MapperDiag;

            public class Source { public int Id { get; set; } }

            [Facet(typeof(Source))]
            public partial class LocalFacet;
            """);

        var fac114 = diagnostics.Where(d => d.Id == "FAC114").ToList();

        fac114.Should().HaveCount(1);
        fac114[0].GetMessage().Should().Contain("not visible to this generator");
    }

    [Fact]
    public void Fac114_IsSilentForAHandWrittenTypeInTheSameCompilation()
    {
        // No [Facet], so nothing is generated onto it and everything it has is visible.
        var diagnostics = Run("""
            using Facet;
            [assembly: FacetMapper(typeof(MapperDiag.Source), typeof(MapperDiag.PlainTarget))]

            namespace MapperDiag;

            public class Source { public int Id { get; set; } }
            public class PlainTarget { public int Id { get; set; } }
            """);

        diagnostics.Should().NotContain(d => d.Id == "FAC114");
    }

    [Theory]
    [InlineData("System.Collections.Immutable.ImmutableArray<MapperDiag.ChildFacet>")]
    [InlineData("System.Collections.Immutable.ImmutableList<MapperDiag.ChildFacet>")]
    [InlineData("System.Collections.ObjectModel.Collection<MapperDiag.ChildFacet>")]
    [InlineData("MapperDiag.ChildFacet[]")]
    [InlineData("System.Collections.Generic.IReadOnlyList<MapperDiag.ChildFacet>")]
    public void NestedCollections_SupportEveryShapeTheFacetGeneratorSupports(string facetCollectionType)
    {
        // The mapper generator used to carry its own seven-entry table of materialising calls and
        // report anything else as unmappable — so a facet member [Facet] handles happily was
        // rejected here. It now shares GeneratorUtilities.WrapCollectionProjection, so the two
        // generators accept the same set by construction.
        var diagnostics = Run($$"""
            using Facet;
            [assembly: FacetMapper(typeof(MapperDiag.Child), typeof(MapperDiag.ChildFacet))]
            [assembly: FacetMapper(typeof(MapperDiag.Parent), typeof(MapperDiag.ParentFacet))]

            namespace MapperDiag;

            public class Child { public string Name { get; set; } = ""; }
            public class ChildFacet { public string Name { get; set; } = ""; }

            public class Parent
            {
                public System.Collections.Generic.List<Child> Children { get; set; } = new();
            }

            public class ParentFacet
            {
                public {{facetCollectionType}} Children { get; set; } = default!;
            }
            """);

        diagnostics.Should().NotContain(d => d.Id == "FAC110",
            "the shared materialiser covers this collection shape");
    }

    private static ImmutableArray<Diagnostic> Run(string source)
    {
        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => (MetadataReference)MetadataReference.CreateFromFile(a.Location))
            .ToList();

        var attributesPath = typeof(global::Facet.FacetAttribute).Assembly.Location;
        if (!string.IsNullOrEmpty(attributesPath) && references.All(r => r.Display != attributesPath))
            references.Add(MetadataReference.CreateFromFile(attributesPath));

        var compilation = CSharpCompilation.Create(
            "MapperDiagTests",
            new[] { CSharpSyntaxTree.ParseText(source) },
            references,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));

        // The generator directly, not its SourceGenerator.Foundations hoist: the hoist only adds
        // exception isolation and embedded-assembly resolution, neither of which this generator
        // needs, and running it raw keeps a generator fault visible as a test failure.
        CSharpGeneratorDriver
            .Create(new FacetMapperGenerator())
            .RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);

        return diagnostics;
    }
}
