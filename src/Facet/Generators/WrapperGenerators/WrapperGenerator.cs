using Facet.Generators.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Linq;
using System.Text;

namespace Facet.Generators;

[Generator(LanguageNames.CSharp)]
public sealed class WrapperGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var wrappers = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                FacetConstants.WrapperAttributeFullName,
                predicate: static (node, _) => node is TypeDeclarationSyntax,
                transform: static (ctx, token) => WrapperModelBuilder.BuildModel(ctx, token))
            .Where(static m => m is not null);

        context.RegisterSourceOutput(wrappers, static (spc, model) =>
        {
            if (model is null) return;

            spc.CancellationToken.ThrowIfCancellationRequested();

            var code = WrapperCodeBuilder.Generate(model);
            spc.AddSource($"{model.FullName}.Wrapper.g.cs", SourceText.From(code, Encoding.UTF8));
        });
    }
}
