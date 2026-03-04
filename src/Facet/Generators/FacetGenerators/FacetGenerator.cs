using Facet.Generators.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
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
            .Select(static (combined, token) => ModelBuilder.BuildModel(combined.Left.ctx, combined.Right, combined.Left.token))
            .Where(static m => m is not null);

        // Collect all facet models to enable nested facet lookup during generation
        var allFacets = facets.Collect();

        context.RegisterSourceOutput(allFacets, static (spc, models) =>
        {
            spc.CancellationToken.ThrowIfCancellationRequested();

            // Build a lookup dictionary for nested facet resolution
            var facetLookup = models
                .Where(m => m is not null)
                .ToDictionary(m => m!.FullName, m => m!);

            // Generate code for each facet with access to all facet models
            foreach (var model in models)
            {
                if (model is null) continue;

                var code = CodeBuilder.Generate(model, facetLookup);
                spc.AddSource($"{model.FullName}.g.cs", SourceText.From(code, Encoding.UTF8));
            }
        });
    }
}
