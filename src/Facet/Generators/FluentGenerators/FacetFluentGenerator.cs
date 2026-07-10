using System;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Facet.Generators.Fluent;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using SGF;

namespace Facet.Generators;

/// <summary>
/// Generates the fluent, projection-first EF query surface
/// (<c>ctx.FacetOrder().WithUser().WithItems(i =&gt; i.WithProduct()).ToListAsync()</c>) from
/// the EF model manifest. Opt-in per project via the <c>FacetFluent</c> MSBuild property;
/// entity truth comes from <c>*.facetmodel.json</c> AdditionalFiles (the same manifests
/// that drive ExcludeNavigationProperties), member/navigation types from the compilation's
/// symbols. Shape generation is usage-driven: only the chains actually written (plus a
/// one-step base surface for discoverability) are emitted, so there is no 2^N explosion.
/// </summary>
[IncrementalGenerator]
public sealed class FacetFluentGenerator : IncrementalGenerator
{
    private static readonly DiagnosticDescriptor FluentWithoutManifestRule = new DiagnosticDescriptor(
        "FAC120",
        "FacetFluent requires an EF model manifest",
        "FacetFluent is enabled, but no EF model manifest was found in AdditionalFiles. Set <FacetEfDesignTime>true</FacetEfDesignTime> in the DbContext project, run 'dotnet ef migrations add', and wire the *.facetmodel.json into this project (automatic in the DbContext project itself; a cross-project <AdditionalFiles> glob otherwise).",
        "Generator",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "The fluent query surface is generated entirely from the EF model manifest — without one there is nothing safe to generate, and generating nothing silently would look like the feature is broken. Severity is situational: during first-time setup the first 'dotnet ef migrations add' builds the project BEFORE any manifest exists, so an unconditional error would deadlock the bootstrap — it stays a warning until fluent chains are actually written, and escalates to an error exactly then, alongside the CS1061s it explains.");

    private static readonly DiagnosticDescriptor ChainDepthCappedRule = new DiagnosticDescriptor(
        "FAC121",
        "Fluent chain exceeds the configured depth",
        "This fluent chain nests deeper than FacetMaxChainDepth ({0}); the shapes were capped at that depth. Raise <FacetMaxChainDepth> in the project file if the depth is intentional.",
        "Generator",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Deep include chains multiply generated types and produce very wide SQL; the cap is a guardrail, not a correctness limit. The capped chain's missing inner shapes surface as ordinary compile errors on the marker lambda.");

    private static readonly DiagnosticDescriptor FluentWithoutEfCoreRule = new DiagnosticDescriptor(
        "FAC122",
        "FacetFluent requires Microsoft.EntityFrameworkCore",
        "FacetFluent is enabled, but this compilation does not reference Microsoft.EntityFrameworkCore — the generated query surface cannot compile without it.",
        "Generator",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The generated code uses DbContext.Set, AsNoTracking, EF.Property and the async query operators. Reference Microsoft.EntityFrameworkCore (directly or transitively) in the project that sets FacetFluent.");

    private sealed class FluentConfig : IEquatable<FluentConfig>
    {
        public FluentConfig(bool enabled, int maxChainDepth)
        {
            Enabled = enabled;
            MaxChainDepth = maxChainDepth;
        }

        public bool Enabled { get; }
        public int MaxChainDepth { get; }

        public bool Equals(FluentConfig? other)
            => other != null && Enabled == other.Enabled && MaxChainDepth == other.MaxChainDepth;

        public override bool Equals(object? obj) => obj is FluentConfig other && Equals(other);

        public override int GetHashCode() => unchecked(Enabled.GetHashCode() * 31 + MaxChainDepth);
    }

    public FacetFluentGenerator() : base(nameof(FacetFluentGenerator))
    {
    }

    public override void OnInitialize(SgfInitializationContext context)
    {
        var config = context.AnalyzerConfigOptionsProvider.Select(static (options, _) =>
        {
            options.GlobalOptions.TryGetValue("build_property.FacetFluent", out var enabledValue);
            options.GlobalOptions.TryGetValue("build_property.FacetMaxChainDepth", out var depthValue);
            var enabled = string.Equals(enabledValue, "true", StringComparison.OrdinalIgnoreCase);
            var depth = int.TryParse(depthValue, out var parsed) && parsed > 0 ? parsed : 3;
            return new FluentConfig(enabled, depth);
        });

        var manifest = context.AdditionalTextsProvider
            .Where(static file => file.Path.EndsWith(EfModelManifest.FileExtension, StringComparison.OrdinalIgnoreCase))
            .Select(static (file, token) => (file.Path, Text: file.GetText(token)?.ToString() ?? string.Empty))
            .Collect()
            .Select(static (files, _) => EfModelManifest.Parse(files));

        var chains = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node, _) => FluentChainDiscovery.IsCandidate(node),
                static (ctx, token) => FluentChainDiscovery.Parse(ctx, token))
            .Where(static chain => chain != null)
            .Collect();

        var input = config
            .Combine(manifest)
            .Combine(chains)
            .Combine(context.CompilationProvider);

        context.RegisterSourceOutput(input, (spc, data) =>
        {
            var (((cfg, efManifest), discovered), compilation) = data;
            if (!cfg.Enabled) return;

            if (compilation.GetTypeByMetadataName("Microsoft.EntityFrameworkCore.DbContext") == null)
            {
                spc.ReportDiagnostic(Diagnostic.Create(FluentWithoutEfCoreRule, Location.None));
                return;
            }

            // Manifest file problems (FAC103/FAC104) are reported once by the GenerateDtos
            // pipeline; this generator only cares whether any manifest made it through.
            if (!efManifest.HasAcceptedManifests)
            {
                // Warning while no chain exists (the bootstrap migration builds before the
                // first manifest can exist); error once chains are written, so the wall of
                // CS1061s at the call sites comes with its explanation.
                var anyChain = discovered.FirstOrDefault(c => c != null);
                spc.ReportDiagnostic(Diagnostic.Create(
                    FluentWithoutManifestRule,
                    anyChain?.Location.ToLocation() ?? Location.None,
                    anyChain != null ? DiagnosticSeverity.Error : DiagnosticSeverity.Warning,
                    additionalLocations: null,
                    properties: null));
                return;
            }

            var models = FluentEntityModel.Build(efManifest, efManifest.EntityClrNames, compilation);
            if (models.Count == 0) return;

            var plan = ChainPlanner.Build(
                models,
                discovered.Where(c => c != null).Select(c => c!).ToImmutableArray(),
                cfg.MaxChainDepth);

            foreach (var capped in plan.CappedChains)
            {
                spc.ReportDiagnostic(Diagnostic.Create(
                    ChainDepthCappedRule,
                    capped.Location.ToLocation(),
                    cfg.MaxChainDepth));
            }

            var emitter = new FluentEmitter(plan.Plans);
            foreach (var entityPlan in plan.Plans.Values.OrderBy(p => p.Model.ClrName, StringComparer.Ordinal))
            {
                spc.CancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var source = emitter.EmitEntity(entityPlan);
                    spc.AddSource(entityPlan.Model.ClrName + ".FacetQuery.g.cs", SourceText.From(source, Encoding.UTF8));
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, $"Error generating fluent query surface for '{entityPlan.Model.ClrName}'");
                    throw;
                }
            }
        });
    }
}
