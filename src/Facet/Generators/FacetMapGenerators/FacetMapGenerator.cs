using Facet.Generators.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace Facet.Generators.FacetMapGenerators;

[Generator(LanguageNames.CSharp)]
public sealed class FacetMapGenerator : IIncrementalGenerator
{
    private const string FacetMapAttributeFullName = "Facet.FacetMapAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var globalOptions = context.AnalyzerConfigOptionsProvider
            .Select(static (provider, _) => GlobalConfigurationDefaults.FromOptions(provider.GlobalOptions));

        var facetMaps = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                FacetMapAttributeFullName,
                predicate: static (node, _) => node is ClassDeclarationSyntax,
                transform: static (ctx, token) => (ctx, token))
            .Combine(globalOptions)
            .SelectMany(static (combined, token) =>
                FacetMapModelBuilder.BuildModels(combined.Left.ctx, combined.Right, combined.Left.token))
            .Where(static m => m is not null);

        context.RegisterSourceOutput(facetMaps, static (spc, model) =>
        {
            if (model == null) return;

            spc.CancellationToken.ThrowIfCancellationRequested();

            var code = FacetMapCodeBuilder.Generate(model);
            var hintName = model.AttributeCount > 1
                ? $"{model.FullName}.FacetMap.{model.AttributeIndex}.g.cs"
                : $"{model.FullName}.FacetMap.g.cs";
            spc.AddSource(hintName, SourceText.From(code, Encoding.UTF8));
        });
    }
}
