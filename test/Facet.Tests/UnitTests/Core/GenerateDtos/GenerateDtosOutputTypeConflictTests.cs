using Facet.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Facet.Tests.UnitTests.Core.GenerateDtos;

/// <summary>
/// Driver-based tests for FAC101: combining multiple concrete output kinds in one
/// <c>OutputType</c> flags value is a hard error (their generated type names would collide),
/// while Interface + one concrete kind remains valid. These run the generator against an
/// in-memory compilation because an entity that triggers FAC101 cannot live in TestModels —
/// it would fail this test project's own build.
/// </summary>
public class GenerateDtosOutputTypeConflictTests
{
    private static CSharpCompilation CreateCompilation(string source)
    {
        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => (MetadataReference)MetadataReference.CreateFromFile(a.Location))
            .ToList();

        return CSharpCompilation.Create(
            "ConflictTests",
            new[] { CSharpSyntaxTree.ParseText(source) },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
    }

    private static (ImmutableDiagnosticList Diagnostics, GeneratorDriverRunResult Result) RunGenerator(string source)
    {
        var compilation = CreateCompilation(source);
        var driver = CSharpGeneratorDriver.Create(new GenerateDtosGeneratorHoist(new GenerateDtosGenerator()));
        var ranDriver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);
        return (new ImmutableDiagnosticList(diagnostics), ranDriver.GetRunResult());
    }

    /// <summary>Small wrapper so test assertions read naturally.</summary>
    public sealed class ImmutableDiagnosticList
    {
        public ImmutableDiagnosticList(System.Collections.Immutable.ImmutableArray<Diagnostic> diagnostics) => All = diagnostics;
        public System.Collections.Immutable.ImmutableArray<Diagnostic> All { get; }
    }

    [Fact]
    public void TwoConcreteKinds_ReportsFac101Error_AndGeneratesNothing()
    {
        var (diagnostics, result) = RunGenerator("""
            using Facet;
            namespace ConflictTests;

            [GenerateDtos(Types = DtoTypes.Create, OutputType = OutputType.Class | OutputType.Record)]
            public class Collide
            {
                public int Id { get; set; }
                public string? Name { get; set; }
            }
            """);

        var fac101 = diagnostics.All.Where(d => d.Id == "FAC101").ToList();
        fac101.Should().HaveCount(1, "combining two concrete output kinds must be rejected");
        fac101[0].Severity.Should().Be(DiagnosticSeverity.Error);
        fac101[0].GetMessage().Should().Contain("Collide").And.Contain("Class, Record");

        result.GeneratedTrees.Should().BeEmpty("a conflicting attribute must not emit colliding sources");
    }

    [Fact]
    public void InterfacePlusTwoConcreteKinds_StillReportsFac101()
    {
        var (diagnostics, result) = RunGenerator("""
            using Facet;
            namespace ConflictTests;

            [GenerateDtos(Types = DtoTypes.Create, OutputType = OutputType.Interface | OutputType.Struct | OutputType.PartialClass)]
            public class Collide
            {
                public int Id { get; set; }
            }
            """);

        diagnostics.All.Should().Contain(d => d.Id == "FAC101" && d.Severity == DiagnosticSeverity.Error,
            "Interface composes with at most ONE concrete kind");
        result.GeneratedTrees.Should().BeEmpty();
    }

    [Fact]
    public void InterfacePlusOneConcreteKind_IsValid_NoFac101()
    {
        var (diagnostics, result) = RunGenerator("""
            using Facet;
            namespace ConflictTests;

            [GenerateDtos(Types = DtoTypes.Create, OutputType = OutputType.Interface | OutputType.PartialClass)]
            public class Fine
            {
                public int Id { get; set; }
                public string? Name { get; set; }
            }
            """);

        diagnostics.All.Should().NotContain(d => d.Id == "FAC101");
        result.GeneratedTrees.Should().HaveCount(2, "Interface + PartialClass emit one file each");
    }

    [Fact]
    public void PartialWithoutKind_ReportsFac102Error_AndGeneratesNothing()
    {
        var (diagnostics, result) = RunGenerator("""
            using Facet;
            namespace ConflictTests;

            [GenerateDtos(Types = DtoTypes.Create, OutputType = OutputType.Partial)]
            public class Kindless
            {
                public int Id { get; set; }
            }
            """);

        var fac102 = diagnostics.All.Where(d => d.Id == "FAC102").ToList();
        fac102.Should().HaveCount(1, "a modifier with nothing to modify should fail loudly, not silently generate nothing");
        fac102[0].Severity.Should().Be(DiagnosticSeverity.Error);
        fac102[0].GetMessage().Should().Contain("Kindless").And.Contain("Partial");

        result.GeneratedTrees.Should().BeEmpty();
    }

    [Fact]
    public void ConflictOnOneAttribute_DoesNotSuppressOtherAttributes()
    {
        var (diagnostics, result) = RunGenerator("""
            using Facet;
            namespace ConflictTests;

            [GenerateDtos(Types = DtoTypes.Create, OutputType = OutputType.Class | OutputType.Record)]
            [GenerateDtos(Types = DtoTypes.Update, OutputType = OutputType.Record)]
            public class Mixed
            {
                public int Id { get; set; }
                public string? Name { get; set; }
            }
            """);

        diagnostics.All.Should().Contain(d => d.Id == "FAC101");
        result.GeneratedTrees.Should().HaveCount(1, "the valid Update attribute still generates its DTO");
    }
}
