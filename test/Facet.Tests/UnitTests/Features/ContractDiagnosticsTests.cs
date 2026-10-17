using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Facet.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Facet.Tests.UnitTests.Features;

/// <summary>
/// Tests for the diagnostics that guard a contract assembly: FAC116 (a property still typed from
/// the source layer) and FAC117 (a mapping hook that cannot run because the mappings are
/// suppressed).
/// </summary>
/// <remarks>
/// Both need a real second assembly — the source layer the contract must not reference — so it is
/// compiled separately and referenced as metadata rather than parsed into the same compilation.
/// </remarks>
public class ContractDiagnosticsTests
{
    private const string SourceLayer = """
        namespace AppLayer;

        public class UserUseCaseModel
        {
            public int Id { get; set; }
            public string Name { get; set; } = "";
            public AddressUseCaseModel? Address { get; set; }
            public System.Collections.Generic.List<OrderUseCaseModel> Orders { get; set; } = new();
        }

        public class AddressUseCaseModel { public string City { get; set; } = ""; }
        public class OrderUseCaseModel { public int Number { get; set; } }
        """;

    [Fact]
    public void Fac116_ReportsAPropertyStillTypedFromTheSourceLayer()
    {
        // Address is typed as the app-layer class, which is what happens when NestedFacets is not
        // set: the reference the contract assembly exists to avoid comes back through the property.
        var diagnostics = Analyze("""
            using Facet;
            namespace ApiModels;

            [Facet(typeof(AppLayer.UserUseCaseModel))]
            public partial class UserApiModel
            {
                public int Id { get; set; }
                public global::AppLayer.AddressUseCaseModel? Address { get; set; }
            }
            """);

        var fac116 = diagnostics.Where(d => d.Id == "FAC116").ToList();

        fac116.Should().HaveCount(1);
        fac116[0].GetMessage().Should().Contain("UserApiModel.Address");
        fac116[0].GetMessage().Should().Contain("AppLayer.AddressUseCaseModel");
        fac116[0].GetMessage().Should().Contain("NestedFacets");
    }

    [Fact]
    public void Fac116_LooksThroughCollections()
    {
        var diagnostics = Analyze("""
            using Facet;
            namespace ApiModels;

            [Facet(typeof(AppLayer.UserUseCaseModel))]
            public partial class UserApiModel
            {
                public System.Collections.Generic.List<global::AppLayer.OrderUseCaseModel> Orders { get; set; } = new();
            }
            """);

        diagnostics.Should().Contain(d => d.Id == "FAC116" && d.GetMessage().Contains("Orders"));
    }

    [Fact]
    public void Fac116_IsSilentWhenThePropertyTypeIsItselfAFacet()
    {
        // What NestedFacets produces, and the shape a clean contract has.
        var diagnostics = Analyze("""
            using Facet;
            namespace ApiModels;

            [Facet(typeof(AppLayer.AddressUseCaseModel))]
            public partial class AddressApiModel { public string City { get; set; } = ""; }

            [Facet(typeof(AppLayer.UserUseCaseModel), NestedFacets = new[] { typeof(AddressApiModel) })]
            public partial class UserApiModel
            {
                public AddressApiModel? Address { get; set; }
            }
            """);

        diagnostics.Should().NotContain(d => d.Id == "FAC116");
    }

    [Fact]
    public void Fac116_IsSilentWhenMappingsAreNotSuppressed()
    {
        // Without Facet_GenerateMappings=false the project has not declared its facets to be
        // contracts, so a source-layer property type is nobody's mistake.
        var diagnostics = Analyze("""
            using Facet;
            namespace ApiModels;

            [Facet(typeof(AppLayer.UserUseCaseModel))]
            public partial class UserApiModel
            {
                public global::AppLayer.AddressUseCaseModel? Address { get; set; }
            }
            """,
            generateMappings: true);

        diagnostics.Should().NotContain(d => d.Id == "FAC116");
    }

    [Fact]
    public void Fac117_ReportsAHookThatCannotRun()
    {
        var diagnostics = Analyze("""
            using Facet;
            namespace ApiModels;

            public sealed class SomeConfig { }

            [Facet(typeof(AppLayer.AddressUseCaseModel), Configuration = typeof(SomeConfig),
                   AfterMapConfiguration = typeof(SomeConfig))]
            public partial class AddressApiModel { public string City { get; set; } = ""; }
            """);

        var fac117 = diagnostics.Where(d => d.Id == "FAC117").ToList();

        fac117.Should().HaveCount(1);
        fac117[0].GetMessage().Should().Contain("Configuration and AfterMapConfiguration");
        fac117[0].GetMessage().Should().Contain("has no effect");
    }

    [Fact]
    public void Fac117_IsSilentWhenMappingsAreGenerated()
    {
        var diagnostics = Analyze("""
            using Facet;
            namespace ApiModels;

            public sealed class SomeConfig { }

            [Facet(typeof(AppLayer.AddressUseCaseModel), Configuration = typeof(SomeConfig))]
            public partial class AddressApiModel { public string City { get; set; } = ""; }
            """,
            generateMappings: true);

        diagnostics.Should().NotContain(d => d.Id == "FAC117");
    }

    // --- harness ---

    private static ImmutableArray<Diagnostic> Analyze(string contractSource, bool generateMappings = false)
    {
        var sourceLayer = Compile("AppLayer", SourceLayer, BaseReferences());

        var references = BaseReferences();
        references.Add(sourceLayer.ToMetadataReference());

        var contract = Compile("ApiModels", contractSource, references);

        return Run(contract, new FacetContractAnalyzer(), new Dictionary<string, string>
        {
            ["build_property.Facet_GenerateMappings"] = generateMappings ? "true" : "false",
        });
    }

    private static List<MetadataReference> BaseReferences()
    {
        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => (MetadataReference)MetadataReference.CreateFromFile(a.Location))
            .ToList();

        var attributesPath = typeof(global::Facet.FacetAttribute).Assembly.Location;
        if (!string.IsNullOrEmpty(attributesPath) && references.All(r => r.Display != attributesPath))
            references.Add(MetadataReference.CreateFromFile(attributesPath));

        return references;
    }

    private static CSharpCompilation Compile(string name, string source, List<MetadataReference> references) =>
        CSharpCompilation.Create(
            name,
            new[] { CSharpSyntaxTree.ParseText(source, path: name + ".cs") },
            references,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));

    private static ImmutableArray<Diagnostic> Run(
        Compilation compilation,
        DiagnosticAnalyzer analyzer,
        Dictionary<string, string> buildProperties)
    {
        var options = new AnalyzerOptions(
            ImmutableArray<AdditionalText>.Empty,
            new TestConfigOptionsProvider(buildProperties));

        return compilation
            .WithAnalyzers(ImmutableArray.Create(analyzer), options)
            .GetAnalyzerDiagnosticsAsync().Result;
    }

    private sealed class TestConfigOptionsProvider : AnalyzerConfigOptionsProvider
    {
        private readonly TestConfigOptions _global;

        public TestConfigOptionsProvider(Dictionary<string, string> values) =>
            _global = new TestConfigOptions(values);

        public override AnalyzerConfigOptions GlobalOptions => _global;

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => Empty;

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => Empty;

        private static TestConfigOptions Empty { get; } = new(new Dictionary<string, string>());
    }

    private sealed class TestConfigOptions(Dictionary<string, string> values) : AnalyzerConfigOptions
    {
        public override bool TryGetValue(string key, out string value)
        {
            if (values.TryGetValue(key, out var found))
            {
                value = found;
                return true;
            }

            value = null!;
            return false;
        }
    }
}
