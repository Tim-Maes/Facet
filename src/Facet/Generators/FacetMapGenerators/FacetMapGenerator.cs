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
    private const int MaxHintNameLength = 100;

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
            var baseName = model.MarkerClassName;
            var suffix = model.AttributeCount > 1
                ? $".FacetMap.{model.AttributeIndex}.g.cs"
                : ".FacetMap.g.cs";

            // If the hint name would be too long, truncate the class name and append a hash for uniqueness
            var hintName = baseName + suffix;
            if (hintName.Length > MaxHintNameLength)
            {
                var hash = GetStableHash(model.FullName);
                var maxBaseLength = MaxHintNameLength - suffix.Length - 9; // 9 = 1 dot + 8 hex chars
                if (maxBaseLength < 10) maxBaseLength = 10;
                baseName = baseName.Substring(0, maxBaseLength) + "." + hash;
                hintName = baseName + suffix;
            }

            spc.AddSource(hintName, SourceText.From(code, Encoding.UTF8));
        });
    }

    private static string GetStableHash(string input)
    {
        unchecked
        {
            uint hash = 2166136261;
            foreach (var c in input)
            {
                hash = (hash ^ c) * 16777619;
            }
            return hash.ToString("x8");
        }
    }
}
