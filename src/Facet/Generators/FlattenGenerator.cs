using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Linq;
using System.Text;

namespace Facet.Generators;

[Generator(LanguageNames.CSharp)]
public sealed class FlattenGenerator : IIncrementalGenerator
{
    private const string FlattenAttributeFullName = "Facet.FlattenAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var flattenTargets = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                FlattenAttributeFullName,
                predicate: static (node, _) => node is TypeDeclarationSyntax,
                transform: static (ctx, token) => FlattenModelBuilder.BuildModel(ctx, token))
            .Where(static m => m is not null);

        context.RegisterSourceOutput(flattenTargets, static (spc, model) =>
        {
            if (model is null) return;

            spc.CancellationToken.ThrowIfCancellationRequested();

            var code = FlattenCodeBuilder.Generate(model);
            spc.AddSource($"{model.FullName}.g.cs", SourceText.From(code, Encoding.UTF8));
        });
    }
}
