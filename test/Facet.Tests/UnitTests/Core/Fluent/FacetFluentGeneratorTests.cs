using System.Collections.Immutable;
using System.Text;
using Facet.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Facet.Tests.UnitTests.Core.Fluent;

/// <summary>
/// The fluent query surface is generated from the EF model manifest, gated by the
/// FacetFluent MSBuild property, with chain shapes discovered from the consuming code
/// itself. The strongest assertions here compile a CONSUMER of the generated surface inside
/// the test and require the result to be error-free — the generated code is not just
/// inspected as text, it must actually satisfy the chains as written, including the static
/// unreachability of navigations a chain did not include.
/// </summary>
public class FacetFluentGeneratorTests
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

    private sealed class TestOptions : AnalyzerConfigOptions
    {
        private readonly Dictionary<string, string> _values;

        public TestOptions(Dictionary<string, string> values) => _values = values;

        public override bool TryGetValue(string key, out string value)
            => _values.TryGetValue(key, out value!);
    }

    private sealed class TestOptionsProvider : AnalyzerConfigOptionsProvider
    {
        private readonly TestOptions _options;

        public TestOptionsProvider(Dictionary<string, string> values) => _options = new TestOptions(values);

        public override AnalyzerConfigOptions GlobalOptions => _options;

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => _options;

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => _options;
    }

    private static (Compilation Output, ImmutableArray<Diagnostic> GeneratorDiagnostics) RunFluent(
        string source,
        string? manifest,
        string? maxChainDepth = null,
        bool enabled = true,
        bool referenceEfCore = true)
    {
        // The generated surface uses DbContext/EF.Property/Queryable/expression trees; make
        // sure those assemblies are loaded before the loaded-assembly closure is captured.
        _ = typeof(Microsoft.EntityFrameworkCore.DbContext).Assembly;
        _ = typeof(System.Linq.Queryable).Assembly;
        _ = typeof(System.Linq.Expressions.Expression).Assembly;

        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Where(a => referenceEfCore || !(a.GetName().Name ?? string.Empty).StartsWith("Microsoft.EntityFrameworkCore"))
            .Select(a => (MetadataReference)MetadataReference.CreateFromFile(a.Location))
            .ToList();

        var compilation = CSharpCompilation.Create("FluentRun",
            new[] { CSharpSyntaxTree.ParseText(source, path: "Consumer.cs") }, references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        var options = new Dictionary<string, string>();
        if (enabled) options["build_property.FacetFluent"] = "true";
        if (maxChainDepth != null) options["build_property.FacetMaxChainDepth"] = maxChainDepth;

        var additionalTexts = manifest == null
            ? Array.Empty<AdditionalText>()
            : new AdditionalText[] { new TestAdditionalText("/model/AppDbContext.facetmodel.json", manifest) };

        var driver = CSharpGeneratorDriver.Create(
            new[] { new FacetFluentGeneratorHoist(new FacetFluentGenerator()).AsSourceGenerator() },
            additionalTexts,
            optionsProvider: new TestOptionsProvider(options));

        var result = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out _);
        return (output, result.GetRunResult().Diagnostics);
    }

    private const string Entities = """
        using System;
        using System.Collections.Generic;
        namespace FluentTest;

        public class Order
        {
            public int Id { get; set; }
            public DateTime PlacedAt { get; set; }
            public decimal Total { get; set; }
            public int? UserId { get; set; }
            public User? User { get; set; }
            public List<OrderItem> Items { get; set; } = new();
        }

        public class User
        {
            public int Id { get; set; }
            public string Name { get; set; } = null!;
        }

        public class OrderItem
        {
            public int Id { get; set; }
            public int Quantity { get; set; }
            public int ProductId { get; set; }
            public Product? Product { get; set; }
            public Note? Note { get; set; }
        }

        public class Note
        {
            public int Id { get; set; }
            public string Text { get; set; } = null!;
        }

        public class Product
        {
            public int Id { get; set; }
            public string Sku { get; set; } = null!;
        }
        """;

    private const string Manifest = """
        {
          "version": 1,
          "entities": [
            { "clrType": "FluentTest.Order", "key": ["Id"], "scalar": ["Id", "PlacedAt", "Total", "UserId"], "nav": ["Items", "User"], "navOptional": ["User"] },
            { "clrType": "FluentTest.User", "key": ["Id"], "scalar": ["Id", "Name"] },
            { "clrType": "FluentTest.OrderItem", "key": ["Id"], "scalar": ["Id", "Quantity", "ProductId"], "nav": ["Note", "Product"], "navOptional": ["Note"] },
            { "clrType": "FluentTest.Product", "key": ["Id"], "scalar": ["Id", "Sku"] },
            { "clrType": "FluentTest.Note", "key": ["Id"], "scalar": ["Id", "Text"] }
          ]
        }
        """;

    [Fact]
    public void Disabled_GeneratesNothingAndStaysSilent()
    {
        var (output, diagnostics) = RunFluent(Entities, Manifest, enabled: false);

        diagnostics.Should().BeEmpty();
        output.SyntaxTrees.Should().HaveCount(1, "without the FacetFluent property the generator must not add sources");
    }

    [Fact]
    public void Enabled_WithoutManifestOrChains_ReportsFac120_AsWarning()
    {
        // The first 'dotnet ef migrations add' builds the project before any manifest can
        // exist; an unconditional error would deadlock that bootstrap.
        var (_, diagnostics) = RunFluent(Entities, manifest: null);

        var fac120 = diagnostics.Should().ContainSingle(d => d.Id == "FAC120").Subject;
        fac120.Severity.Should().Be(DiagnosticSeverity.Warning);
    }

    [Fact]
    public void Enabled_WithoutManifest_ButWithChains_EscalatesFac120_ToError()
    {
        var (_, diagnostics) = RunFluent(Entities + """

            public static class Consumer
            {
                public static async System.Threading.Tasks.Task Use(Microsoft.EntityFrameworkCore.DbContext ctx)
                {
                    _ = await ctx.FacetOrder().WithUser().ToListAsync();
                }
            }
            """, manifest: null);

        var fac120 = diagnostics.Should().ContainSingle(d => d.Id == "FAC120").Subject;
        fac120.Severity.Should().Be(DiagnosticSeverity.Error,
            "once chains are written, the missing manifest must explain the CS1061 wall loudly");
        fac120.Location.Should().NotBe(Location.None, "anchored at a chain so the error lands near the broken call sites");
    }

    [Fact]
    public void Enabled_WithoutEfCore_ReportsFac122()
    {
        var (_, diagnostics) = RunFluent(Entities, Manifest, referenceEfCore: false);

        diagnostics.Should().ContainSingle(d => d.Id == "FAC122");
    }

    [Fact]
    public void ConsumerChain_CompilesAgainstTheGeneratedSurface()
    {
        // The full documented shape of the API, as a consumer would write it. If any
        // generated piece is off — interfaces, variance, selectors, markers, terminals,
        // typed keys — this compilation has errors.
        var (output, diagnostics) = RunFluent(Entities + """

            public static class Consumer
            {
                public static async System.Threading.Tasks.Task Use(Microsoft.EntityFrameworkCore.DbContext ctx)
                {
                    var orders = await ctx.FacetOrder()
                        .WithUser()
                        .WithItems(i => i.WithProduct())
                        .ToListAsync();

                    decimal total = orders[0].Total;
                    string? userName = orders[0].User?.Name;
                    string sku = orders[0].Items[0].Product.Sku;

                    var singleById = await ctx.FacetOrder().WithUser().GetByKeyAsync(1);
                    string? maybe = singleById?.User?.Name;

                    var filtered = await ctx.FacetOrder()
                        .Where(o => o.Total > 10m)
                        .FirstOrDefaultAsync();
                    _ = filtered?.PlacedAt;
                }
            }
            """, Manifest);

        diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();
        output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();
    }

    [Fact]
    public void UnincludedNavigation_IsStaticallyUnreachable()
    {
        var (output, _) = RunFluent(Entities + """

            public static class Consumer
            {
                public static async System.Threading.Tasks.Task Use(Microsoft.EntityFrameworkCore.DbContext ctx)
                {
                    var bare = await ctx.FacetOrder().FirstOrDefaultAsync();
                    _ = bare!.User; // not included -> must not exist on the bare shape
                }
            }
            """, Manifest);

        output.GetDiagnostics().Should().Contain(d => d.Id == "CS1061",
            "a navigation the chain did not include must not be reachable through that chain's shape");
    }

    [Fact]
    public void OrderOfChaining_LandsOnTheSameComposedShape()
    {
        var (output, diagnostics) = RunFluent(Entities + """

            public static class Consumer
            {
                public static async System.Threading.Tasks.Task Use(Microsoft.EntityFrameworkCore.DbContext ctx)
                {
                    var a = await ctx.FacetOrder().WithUser().WithItems(i => i.WithProduct()).FirstOrDefaultAsync();
                    var b = await ctx.FacetOrder().WithItems(i => i.WithProduct()).WithUser().FirstOrDefaultAsync();
                    // Same static type either way: assignable in both directions.
                    a = b;
                    b = a;
                }
            }
            """, Manifest);

        diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();
        output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();
    }

    [Fact]
    public void ChainBeyondMaxDepth_ReportsFac121_Anchored()
    {
        var (_, diagnostics) = RunFluent(Entities + """

            public static class Consumer
            {
                public static async System.Threading.Tasks.Task Use(Microsoft.EntityFrameworkCore.DbContext ctx)
                {
                    _ = await ctx.FacetOrder().WithItems(i => i.WithProduct()).ToListAsync();
                }
            }
            """, Manifest, maxChainDepth: "1");

        var fac121 = diagnostics.Should().ContainSingle(d => d.Id == "FAC121").Subject;
        fac121.Severity.Should().Be(DiagnosticSeverity.Warning);
        fac121.Location.Should().NotBe(Location.None, "the warning must point at the chain that was capped");
        fac121.Location.GetLineSpan().Path.Should().Be("Consumer.cs");
    }

    [Fact]
    public void RepeatedSteps_WithDifferentNestedLambdas_MergeOntoOneCompilableShape()
    {
        // Two WithItems steps whose lambdas include different navs merge into a nested set
        // no single lambda produced ({Note, Product}); the composed interface for that union
        // must be declared on the target entity or the generated file cannot compile.
        var (output, diagnostics) = RunFluent(Entities + """

            public static class Consumer
            {
                public static async System.Threading.Tasks.Task Use(Microsoft.EntityFrameworkCore.DbContext ctx)
                {
                    var rows = await ctx.FacetOrder()
                        .WithItems(i => i.WithProduct())
                        .WithItems(i => i.WithNote())
                        .ToListAsync();
                    string sku = rows[0].Items[0].Product.Sku;
                    string? note = rows[0].Items[0].Note?.Text;
                }
            }
            """, Manifest);

        diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();
        output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();
    }

    [Fact]
    public void KeywordFormingKeyMember_IsVerbatimEscaped()
    {
        // A key member named "Class" camel-cases into a reserved keyword; the emitted
        // GetByKeyAsync parameter must be verbatim-escaped or the whole file fails to parse.
        var (output, diagnostics) = RunFluent("""
            namespace FluentTest;

            public class Lookup
            {
                public int Class { get; set; }
                public string Label { get; set; } = null!;
            }

            public static class Consumer
            {
                public static async System.Threading.Tasks.Task Use(Microsoft.EntityFrameworkCore.DbContext ctx)
                {
                    _ = await ctx.FacetLookup().GetByKeyAsync(1);
                }
            }
            """, """
            {
              "version": 1,
              "entities": [
                { "clrType": "FluentTest.Lookup", "key": ["Class"], "scalar": ["Class", "Label"] }
              ]
            }
            """);

        diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();
        output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();
    }

    [Fact]
    public void PreNavOptionalManifest_TreatsReferenceNavsAsNullable()
    {
        // A manifest written before the navOptional field existed cannot say which
        // references are required; pessimistic-nullable never lies. The null guard in the
        // selector and the nullable member type must both appear.
        var (output, diagnostics) = RunFluent(Entities + """

            public static class Consumer
            {
                public static async System.Threading.Tasks.Task Use(Microsoft.EntityFrameworkCore.DbContext ctx)
                {
                    var row = await ctx.FacetOrder().WithUser().FirstOrDefaultAsync();
                    string? name = row?.User?.Name;
                }
            }
            """, """
            {
              "version": 1,
              "entities": [
                { "clrType": "FluentTest.Order", "key": ["Id"], "scalar": ["Id", "PlacedAt", "Total", "UserId"], "nav": ["Items", "User"] },
                { "clrType": "FluentTest.User", "key": ["Id"], "scalar": ["Id", "Name"] },
                { "clrType": "FluentTest.OrderItem", "key": ["Id"], "scalar": ["Id", "Quantity", "ProductId"], "nav": ["Note", "Product"] },
                { "clrType": "FluentTest.Product", "key": ["Id"], "scalar": ["Id", "Sku"] },
                { "clrType": "FluentTest.Note", "key": ["Id"], "scalar": ["Id", "Text"] }
              ]
            }
            """);

        diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();
        output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        var orderFile = output.SyntaxTrees.Select(t => t.ToString())
            .Single(t => t.Contains("class OrderFacetShape"));
        orderFile.Should().Contain("e.User == null ? null :",
            "unknown requiredness must produce the null guard, not a materialization crash on NULL rows");
    }

    [Fact]
    public void ManifestWithoutKey_OmitsGetByKeyAsync()
    {
        var (output, _) = RunFluent(Entities + """

            public static class Consumer
            {
                public static async System.Threading.Tasks.Task Use(Microsoft.EntityFrameworkCore.DbContext ctx)
                {
                    _ = await ctx.FacetOrder().GetByKeyAsync(1);
                }
            }
            """, """
            {
              "version": 1,
              "entities": [
                { "clrType": "FluentTest.Order", "scalar": ["Id", "PlacedAt", "Total", "UserId"], "nav": ["Items", "User"], "navOptional": ["User"] },
                { "clrType": "FluentTest.User", "scalar": ["Id", "Name"] },
                { "clrType": "FluentTest.OrderItem", "scalar": ["Id", "Quantity", "ProductId"], "nav": ["Product"] },
                { "clrType": "FluentTest.Product", "scalar": ["Id", "Sku"] }
              ]
            }
            """);

        output.GetDiagnostics().Should().Contain(d => d.Id == "CS1061",
            "a keyless (or pre-key-field) manifest entry cannot get a typed GetByKeyAsync");
    }
}
