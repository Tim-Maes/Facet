using Facet.Generators.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Facet.Generators;

[Generator(LanguageNames.CSharp)]
public sealed class FacetGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Read global configuration defaults from MSBuild properties
        var globalOptions = context.AnalyzerConfigOptionsProvider
            .Select(static (provider, _) => GlobalConfigurationDefaults.FromOptions(provider.GlobalOptions));

        var facets = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                FacetConstants.FacetAttributeFullName,
                predicate: static (node, _) => node is TypeDeclarationSyntax,
                transform: static (ctx, token) => (ctx, token))
            .Combine(globalOptions)
            // Each context may carry multiple [Facet] attributes; build one model per attribute.
            .SelectMany(static (combined, token) => ModelBuilder.BuildModels(combined.Left.ctx, combined.Right, combined.Left.token))
            .Where(static m => m is not null);

        // Collect all facet models to enable nested facet lookup during generation
        var allFacets = facets.Collect();

        context.RegisterSourceOutput(allFacets, static (spc, models) =>
        {
            spc.CancellationToken.ThrowIfCancellationRequested();

            // Build a lookup dictionary for nested facet resolution.
            // When multiple models share the same FullName (multi-source scenario), only the first
            // model is kept in the lookup; that is sufficient for nested-facet type resolution.
            var facetLookup = new Dictionary<string, FacetTargetModel>();
            foreach (var model in models)
            {
                if (model is not null && !facetLookup.ContainsKey(model.FullName))
                    facetLookup[model.FullName] = model;
            }

            // Group models by target type FullName. Multiple models for the same target arise when
            // the target class carries more than one [Facet] attribute (different source types).
            var modelsByTarget = models
                .Where(m => m is not null)
                .GroupBy(m => m!.FullName)
                .ToList();

            foreach (var group in modelsByTarget)
            {
                spc.CancellationToken.ThrowIfCancellationRequested();

                var modelsForTarget = group.Select(m => m!).ToList();
                var code = CodeBuilder.GenerateForGroup(modelsForTarget, facetLookup);
                spc.AddSource($"{group.Key}.g.cs", SourceText.From(code, Encoding.UTF8));
            }
        });
    }
}
