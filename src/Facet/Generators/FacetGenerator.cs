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
        var facets = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                FacetConstants.FacetAttributeFullName,
                predicate: static (node, _) => node is TypeDeclarationSyntax,
                transform: static (ctx, token) => ModelBuilder.BuildModel(ctx, token))
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
