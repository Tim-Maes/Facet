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
        var globalOptions = context.AnalyzerConfigOptionsProvider
            .Select(static (provider, _) => GlobalConfigurationDefaults.FromOptions(provider.GlobalOptions));

        var facets = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                FacetConstants.FacetAttributeFullName,
                predicate: static (node, _) => node is TypeDeclarationSyntax,
                transform: static (ctx, token) => (ctx, token))
            .Combine(globalOptions)
            
            .SelectMany(static (combined, token) => ModelBuilder.BuildModels(combined.Left.ctx, combined.Right, combined.Left.token))
            .Where(static m => m is not null);

        var allFacets = facets.Collect();

        context.RegisterSourceOutput(allFacets, static (spc, models) =>
        {
            spc.CancellationToken.ThrowIfCancellationRequested();

            var modelsByTarget = models
                .Where(m => m is not null)
                .GroupBy(m => m!.FullName)
                .ToList();

            var facetLookup = modelsByTarget.ToDictionary(g => g.Key, g => g.Select(m => m!).ToList());

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
